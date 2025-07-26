using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Хранит и управляет всеми атрибутами и производными статами персонажа.
/// Является центральным компонентом для любой сущности, имеющей статы (игрок, NPC).
/// </summary>
public class CharacterStats : MonoBehaviour
{
    #region Unity Lifecycle
    private void Awake()
    {
        if (GetComponent<CharacterEquipment>() == null)
        {
            gameObject.AddComponent<CharacterEquipment>();
        }
        aiController = GetComponent<AIController>();
        characterInventory = GetComponent<Inventory>();
        characterEquipment = GetComponent<CharacterEquipment>();
        
        _timedAttributeModifiers = new Dictionary<AssociatedAttribute, int>();
        _restAttributeModifiers = new Dictionary<AssociatedAttribute, int>();

        RecalculateAllStats();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Вызываем события для инициализации UI и других систем с актуальными данными
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onLevelUp?.Invoke(level);
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        onAttributesChanged?.Invoke();
    }
    #endregion

    #region Events
    [System.Serializable] public class HealthChangeEvent : UnityEvent<int, int> { }
    public HealthChangeEvent onHealthChanged;
    public event System.Action onDied;
    public event System.Action<int, int> onExperienceChanged;
    public event System.Action<int> onLevelUp;
    public event System.Action onAttributesChanged;
    #endregion

    #region State & Core Properties
    [Header("Состояние")]
    [SerializeField] private bool isDead = false;
    public bool IsDead => isDead;

    [Header("Здоровье")]
    public int currentHealth;
    public int maxHealth { get; private set; }


    [Header("Движение")]
    [SerializeField] private float baseMovementSpeed = 3f;
    public float CurrentMovementSpeed { get; private set; }
    private float movementSpeedMultiplierFromStatus = 1f;

    private AIController aiController;
    #endregion

    #region Experience & Leveling
    [Header("Опыт и Уровни")]
    [SerializeField] private int level = 1;
    [SerializeField] private int experience = 0;
    [SerializeField] private int experienceToNextLevel = 100;
    public int Level => level;
    public int Experience => experience;
    public int ExperienceToNextLevel => experienceToNextLevel;

    /// <summary>
    /// Начисляет персонажу опыт. Может вызвать повышение уровня.
    /// Не работает, если персонаж мертв.
    /// </summary>
    /// <param name="amount">Количество начисляемого опыта.</param>
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
        experienceToNextLevel = CalculateExperienceForLevel(level + 1);
        
        baseBody++; // Пример увеличения атрибута при левелапе
        
        bool wasDead = isDead;
        RecalculateAllStats();

        // Полное восстановление при левелапе
        currentHealth = maxHealth;
        if(wasDead) isDead = false;
        
        onLevelUp?.Invoke(level);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
    }
    
    private int CalculateExperienceForLevel(int nextLevelTarget)
    {
        if (nextLevelTarget <= 1) return 100;
        // Простая формула квадратичного роста
        return 50 * (nextLevelTarget - 1) * (nextLevelTarget - 1) + 50 * (nextLevelTarget - 1) + 100;
    }
    #endregion
    
    #region Attributes & Modifiers
    [Header("Атрибуты (Базовые значения)")]
    [SerializeField] private int baseBody = 3;
    [SerializeField] private int baseMind = 3;
    [SerializeField] private int baseSpirit = 3;
    [SerializeField] private int baseAgility = 3;
    [SerializeField] private int baseProficiency = 1;

    public int BaseBody => baseBody;
    public int BaseMind => baseMind;
    public int BaseSpirit => baseSpirit;
    public int BaseAgility => baseAgility;
    public int BaseProficiency => baseProficiency;

    public int CurrentBody { get; private set; }
    public int CurrentMind { get; private set; }
    public int CurrentSpirit { get; private set; }
    public int CurrentAgility { get; private set; }
    public int CurrentProficiency { get; private set; }

    private Dictionary<AssociatedAttribute, int> _timedAttributeModifiers;
    private Dictionary<AssociatedAttribute, int> _restAttributeModifiers;

    /// <summary>
    /// Возвращает текущее значение указанного атрибута (с учетом всех модификаторов).
    /// </summary>
    public int GetAttributeValue(AssociatedAttribute attribute)
    {
        switch (attribute)
        {
            case AssociatedAttribute.Body: return CurrentBody;
            case AssociatedAttribute.Mind: return CurrentMind;
            case AssociatedAttribute.Spirit: return CurrentSpirit;
            case AssociatedAttribute.Agility: return CurrentAgility;
            case AssociatedAttribute.Proficiency: return CurrentProficiency;
            case AssociatedAttribute.None: return 0;
            default:
                Debug.LogWarning($"GetAttributeValue: Unknown attribute {attribute} for {gameObject.name}");
                return 0;
        }
    }

    /// <summary>
    /// Добавляет временный или постоянный модификатор к атрибуту.
    /// </summary>
    public void AddAttributeModifier(AssociatedAttribute attribute, int value, StatusEffectData.RestoreCondition conditionType)
    {
        if (attribute == AssociatedAttribute.None) return;

        var targetDictionary = (conditionType == StatusEffectData.RestoreCondition.Timer)
            ? _timedAttributeModifiers : _restAttributeModifiers;

        if (targetDictionary.ContainsKey(attribute))
            targetDictionary[attribute] += value;
        else
            targetDictionary[attribute] = value;

        RecalculateAllStats();
    }

    /// <summary>
    /// Убирает ранее добавленный модификатор атрибута.
    /// </summary>
    public void RemoveAttributeModifier(AssociatedAttribute attribute, int value, StatusEffectData.RestoreCondition conditionType)
    {
        if (attribute == AssociatedAttribute.None) return;

        var targetDictionary = (conditionType == StatusEffectData.RestoreCondition.Timer)
            ? _timedAttributeModifiers : _restAttributeModifiers;

        if (targetDictionary.ContainsKey(attribute))
        {
            targetDictionary[attribute] -= value;
        }
        RecalculateAllStats();
    }

    /// <summary>
    /// Снимает все модификаторы атрибутов, действующие "до отдыха".
    /// </summary>
    public void ClearRestAttributeModifiers()
    {
        if (_restAttributeModifiers.Count == 0) return;
        _restAttributeModifiers.Clear();
        RecalculateAllStats();
    }
    
    /// <summary>
    /// Снимает все временные модификаторы атрибутов (действующие по таймеру).
    /// </summary>
    public void ClearAllTimedAttributeModifiers()
    {
        if (_timedAttributeModifiers.Count == 0) return;
        _timedAttributeModifiers.Clear();
        RecalculateAllStats();
    }
    #endregion
    
    #region Derived Stats & Calculations
    public int CalculatedDamage { get; private set; }
    public float CalculatedMaxCarryWeight { get; private set; }
    public float CalculatedAttackCooldown { get; private set; }
    public int AgilityHitBonusPercent { get; private set; }
    public int AgilityEvasionBonusPercent { get; private set; }

    [Header("Формулы производных статов")]
    [SerializeField] private int healthPerLevel = 5;
    [SerializeField] private int healthPerBodyPoint = 15;
    [SerializeField] private int baseHealthOffset = 50;
    [SerializeField] private int baseDamagePerBodyPoint = 5;
    [SerializeField] private float baseCarryWeight = 10f;
    [SerializeField] private float carryWeightPerBodyPoint = 10f;
    [SerializeField] private float baseAttackCooldown = 1.0f;
    [SerializeField] private float agilityCooldownReduction = 0.05f;
    [SerializeField] private float minAttackCooldown = 0.2f;
    [SerializeField] private int agilityBonusPerPoint = 5;
    private Inventory characterInventory;
    private CharacterEquipment characterEquipment;

    /// <summary>
    /// Общий переносимый вес (рюкзак + экипировка).
    /// </summary>
    public float TotalCarriedWeight
    {
        get
        {
            float inventoryWeight = characterInventory != null ? characterInventory.CurrentWeight : 0f;
            float equipmentWeight = characterEquipment != null ? characterEquipment.CurrentWeight : 0f;
            return inventoryWeight + equipmentWeight;
        }
    }

    /// <summary>
    /// Пересчитывает все производные статы на основе базовых атрибутов и модификаторов.
    /// Вызывается автоматически при изменении атрибутов.
    /// </summary>
    public void RecalculateAllStats()
    {
        int previousMaxHealth = maxHealth;

        ApplyAllModifiersToCurrentAttributes();
        EnforceMinimumAttributeValues();

        maxHealth = baseHealthOffset + (CurrentBody * healthPerBodyPoint) + (level * healthPerLevel);
        maxHealth = Mathf.Max(1, maxHealth);

        CalculatedDamage = Mathf.Max(1, CurrentBody * baseDamagePerBodyPoint);
        CalculatedMaxCarryWeight = baseCarryWeight + (CurrentBody * carryWeightPerBodyPoint);
        CalculatedAttackCooldown = Mathf.Max(minAttackCooldown, baseAttackCooldown - (CurrentAgility * agilityCooldownReduction));
        AgilityHitBonusPercent = CurrentAgility * agilityBonusPerPoint;
        AgilityEvasionBonusPercent = CurrentAgility * agilityBonusPerPoint;
        CurrentMovementSpeed = Mathf.Max(0.5f, (baseMovementSpeed + CurrentAgility) * movementSpeedMultiplierFromStatus);

        if (currentHealth > maxHealth || (currentHealth == previousMaxHealth && previousMaxHealth != 0))
        {
            currentHealth = maxHealth;
        }

        onAttributesChanged?.Invoke();
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    private void ApplyAllModifiersToCurrentAttributes()
    {
        CurrentBody = baseBody;
        CurrentMind = baseMind;
        CurrentSpirit = baseSpirit;
        CurrentAgility = baseAgility;
        CurrentProficiency = baseProficiency;

        foreach (var mod in _timedAttributeModifiers) ApplyModifier(mod.Key, mod.Value);
        foreach (var mod in _restAttributeModifiers) ApplyModifier(mod.Key, mod.Value);
    }

    private void ApplyModifier(AssociatedAttribute attribute, int value)
    {
        switch (attribute)
        {
            case AssociatedAttribute.Body: CurrentBody += value; break;
            case AssociatedAttribute.Mind: CurrentMind += value; break;
            case AssociatedAttribute.Spirit: CurrentSpirit += value; break;
            case AssociatedAttribute.Agility: CurrentAgility += value; break;
            case AssociatedAttribute.Proficiency: CurrentProficiency += value; break;
        }
    }

    private void EnforceMinimumAttributeValues()
    {
        CurrentBody = Mathf.Max(1, CurrentBody);
        CurrentMind = Mathf.Max(1, CurrentMind);
        CurrentSpirit = Mathf.Max(1, CurrentSpirit);
        CurrentAgility = Mathf.Max(1, CurrentAgility);
        CurrentProficiency = Mathf.Max(1, CurrentProficiency);
    }
    #endregion

    #region Health & Damage
    /// <summary>
    /// Наносит урон персонажу. Может вызвать смерть.
    /// </summary>
    /// <param name="damageAmount">Количество урона.</param>
    /// <param name="attacker">Трансформ атакующего (для реакции AI).</param>
    public void TakeDamage(int damageAmount, Transform attacker = null)
    {
        if (isDead || damageAmount <= 0) return;

        currentHealth -= damageAmount;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        aiController?.ReactToDamage(attacker);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    /// <summary>
    /// Восстанавливает здоровье персонажа. Не работает на мертвых.
    /// </summary>
    /// <param name="healAmount">Количество восстанавливаемого здоровья.</param>
    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        
        ClearAllTimedAttributeModifiers();
        onDied?.Invoke();
    }
    #endregion

    #region Speed Modifiers
    /// <summary>
    /// Применяет множитель к скорости передвижения (например, от статусов).
    /// </summary>
    public void ApplySpeedMultiplier(float multiplier)
    {
        movementSpeedMultiplierFromStatus *= multiplier;
        RecalculateAllStats();
    }

    /// <summary>
    /// Убирает ранее примененный множитель скорости передвижения.
    /// </summary>
    public void RemoveSpeedMultiplier(float multiplier)
    {
        if (Mathf.Approximately(multiplier, 0f))
        {
            Debug.LogWarning($"Attempted to remove a zero speed multiplier from {gameObject.name}. Resetting.");
            movementSpeedMultiplierFromStatus = 1f;
        }
        else
        {
            movementSpeedMultiplierFromStatus /= multiplier;
        }
        RecalculateAllStats();
    }
    #endregion

    #region Save/Load
    /// <summary>
    /// Устанавливает базовые атрибуты, уровень и опыт из данных сохранения.
    /// Этот метод должен вызываться ТОЛЬКО системой загрузки.
    /// </summary>
    public void RestoreBaseStatsFromSave(PartyMemberSaveData data)
    {
        baseBody = data.BaseBody;
        baseMind = data.BaseMind;
        baseSpirit = data.BaseSpirit;
        baseAgility = data.BaseAgility;
        baseProficiency = data.BaseProficiency;
        level = data.Level;
        experience = data.Experience;
        experienceToNextLevel = data.ExperienceToNextLevel;
        currentHealth = data.CurrentHealth;
    }
    /// <summary>
    /// Восстанавливает состояние статов после загрузки сохранения.
    /// </summary>
    public void RefreshStatsAfterLoad()
    {
        bool wasDead = isDead;
        RecalculateAllStats();
        isDead = (currentHealth <= 0);

        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        onLevelUp?.Invoke(level);

        if (isDead && !wasDead)
        {
            onDied?.Invoke();
        }
    }
    #endregion
}