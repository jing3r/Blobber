using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(AIMovement))]
[RequireComponent(typeof(AICombat))]
[RequireComponent(typeof(AIPerception))]
[RequireComponent(typeof(AIWanderBehavior))]
public class AIController : MonoBehaviour
{
    public enum AIState { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
    [SerializeField] private AIState currentStateDebugView;
    private IAIState currentStateObject;
    private Dictionary<AIState, IAIState> availableStates;

    public enum Alignment { Friendly, Neutral, Hostile }
    [Header("AI Behavior")]
    public Alignment currentAlignment = Alignment.Hostile;
    public bool canBecomeHostileOnAttack = true;
    public bool canFlee = false;
    public float fleeHealthThreshold = 0.3f;
    public bool fleesOnSightOfPlayer = false;
    public bool canWander = true;

    [Header("State Transition Parameters (Distances)")]
    public float attackStateSwitchRadius = 2f; 
    public float fleeDistance = 20f;

    [Header("Rewards")]
    public List<InventoryItem> potentialLoot = new List<InventoryItem>();
    public int experienceReward = 25;

    public AIMovement Movement { get; private set; }
    public AICombat Combat { get; private set; }
    public AIPerception Perception { get; private set; }
    public AIWanderBehavior WanderBehavior { get; private set; }
    public CharacterStats MyStats { get; private set; }
    public Transform PlayerPartyTransformRef { get; private set; }
    public PartyManager PartyManagerRef { get; private set; }
    public FeedbackManager FeedbackManagerRef { get; private set; }
    
    private Transform currentThreatInternal;
    public Transform CurrentThreat => currentThreatInternal;
    private float _stateLockTimer = 0f;
public void LockStateForDuration(float duration)
{
    _stateLockTimer = Time.time + duration;
    // Debug.Log($"{gameObject.name} state change locked for {duration} seconds.");
}
    void Awake()
    {
        Movement = GetComponent<AIMovement>();
        Combat = GetComponent<AICombat>(); 
        Perception = GetComponent<AIPerception>();
        WanderBehavior = GetComponent<AIWanderBehavior>();
        MyStats = GetComponent<CharacterStats>();

        if (MyStats == null || Movement == null || Combat == null || Perception == null || WanderBehavior == null)
        {
            Debug.LogError($"AIController ({gameObject.name}): One or more required components are missing! AI will be disabled.", this);
            enabled = false; 
            return;
        }

        MyStats.onDied += HandleDeath;
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
            FeedbackManagerRef = GetComponent<FeedbackManager>();
            if (FeedbackManagerRef == null) 
            {
                FeedbackManagerRef = playerObject.GetComponentInChildren<FeedbackManager>();
            }
        }
        
        if (canWander)
        {
            WanderBehavior.InitializeWanderTimer(); 
        }
        
        if (Combat != null) Combat.effectiveAttackRange = attackStateSwitchRadius;
        Movement.StoppingDistance = attackStateSwitchRadius * 0.9f;
        
        ChangeState(AIState.Idle);
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
        };
    }

    void Update()
    {
        if (currentStateObject == null || MyStats == null || MyStats.IsDead)
        {
            if (currentStateObject == null && (MyStats != null && !MyStats.IsDead))
            {
                ChangeState(AIState.Idle); 
            }
            return; 
        }

        currentStateObject.UpdateState(this);
        
        if (currentStateObject != null) 
        {
            currentStateDebugView = currentStateObject.GetStateType();
        }
    }

public void ChangeState(AIState newStateKey)
{
    // Проверяем, не заблокирована ли смена состояния, если текущее состояние Fleeing
    if (currentStateObject?.GetStateType() == AIState.Fleeing && 
        newStateKey != AIState.Dead && // Смерть всегда должна иметь приоритет
        Time.time < _stateLockTimer)
    {
        // Debug.Log($"{gameObject.name}: State change from Fleeing to {newStateKey} blocked by timer.");
        return; 
    }

    if (currentStateObject != null && currentStateObject.GetStateType() == AIState.Dead && newStateKey != AIState.Dead) 
    {
        // Если уже мертв, не можем сменить состояние ни на что, кроме как на Dead (на случай повторного вызова HandleDeath)
        return;
    }


    if (availableStates.TryGetValue(newStateKey, out IAIState newStateObject))
    {
        currentStateObject?.ExitState(this);
        currentStateObject = newStateObject;
        currentStateObject.EnterState(this);
        currentStateDebugView = newStateKey;
        // Debug.Log($"{gameObject.name} changed state to {newStateKey}");
    }
    else
    {
        Debug.LogWarning($"AIController ({gameObject.name}): Attempted to change to an unknown state: {newStateKey}");
    }
}
    
    public void ClearCurrentThreat()
    {
        currentThreatInternal = null;
    }

    public void SetCurrentThreat(Transform threat)
    {
        currentThreatInternal = threat;
    }

    private void CheckFleeConditionOnHealthChange(int currentHp, int maxHp)
    {
        if (!canFlee || currentStateObject?.GetStateType() == AIState.Dead || 
            currentStateObject?.GetStateType() == AIState.Fleeing || MyStats == null) return;
        
        if ((float)currentHp / MyStats.maxHealth <= fleeHealthThreshold)
        {
            if (CurrentThreat != null) { ForceFlee(CurrentThreat); }
            else if (Perception.PlayerTarget != null && Perception.IsTargetInRadius(Perception.PlayerTarget, Perception.engageRadius))
            {
                 ForceFlee(Perception.PlayerTarget);
            }
        }
    }
    
    public void BecomeHostileTowards(Transform threatSource, bool forceAggro = false)
    {
        if (currentAlignment == Alignment.Hostile && CurrentThreat == threatSource && !forceAggro)
        {
            if (currentStateObject?.GetStateType() == AIState.Idle || 
                currentStateObject?.GetStateType() == AIState.Wandering ||
                currentStateObject?.GetStateType() == AIState.Fleeing) 
            {
                Movement.FaceTarget(threatSource); 
                ChangeState(AIState.Chasing);
            }
            return;
        }
    
        bool canActuallyBecomeHostile;
        if (currentAlignment == Alignment.Hostile)
        {
            canActuallyBecomeHostile = true; 
        }
        else 
        {
            canActuallyBecomeHostile = (currentAlignment == Alignment.Neutral) || 
                                        (currentAlignment == Alignment.Friendly && canBecomeHostileOnAttack) ||
                                        forceAggro;
        }
        
        if (canActuallyBecomeHostile)
        {
            bool wasPreviouslyHostile = (currentAlignment == Alignment.Hostile);
            Transform previousThreat = CurrentThreat;

            currentAlignment = Alignment.Hostile;
            SetCurrentThreat(threatSource); 
            
            Movement.FaceTarget(threatSource); 

            if (!wasPreviouslyHostile || previousThreat != threatSource || forceAggro)
            {
                string message = $"{gameObject.name} теперь враждебен к {threatSource.name}!";
                FeedbackManagerRef?.ShowFeedbackMessage(message);
            }
            
            bool shouldChangeToChasing = false;
            AIState currentStateBeforeChange = currentStateObject?.GetStateType() ?? AIState.Idle;

            if (currentStateBeforeChange == AIState.Idle || 
                currentStateBeforeChange == AIState.Wandering || 
                currentStateBeforeChange == AIState.Fleeing)
            {
                shouldChangeToChasing = true;
            }
            else if (forceAggro)
            {
                if (currentStateBeforeChange == AIState.Attacking && CurrentThreat == previousThreat && CurrentThreat == threatSource)
                {
                    shouldChangeToChasing = false;
                }
                else
                {
                    shouldChangeToChasing = true;
                }
            }
            else if (currentStateBeforeChange == AIState.Attacking && CurrentThreat != previousThreat)
            {
                shouldChangeToChasing = true;
            }
            
            if (shouldChangeToChasing)
            {
                ChangeState(AIState.Chasing);
            }
        }
    }

public void ForceFlee(Transform threatToFleeFrom)
{
    if (currentStateObject?.GetStateType() == AIState.Dead) return;

    if (!canFlee) // Если NPC в принципе не может убегать (например, стационарная турель или очень храбрый)
    {
        // Если он не враждебен и не может убегать, он может просто стать враждебным
        if (currentAlignment != Alignment.Hostile && PlayerPartyTransformRef != null && threatToFleeFrom == PlayerPartyTransformRef)
        {
            BecomeHostileTowards(threatToFleeFrom, true); 
        }
        return; // Не убегаем
    }
    
    // Сообщение в фидбек менеджер
    string message = $"{gameObject.name} напуган {threatToFleeFrom.name} и убегает!";
    FeedbackManagerRef?.ShowFeedbackMessage(message); // Используем FeedbackManagerRef, который должен быть назначен

    SetCurrentThreat(threatToFleeFrom); 
    ChangeState(AIState.Fleeing); // Это вызовет EnterState для AIStateFleeing

    // Определяем длительность блокировки. Она должна быть связана с длительностью статуса "Страх".
    // Пока что, для простоты, можно поставить фиксированное значение или взять из CharacterStats, если там есть что-то вроде "сопротивления страху".
    // Идеально - CharacterStatusEffects должен сообщить длительность наложенного статуса "Feared".
    // Поскольку ForceFlee вызывается до того, как статус "Feared" (с его конкретной длительностью) может быть полностью обработан,
    // мы можем установить здесь разумное минимальное время блокировки, или передать длительность страха как параметр в ForceFlee.
    // Или, AIStateFleeing при входе может сам установить этот лок, если обнаружит, что на нем есть статус Feared.

    // Пока поставим короткий лок, чтобы AI не развернулся мгновенно.
    // Длительность статуса "Feared" у нас Spirit_кастера * 1.0.
    // Если кастер - игрок, мы не знаем его Spirit здесь напрямую.
    // Предположим, что средняя длительность страха будет около 3-5 секунд.
    LockStateForDuration(3.0f); 
}

    public void ReactToDamage(Transform attacker)
    {
        if (currentStateObject?.GetStateType() == AIState.Fleeing)
        {
            return; 
        }

        if (currentAlignment == Alignment.Hostile && CurrentThreat == attacker &&
            (currentStateObject?.GetStateType() == AIState.Chasing || currentStateObject?.GetStateType() == AIState.Attacking))
        {
            Movement.FaceTarget(attacker); 
            return;
        }

        bool forceAggroSwitch = true; 
        BecomeHostileTowards(attacker, forceAggroSwitch); 
    }

    private void HandleDeath() 
    {
        currentStateObject?.ExitState(this);
        currentStateObject = null; 
        currentStateDebugView = AIState.Dead;

        Movement.ResetAndStopAgent(); 
        Movement.DisableAgent();      

        GrantExperienceToParty();
        SetupLootableCorpse();
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
        if (Perception != null)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, Perception.engageRadius);
            if (fleesOnSightOfPlayer) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, Perception.fleeOnSightRadius); }
            Gizmos.color = Color.gray; Gizmos.DrawWireSphere(transform.position, Perception.disengageRadius);
        }
        
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackStateSwitchRadius);
        if (canFlee) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, fleeDistance); }
        if (canWander && WanderBehavior != null) { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, WanderBehavior.wanderRadius); }
    }
}