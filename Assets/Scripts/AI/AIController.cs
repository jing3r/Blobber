using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Центральный "мозг" AI, управляющий сменой состояний (State Machine) и поведением.
/// Является точкой сборки для всех остальных AI-компонентов.
/// </summary>
[RequireComponent(typeof(CharacterStats), typeof(AIMovement), typeof(AICombat))]
[RequireComponent(typeof(AIPerception), typeof(AIWanderBehavior))]
public class AIController : MonoBehaviour, ISaveable
{
    public enum AIState { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
    public enum Alignment { Friendly, Neutral, Hostile }
    
    [Header("Поведение AI")]
    [SerializeField] private Alignment currentAlignment = Alignment.Hostile;
    [SerializeField] private bool canBecomeHostileOnAttack = true;
    [SerializeField] private bool canFlee = false;
    [SerializeField] [Range(0f, 1f)] private float fleeHealthThreshold = 0.3f;
    [SerializeField] private bool fleesOnSightOfPlayer = false;
    [SerializeField] private bool canWander = true;

    [Header("Параметры состояний")]
    [SerializeField] private float attackStateSwitchRadius = 2f; 
    [SerializeField] private float fleeDistance = 20f;

    [Header("Награды")]
    [SerializeField] private List<InventoryItem> potentialLoot = new List<InventoryItem>();
    [SerializeField] private int experienceReward = 25;
    
    [Header("Отладка")]
    [SerializeField] private AIState currentStateDebugView;

    public AIMovement Movement { get; private set; }
    public AICombat Combat { get; private set; }
    public AIPerception Perception { get; private set; }
    public AIWanderBehavior WanderBehavior { get; private set; }
    public CharacterStats MyStats { get; private set; }
    
    public bool FleesOnSightOfPlayer => fleesOnSightOfPlayer;
    public bool CanWander => canWander;
    public float FleeDistance => fleeDistance;
    public float AttackStateSwitchRadius => attackStateSwitchRadius;
    public Alignment CurrentAlignment => currentAlignment;

    public PartyManager PartyManagerRef { get; private set; }
    
    // State Machine
    private IAIState currentStateObject;
    private Dictionary<AIState, IAIState> availableStates;
    
    public Transform CurrentThreat { get; private set; }
    private float stateLockTimer;
    
    #region Unity Lifecycle & Initialization
    private void Awake()
    {

        Movement = GetComponent<AIMovement>();
        Combat = GetComponent<AICombat>(); 
        Perception = GetComponent<AIPerception>();
        WanderBehavior = GetComponent<AIWanderBehavior>();
        MyStats = GetComponent<CharacterStats>();

        InitializeStates();
    }

    private void Start()
    {
        PartyManagerRef = FindObjectOfType<PartyManager>();
        
        if (CanWander) WanderBehavior.InitializeWanderTimer();
        Movement.StoppingDistance = AttackStateSwitchRadius * 0.9f;
        
        MyStats.onDied += HandleDeath;
        MyStats.onHealthChanged.AddListener(CheckFleeConditionOnHealthChange);
        
        ChangeState(AIState.Idle);
    }

    private void OnDestroy() 
    {
        if (MyStats != null)
        {
            MyStats.onDied -= HandleDeath;
            MyStats.onHealthChanged.RemoveListener(CheckFleeConditionOnHealthChange);
        }
    }

    private void Update()
    {
        if (currentStateObject == null || MyStats.IsDead) return;
        
        currentStateObject.UpdateState(this);
        currentStateDebugView = currentStateObject.GetStateType();
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
    #endregion

    #region State Machine Management
    /// <summary>
    /// Переключает AI в новое состояние.
    /// </summary>
    public void ChangeState(AIState newStateKey)
    {
        // Защита от смены состояний в определенных условиях
        if (MyStats.IsDead && newStateKey != AIState.Dead) return;
        if (currentStateObject?.GetStateType() == AIState.Fleeing && Time.time < stateLockTimer) return;

        if (availableStates.TryGetValue(newStateKey, out IAIState newStateObject))
        {
            currentStateObject?.ExitState(this);
            currentStateObject = newStateObject;
            currentStateObject.EnterState(this);
        }
    }

    /// <summary>
    /// Блокирует смену состояния на определенное время. Используется для бегства.
    /// </summary>
    public void LockStateForDuration(float duration)
    {
        stateLockTimer = Time.time + duration;
    }
    #endregion

    #region Threat Management & Reactions
    /// <summary>
    /// Устанавливает новую угрозу для AI.
    /// </summary>
    public void SetCurrentThreat(Transform threat) => CurrentThreat = threat;
    
    /// <summary>
    /// Сбрасывает текущую угрозу.
    /// </summary>
    public void ClearCurrentThreat() => CurrentThreat = null;

    /// <summary>
    /// Реакция на получение урона.
    /// </summary>
    public void ReactToDamage(Transform attacker)
    {
        // Если уже убегаем или уже атакуем эту цель, ничего не делаем
        if (currentStateObject?.GetStateType() == AIState.Fleeing) return;
        if (CurrentThreat == attacker && (currentStateObject?.GetStateType() == AIState.Chasing || currentStateObject?.GetStateType() == AIState.Attacking)) return;
        
        BecomeHostileTowards(attacker, true); 
    }
    /// <summary>
    /// Изменяет текущее отношение AI к игровым персонажам.
    /// </summary>
    public void SetAlignment(Alignment newAlignment)
    {
        currentAlignment = newAlignment;
    }
    /// <summary>
    /// Заставляет AI стать враждебным к цели.
    /// </summary>
    public void BecomeHostileTowards(Transform threatSource, bool forceAggro = false)
    {
        bool canTurnHostile = currentAlignment != Alignment.Friendly || canBecomeHostileOnAttack || forceAggro;
        if (!canTurnHostile) return;
        
        currentAlignment = Alignment.Hostile;
        SetCurrentThreat(threatSource); 
        ChangeState(AIState.Chasing);
    }

    /// <summary>
    /// Заставляет AI убегать от источника угрозы.
    /// </summary>
    public void ForceFlee(Transform threatToFleeFrom)
    {
        if (MyStats.IsDead) return;
        
        if (!canFlee) 
        {
            // Если не умеет убегать, становится враждебным в ответ
            BecomeHostileTowards(threatToFleeFrom, true);
            return;
        }

        SetCurrentThreat(threatToFleeFrom); 
        ChangeState(AIState.Fleeing);
        LockStateForDuration(3.0f); 
    }

    private void CheckFleeConditionOnHealthChange(int currentHp, int maxHp)
    {
        if (!canFlee || MyStats.IsDead || currentStateObject?.GetStateType() == AIState.Fleeing) return;
        
        if ((float)currentHp / maxHp <= fleeHealthThreshold)
        {
            // Если есть текущая угроза - убегаем от нее. Если нет - от игрока, если он виден.
            var threat = CurrentThreat ?? Perception.PrimaryHostileThreat;
            if (threat != null)
            {
                ForceFlee(threat);
            }
        }
    }
    #endregion

    #region Death & Loot
    private void HandleDeath() 
    {
        ChangeState(AIState.Dead); // Формально переключаем состояние для логгирования
        currentStateObject = null;
        this.enabled = false;

        Movement.ResetAndStopAgent(); 
        Movement.DisableAgent();
        
        SetupLootableCorpse();
        GrantExperienceToParty();
    }

    private void GrantExperienceToParty() 
    {
        if (PartyManagerRef == null || experienceReward <= 0) return;
        
        var livingMembers = PartyManagerRef.PartyMembers.Where(member => member != null && !member.IsDead).ToList();
        if (livingMembers.Count > 0)
        {
            int xpPerMember = Mathf.Max(1, experienceReward / livingMembers.Count);
            foreach (var member in livingMembers)
            {
                member.GainExperience(xpPerMember);
            }
        }
    }

    private void SetupLootableCorpse()
    {
        // Гарантируем наличие необходимых компонентов для трупа
        var lootable = GetComponent<LootableCorpse>() ?? gameObject.AddComponent<LootableCorpse>();
        var corpseInventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        lootable.enabled = true;

        if (corpseInventory.Items.Count == 0 && potentialLoot.Count > 0)
        {
            foreach (var lootEntry in potentialLoot)
            {
                if (lootEntry?.ItemData != null && lootEntry.Quantity > 0)
                {
                    corpseInventory.AddItem(lootEntry.ItemData, lootEntry.Quantity);
                }
            }
        }
    }
    #endregion
    
    #region Save System Implementation
    [Serializable]
    private struct EnemySaveData
    {
        public int CurrentHealth;
        public Alignment CurrentAlignment;
        public float PositionX, PositionY, PositionZ;
        public float RotationY;
        public bool IsDead;
        public List<InventorySlotSaveData> CorpseLoot;
    }

    public object CaptureState()
    {
        var data = new EnemySaveData
        {
            CurrentHealth = MyStats.currentHealth,
            CurrentAlignment = this.currentAlignment,
            PositionX = transform.position.x,
            PositionY = transform.position.y,
            PositionZ = transform.position.z,
            RotationY = transform.eulerAngles.y,
            IsDead = MyStats.IsDead,
        };

        if (data.IsDead)
        {
            var corpseInventory = GetComponent<Inventory>();
            if (corpseInventory != null)
            {
                data.CorpseLoot = corpseInventory.Items
                    .Where(item => item?.ItemData != null)
                    .Select(item => new InventorySlotSaveData 
                    { 
                        ItemDataName = item.ItemData.name, 
                        Quantity = item.Quantity 
                    }).ToList();
            }
        }
        return data;
    }

    public void RestoreState(object state)
    {
        if (state is EnemySaveData data)
        {
            MyStats.currentHealth = data.CurrentHealth;
            this.currentAlignment = data.CurrentAlignment;
            transform.position = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
            transform.eulerAngles = new Vector3(0, data.RotationY, 0);

            MyStats.RefreshStatsAfterLoad();

            if (data.IsDead)
            {
                RestoreDeadState();
                RestoreCorpseInventory(data.CorpseLoot);
            }
            else
            {
                RestoreAliveState();
            }
        }
    }

    private void RestoreDeadState()
    {
        if (!MyStats.IsDead)
        {
            // Убиваем персонажа, если он почему-то был жив в сцене
            MyStats.TakeDamage(MyStats.maxHealth + 999);
        }
        var lootableCorpse = GetComponent<LootableCorpse>() ?? gameObject.AddComponent<LootableCorpse>();
        lootableCorpse.enabled = true;
    }

    private void RestoreAliveState()
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
    
    private void RestoreCorpseInventory(List<InventorySlotSaveData> lootData)
    {
        var corpseInventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        corpseInventory.Clear();

        if (lootData == null) return;
        
        foreach(var savedItem in lootData)
        {
            ItemData itemData = Resources.Load<ItemData>($"Items/{savedItem.ItemDataName}");
            if (itemData != null)
            {
                corpseInventory.AddItem(itemData, savedItem.Quantity);
            }
        }
        corpseInventory.TriggerInventoryChanged();
    }
    #endregion
}