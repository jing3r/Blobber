using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class CharacterStats : MonoBehaviour
{
    #region Unity Lifecycle
    
    void Awake()
    {
                // Проверяем, есть ли на этом объекте CharacterEquipment.
        // Если нет, добавляем его. Это гарантирует, что у любой сущности со статами будет система экипировки.
        if (GetComponent<CharacterEquipment>() == null)
        {
            gameObject.AddComponent<CharacterEquipment>();
        }
        aiController = GetComponent<AIController>();
        isDead = false; 
        
        if (_timedAttributeModifiers == null) _timedAttributeModifiers = new Dictionary<AssociatedAttribute, int>();
        if (_restAttributeModifiers == null) _restAttributeModifiers = new Dictionary<AssociatedAttribute, int>();

        RecalculateAllStats();
        currentHealth = maxHealth;
    }

    void Start()
    {
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
    
    [Header("Основные статы")]
    public int baseMaxHealth = 100;
    [HideInInspector] public int maxHealth;
    public int currentHealth;

    [Header("Movement Settings")]
    public float baseMovementSpeed = 3f;
    public float CurrentMovementSpeed { get; private set; }
    private float _movementSpeedMultiplierFromStatus = 1f;

    private AIController aiController;

    #endregion

    #region Experience & Leveling

    [Header("Опыт и Уровни")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100;

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
        baseBody++; 

        bool wasDeadBeforeLevelUp = isDead;
        RecalculateAllStats();

        int healthGained = maxHealth - currentHealth;
        currentHealth = maxHealth;

        if (wasDeadBeforeLevelUp && currentHealth > 0) {
            isDead = false;
        }
        
        experienceToNextLevel = CalculateExperienceForLevel(level + 1);

        onLevelUp?.Invoke(level);
        if (healthGained > 0 || wasDeadBeforeLevelUp) onHealthChanged?.Invoke(currentHealth, maxHealth);
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
    }
    
    private int CalculateExperienceForLevel(int nextLevelTarget)
    {
        if (nextLevelTarget <= 1) return 100;
        return 50 * (nextLevelTarget - 1) * (nextLevelTarget - 1) + 50 * (nextLevelTarget - 1) + 100;
    }
    
    #endregion
    
    #region Attributes & Modifiers

    [Header("Атрибуты (Базовые значения)")]
    public int baseBody = 3;
    public int baseMind = 3;
    public int baseSpirit = 3;
    public int baseAgility = 3;
    public int baseProficiency = 1;

    public int CurrentBody { get; private set; }
    public int CurrentMind { get; private set; }
    public int CurrentSpirit { get; private set; }
    public int CurrentAgility { get; private set; }
    public int CurrentProficiency { get; private set; }

    private Dictionary<AssociatedAttribute, int> _timedAttributeModifiers = new Dictionary<AssociatedAttribute, int>();
    private Dictionary<AssociatedAttribute, int> _restAttributeModifiers = new Dictionary<AssociatedAttribute, int>();

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
        }
        RecalculateAllStats();
    }

    public void ClearRestAttributeModifiers()
    {
        bool hadModifiers = _restAttributeModifiers.Count > 0;
        _restAttributeModifiers.Clear();
        if(hadModifiers) RecalculateAllStats();
    }
    
    public void ClearAllTimedAttributeModifiers()
    {
        bool hadModifiers = _timedAttributeModifiers.Count > 0;
        _timedAttributeModifiers.Clear();
        if(hadModifiers) RecalculateAllStats();
    }
    
    #endregion
    
    #region Derived Stats & Calculations

    public int CalculatedDamage { get; private set; }
    public float CalculatedMaxCarryWeight { get; private set; }
    public float CalculatedAttackCooldown { get; private set; }
    public int AgilityHitBonusPercent { get; private set; }
    public int AgilityEvasionBonusPercent { get; private set; }

    [Header("Настройки производных статов (константы для формул)")]
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

    public void RecalculateAllStats()
    {
        ApplyAllModifiersToCurrentAttributes();
        EnforceMinimumAttributeValues();

        int previousMaxHealth = maxHealth;
        maxHealth = baseHealthOffset + (CurrentBody * healthPerBodyPoint) + (level * healthPerLevel);
        if (maxHealth < 1) maxHealth = 1;

        if (currentHealth == previousMaxHealth && previousMaxHealth != 0)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
        if (isDead && currentHealth > 0) isDead = false;

        CalculatedDamage = CurrentBody * baseDamagePerBodyPoint;
        if (CalculatedDamage < 1) CalculatedDamage = 1;

        CalculatedMaxCarryWeight = baseCarryWeight + (CurrentBody * carryWeightPerBodyPoint);
        CalculatedAttackCooldown = Mathf.Max(minAttackCooldown, baseAttackCooldown - (CurrentAgility * agilityCooldownReduction));
        AgilityHitBonusPercent = CurrentAgility * agilityBonusPerPoint;
        AgilityEvasionBonusPercent = CurrentAgility * agilityBonusPerPoint;
        CurrentMovementSpeed = (baseMovementSpeed + CurrentAgility) * _movementSpeedMultiplierFromStatus;
        CurrentMovementSpeed = Mathf.Max(0.5f, CurrentMovementSpeed);

        onAttributesChanged?.Invoke();
    
        if (currentHealth != previousMaxHealth || maxHealth != previousMaxHealth)
        {
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    private void ApplyAllModifiersToCurrentAttributes()
    {
        CurrentBody = baseBody;
        CurrentMind = baseMind;
        CurrentSpirit = baseSpirit;
        CurrentAgility = baseAgility;
        CurrentProficiency = baseProficiency;

        foreach (var modEntry in _timedAttributeModifiers) ApplyModifierToSpecificAttribute(modEntry.Key, modEntry.Value);
        foreach (var modEntry in _restAttributeModifiers) ApplyModifierToSpecificAttribute(modEntry.Key, modEntry.Value);
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
    
    #endregion

    #region Health & Damage

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
        ClearAllTimedAttributeModifiers();
        onDied?.Invoke();
    }

    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    #endregion
    
    #region Speed Modifiers

    public void ApplySpeedMultiplier(float multiplier)
    {
        _movementSpeedMultiplierFromStatus *= multiplier;
        _movementSpeedMultiplierFromStatus = Mathf.Max(0.1f, _movementSpeedMultiplierFromStatus);
        RecalculateAllStats();
    }

    public void RemoveSpeedMultiplier(float multiplier)
    {
        if (Mathf.Approximately(multiplier, 0f))
        {
            Debug.LogWarning($"Attempted to remove a 0 speed multiplier from {gameObject.name}. Resetting to 1.");
            _movementSpeedMultiplierFromStatus = 1f;
        }
        else
        {
            _movementSpeedMultiplierFromStatus /= multiplier;
        }
        
        if (Mathf.Abs(_movementSpeedMultiplierFromStatus - 1.0f) < 0.001f)
        {
            _movementSpeedMultiplierFromStatus = 1.0f;
        }
        RecalculateAllStats();
    }
    
    public void ResetSpeedMultiplier()
    {
        _movementSpeedMultiplierFromStatus = 1f;
        RecalculateAllStats();
    }
    
    #endregion

    #region Save/Load
    
    public void RefreshStatsAfterLoad()
    {
        bool previousDeadState = isDead;
        RecalculateAllStats();
        isDead = (currentHealth <= 0);

        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onExperienceChanged?.Invoke(experience, experienceToNextLevel);
        onLevelUp?.Invoke(level);

        if (isDead && !previousDeadState)
        {
            onDied?.Invoke();
        }
    }

    #endregion
}