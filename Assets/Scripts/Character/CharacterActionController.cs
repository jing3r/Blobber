using UnityEngine;

/// <summary>
/// Управляет состоянием "готов / не готов" персонажа и является точкой входа для всех его действий (атака, способности).
/// </summary>
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterAbilities))]
public class CharacterActionController : MonoBehaviour
{
    public enum ActionState { Ready, Recovery }

    [Header("Состояние")]
    [SerializeField] private ActionState currentState = ActionState.Ready;
    public ActionState CurrentState => currentState;

    [Header("Настройки")]
    [SerializeField]
    [Tooltip("Базовое время восстановления, если не указано иное.")]
    private float defaultRecoveryTime = 1.0f;

    private float recoveryTimer = 0f;

    private CharacterStats myStats;
    private CharacterAbilities myAbilities;
    private FeedbackManager feedbackManager;

    public event System.Action<ActionState> OnStateChanged;
    public event System.Action OnActionStarted;

    private void Awake()
    {
        myStats = GetComponent<CharacterStats>();
        myAbilities = GetComponent<CharacterAbilities>();
        feedbackManager = FindObjectOfType<FeedbackManager>(); // TODO: Рассмотреть инъекцию зависимости
    }

    private void Start()
    {
        SetState(ActionState.Ready);
    }

    private void Update()
    {
        if (currentState == ActionState.Recovery)
        {
            recoveryTimer -= Time.deltaTime;
            if (recoveryTimer <= 0)
            {
                SetState(ActionState.Ready);
            }
        }
    }
    
    /// <summary>
    /// Пытается выполнить базовую атаку по цели.
    /// </summary>
    /// <returns>True, если атака была предпринята (попадание или промах).</returns>
    public bool TryAttack(CharacterStats targetStats)
    {
        if (currentState != ActionState.Ready || myStats.IsDead) return false;
        if (targetStats == null || targetStats.IsDead)
        {
            feedbackManager?.ShowFeedbackMessage(targetStats == null ? "Invalid target." : $"{targetStats.name} is already defeated.");
            return false;
        }

        var result = new EffectResult
        {
            WasApplied = true,
            IsSingleTarget = true,
            TargetName = targetStats.gameObject.name
        };

        int hitChance = CalculateHitChance(targetStats);
        
        if (Random.Range(0, 100) < hitChance)
        {
            int damageToDeal = myStats.CalculatedDamage;
            targetStats.TakeDamage(damageToDeal, transform);
            result.TargetsAffected = 1;
            result.TotalValue = damageToDeal;
        }
        
        feedbackManager?.ShowFeedbackMessage(FeedbackGenerator.GenerateAttackFeedback(result, myStats.name));
        PerformRecovery(myStats.CalculatedAttackCooldown);
        return true;
    }
    
    /// <summary>
    /// Пытается использовать способность по индексу.
    /// </summary>
    /// <returns>True, если использование способности было инициировано.</returns>
    public bool TryUseAbility(int abilityIndex, CharacterStats primaryTarget, Transform interactableTarget, Vector3 groundTarget)
    {
        if (currentState != ActionState.Ready || myStats.IsDead) return false;
        
        AbilitySlot slot = myAbilities.GetAbilitySlotByIndex(abilityIndex);
        if (slot == null || !slot.IsReady()) return false;
        
        Transform finalTargetTransform = interactableTarget;
        Vector3 effectPoint = groundTarget;

        if (primaryTarget != null)
        {
            finalTargetTransform = primaryTarget.transform;
            effectPoint = finalTargetTransform.position;
        } 
        else if (slot.AbilityData.TargetType == TargetType.AreaAroundCaster || slot.AbilityData.TargetType == TargetType.Self)
        {
            finalTargetTransform = myStats.transform;
            effectPoint = finalTargetTransform.position;
        }
        
        if (myAbilities.TryUseAbility(slot, finalTargetTransform, effectPoint))
        {
            PerformRecovery(slot.AbilityData.Cooldown > 0 ? slot.AbilityData.Cooldown : defaultRecoveryTime);
            return true;
        }
        
        return false;
    }
    
    private void SetState(ActionState newState)
    {
        if (currentState == newState && Application.isPlaying) return;
        
        currentState = newState;
        OnStateChanged?.Invoke(newState);
    }
    
    private void PerformRecovery(float recoveryTime)
    {
        recoveryTimer = Mathf.Max(0.1f, recoveryTime); // Минимальное время восстановления, чтобы избежать 0
        SetState(ActionState.Recovery);
        OnActionStarted?.Invoke();
    }

    private int CalculateHitChance(CharacterStats targetStats)
    {
        const int baseHitChance = 70;
        const int minHitChance = 10;
        const int maxHitChance = 95;

        int hitChance = baseHitChance + myStats.AgilityHitBonusPercent - targetStats.AgilityEvasionBonusPercent;
        return Mathf.Clamp(hitChance, minHitChance, maxHitChance);
    }
}