using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(AIMovement))]
[RequireComponent(typeof(AICombat))]
[RequireComponent(typeof(AIPerception))] // Добавляем зависимость
public class AIController : MonoBehaviour
{
    public enum AIState { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
    [SerializeField] private AIState currentState = AIState.Idle;

    public enum Alignment { Friendly, Neutral, Hostile }
    [Header("AI Behavior")]
    public Alignment currentAlignment = Alignment.Hostile;
    public bool canBecomeHostileOnAttack = true; // Если атакован игроком
    public bool canFlee = false;
    public float fleeHealthThreshold = 0.3f;
    public bool fleesOnSightOfPlayer = false; // Использует радиус из AIPerception.fleeOnSightRadiusPlayer

    [Header("Idle Behavior")]
    public bool canWander = true;
    public float wanderRadius = 5f;
    public float minWanderWaitTime = 2f;
    public float maxWanderWaitTime = 5f;

    [Header("State Transition Parameters (Distances)")]
    // aggroRadius, sightRadiusForFlee теперь в AIPerception
    [Tooltip("Radius to switch from Chasing to Attacking state. AICombat.effectiveAttackRange should be consistent.")]
    public float attackStateSwitchRadius = 2f; 
    public float fleeDistance = 15f; // Как далеко убегать

    [Header("Rewards")]
    public List<InventoryItem> potentialLoot = new List<InventoryItem>();
    public int experienceReward = 25;

    private AIMovement movement;
    private AICombat combat; 
    private AIPerception perception; // Новая ссылка
    private CharacterStats myStats;
    private Transform playerPartyTransformRef; // Только для ссылки, актуальный видимый игрок через perception
    private PartyManager partyManager; 
    private FeedbackManager feedbackManager; 

    private Transform currentThreatInternal; // Используется для хранения текущей цели AI
    public Transform CurrentThreat => currentThreatInternal; // Публичный геттер, если нужно извне

    private float nextWanderTime = 0f;
    private Vector3 wanderDestination;
    private bool isWanderingToDestination = false;

    void Awake()
    {
        movement = GetComponent<AIMovement>();
        combat = GetComponent<AICombat>(); 
        perception = GetComponent<AIPerception>();
        myStats = GetComponent<CharacterStats>();

        if (myStats == null) { enabled = false; return; } // Остальные проверки на null в компонентах
        if (movement == null || combat == null || perception == null) { enabled = false; return; }

        myStats.onDied += HandleDeath;
        myStats.onHealthChanged.AddListener(CheckFleeConditionOnHealthChange);
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerPartyTransformRef = playerObject.transform; // Сохраняем ссылку на объект игрока
            partyManager = playerObject.GetComponent<PartyManager>();
            // FeedbackManager для сообщений AIController (не боевых)
            // Боевые сообщения обрабатываются AICombat
            feedbackManager = GetComponent<FeedbackManager>();
            // if (feedbackManager == null && playerObject != null) { feedbackManager = playerObject.GetComponentInChildren<FeedbackManager>(); }
        }

        if (currentAlignment == Alignment.Hostile && playerPartyTransformRef != null && perception.IsTargetInRadius(playerPartyTransformRef, perception.aggroRadiusPlayer) && perception.HasLineOfSightToTarget(playerPartyTransformRef) )
        {
            // Изначально враждебный AI сразу пытается найти игрока как угрозу, если он в радиусе агрессии
            // AIPerception обновит PrimaryHostileThreat, и мы его подхватим в Update
        }
        if (canWander)
        {
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
        }
        
        if (combat != null) combat.effectiveAttackRange = attackStateSwitchRadius;
    }

    void Update()
    {
        if (currentState == AIState.Dead || myStats == null || myStats.IsDead)
        {
            if (myStats !=null && myStats.IsDead && currentState != AIState.Dead) HandleDeath();
            return;
        }

        // Обновляем информацию об угрозах из AIPerception
        // currentThreatInternal должен быть приоритетной целью для текущих действий AI
        // AIPerception.PrimaryHostileThreat - это то, что сенсор считает основной враждебной целью
        // AIController решает, на кого реагировать, на основе своего состояния и alignment
        
        // Если AI враждебен, его основная угроза - это то, что видит perception как PrimaryHostileThreat
        if (currentAlignment == Alignment.Hostile)
        {
            currentThreatInternal = perception.PrimaryHostileThreat;
        }
        // Если AI не враждебен, но игрок видим и близко (в aggroRadiusPlayer),
        // то игрок становится *потенциальной* угрозой, если AI будет атакован.
        // Но currentThreatInternal пока не устанавливается на игрока, если AI не враждебен.
        // Это произойдет в BecomeHostileTowards.
        else if (perception.PlayerTarget != null && Vector3.Distance(transform.position, perception.PlayerTarget.position) <= perception.aggroRadiusPlayer)
        {
            // Игрок рядом, но AI еще не враждебен. currentThreatInternal остается null или предыдущим значением,
            // если он не был враждебен к кому-то другому.
        }
        // Если текущая угроза исчезла (например, убита или вышла из зоны видимости сенсора), сбрасываем
        if (currentThreatInternal != null && (currentThreatInternal.GetComponent<CharacterStats>() == null || currentThreatInternal.GetComponent<CharacterStats>().IsDead || !perception.IsTargetInRadius(currentThreatInternal, perception.sightRadius)))
        {
            // Проверяем, не видит ли сенсор эту угрозу все еще как PrimaryHostileThreat
            if (currentThreatInternal != perception.PrimaryHostileThreat)
            {
                 currentThreatInternal = null;
            }
        }


        // Высокоприоритетная проверка: Бегство при виде игрока (если не враждебен)
        if (fleesOnSightOfPlayer && perception.PlayerTarget != null &&
            currentState != AIState.Fleeing && currentState != AIState.Dead && currentAlignment != Alignment.Hostile)
        {
            if (perception.IsPlayerVisibleAndInFleeRadius())
            {
                ForceFlee(perception.PlayerTarget); 
                return; 
            }
        }
        
        switch (currentState)
        {
            case AIState.Idle:      UpdateIdleState();      break;
            case AIState.Wandering: UpdateWanderingState(); break;
            case AIState.Chasing:   UpdateChasingState();   break;
            case AIState.Attacking: UpdateAttackingState(); break;
            case AIState.Fleeing:   UpdateFleeingState();   break;
        }
    }

    private void UpdateIdleState()
    {
        if (currentAlignment == Alignment.Hostile && currentThreatInternal != null)
        {
            // Расстояние до currentThreatInternal уже проверено в Update через perception.PrimaryHostileThreat
            // или будет проверено при BecomeHostile.
            // Здесь просто переключаем состояние, если угроза есть и мы враждебны.
            currentState = AIState.Chasing;
            isWanderingToDestination = false; 
            return;
        }

        if (canWander && Time.time >= nextWanderTime && movement.IsOnNavMesh())
        {
            SetNewWanderDestination();
            if (isWanderingToDestination) { currentState = AIState.Wandering; }
            else { nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime); }
        }
    }

    private void SetNewWanderDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        UnityEngine.AI.NavMeshHit navHit; 
        if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out navHit, wanderRadius, UnityEngine.AI.NavMesh.AllAreas))
        {
            wanderDestination = navHit.position;
            isWanderingToDestination = true;
        }
        else 
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position + (Random.insideUnitSphere * 2f), out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                wanderDestination = navHit.position;
                isWanderingToDestination = true;
            }
            else { isWanderingToDestination = false; }
        }
    }

    private void UpdateWanderingState()
    {
        if (currentAlignment == Alignment.Hostile && currentThreatInternal != null)
        {
            currentState = AIState.Chasing;
            isWanderingToDestination = false;
            movement.StopMovement(); 
            return;
        }

        if (!movement.IsOnNavMesh() || !isWanderingToDestination)
        {
            currentState = AIState.Idle;
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
            return;
        }
        
        movement.MoveTo(wanderDestination);

        if (movement.HasReachedDestination)
        {
            currentState = AIState.Idle;
            isWanderingToDestination = false;
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
        }
    }

    private void UpdateChasingState()
    {
        if (currentThreatInternal == null) { currentState = AIState.Idle; isWanderingToDestination = false; return; }
        if (!movement.IsOnNavMesh()) { currentState = AIState.Idle; isWanderingToDestination = false; return; }

        movement.Follow(currentThreatInternal);
        movement.StoppingDistance = attackStateSwitchRadius * 0.85f; 

        float distanceToThreat = Vector3.Distance(transform.position, currentThreatInternal.position);
        if (distanceToThreat <= attackStateSwitchRadius) 
        {
            currentState = AIState.Attacking;
            movement.StopMovement(false); 
        }
    }

    private void UpdateAttackingState()
    {
        if (currentThreatInternal == null) { currentState = AIState.Idle; isWanderingToDestination = false; return; }
        if (!movement.IsOnNavMesh() && movement.enabled) { movement.StopMovement(false); return; } 

        FaceTarget(currentThreatInternal);
        float distanceToThreat = Vector3.Distance(transform.position, currentThreatInternal.position);

        if (distanceToThreat > attackStateSwitchRadius + 0.2f) 
        {
            currentState = AIState.Chasing;
            return;
        }
        
        if (movement.IsMoving) { movement.StopMovement(false); }

        if (combat.IsReadyToAttack)
        {
            if (!combat.PerformAttack(currentThreatInternal))
            {
                // Атака не удалась (цель мертва/невалидна), сбрасываем угрозу
                currentThreatInternal = null; 
                currentState = AIState.Idle;
            }
        }
    }
    
    private void UpdateFleeingState()    
    {
        // currentThreatInternal должен быть установлен перед входом в это состояние (через ForceFlee или CheckFleeCondition)
        if (currentThreatInternal == null) { currentState = AIState.Idle; isWanderingToDestination = false; return; }
        if (!movement.IsOnNavMesh()) { currentState = AIState.Idle; isWanderingToDestination = false; return; }

        float distanceToThreat = Vector3.Distance(transform.position, currentThreatInternal.position);
        if (distanceToThreat > fleeDistance)
        {
            currentState = AIState.Idle;
            isWanderingToDestination = false;
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
            movement.StopMovement(); 
            // currentThreatInternal = null; // Опционально: "забыть" угрозу
            return;
        }

        Vector3 directionFromThreat = (transform.position - currentThreatInternal.position).normalized;
        Vector3 fleeDestination = transform.position + directionFromThreat * 5f;

        UnityEngine.AI.NavMeshHit hit; 
        if (UnityEngine.AI.NavMesh.SamplePosition(fleeDestination, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            movement.MoveTo(hit.position);
        }
        else 
        {
            Vector3 randomDir = Random.insideUnitSphere * 5f;
            randomDir += transform.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(randomDir, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) { movement.MoveTo(hit.position); }
            else { currentState = AIState.Idle; isWanderingToDestination = false; nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime); movement.StopMovement(); }
        }
    }
    
    private void CheckFleeConditionOnHealthChange(int currentHp, int maxHp)
    {
        if (!canFlee || currentState == AIState.Dead || currentState == AIState.Fleeing || myStats == null) return;
        if ((float)currentHp / myStats.maxHealth <= fleeHealthThreshold)
        {
            // Убегаем от текущей активной угрозы, если она есть.
            // Если нет, но здоровье низкое, то при следующей атаке/обнаружении угрозы
            // currentThreatInternal будет установлен, и этот метод (или fleesOnSight) сработает.
            if (currentThreatInternal != null) { ForceFlee(currentThreatInternal); }
            // Если нет currentThreatInternal, но игрок виден и является причиной низкого здоровья, можно его назначить угрозой для бегства
            else if (perception.PlayerTarget != null && perception.IsTargetInRadius(perception.PlayerTarget, perception.sightRadius))
            {
                 ForceFlee(perception.PlayerTarget);
            }
        }
    }
    
    public void BecomeHostileTowards(Transform threatSource, bool forceAggro = false)
    {
        if (currentAlignment == Alignment.Hostile && currentThreatInternal == threatSource && !forceAggro) return; 
        
        bool canActuallyBecomeHostile = (currentAlignment == Alignment.Neutral) || 
                                        (currentAlignment == Alignment.Friendly && canBecomeHostileOnAttack) ||
                                        forceAggro;
        if (canActuallyBecomeHostile)
        {
            bool wasAlreadyHostileToThis = (currentAlignment == Alignment.Hostile && currentThreatInternal == threatSource);
            currentAlignment = Alignment.Hostile;
            currentThreatInternal = threatSource; // Устанавливаем новую угрозу

            if(!wasAlreadyHostileToThis || forceAggro) // Показываем сообщение, если это новая агрессия или форсированная
            {
                string message = $"{gameObject.name} теперь враждебен к {threatSource.name}!";
                if (feedbackManager != null) { feedbackManager.ShowFeedbackMessage(message); }
                else { playerPartyTransformRef?.GetComponentInChildren<FeedbackManager>()?.ShowFeedbackMessage(message); }
            }
            
            // Переходим в Chasing, если не были уже в бою с этой целью или если это форсированная агрессия
            if (currentState != AIState.Attacking || currentThreatInternal != threatSource || forceAggro)
            {
                 if (currentState == AIState.Idle || currentState == AIState.Wandering || currentState == AIState.Fleeing || forceAggro)
                 {
                    currentState = AIState.Chasing;
                    isWanderingToDestination = false;
                 }
            }
        }
    }
    
    public void ForceFlee(Transform threatToFleeFrom)
    {
        if (currentState == AIState.Dead) return;
        if (!canFlee)
        {
            if (currentAlignment != Alignment.Hostile && playerPartyTransformRef != null && threatToFleeFrom == playerPartyTransformRef)
            { BecomeHostileTowards(threatToFleeFrom, true); }
            return;
        }
        
        string message = $"{gameObject.name} напуган {threatToFleeFrom.name} и убегает!";
        if (feedbackManager != null) { feedbackManager.ShowFeedbackMessage(message); }
        else { playerPartyTransformRef?.GetComponentInChildren<FeedbackManager>()?.ShowFeedbackMessage(message); }
        
        currentThreatInternal = threatToFleeFrom; // Устанавливаем угрозу, от которой бежим
        currentState = AIState.Fleeing;
        isWanderingToDestination = false;
    }
    
    private void FaceTarget(Transform target)
    {
        if (target == null) return;
        Vector3 direction = (target.position - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = lookRotation; // Consider slerping for smoother rotation
        }
    }

    private void HandleDeath() 
    {
        if (currentState == AIState.Dead) return;
        currentState = AIState.Dead;
        isWanderingToDestination = false;

        movement.ResetAndStopAgent(); 
        movement.DisableAgent();      

        GrantExperienceToParty();
        SetupLootableCorpse();
    }

    private void GrantExperienceToParty() 
    {
        if (partyManager == null || experienceReward <= 0) return;
        List<CharacterStats> livingMembers = partyManager.partyMembers.Where(member => member != null && !member.IsDead).ToList();
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
        if (myStats != null)
        {
            myStats.onDied -= HandleDeath;
            myStats.onHealthChanged.RemoveListener(CheckFleeConditionOnHealthChange);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Параметры обнаружения и состояния из AIPerception, если он уже получен
        if (perception != null)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, perception.aggroRadiusPlayer);
            if (fleesOnSightOfPlayer) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, perception.fleeOnSightRadiusPlayer); }
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