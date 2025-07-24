using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Добавляем ISaveable
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(AIMovement))]
[RequireComponent(typeof(AICombat))]
[RequireComponent(typeof(AIPerception))]
[RequireComponent(typeof(AIWanderBehavior))]
public class AIController : MonoBehaviour, ISaveable
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
            if (FeedbackManagerRef == null) 
            {
                FeedbackManagerRef = FindObjectOfType<FeedbackManager>();
            }
        }
        
        if (canWander) WanderBehavior.InitializeWanderTimer(); 
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
        if (currentStateObject != null) currentStateDebugView = currentStateObject.GetStateType();
    }

    public void ChangeState(AIState newStateKey)
    {
        if (currentStateObject?.GetStateType() == AIState.Fleeing && newStateKey != AIState.Dead && Time.time < _stateLockTimer) return;
        if (currentStateObject != null && currentStateObject.GetStateType() == AIState.Dead && newStateKey != AIState.Dead) return;

        if (availableStates.TryGetValue(newStateKey, out IAIState newStateObject))
        {
            currentStateObject?.ExitState(this);
            currentStateObject = newStateObject;
            currentStateObject.EnterState(this);
            currentStateDebugView = newStateKey;
        }
    }
    
    public void ClearCurrentThreat() { currentThreatInternal = null; }
    public void SetCurrentThreat(Transform threat) { currentThreatInternal = threat; }

    private void CheckFleeConditionOnHealthChange(int currentHp, int maxHp)
    {
        if (!canFlee || currentStateObject?.GetStateType() == AIState.Dead || currentStateObject?.GetStateType() == AIState.Fleeing || MyStats == null) return;
        if ((float)currentHp / MyStats.maxHealth <= fleeHealthThreshold)
        {
            if (CurrentThreat != null) { ForceFlee(CurrentThreat); }
            else if (Perception.PlayerTarget != null && Perception.IsTargetInRadius(Perception.PlayerTarget, Perception.engageRadius)) { ForceFlee(Perception.PlayerTarget); }
        }
    }
    
    public void BecomeHostileTowards(Transform threatSource, bool forceAggro = false)
    {
        if (currentAlignment == Alignment.Hostile && CurrentThreat == threatSource && !forceAggro)
        {
            if (currentStateObject?.GetStateType() == AIState.Idle || currentStateObject?.GetStateType() == AIState.Wandering || currentStateObject?.GetStateType() == AIState.Fleeing) 
            {
                Movement.FaceTarget(threatSource); 
                ChangeState(AIState.Chasing);
            }
            return;
        }
        
        bool canActuallyBecomeHostile = currentAlignment == Alignment.Hostile || (currentAlignment == Alignment.Neutral) || (currentAlignment == Alignment.Friendly && canBecomeHostileOnAttack) || forceAggro;
        
        if (canActuallyBecomeHostile)
        {
            currentAlignment = Alignment.Hostile;
            SetCurrentThreat(threatSource); 
            Movement.FaceTarget(threatSource); 
            ChangeState(AIState.Chasing);
        }
    }

    public void ForceFlee(Transform threatToFleeFrom)
    {
        if (currentStateObject?.GetStateType() == AIState.Dead) return;
        if (!canFlee) {
            if (currentAlignment != Alignment.Hostile && PlayerPartyTransformRef != null && threatToFleeFrom == PlayerPartyTransformRef) { BecomeHostileTowards(threatToFleeFrom, true); }
            return;
        }
        SetCurrentThreat(threatToFleeFrom); 
        ChangeState(AIState.Fleeing);
        LockStateForDuration(3.0f); 
    }

    public void ReactToDamage(Transform attacker)
    {
        if (currentStateObject?.GetStateType() == AIState.Fleeing) return;
        if (currentAlignment == Alignment.Hostile && CurrentThreat == attacker && (currentStateObject?.GetStateType() == AIState.Chasing || currentStateObject?.GetStateType() == AIState.Attacking))
        {
            Movement.FaceTarget(attacker); 
            return;
        }
        BecomeHostileTowards(attacker, true); 
    }

    private void HandleDeath() 
    {
        // Переходим в состояние "мертв"
        currentStateObject?.ExitState(this);
        currentStateObject = null; 
        currentStateDebugView = AIState.Dead;

        // Выключаем компоненты, отвечающие за движение и поведение
        Movement.ResetAndStopAgent(); 
        Movement.DisableAgent();
        this.enabled = false; // Отключаем сам AIController, чтобы Update не работал
        
        // Включаем "трупное" поведение
        SetupLootableCorpse();

        // Оповещаем о смерти (для квестов, UI и т.д.)
        GrantExperienceToParty();
        
        // ВАЖНО: Мы больше НЕ УНИЧТОЖАЕМ gameObject!
    }

    private void GrantExperienceToParty() 
    {
        if (PartyManagerRef == null || experienceReward <= 0) return;
        var livingMembers = PartyManagerRef.partyMembers.Where(member => member != null && !member.IsDead).ToList();
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
    lootable.enabled = true;

    Inventory corpseInventory = gameObject.GetComponent<Inventory>();
    if (corpseInventory == null) corpseInventory = gameObject.AddComponent<Inventory>();
    
    // Работаем с инвентарем трупа, а не с самим LootableCorpse
    if (corpseInventory.items.Count == 0)
    {
        if (potentialLoot != null)
        {
            foreach (var lootEntry in potentialLoot)
            {
                if (lootEntry.itemData != null && lootEntry.quantity > 0)
                {
                    corpseInventory.AddItem(lootEntry.itemData, lootEntry.quantity);
                }
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
    }

    #region SaveSystem

    [System.Serializable]
    private struct EnemySaveData
    {
        // Состояние "живого" NPC
        public int currentHealth;
        public AIController.Alignment currentAlignment;
        public float positionX, positionY, positionZ;
        public float rotationY;
        
        // Состояние "мертвого" NPC
        public bool isDead;
        public List<InventorySlotSaveData> corpseLoot; // Сохраняем оставшийся лут
    }

public object CaptureState()
{
    var data = new EnemySaveData
    {
        currentHealth = MyStats.currentHealth,
        currentAlignment = this.currentAlignment,
        positionX = transform.position.x,
        positionY = transform.position.y,
        positionZ = transform.position.z,
        rotationY = transform.eulerAngles.y,
        isDead = MyStats.IsDead,
        corpseLoot = new List<InventorySlotSaveData>()
    };

    // Если NPC мертв, сохраняем его инвентарь
    if (data.isDead)
    {
        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        // Получаем компонент Inventory, а не LootableCorpse
        var corpseInventory = GetComponent<Inventory>();
        if (corpseInventory != null)
        {
            // Итерируемся по списку items из инвентаря
            foreach (var item in corpseInventory.items)
            {
                if (item?.itemData == null) continue;
                data.corpseLoot.Add(new InventorySlotSaveData 
                { 
                    itemDataName = item.itemData.name, 
                    quantity = item.quantity 
                });
            }
        }
    }

    return data;
}

    public void RestoreState(object state)
    {
        if (state is EnemySaveData saveData)
        {
            MyStats.currentHealth = saveData.currentHealth;
            this.currentAlignment = saveData.currentAlignment;
            transform.position = new Vector3(saveData.positionX, saveData.positionY, saveData.positionZ);
            transform.eulerAngles = new Vector3(0, saveData.rotationY, 0);

        MyStats.RefreshStatsAfterLoad();

        if (saveData.isDead)
        {
            if (!MyStats.IsDead)
            {
                MyStats.TakeDamage(MyStats.maxHealth + 999); 
            }
            
            // Получаем инвентарь и LootableCorpse
            var lootableCorpse = GetComponent<LootableCorpse>();
            if (lootableCorpse == null) lootableCorpse = gameObject.AddComponent<LootableCorpse>();
            
            var corpseInventory = GetComponent<Inventory>();
            if (corpseInventory == null) corpseInventory = gameObject.AddComponent<Inventory>();

            // Восстанавливаем инвентарь
            corpseInventory.items.Clear();
            foreach(var savedItem in saveData.corpseLoot)
            {
                ItemData itemData = Resources.Load<ItemData>($"Items/{savedItem.itemDataName}");
                if (itemData != null)
                {
                    // Создаем InventoryItem напрямую, т.к. AddItem будет искать место, а нам нужно восстановить как было
                    corpseInventory.items.Add(new InventoryItem(itemData, savedItem.quantity, -1, -1, corpseInventory));
                }
            }
            // Вызываем событие, чтобы UI обновился, если он открыт
            corpseInventory.SendMessage("OnInventoryChanged", SendMessageOptions.DontRequireReceiver);

            // Обновляем подсказку в зависимости от пустоты инвентаря
            if (corpseInventory.items.Count == 0)
            {
                lootableCorpse.interactionPrompt = "Searched";
            }
            else
            {
                lootableCorpse.interactionPrompt = $"Search {gameObject.name}";
            }
        }
        else
        {
                this.enabled = true;
                Movement.EnableAgent();
                GetComponent<Collider>().enabled = true;

                var lootableCorpse = GetComponent<LootableCorpse>();
                if (lootableCorpse != null) lootableCorpse.enabled = false;

                var healthUI = GetComponentInChildren<EnemyHealthUI>(true);
                if (healthUI != null) healthUI.gameObject.SetActive(true);

                ChangeState(AIState.Idle);
            }
        }
    }
    #endregion
}