using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(AIMovement))]
[RequireComponent(typeof(AICombat))]
[RequireComponent(typeof(AIPerception))]
public class AIController : MonoBehaviour
{
    // Enum состояний остается здесь, так как он определяет типы состояний
    public enum AIState { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
    [SerializeField] private AIState currentStateDebugView; // Для отладки в инспекторе
    private IAIState currentStateObject;
    private Dictionary<AIState, IAIState> availableStates;

    public enum Alignment { Friendly, Neutral, Hostile }
    [Header("AI Behavior")]
    public Alignment currentAlignment = Alignment.Hostile;
    public bool canBecomeHostileOnAttack = true;
    public bool canFlee = false;
    public float fleeHealthThreshold = 0.3f;
    public bool fleesOnSightOfPlayer = false;

    [Header("Idle Behavior")]
    public bool canWander = true;
    public float wanderRadius = 5f; // Используется AIStateIdle/Wandering через геттер
    public float minWanderWaitTime = 2f;
    public float maxWanderWaitTime = 5f;
    private float nextWanderTimeInternal = 0f;
    private Vector3 currentWanderDestinationInternal;
    private bool isWanderingToActiveDestinationInternal = false;


    [Header("State Transition Parameters (Distances)")]
    public float attackStateSwitchRadius = 2f; 
    public float fleeDistance = 20f;

    [Header("Rewards")]
    public List<InventoryItem> potentialLoot = new List<InventoryItem>();
    public int experienceReward = 25;

    // Публичные свойства для доступа из состояний
    public AIMovement Movement { get; private set; }
    public AICombat Combat { get; private set; }
    public AIPerception Perception { get; private set; }
    public CharacterStats MyStats { get; private set; }
    public Transform PlayerPartyTransformRef { get; private set; } // Ссылка на объект игрока
    public PartyManager PartyManagerRef { get; private set; }     // Ссылка на PartyManager
    public FeedbackManager FeedbackManagerRef { get; private set; } // Локальный или общий FeedbackManager
    private Transform currentThreatInternal;
    public Transform CurrentThreat => currentThreatInternal;


    void Awake()
    {
        Movement = GetComponent<AIMovement>();
        Combat = GetComponent<AICombat>(); 
        Perception = GetComponent<AIPerception>();
        MyStats = GetComponent<CharacterStats>();

        if (MyStats == null || Movement == null || Combat == null || Perception == null)
        {
            Debug.LogError($"AIController ({gameObject.name}): One or more required components are missing! AI will be disabled.", this);
            enabled = false; 
            return;
        }

        MyStats.onDied += HandleDeath; // Подписываемся на смерть
        MyStats.onHealthChanged.AddListener(CheckFleeConditionOnHealthChange);

        InitializeStates();
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            PlayerPartyTransformRef = playerObject.transform;
            PartyManagerRef = playerObject.GetComponent<PartyManager>();
            // Пытаемся получить локальный FeedbackManager, если нет, то с игрока
            FeedbackManagerRef = GetComponent<FeedbackManager>();
            if (FeedbackManagerRef == null && playerObject != null)
            {
                FeedbackManagerRef = playerObject.GetComponentInChildren<FeedbackManager>();
            }
        }

        if (currentAlignment == Alignment.Hostile && PlayerPartyTransformRef != null && 
            Perception.IsTargetInRadius(PlayerPartyTransformRef, Perception.engageRadius) && 
            Perception.HasLineOfSightToTarget(PlayerPartyTransformRef))
        {
            // Первоначальная угроза устанавливается через Update, который вызовет Perception
        }
        if (canWander)
        {
            ResetWanderTimer();
        }
        
        if (Combat != null) Combat.effectiveAttackRange = attackStateSwitchRadius;
        
        // Устанавливаем начальное состояние
        ChangeState(AIState.Idle); // Начинаем с Idle
    }

    private void InitializeStates()
    {
        availableStates = new Dictionary<AIState, IAIState>
        {
            { AIState.Idle, new AIStateIdle() },
            { AIState.Wandering, new AIStateWandering() },
            { AIState.Chasing, new AIStateChasing() },
            { AIState.Attacking, new AIStateAttacking() },
            { AIState.Fleeing, new AIStateFleeing() }
            // Состояние Dead обрабатывается особым образом (через HandleDeath)
        };
    }

// В AIController.cs

void Update()
{
    if (currentStateObject == null || MyStats == null || MyStats.IsDead)
    {
        // Если AI мертв, HandleDeath уже должен был все остановить.
        // Если currentStateObject null, но AI не мертв (ошибка инициализации), пытаемся восстановить.
        if (currentStateObject == null && (MyStats != null && !MyStats.IsDead))
        {
            ChangeState(AIState.Idle);
        }
        return;
    }

    // --- 1. Определение/Обновление CurrentThreat на основе данных от Perception ---
    Transform perceivedHostileThreat = null;
    if (currentAlignment == Alignment.Hostile) // Только если мы в принципе враждебны
    {
        // Perception должен давать нам наиболее подходящую враждебную цель, которую он видит СЕЙЧАС
        // с учетом engageRadius и LOS.
        perceivedHostileThreat = Perception.GetPrimaryHostileThreatAggro();
    }

    // Если Perception видит новую/другую враждебную цель, ИЛИ если у нас нет текущей угрозы, а Perception нашел.
    if (perceivedHostileThreat != null && currentThreatInternal != perceivedHostileThreat)
    {
        // Debug.Log($"AIController: Perception identified new/better threat: {perceivedHostileThreat.name}. Old threat: {currentThreatInternal?.name}.");
        SetCurrentThreat(perceivedHostileThreat);
        // Если мы были в Idle/Wandering и сагрились, состояние Chasing установится в UpdateState этих состояний или в BecomeHostile.
    }
    // Если perceivedHostileThreat == null (Perception никого не видит как агрессивную цель),
    // а у нас был currentThreatInternal, мы НЕ сбрасываем его здесь сразу.
    // Сброс currentThreatInternal произойдет ниже, если он станет невалидным, выйдет за disengageRadius, или потеряет LOS надолго.


    // --- 2. Обработка текущей установленной угрозы currentThreatInternal ---
    if (currentThreatInternal != null)
    {
        bool threatIsInvalid = false;
        CharacterStats singleThreatStats = null; // Для кеширования, если это одиночный NPC

        if (currentThreatInternal.CompareTag("Player"))
        {
            if (PartyManagerRef != null)
            {
                bool anyPartyMemberAlive = false;
                foreach (CharacterStats member in PartyManagerRef.partyMembers)
                {
                    if (member != null && !member.IsDead)
                    {
                        anyPartyMemberAlive = true;
                        break;
                    }
                }
                if (!anyPartyMemberAlive)
                {
                    threatIsInvalid = true;
                    // Debug.Log($"AIController.Update: Player party (threat: {currentThreatInternal.name}) is wiped out.");
                }
            }
            // Если PartyManagerRef null, считаем игрока валидной угрозой, пока сам объект существует.
        }
        else // Это не объект "Player", предполагаем, что это NPC
        {
            singleThreatStats = currentThreatInternal.GetComponent<CharacterStats>();
            if (singleThreatStats != null)
            {
                if (singleThreatStats.IsDead)
                {
                    threatIsInvalid = true;
                }
            }
            else // Нет CharacterStats, считаем невалидной боевой целью
            {
                threatIsInvalid = true;
                // Debug.LogWarning($"AIController.Update: CurrentThreat {currentThreatInternal.name} is not Player and has no CharacterStats. Marking as invalid for combat.");
            }
        }

        if (threatIsInvalid)
        {
            // Debug.Log($"AIController.Update: CurrentThreat {currentThreatInternal.name} is now invalid. Clearing.");
            ClearCurrentThreat(); 
            // Состояния (Chasing, Attacking, Fleeing) сами должны обработать null currentThreat и перейти в Idle.
        }
        else // Угроза (или хотя бы один член партии игрока) жива
        {
            // Проверяем расстояние и LOS только если мы не в состоянии бегства ОТ этой угрозы
            if (currentStateObject.GetStateType() != AIState.Fleeing || 
                (currentStateObject.GetStateType() == AIState.Fleeing && currentThreatInternal != PlayerPartyTransformRef && PlayerPartyTransformRef != null)) // Если убегаем не от игрока, а игрок стал новой угрозой (маловероятно без сложной логики)
            {
                // Для LOS и disengageRadius используем позицию currentThreatInternal (которая будет позицией объекта Player для партии)
                float distanceToThreat = Vector3.Distance(transform.position, currentThreatInternal.position);

                if (distanceToThreat > Perception.disengageRadius)
                {
                    // Debug.Log($"AIController.Update: CurrentThreat {currentThreatInternal.name} is beyond disengage radius ({distanceToThreat} > {Perception.disengageRadius}). Clearing.");
                    ClearCurrentThreat();
                }
                else
                {
                    // Проверка на потерю Line of Sight (без "памяти")
                    // Если AI враждебен и Perception больше не видит ЭТУ ЖЕ цель как PrimaryHostileThreatAggro
                    // (например, она скрылась за препятствием, и GetPrimaryHostileThreatAggro теперь null или другая цель),
                    // то мы должны сбросить currentThreatInternal, чтобы AI не продолжал тупо идти в стену.
                    if (currentAlignment == Alignment.Hostile)
                    {
                        Transform currentlyVisibleAggroThreat = Perception.GetPrimaryHostileThreatAggro();
                        if (currentlyVisibleAggroThreat != currentThreatInternal)
                        {
                            // Если основная видимая угроза изменилась (или исчезла),
                            // а мы все еще сфокусированы на старой currentThreatInternal.
                            // Debug.Log($"AIController.Update: Current threat {currentThreatInternal.name} no longer primary visible aggro threat (Perception sees: {currentlyVisibleAggroThreat?.name}). Clearing current.");
                            ClearCurrentThreat(); 
                            // В следующем кадре, если currentlyVisibleAggroThreat все еще есть, он станет новым currentThreatInternal.
                            // Если currentlyVisibleAggroThreat null, то AI перейдет в Idle.
                        }
                    }
                    // Если AI не враждебен, но currentThreatInternal был установлен (например, для бегства),
                    // то мы не сбрасываем его по потере LOS, пока не сработает fleeDistance.
                }
            }
        }
    }
    
    // Если после всех проверок у нас нет активной угрозы (currentThreatInternal == null),
    // и AI враждебен, то он может просто стоять или блуждать,
    // пока Perception не найдет новую цель (perceivedHostileThreat) в следующем цикле Update.
    // Этот блок может быть избыточен, если perceivedHostileThreat уже устанавливает currentThreatInternal в начале.
    // if (currentThreatInternal == null && currentAlignment == Alignment.Hostile)
    // {
    //     Transform potentialNewThreat = Perception.GetPrimaryHostileThreatAggro();
    //     if (potentialNewThreat != null)
    //     {
    //         Debug.Log($"AIController.Update: No current threat, Perception sees new aggro target {potentialNewThreat.name}. Engaging.");
    //         SetCurrentThreat(potentialNewThreat);
    //     }
    // }


    // --- 3. Логика "бегства при виде игрока" (для не-враждебных NPC) ---
    if (fleesOnSightOfPlayer && Perception.PlayerTarget != null && // PlayerTarget из Perception уже учитывает LOS и visionCone
        currentStateObject.GetStateType() != AIState.Fleeing && 
        currentStateObject.GetStateType() != AIState.Dead &&    
        currentAlignment != Alignment.Hostile)                 
    {
        // IsPlayerSpottedForFleeing в Perception должен учитывать и угол, и LOS, и радиус для *первоначального* "испуга"
        if (Perception.IsPlayerSpottedForFleeing()) 
        {
            // Debug.Log($"AIController: Player spotted for fleeing via Perception. Forcing flee.");
            ForceFlee(Perception.PlayerTarget); 
            // Не нужно вызывать currentStateObject.UpdateState(this) здесь,
            // так как ForceFlee меняет состояние, и следующий Update обработает новое состояние.
            return; // Выходим, так как состояние изменилось, и UpdateState для старого состояния не нужен.
        }
    }
    
    // --- 4. Обновление текущего состояния AI ---
    // Убедимся, что currentStateObject не null (на случай если HandleDeath его обнулил, а мы еще здесь)
    if (currentStateObject != null) 
    {
        currentStateObject.UpdateState(this);
    }
    
    // currentStateDebugView = currentStateObject?.GetStateType() ?? AIState.Dead; // Обновляем для отладки
}
public void BecomeHostileTowards(Transform threatSource, bool forceAggro = false)
{
    // Debug.Log($"BecomeHostileTowards called. Current State: {currentStateObject?.GetStateType()}, Threat: {threatSource?.name}, ForceAggro: {forceAggro}, CurrentAlignment: {currentAlignment}");

    // Если уже враждебны к этой цели и не форсируем агрессию,
    // но находимся в "мирном" состоянии, то просто переводим в Chasing.
    if (currentAlignment == Alignment.Hostile && currentThreatInternal == threatSource && !forceAggro)
    {
        if (currentStateObject.GetStateType() == AIState.Idle || 
            currentStateObject.GetStateType() == AIState.Wandering ||
            currentStateObject.GetStateType() == AIState.Fleeing) // Если были в Fleeing от другой цели, а эта снова сагрила
        {
            // Debug.Log($"BecomeHostile: Already hostile to {threatSource.name}, was in {currentStateObject.GetStateType()}, ensuring Chasing.");
            FaceTarget(threatSource); // Все равно разворачиваемся, на всякий случай
            ChangeState(AIState.Chasing);
        }
        return;
    }
    
    bool canActuallyBecomeHostile = (currentAlignment == Alignment.Neutral) || 
                                    (currentAlignment == Alignment.Friendly && canBecomeHostileOnAttack) ||
                                    forceAggro;

    if (canActuallyBecomeHostile)
    {
        bool wasPreviouslyHostile = (currentAlignment == Alignment.Hostile);
        Transform previousThreat = currentThreatInternal;

        currentAlignment = Alignment.Hostile;
        SetCurrentThreat(threatSource); // Устанавливаем новую/текущую угрозу

        // ПРИНУДИТЕЛЬНЫЙ РАЗВОРОТ К УГРОЗЕ
        // Debug.Log($"BecomeHostile: About to face target {threatSource.name}. Current rotation: {transform.rotation.eulerAngles}");
        FaceTarget(threatSource); 
        // Debug.Log($"BecomeHostile: Faced target {threatSource.name}. New rotation: {transform.rotation.eulerAngles}");

        // Показываем сообщение, если изменилось отношение или цель, или если форсируем
        if (!wasPreviouslyHostile || previousThreat != threatSource || forceAggro)
        {
            string message = $"{gameObject.name} теперь враждебен к {threatSource.name}!";
            FeedbackManagerRef?.ShowFeedbackMessage(message);
            // Debug.Log(message);
        }
        
        // Переходим в Chasing, если:
        // 1. Мы были в "мирном" состоянии (Idle, Wandering, Fleeing).
        // 2. Это forceAggro (принудительная смена состояния на агрессивное).
        // 3. Мы были в Attacking, но теперь цель другая.
        if (currentStateObject.GetStateType() == AIState.Idle || 
            currentStateObject.GetStateType() == AIState.Wandering || 
            currentStateObject.GetStateType() == AIState.Fleeing || 
            forceAggro || 
            (currentStateObject.GetStateType() == AIState.Attacking && currentThreatInternal != previousThreat) ) 
        {
            // Debug.Log($"BecomeHostile: Changing state to Chasing. Old state: {currentStateObject.GetStateType()}");
            ChangeState(AIState.Chasing);
        }
    }
    // else { Debug.Log($"BecomeHostile: Cannot become hostile. Alignment: {currentAlignment}, canBecomeHostile: {canBecomeHostileOnAttack}"); }
}

public void ForceFlee(Transform threatToFleeFrom)
{
    if (currentStateObject != null && currentStateObject.GetStateType() == AIState.Dead) return;
    
    // Debug.Log($"ForceFlee called. Threat: {threatToFleeFrom?.name}. CanFlee: {canFlee}");

    if (!canFlee)
    {
        if (currentAlignment != Alignment.Hostile && PlayerPartyTransformRef != null && threatToFleeFrom == PlayerPartyTransformRef)
        {
            // Debug.Log($"ForceFlee: Cannot flee, becoming hostile instead.");
            BecomeHostileTowards(threatToFleeFrom, true); 
        }
        return;
    }
    
    string message = $"{gameObject.name} напуган {threatToFleeFrom.name} и убегает!";
    FeedbackManagerRef?.ShowFeedbackMessage(message);
    // Debug.Log(message);
    
    SetCurrentThreat(threatToFleeFrom); 
    ChangeState(AIState.Fleeing);
}

    public void ChangeState(AIState newStateKey)
    {
        if (currentStateObject != null && currentStateObject.GetStateType() == AIState.Dead) return; // Нельзя сменить состояние, если мертвы

        if (availableStates.TryGetValue(newStateKey, out IAIState newStateObject))
        {
            // Убрал: if (currentStateObject == newStateObject && newStateKey != AIState.Idle) return;
            // Иногда нужно "перезайти" в состояние, особенно в Idle для сброса таймеров или логики.
            // Если нужно строгое предотвращение повторного входа, можно вернуть проверку,
            // но убедиться, что она не мешает нужным переходам (например, принудительный сброс в Idle).
        
            currentStateObject?.ExitState(this);
            currentStateObject = newStateObject;
            currentStateObject.EnterState(this);
            currentStateDebugView = newStateKey; // Для отладки
        }
        else
        {
            Debug.LogWarning($"AIController ({gameObject.name}): Attempted to change to an unknown state: {newStateKey}");
        }
    }
    
    // Методы, используемые состояниями для доступа к параметрам/логике AIController
    public float GetNextWanderTime() => nextWanderTimeInternal;
    public void ResetWanderTimer() { nextWanderTimeInternal = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime); }
    public bool TrySetNewWanderDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        UnityEngine.AI.NavMeshHit navHit; 
        if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out navHit, wanderRadius, UnityEngine.AI.NavMesh.AllAreas))
        {
            currentWanderDestinationInternal = navHit.position;
            isWanderingToActiveDestinationInternal = true;
            return true;
        }
        else 
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position + (Random.insideUnitSphere * 2f), out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                currentWanderDestinationInternal = navHit.position;
                isWanderingToActiveDestinationInternal = true;
                return true;
            }
        }
        isWanderingToActiveDestinationInternal = false;
        return false;
    }
    public Vector3 GetCurrentWanderDestination() => currentWanderDestinationInternal;
    public bool IsWanderingToActiveDestination() => isWanderingToActiveDestinationInternal;
    public void ResetWanderingState() { 
        isWanderingToActiveDestinationInternal = false; 
        ResetWanderTimer(); 
    }

    public void ClearCurrentThreat()
    {
        currentThreatInternal = null;
    }

    public void SetCurrentThreat(Transform threat)
    {
        currentThreatInternal = threat;
    }
// В AIController.cs
public void FaceTarget(Transform target) 
{
    if (target == null || Movement == null || !Movement.IsOnNavMesh()) return; 
    Vector3 direction = (target.position - transform.position).normalized;
    if (direction != Vector3.zero)
    {
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // Для более плавного поворота можно использовать Slerp, но это потребует управления скоростью поворота
        //transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * Movement.GetSpeed() * 0.5f); // Пример плавного поворота
        // Или мгновенный:
        transform.rotation = lookRotation;
    }
}

    private void CheckFleeConditionOnHealthChange(int currentHp, int maxHp)
    {
        if (!canFlee || (currentStateObject != null && currentStateObject.GetStateType() == AIState.Dead) || 
            (currentStateObject != null && currentStateObject.GetStateType() == AIState.Fleeing) || MyStats == null) return;
        
        if ((float)currentHp / MyStats.maxHealth <= fleeHealthThreshold)
        {
            // Убегаем от текущей активной угрозы, если она есть.
            if (currentThreatInternal != null) { ForceFlee(currentThreatInternal); }
            // Если нет currentThreatInternal, но игрок виден и является причиной низкого здоровья, назначаем его угрозой для бегства.
            else if (Perception.PlayerTarget != null && Perception.IsTargetInRadius(Perception.PlayerTarget, Perception.sightRadius))
            {
                 ForceFlee(Perception.PlayerTarget);
            }
        }
    }

    
    public void ClearCurrentThreatAndSearch() // Новый метод для удобства
    {
        currentThreatInternal = null;
    }
    private void HandleDeath() 
    {
        // Вызываем ExitState для текущего состояния, если оно было
        currentStateObject?.ExitState(this);
        currentStateObject = null; // Устанавливаем в null, чтобы Update не вызывался

        currentStateDebugView = AIState.Dead; // Для отладки

        Movement.ResetAndStopAgent(); 
        Movement.DisableAgent();      

        GrantExperienceToParty();
        SetupLootableCorpse();
        
        // Отключаем сам AIController, чтобы он больше не выполнял Update
        // enabled = false; // Опционально, если есть другие компоненты, которые могут на него ссылаться
    }
    private void GrantExperienceToParty() 
    {
        if (PartyManagerRef == null || experienceReward <= 0) return;
        List<CharacterStats> livingMembers = PartyManagerRef.partyMembers.Where(member => member != null && !member.IsDead).ToList();
        if (livingMembers.Count > 0)
        {
            int xpPerMember = Mathf.Max(1, experienceReward / livingMembers.Count);
            foreach (CharacterStats member in livingMembers) { member.GainExperience(xpPerMember); }
        }
    }

    private void SetupLootableCorpse() 
    {
        LootableCorpse lootable = gameObject.GetComponent<LootableCorpse>();
        if (lootable == null) lootable = gameObject.AddComponent<LootableCorpse>();
        lootable.interactionPrompt = $"Осмотреть {gameObject.name}";
        lootable.loot.Clear();
        if (potentialLoot != null && potentialLoot.Count > 0)
        {
            foreach (var lootEntry in potentialLoot)
            {
                if (lootEntry.itemData != null && lootEntry.quantity > 0)
                {
                    lootable.loot.Add(new InventoryItem(lootEntry.itemData, lootEntry.quantity));
                }
            }
        }
    }

    void OnDestroy() 
    {
        if (MyStats != null)
        {
            MyStats.onDied -= HandleDeath;
            MyStats.onHealthChanged.RemoveListener(CheckFleeConditionOnHealthChange);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Параметры обнаружения и состояния из AIPerception, если он уже получен
        if (Perception != null)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, Perception.engageRadius);
            if (fleesOnSightOfPlayer) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, Perception.fleeOnSightRadius); }
        }
        else // Рисуем значения по умолчанию, если perception еще не инициализирован (например, в эдиторе до Play)
        {
            // Можно попытаться получить компонент здесь, но это не очень хорошо для OnDrawGizmosSelected
            // AIPerception defaultPerception = GetComponent<AIPerception>();
            // if(defaultPerception != null) { ... }
            // Проще оставить так, или не рисовать их до инициализации
        }

        Gizmos.color = Color.red;    Gizmos.DrawWireSphere(transform.position, attackStateSwitchRadius);
        
        if (canFlee) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, fleeDistance); }
        if (canWander) { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, wanderRadius); }
    }
}