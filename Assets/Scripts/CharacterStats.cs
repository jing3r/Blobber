using UnityEngine;
using UnityEngine.Events;

public class CharacterStats : MonoBehaviour
{
    [Header("Основные статы")]
    public int maxHealth = 100; // Это значение будет пересчитываться, но оставим как базовое для инспектора
    public int currentHealth;

    [Header("Состояние")]
    [SerializeField] private bool isDead = false;
    public bool IsDead => isDead;

    [Header("Опыт и Уровни")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100;

    [Header("Атрибуты")]
    public int baseBody = 3;
    public int baseMind = 3;
    public int baseSpirit = 3;
    public int baseAgility = 3;
    public int baseProficiency = 1; // Пока не используется

    public int CurrentBody { get; private set; }
    public int CurrentMind { get; private set; }
    public int CurrentSpirit { get; private set; }
    public int CurrentAgility { get; private set; }
    public int CurrentProficiency { get; private set; }

    // Производные характеристики
    public int CalculatedDamage { get; private set; }
    public float CalculatedMaxCarryWeight { get; private set; }
    public float CalculatedAttackCooldown { get; private set; }
    public int AgilityHitBonusPercent { get; private set; }     // Бонус к попаданию от ловкости атакующего
    public int AgilityEvasionBonusPercent { get; private set; } // Бонус к увороту от ловкости защищающегося

    [Header("Настройки производных статов")]
    [SerializeField] private int baseDamagePerBodyPoint = 5;
    [SerializeField] private float baseCarryWeight = 10f;
    [SerializeField] private float carryWeightPerBodyPoint = 10f;
    [SerializeField] private float baseAttackCooldown = 1.0f;
    [SerializeField] private float agilityCooldownReduction = 0.05f; // Уменьшено для более заметного эффекта с меньшими значениями ловкости
    [SerializeField] private float minAttackCooldown = 0.2f; // Минимальный кулдаун атаки
    [SerializeField] private int agilityBonusPerPoint = 5; // % к шансу попадания/уворота за очко ловкости (было 10, уменьшил для баланса)


    [System.Serializable] public class HealthChangeEvent : UnityEvent<int, int> { }
    public HealthChangeEvent onHealthChanged;
    public event System.Action onDied;
    public event System.Action<int, int> onExperienceChanged; // currentXP, xpToNextLevel
    public event System.Action<int> onLevelUp;                 // newLevel
    public event System.Action onAttributesChanged;

    void Awake()
    {
        isDead = false;
        InitializeAttributes(); // Инициализируем атрибуты
        CalculateDerivedStats(); // Рассчитываем производные статы на основе инициализированных атрибутов
        currentHealth = maxHealth; // Устанавливаем здоровье после того, как maxHealth был рассчитан
    }

    void Start()
    {
        // Вызываем события для инициализации UI и других систем
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onLevelUp?.Invoke(level); // Чтобы UI атрибутов и уровня обновился
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        onAttributesChanged?.Invoke(); // Убедимся, что все подписанные на атрибуты системы обновлены
    }

    private void InitializeAttributes()
    {
        CurrentBody = baseBody;
        CurrentMind = baseMind;
        CurrentSpirit = baseSpirit;
        CurrentAgility = baseAgility;
        CurrentProficiency = baseProficiency;
    }

    private void CalculateDerivedStats()
    {
        // Здоровье
        maxHealth = 50 + (CurrentBody * 15) + (level * 5);

        // Урон
        CalculatedDamage = CurrentBody * baseDamagePerBodyPoint;
        if (CalculatedDamage < 1) CalculatedDamage = 1; // Минимальный урон

        // Переносимый вес
        CalculatedMaxCarryWeight = baseCarryWeight + (CurrentBody * carryWeightPerBodyPoint);

        // Скорость атаки (кулдаун)
        CalculatedAttackCooldown = Mathf.Max(minAttackCooldown, baseAttackCooldown - (CurrentAgility * agilityCooldownReduction));

        // Бонусы от ловкости к попаданию/увороту
        AgilityHitBonusPercent = CurrentAgility * agilityBonusPerPoint;
        AgilityEvasionBonusPercent = CurrentAgility * agilityBonusPerPoint;

        // Вызываем событие, если кто-то зависит от этих производных стат (например, инвентарь от веса)
        onAttributesChanged?.Invoke();
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead || damageAmount <= 0) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        onDied?.Invoke();
    }

    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void GainExperience(int amount)
    {
        if (isDead || amount <= 0) return;

        experience += amount;
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);

        while (experience >= experienceToNextLevel && !isDead)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;
        experience -= experienceToNextLevel;
        
        baseBody++; // Пример увеличения атрибута
        // В будущем здесь может быть более сложная логика выбора атрибутов

        InitializeAttributes(); // Обновляем Current атрибуты на основе базовых
        CalculateDerivedStats(); // Пересчитываем все производные статы
        
        int healthBeforeFullHeal = currentHealth;
        bool wasDeadBeforeLevelUp = isDead; // Запоминаем, был ли персонаж мертв

        currentHealth = maxHealth; // Полное восстановление здоровья

        // Если персонаж был мертв и "воскрес" от левелапа, нужно сбросить флаг isDead
        // и, возможно, вызвать какое-то событие "воскрешения", если такое нужно.
        // Но TakeDamage/Die должны корректно обрабатывать изменение isDead.
        // Для простоты, если currentHealth стал > 0, то isDead должен стать false.
        if (wasDeadBeforeLevelUp && currentHealth > 0) {
            isDead = false;
            // Здесь можно вызвать событие "воскрешения", если нужно (например, для PartyManager)
            Debug.Log($"{gameObject.name} was resurrected by leveling up!");
        }


        experienceToNextLevel = CalculateExperienceForLevel(level + 1);

        onLevelUp?.Invoke(level);
        onHealthChanged?.Invoke(currentHealth, maxHealth); // Здоровье точно изменилось
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        // onAttributesChanged уже был вызван в CalculateDerivedStats
    }

    private int CalculateExperienceForLevel(int nextLevelTarget)
    {
        if (nextLevelTarget <= 1) return 100;
        return 50 * (nextLevelTarget - 1) * (nextLevelTarget - 1) + 50 * (nextLevelTarget - 1) + 100;
    }

    public void RefreshStatsAfterLoad()
    {
        bool previousDeadState = isDead;
        InitializeAttributes(); // Устанавливает Current атрибуты из базовых (которые были загружены)
        CalculateDerivedStats(); // Пересчитывает maxHealth и другие производные статы

        // currentHealth был установлен напрямую из сохранения
        // Важно: maxHealth мог измениться, currentHealth нужно ограничить новым maxHealth
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        isDead = (currentHealth <= 0);

        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onLevelUp?.Invoke(level); // Для обновления UI с уровнем
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        // onAttributesChanged уже был вызван в CalculateDerivedStats

        if (isDead && !previousDeadState)
        {
            onDied?.Invoke();
        }
        // Если персонаж был мертв, а после загрузки и пересчета статов (например, из-за изменения формулы здоровья) стал жив
        else if (!isDead && previousDeadState)
        {
            // Возможно, здесь нужно какое-то событие "оживления после загрузки", но пока оставим так.
            // Главное, что UI и другие системы получат актуальные onHealthChanged и onAttributesChanged.
        }
    }
}