using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class AbilitySlot
{
    public AbilityData abilityData;
    public int currentCharges;
    public float currentCooldownTimer;

    public AbilitySlot(AbilityData data)
    {
        abilityData = data;
        currentCharges = data.maxCharges;
        currentCooldownTimer = 0f;
    }

    public bool IsReady()
    {
        return currentCharges > 0 && currentCooldownTimer <= 0f;
    }

    public void Use()
    {
        if (abilityData.maxCharges > 0) 
        {
            currentCharges--;
        }
        currentCooldownTimer = abilityData.cooldown;
    }

    public void RestoreAllCharges()
    {
        currentCharges = abilityData.maxCharges;
    }
}

public class CharacterAbilities : MonoBehaviour, ISaveable
{
    [Tooltip("Список способностей, которые персонаж знает изначально.")]
    public List<AbilityData> initialAbilities = new List<AbilityData>();

    private List<AbilitySlot> learnedAbilities = new List<AbilitySlot>();
    public IReadOnlyList<AbilitySlot> LearnedAbilities => learnedAbilities.AsReadOnly();

    private CharacterStats characterStats;
    public CharacterStats OwnerStats => characterStats;

    public event System.Action OnAbilitiesChanged;

    void Awake()
    {
        characterStats = GetComponent<CharacterStats>();
        foreach (AbilityData data in initialAbilities)
        {
            LearnAbility(data);
        }
    }

    void Update()
    {
        bool anyCooldownChanged = false;
        foreach (AbilitySlot slot in learnedAbilities)
        {
            if (slot.currentCooldownTimer > 0)
            {
                slot.currentCooldownTimer -= Time.deltaTime;
                if (slot.currentCooldownTimer < 0) slot.currentCooldownTimer = 0;
                anyCooldownChanged = true;
            }
        }
        if (anyCooldownChanged)
        {
            OnAbilitiesChanged?.Invoke();
        }
    }

    public void LearnAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (!learnedAbilities.Any(slot => slot.abilityData == ability))
        {
            learnedAbilities.Add(new AbilitySlot(ability));
            OnAbilitiesChanged?.Invoke();
        }
    }

    public AbilitySlot GetAbilitySlot(AbilityData abilityData)
    {
        return learnedAbilities.FirstOrDefault(slot => slot.abilityData == abilityData);
    }

    public AbilitySlot GetAbilitySlotByIndex(int index)
    {
        if (index >= 0 && index < learnedAbilities.Count)
        {
            return learnedAbilities[index];
        }
        return null;
    }

    public bool CanUseAbility(AbilitySlot slot)
    {
        if (slot != null)
        {
            return slot.IsReady();
        }
        return false;
    }

public bool TryUseAbility(
    AbilitySlot slot,
    CharacterStats caster,
    FeedbackManager feedbackMgr,
    Transform targetTransformForAbility,
    Vector3 pointForAbility,
    AbilityData sourceAbilityRef)
{
    if (slot == null || caster == null || !CanUseAbility(slot))
    {
        return false;
    }

    // ТЕПЕРЬ МЫ ТРАТИМ ЗАРЯД ЗДЕСЬ, В МОМЕНТ ИСПОЛНЕНИЯ
    slot.Use();

    if (sourceAbilityRef.castSound != null)
    {
        AudioSource audioSource = caster.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = caster.gameObject.AddComponent<AudioSource>();
        if (audioSource != null) audioSource.PlayOneShot(sourceAbilityRef.castSound);
    }

        if (sourceAbilityRef.startVFXPrefab != null)
        {
            Instantiate(sourceAbilityRef.startVFXPrefab, caster.transform.position, caster.transform.rotation, caster.transform);
        }

        CharacterStats primarySingleTargetStats = null;
        if (targetTransformForAbility != null)
        {
            primarySingleTargetStats = targetTransformForAbility.GetComponent<CharacterStats>();
        }

        AbilityExecutor.Execute(
            caster,
            sourceAbilityRef,
            primarySingleTargetStats,
            targetTransformForAbility,
            pointForAbility,
            feedbackMgr
        );

        OnAbilitiesChanged?.Invoke();
        return true;
    }

    public void RestoreAllAbilityCharges()
    {
        foreach (AbilitySlot slot in learnedAbilities)
        {
            slot.RestoreAllCharges();
        }
        OnAbilitiesChanged?.Invoke();
    }
    
    #region SaveSystem
    
    public object CaptureState()
    {
        var abilitiesState = new List<AbilitySaveData>();
        foreach (var slot in learnedAbilities)
        {
            if (slot.abilityData == null) continue;
            
            abilitiesState.Add(new AbilitySaveData
            {
                abilityDataName = slot.abilityData.name,
                currentCharges = slot.currentCharges
            });
        }
        return abilitiesState;
    }

    public void RestoreState(object state)
    {
        if (state is List<AbilitySaveData> abilitiesState)
        {
            foreach (var savedAbility in abilitiesState)
            {
                var slot = learnedAbilities.FirstOrDefault(s => s.abilityData != null && s.abilityData.name == savedAbility.abilityDataName);
                if (slot != null)
                {
                    slot.currentCharges = savedAbility.currentCharges;
                }
            }
            OnAbilitiesChanged?.Invoke();
        }
    }
/// <summary>
/// Принудительно вызывает событие OnAbilitiesChanged.
/// Используется после загрузки для обновления UI.
/// </summary>
public void TriggerAbilitiesChanged()
{
    OnAbilitiesChanged?.Invoke();
}
    #endregion
}