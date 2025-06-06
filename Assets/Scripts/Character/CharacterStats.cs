using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic; // Для Dictionary
using System.Linq; // Если будем использовать LINQ для чего-либо еще

public class CharacterStats : MonoBehaviour
{
    [Header("Основные статы")]
    public int baseMaxHealth = 100; // Базовое значение, от которого могут отталкиваться расчеты, если нужно
    [HideInInspector] public int maxHealth; // Будет пересчитываться
    public int currentHealth;
[Header("Movement Settings")] // Можно добавить отдельный хедер для настроек скорости
public float baseMovementSpeed = 3f; // Базовая скорость

public float CurrentMovementSpeed { get; private set; }
private float _movementSpeedMultiplierFromStatus = 1f; // Множитель от статусов (например, замедление 0.5, ускорение 1.5)
    [Header("Состояние")]
    [SerializeField] private bool isDead = false;
    public bool IsDead => isDead;

    [Header("Опыт и Уровни")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100;

    [Header("Атрибуты (Базовые значения)")]
    public int baseBody = 3;
    public int baseMind = 3;
    public int baseSpirit = 3;
    public int baseAgility = 3;
    public int baseProficiency = 1;

    // Публичные свойства для ТЕКУЩИХ значений атрибутов (с учетом всех модификаторов)
    public int CurrentBody { get; private set; }
    public int CurrentMind { get; private set; }
    public int CurrentSpirit { get; private set; }
    public int CurrentAgility { get; private set; }
    public int CurrentProficiency { get; private set; }

    // Для хранения модификаторов от статус-эффектов
    // Ключ - AssociatedAttribute, Значение - суммарный модификатор
    private Dictionary<AssociatedAttribute, int> _timedAttributeModifiers = new Dictionary<AssociatedAttribute, int>();
    private Dictionary<AssociatedAttribute, int> _restAttributeModifiers = new Dictionary<AssociatedAttribute, int>();

    public int CalculatedDamage { get; private set; }
    public float CalculatedMaxCarryWeight { get; private set; }
    public float CalculatedAttackCooldown { get; private set; }
    public int AgilityHitBonusPercent { get; private set; }
    public int AgilityEvasionBonusPercent { get; private set; }

    [Header("Настройки производных статов (константы для формул)")]
    [SerializeField] private int healthPerLevel = 5;
    [SerializeField] private int healthPerBodyPoint = 15;
    [SerializeField] private int baseHealthOffset = 50; // Базовое здоровье, не зависящее от уровня или Body

    [SerializeField] private int baseDamagePerBodyPoint = 5;
    [SerializeField] private float baseCarryWeight = 10f;
    [SerializeField] private float carryWeightPerBodyPoint = 10f;
    [SerializeField] private float baseAttackCooldown = 1.0f;
    [SerializeField] private float agilityCooldownReduction = 0.05f;
    [SerializeField] private float minAttackCooldown = 0.2f;
    [SerializeField] private int agilityBonusPerPoint = 5;


    // События
    [System.Serializable] public class HealthChangeEvent : UnityEvent<int, int> { } // currentHP, maxHP
    public HealthChangeEvent onHealthChanged;
    public event System.Action onDied;
    public event System.Action<int, int> onExperienceChanged; // currentXP, xpToNextLevel
    public event System.Action<int> onLevelUp; // newLevel
    public event System.Action onAttributesChanged; // Вызывается после RecalculateAllStats

    private AIController aiController; // Кэшируем ссылку для производительности

    void Awake()
    {
        aiController = GetComponent<AIController>();
        isDead = false; // Убедимся, что не мертв при старте/пересоздании
        
        // Инициализируем словари модификаторов, если они еще не созданы
        if (_timedAttributeModifiers == null) _timedAttributeModifiers = new Dictionary<AssociatedAttribute, int>();
        if (_restAttributeModifiers == null) _restAttributeModifiers = new Dictionary<AssociatedAttribute, int>();

        RecalculateAllStats(); // Первоначальный расчет всех статов
        currentHealth = maxHealth; // Полное здоровье при первом создании
    }

    void Start()
    {
        // Вызываем события для инициализации UI и других систем уже с актуальными данными
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onLevelUp?.Invoke(level);
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        onAttributesChanged?.Invoke();
    }
    public void ApplySpeedMultiplier(float multiplier)
    {
        // Debug.Log($"{gameObject.name}: Applying speed multiplier {multiplier}. Current: {_movementSpeedMultiplierFromStatus}");
        _movementSpeedMultiplierFromStatus *= multiplier;
        _movementSpeedMultiplierFromStatus = Mathf.Max(0.1f, _movementSpeedMultiplierFromStatus); // Защита от слишком низкого множителя
        RecalculateAllStats();
        // Debug.Log($"{gameObject.name}: New cumulative speed multiplier: {_movementSpeedMultiplierFromStatus}. New speed: {CurrentMovementSpeed}");
    }


    public void RemoveSpeedMultiplier(float multiplier)
    {
        // Debug.Log($"{gameObject.name}: Removing speed multiplier {multiplier}. Current: {_movementSpeedMultiplierFromStatus}");
        if (Mathf.Approximately(multiplier, 0f)) // Защита от деления на ноль, если multiplier вдруг 0
        {
            // Невозможно корректно удалить множитель 0. Просто сбрасываем или предупреждаем.
            Debug.LogWarning($"Attempted to remove a 0 speed multiplier from {gameObject.name}. Resetting to 1.");
            _movementSpeedMultiplierFromStatus = 1f;
        }
        else
        {
            _movementSpeedMultiplierFromStatus /= multiplier;
        }
        // Приближаем к 1.0, если очень близко, чтобы избежать накопления ошибок float
        if (Mathf.Abs(_movementSpeedMultiplierFromStatus - 1.0f) < 0.001f) // Используем Abs для сравнения float
        {
            _movementSpeedMultiplierFromStatus = 1.0f;
        }
        RecalculateAllStats();
        // Debug.Log($"{gameObject.name}: New cumulative speed multiplier: {_movementSpeedMultiplierFromStatus}. New speed: {CurrentMovementSpeed}");
    }

    // При отдыхе или снятии всех временных статусов можно сбрасывать множитель
    public void ResetSpeedMultiplier()
    {
        _movementSpeedMultiplierFromStatus = 1f;
        RecalculateAllStats();
    }

    private void ApplyAllModifiersToCurrentAttributes()
    {
        CurrentBody = baseBody;
        CurrentMind = baseMind;
        CurrentSpirit = baseSpirit;
        CurrentAgility = baseAgility;
        CurrentProficiency = baseProficiency;

        foreach (var modEntry in _timedAttributeModifiers)
        {
            ApplyModifierToSpecificAttribute(modEntry.Key, modEntry.Value);
        }
        foreach (var modEntry in _restAttributeModifiers)
        {
            ApplyModifierToSpecificAttribute(modEntry.Key, modEntry.Value);
        }
    }

    private void ApplyModifierToSpecificAttribute(AssociatedAttribute attribute, int value)
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

    public void RecalculateAllStats()
    {
        ApplyAllModifiersToCurrentAttributes();
        EnforceMinimumAttributeValues();

        int previousMaxHealth = maxHealth;
        maxHealth = baseHealthOffset + (CurrentBody * healthPerBodyPoint) + (level * healthPerLevel);
        if (maxHealth < 1) maxHealth = 1;

        // Корректируем currentHealth, если maxHealth изменился
        // Если здоровье полное, оно должно остаться полным относительно нового maxHealth
        // Если не полное, оно должно остаться тем же, но не превышать новый maxHealth
        if (currentHealth == previousMaxHealth && previousMaxHealth != 0) // Если было полное здоровье
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
        if (isDead && currentHealth > 0) isDead = false; // Если был мертв, но после пересчета здоровье появилось


        CalculatedDamage = CurrentBody * baseDamagePerBodyPoint;
        if (CalculatedDamage < 1) CalculatedDamage = 1;

        CalculatedMaxCarryWeight = baseCarryWeight + (CurrentBody * carryWeightPerBodyPoint);
        CalculatedAttackCooldown = Mathf.Max(minAttackCooldown, baseAttackCooldown - (CurrentAgility * agilityCooldownReduction));
        AgilityHitBonusPercent = CurrentAgility * agilityBonusPerPoint;
        AgilityEvasionBonusPercent = CurrentAgility * agilityBonusPerPoint;

    CurrentMovementSpeed = (baseMovementSpeed + CurrentAgility) * _movementSpeedMultiplierFromStatus;; // Формула: (база + Ловкость) * множитель от статусов
    CurrentMovementSpeed = Mathf.Max(0.5f, CurrentMovementSpeed); // Минимальная скорость

    onAttributesChanged?.Invoke();
    
        if (currentHealth != previousMaxHealth || maxHealth != previousMaxHealth) // Вызываем только если здоровье действительно изменилось
        {
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

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
                Debug.LogWarning($"GetAttributeValue: Неизвестный атрибут {attribute} для {gameObject.name}");
                return 0;
        }
    }

    public void AddAttributeModifier(AssociatedAttribute attribute, int value, StatusEffectData.RestoreCondition conditionType)
    {
        if (attribute == AssociatedAttribute.None) return;
        Dictionary<AssociatedAttribute, int> targetDictionary =
            conditionType == StatusEffectData.RestoreCondition.Timer ? _timedAttributeModifiers : _restAttributeModifiers;

        if (targetDictionary.ContainsKey(attribute))
            targetDictionary[attribute] += value;
        else
            targetDictionary[attribute] = value;

        RecalculateAllStats();
    }

    public void RemoveAttributeModifier(AssociatedAttribute attribute, int value, StatusEffectData.RestoreCondition conditionType)
    {
        if (attribute == AssociatedAttribute.None) return;
        Dictionary<AssociatedAttribute, int> targetDictionary =
            conditionType == StatusEffectData.RestoreCondition.Timer ? _timedAttributeModifiers : _restAttributeModifiers;

        if (targetDictionary.ContainsKey(attribute))
        {
            targetDictionary[attribute] -= value;
            // Оставляем ключ, даже если значение 0, для простоты отката нескольких одинаковых статусов
        }
        RecalculateAllStats();
    }

    public void ClearRestAttributeModifiers()
    {
        bool hadModifiers = _restAttributeModifiers.Count > 0;
        _restAttributeModifiers.Clear();
        if(hadModifiers) RecalculateAllStats(); // Пересчитываем, только если что-то было
        // Debug.Log($"{gameObject.name}: Модификаторы 'до отдыха' сняты.");
    }
    
    public void ClearAllTimedAttributeModifiers() // Может понадобиться при смерти или особой механике
    {
        bool hadModifiers = _timedAttributeModifiers.Count > 0;
        _timedAttributeModifiers.Clear();
        if(hadModifiers) RecalculateAllStats();
    }


    public void TakeDamage(int damageAmount, Transform attacker = null)
    {
        if (isDead || damageAmount <= 0) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (aiController != null && attacker != null && !isDead)
        {
            aiController.ReactToDamage(attacker);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        // Снимаем все временные баффы/дебаффы при смерти
        ClearAllTimedAttributeModifiers();
        onDied?.Invoke();
        // Debug.Log($"{gameObject.name} has died.");
    }

    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return; // Нельзя лечить мертвых обычным лечением

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void GainExperience(int amount)
    {
        if (isDead || amount <= 0) return;

        experience += amount;
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);

        while (experience >= experienceToNextLevel && !isDead) // Нельзя получить уровень, если мертв
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

        bool wasDeadBeforeLevelUp = isDead; // Запоминаем, был ли персонаж мертв

        RecalculateAllStats(); // Пересчитываем все статы, включая maxHealth, на основе новых базовых и текущих модификаторов

        // Полное восстановление здоровья до нового максимума
        int healthGained = maxHealth - currentHealth;
        currentHealth = maxHealth;

        if (wasDeadBeforeLevelUp && currentHealth > 0) {
            isDead = false; // Воскрешение от левелапа
            // Debug.Log($"{gameObject.name} was resurrected by leveling up!");
            // Если CharacterStatusEffects есть, можно ему сообщить о воскрешении,
            // чтобы он перепроверил статусы (хотя модификаторы уже должны быть корректны после RecalculateAllStats)
        }
        
        experienceToNextLevel = CalculateExperienceForLevel(level + 1);

        onLevelUp?.Invoke(level);
        if (healthGained > 0 || wasDeadBeforeLevelUp) onHealthChanged?.Invoke(currentHealth, maxHealth); // Вызываем, если здоровье изменилось
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
    }

    private int CalculateExperienceForLevel(int nextLevelTarget)
    {
        if (nextLevelTarget <= 1) return 100;
        return 50 * (nextLevelTarget - 1) * (nextLevelTarget - 1) + 50 * (nextLevelTarget - 1) + 100;
    }

    public void RefreshStatsAfterLoad()
    {
        bool previousDeadState = isDead;
        // Базовые атрибуты (baseBody, etc.), level, experience, currentHealth уже установлены из SaveData.
        // Модификаторы (_timedAttributeModifiers, _restAttributeModifiers) пока не сохраняются/загружаются,
        // поэтому они будут пустыми после загрузки, если не реализовать их сохранение.

        RecalculateAllStats(); // Это установит Current атрибуты из базовых, пересчитает maxHealth и т.д.

        // currentHealth был загружен, но RecalculateAllStats мог изменить maxHealth,
        // и если currentHealth был > нового maxHealth, он был бы зажат.
        // Поэтому, если currentHealth был явно загружен, его нужно еще раз проверить.
        // Но RecalculateAllStats уже должен был корректно обработать currentHealth.
        // currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Эта строка уже есть в RecalculateAllStats по сути

        isDead = (currentHealth <= 0);

        // События уже были вызваны из RecalculateAllStats или косвенно
        if (isDead && !previousDeadState)
        {
            onDied?.Invoke();
        }
        else if (!isDead && previousDeadState)
        {
            // Оживление после загрузки, если такое возможно (например, если формула здоровья изменилась)
            // Debug.Log($"{gameObject.name} is now alive after loading.");
        }
    }
}