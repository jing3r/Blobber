// Начало CharacterAbilities.cs

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
    // currentCooldownTimer = 0; // Также можно сбросить кулдауны при отдыхе
}
}


public class CharacterAbilities : MonoBehaviour
{
    [Tooltip("Список способностей, которые персонаж знает изначально.")]
    public List<AbilityData> initialAbilities = new List<AbilityData>();

    private List<AbilitySlot> learnedAbilities = new List<AbilitySlot>();
    public IReadOnlyList<AbilitySlot> LearnedAbilities => learnedAbilities.AsReadOnly();

    private CharacterStats characterStats;
    public CharacterStats OwnerStats => characterStats; // Публичный геттер, если понадобится AbilityExecutor

    public event System.Action OnAbilitiesChanged;

    void Awake()
    {
        characterStats = GetComponent<CharacterStats>();
        // if (characterStats == null) { Debug.LogWarning(...); } // Можно оставить

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
        // Debug.LogWarning($"Attempted to get ability slot by invalid index: {index}");
        return null;
    }

    public bool CanUseAbility(AbilityData ability)
    {
        AbilitySlot slot = GetAbilitySlot(ability);
        return CanUseAbility(slot); // Делегируем
    }

    public bool CanUseAbility(AbilitySlot slot)
    {
        if (slot != null)
        {
            return slot.IsReady();
            // + Проверка ресурсов в будущем
        }
        return false;
    }

    /// <summary>
    /// Помечает способность как использованную (заряды, кулдаун) и вызывает ее исполнение.
    /// </summary>
    /// <param name="slot">Слот способности для использования.</param>
    /// <param name="caster">CharacterStats того, кто использует способность.</param>
    /// <param name="primaryTarget">Основная Transform цель (для Single_Creature, Single_Interactable).</param>
    /// <param name="pointTarget">Точка применения (для Point_AreaEffect).</param>
    /// <returns>True, если способность была успешно инициирована.</returns>
    public bool TryUseAbility(
        AbilitySlot slot,
        CharacterStats caster,
        FeedbackManager feedbackMgr,
        Transform targetTransformForAbility, // Переименовал для ясности (это может быть существо или интерактивный объект)
        Vector3 pointForAbility,             // Точка применения для AoE или позиция цели
        AbilityData sourceAbilityRef)        // Ссылка на AbilityData для передачи в Executor
    {
        if (slot == null || caster == null) // feedbackMgr может быть null, если не назначен
        {
            Debug.LogError("TryUseAbility: Slot or Caster is null.");
            return false;
        }

        // Проверка CanUseAbility уже должна была быть сделана в AbilityCastingSystem
        // Но для надежности можно добавить еще раз, хотя это приведет к дублированию сообщения об ошибке.
        // if (!CanUseAbility(slot))
        // {
        //     // Сообщение уже дано в AbilityCastingSystem
        //     return false;
        // }

        // Действия по использованию способности (трата заряда, запуск кулдауна)
        slot.Use();

        // Воспроизводим звук каста
        if (sourceAbilityRef.castSound != null) // Берем из sourceAbilityRef (он же slot.abilityData)
        {
            AudioSource audioSource = caster.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = caster.gameObject.AddComponent<AudioSource>();
            //TODO: Настроить AudioSource более гибко (громкость, микшер и т.д.)
            if (audioSource != null) audioSource.PlayOneShot(sourceAbilityRef.castSound);
        }

        // Воспроизводим VFX на кастующем
        if (sourceAbilityRef.startVFXPrefab != null)
        {
            Instantiate(sourceAbilityRef.startVFXPrefab, caster.transform.position, caster.transform.rotation, caster.transform);
        }

        // Определяем CharacterStats цели, если это возможно, из targetTransformForAbility
        CharacterStats primarySingleTargetStats = null;
        if (targetTransformForAbility != null)
        {
            primarySingleTargetStats = targetTransformForAbility.GetComponent<CharacterStats>();
        }

        // Вызываем исполнителя эффектов способности
        AbilityExecutor.Execute(
            caster,
            sourceAbilityRef, // Передаем sourceAbilityRef (он же slot.abilityData)
            primarySingleTargetStats,
            targetTransformForAbility,
            pointForAbility,
            feedbackMgr
        );

        OnAbilitiesChanged?.Invoke(); // Уведомляем UI и другие системы об изменении состояния способностей
        return true; // Способность была инициирована (заряды/кд обработаны)
    }


    public void RestoreAllAbilityCharges()
    {
        foreach (AbilitySlot slot in learnedAbilities)
        {
            slot.RestoreAllCharges(); // Убедимся, что AbilitySlot.RestoreAllCharges() есть
        }
        OnAbilitiesChanged?.Invoke();
        // Debug.Log($"{gameObject.name}: All ability charges restored.");
    }
}

// Конец CharacterAbilities.cs