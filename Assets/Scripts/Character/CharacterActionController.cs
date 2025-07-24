using UnityEngine;

public class CharacterActionController : MonoBehaviour
{
    // Убираем Acting, оно избыточно
    public enum ActionState { Ready, Recovery }

    [Header("Состояние")]
    [SerializeField] private ActionState currentState = ActionState.Ready;
    public ActionState CurrentState => currentState;

    [Header("Настройки")]
    [Tooltip("Базовое время восстановления, если не указано иное")]
    public float defaultRecoveryTime = 1.0f;

    private float recoveryTimer = 0f;

    private CharacterStats myStats;
    private CharacterAbilities myAbilities;
    private FeedbackManager feedbackManager;

    public event System.Action<ActionState> OnStateChanged;
    public event System.Action OnActionStarted; // Переименовываем событие для ясности

    void Awake()
    {
        myStats = GetComponent<CharacterStats>();
        myAbilities = GetComponent<CharacterAbilities>();
        feedbackManager = FindObjectOfType<FeedbackManager>();
    }

    void Start()
    {
        SetState(ActionState.Ready);
    }

    void Update()
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

    private void SetState(ActionState newState)
    {
        if (currentState == newState && Application.isPlaying) return;
        currentState = newState;
        OnStateChanged?.Invoke(newState);
    }
    
    private void PerformRecovery(float recoveryTime)
    {
        recoveryTimer = (recoveryTime > 0) ? recoveryTime : defaultRecoveryTime;
        SetState(ActionState.Recovery);
        // ВЫЗЫВАЕМ СОБЫТИЕ СРАЗУ
        OnActionStarted?.Invoke();
    }
    
    public bool TryAttack(CharacterStats targetStats)
    {
        if (currentState != ActionState.Ready || myStats.IsDead || targetStats == null) return false;

        if (targetStats.IsDead)
        {
            feedbackManager?.ShowFeedbackMessage($"{targetStats.name} is already defeated.");
            return false;
        }

        // --- ИЗМЕНЕНИЕ: Переход на EffectResult и FeedbackGenerator ---
        
        // 1. Создаем структуру для хранения результата
        var result = new EffectResult
        {
            WasApplied = true,
            IsSingleTarget = true,
            TargetName = targetStats.gameObject.name
        };

        int baseHitChance = 70;
        int minHitChance = 10;
        int maxHitChance = 95;

        int hitChance = Mathf.Clamp(baseHitChance + myStats.AgilityHitBonusPercent - targetStats.AgilityEvasionBonusPercent, minHitChance, maxHitChance);
        
        if (Random.Range(0, 100) < hitChance)
        {
            // Попадание
            int damageToDeal = myStats.CalculatedDamage;
            targetStats.TakeDamage(damageToDeal, transform);
            
            // 2. Заполняем результат для попадания
            result.TargetsAffected = 1;
            result.TotalValue = damageToDeal;
        }
        else
        {
            // Промах
            // 2. Заполняем результат для промаха
            result.TargetsAffected = 0;
            result.TotalValue = 0;
        }

        // 3. Генерируем сообщение и показываем его
        string feedbackMessage = FeedbackGenerator.GenerateAttackFeedback(result, myStats.gameObject.name);
        feedbackManager?.ShowFeedbackMessage(feedbackMessage);

        // 4. Уходим на восстановление
        PerformRecovery(myStats.CalculatedAttackCooldown);
        return true;
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---
    }

    public bool TryUseAbility(int abilityIndex, CharacterStats primaryTarget, Transform interactableTarget, Vector3 groundTarget)
    {
        if (currentState != ActionState.Ready || myStats.IsDead) return false;
        
        AbilitySlot slot = myAbilities.GetAbilitySlotByIndex(abilityIndex);
        if (slot == null || !myAbilities.CanUseAbility(slot)) return false;
        
        Vector3 effectPoint = groundTarget;
        Transform finalTargetTransform = interactableTarget;

        if (primaryTarget != null)
        {
            effectPoint = primaryTarget.transform.position;
            finalTargetTransform = primaryTarget.transform;
        } 
        else if (slot.abilityData.targetType == TargetType.AreaAroundCaster || slot.abilityData.targetType == TargetType.Self)
        {
            effectPoint = myStats.transform.position;
            // Для Self/AreaAroundCaster целью является сам кастер
            primaryTarget = myStats;
            finalTargetTransform = myStats.transform;
        }
        
        bool castInitiated = myAbilities.TryUseAbility(
            slot, myStats, feedbackManager, 
            finalTargetTransform, effectPoint, slot.abilityData
        );

        if(castInitiated)
        {
            // Важно: Use() теперь вызывается внутри TryUseAbility в CharacterAbilities
            PerformRecovery(slot.abilityData.cooldown > 0 ? slot.abilityData.cooldown : defaultRecoveryTime);
            return true;
        }
        
        return false;
    }
}