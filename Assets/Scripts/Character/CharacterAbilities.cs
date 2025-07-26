using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

/// <summary>
/// Хранит состояние способности персонажа (заряды, кулдауны).
/// </summary>
[Serializable]
public class AbilitySlot
{
    public AbilityData AbilityData;
    public int CurrentCharges;
    public float CurrentCooldownTimer;
    public AbilitySlot(AbilityData data)
    {
        AbilityData = data;
        CurrentCharges = data.MaxCharges;
        CurrentCooldownTimer = 0f;
    }

    public bool IsReady() => CurrentCharges > 0 && CurrentCooldownTimer <= 0f;

    public void Use()
    {
        if (AbilityData.MaxCharges > 0)
        {
            CurrentCharges--;
        }
        CurrentCooldownTimer = AbilityData.Cooldown;
    }

    public void RestoreAllCharges()
    {
        CurrentCharges = AbilityData.MaxCharges;
    }
}

/// <summary>
/// Управляет способностями, которые знает и может использовать персонаж.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
public class CharacterAbilities : MonoBehaviour, ISaveable
{
    [SerializeField]
    [Tooltip("Список способностей, которые персонаж знает изначально.")]
    private List<AbilityData> initialAbilities = new List<AbilityData>();

    private readonly List<AbilitySlot> learnedAbilities = new List<AbilitySlot>();
    private AudioSource audioSource;
    public IReadOnlyList<AbilitySlot> LearnedAbilities => learnedAbilities.AsReadOnly();

    public CharacterStats OwnerStats { get; private set; }
    public event Action OnAbilitiesChanged;

    private void Awake()
    {
        OwnerStats = GetComponent<CharacterStats>();
        audioSource = OwnerStats.GetComponent<AudioSource>();
        
        foreach (AbilityData data in initialAbilities)
        {
            LearnAbility(data);
        }
    }

    private void Update()
    {
        // Оптимизация: выходим, если нет активных кулдаунов, чтобы не вызывать событие каждый кадр.
        if (learnedAbilities.All(slot => slot.CurrentCooldownTimer <= 0)) return;

        bool anyCooldownChanged = false;
        foreach (AbilitySlot slot in learnedAbilities)
        {
            if (slot.CurrentCooldownTimer > 0)
            {
                slot.CurrentCooldownTimer -= Time.deltaTime;
                if (slot.CurrentCooldownTimer < 0)
                {
                    slot.CurrentCooldownTimer = 0;
                }
                anyCooldownChanged = true;
            }
        }

        if (anyCooldownChanged)
        {
            OnAbilitiesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Добавляет новую способность персонажу.
    /// </summary>
    public void LearnAbility(AbilityData ability)
    {
        if (ability == null || learnedAbilities.Any(slot => slot.AbilityData == ability)) return;

        learnedAbilities.Add(new AbilitySlot(ability));
        OnAbilitiesChanged?.Invoke();
    }

    /// <summary>
    /// Возвращает слот способности по индексу в списке изученных.
    /// </summary>
    public AbilitySlot GetAbilitySlotByIndex(int index)
    {
        bool isIndexValid = index >= 0 && index < learnedAbilities.Count;
        return isIndexValid ? learnedAbilities[index] : null;
    }
    
    /// <summary>
    /// Пытается исполнить способность. Основная логика вынесена в AbilityExecutor.
    /// </summary>
    /// <returns>True, если способность была успешно запущена.</returns>
    public bool TryUseAbility(AbilitySlot slot, Transform targetTransform, Vector3 point)
    {
        if (slot == null || !slot.IsReady()) return false;

        slot.Use();
        PlayAbilityEffects(slot.AbilityData);

        CharacterStats primaryTargetStats = targetTransform?.GetComponent<CharacterStats>();

        AbilityExecutor.Execute(
            OwnerStats,
            slot.AbilityData,
            primaryTargetStats,
            targetTransform,
            point,
            FindObjectOfType<FeedbackManager>() // TODO: Рассмотреть инъекцию зависимости вместо FindObjectOfType
        );

        OnAbilitiesChanged?.Invoke();
        return true;
    }
    
    /// <summary>
    /// Восстанавливает все заряды всех способностей.
    /// </summary>
    public void RestoreAllAbilityCharges()
    {
        foreach (AbilitySlot slot in learnedAbilities)
        {
            slot.RestoreAllCharges();
        }
        OnAbilitiesChanged?.Invoke();
    }

    private void PlayAbilityEffects(AbilityData AbilityData)
    {
        if (AbilityData.CastSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(AbilityData.CastSound);
        }

        if (AbilityData.StartVFXPrefab != null)
        {
            Instantiate(AbilityData.StartVFXPrefab, OwnerStats.transform.position, OwnerStats.transform.rotation, OwnerStats.transform);
        }
    }

    #region Save System Implementation
    public object CaptureState()
    {
        return learnedAbilities
            .Where(slot => slot.AbilityData != null)
            .Select(slot => new AbilitySaveData
            {
                AbilityDataName = slot.AbilityData.name,
                CurrentCharges = slot.CurrentCharges
            }).ToList();
    }

    public void RestoreState(object state)
    {
        if (state is List<AbilitySaveData> abilitiesState)
        {
            foreach (var savedAbility in abilitiesState)
            {
                var slot = learnedAbilities.FirstOrDefault(s => s.AbilityData != null && s.AbilityData.name == savedAbility.AbilityDataName);
                if (slot != null)
                {
                    slot.CurrentCharges = savedAbility.CurrentCharges;
                }
            }
            OnAbilitiesChanged?.Invoke();
        }
    }
    
    public void TriggerAbilitiesChanged()
    {
        OnAbilitiesChanged?.Invoke();
    }
    #endregion
}