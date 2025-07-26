using UnityEngine;

/// <summary>
/// Управляет боевыми возможностями AI, включая расчеты урона и кулдауны атак.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
public class AICombat : MonoBehaviour
{
    [Header("Параметры боя")]
    [Tooltip("Эффективная дальность для начала атаки.")]
    [SerializeField] private float effectiveAttackRange = 2f;
    [Tooltip("Если > 0, переопределяет урон из CharacterStats.")]
    [SerializeField] private int overrideAttackDamage = 0;
    [Tooltip("Если > 0, переопределяет кулдаун атаки из CharacterStats.")]
    [SerializeField] private float overrideAttackCooldown = 0f;

    [Header("Расчет шанса попадания")]
    [SerializeField] [Range(0, 100)] private int baseHitChance = 50;
    [SerializeField] [Range(0, 100)] private int minHitChance = 5;
    [SerializeField] [Range(0, 100)] private int maxHitChance = 95;

    private CharacterStats myStats;
    private CharacterStatusEffects myStatusEffects;
    private PartyManager partyManager;
    private FeedbackManager feedbackManager;
    
    private StatusEffectData stunnedStatusData;

    private float nextAttackTime;
    public bool IsReadyToAttack => Time.time >= nextAttackTime;
    public float EffectiveAttackRange => effectiveAttackRange;

    private void Awake()
    {
        myStats = GetComponent<CharacterStats>();
        myStatusEffects = GetComponent<CharacterStatusEffects>();

        // Поиск внешних зависимостей
        partyManager = FindObjectOfType<PartyManager>();
        feedbackManager = FindObjectOfType<FeedbackManager>();
    }

    private void Start()
    {
        // Даем случайную задержку перед первой атакой, чтобы AI не атаковали синхронно
        ResetAttackTimer(true);

        // TODO: Заменить Resources.Load на систему управления ассетами
        stunnedStatusData = Resources.Load<StatusEffectData>("StatusEffects/Stunned");
        if (stunnedStatusData == null)
        {
            Debug.LogError("[AICombat] Could not load 'Stunned' StatusEffectData. Stun check will not work.");
        }
    }

    /// <summary>
    /// Пытается выполнить атаку по цели.
    /// </summary>
    /// <returns>True, если атака была предпринята (не обязательно успешна).</returns>
    public bool PerformAttack(Transform targetTransform)
    {
        if (!IsReadyToAttack || myStats.IsDead) return false;
        if (myStatusEffects != null && stunnedStatusData != null && myStatusEffects.IsStatusActive(stunnedStatusData)) return false;

        var targetStats = ResolveTarget(targetTransform);
        if (targetStats == null || targetStats.IsDead)
        {
            return false;
        }
        
        var result = new EffectResult
        {
            WasApplied = true,
            IsSingleTarget = true,
            TargetName = targetStats.name
        };

        int hitChance = CalculateHitChance(targetStats);
        if (Random.Range(0, 100) < hitChance)
        {
            int damageToDeal = (overrideAttackDamage > 0) ? overrideAttackDamage : myStats.CalculatedDamage;
            targetStats.TakeDamage(damageToDeal, transform);
            result.TargetsAffected = 1;
            result.TotalValue = damageToDeal;
        }

        feedbackManager?.ShowFeedbackMessage(FeedbackGenerator.GenerateAttackFeedback(result, myStats.name));
        ResetAttackTimer();
        return true;
    }

    private CharacterStats ResolveTarget(Transform overallTarget)
    {
        // Если цель - это вся партия игрока, выбираем случайного живого члена
        if (overallTarget.CompareTag("Player") && partyManager != null)
        {
            return partyManager.GetRandomLivingMember();
        }
        
        // В противном случае, считаем, что цель - это конкретный NPC
        return overallTarget.GetComponent<CharacterStats>();
    }

    private void ResetAttackTimer(bool initialRandomization = false)
    {
        float cooldown = (overrideAttackCooldown > 0f) ? overrideAttackCooldown : myStats.CalculatedAttackCooldown;
        if (initialRandomization)
        {
            // Добавляем случайную задержку, чтобы избежать синхронных атак в начале боя
            cooldown += Random.Range(0, cooldown * 0.3f);
        }
        nextAttackTime = Time.time + cooldown;
    }
    
    private int CalculateHitChance(CharacterStats targetStats)
    {
        int hitChance = baseHitChance + myStats.AgilityHitBonusPercent - targetStats.AgilityEvasionBonusPercent;
        return Mathf.Clamp(hitChance, minHitChance, maxHitChance);
    }
}