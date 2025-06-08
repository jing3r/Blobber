using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CharacterStatusEffects : MonoBehaviour
{
    private CharacterStats _characterStats;
    private List<ActiveStatusEffect> _activeEffects = new List<ActiveStatusEffect>();
    public bool IsStatusActive(StatusEffectData statusData)
    {
        if (statusData == null) return false;
        // Сравниваем напрямую SO или по ID, если SO может быть дублирован. Прямое сравнение надежнее.
        return _activeEffects.Any(e => e.Data == statusData);
    }
    [System.Obsolete("Use IsStatusActive(StatusEffectData) instead.")]
    public bool IsStatusActive(string statusID)
    {
        return _activeEffects.Any(e => e.Data.statusID == statusID);
    }

    // ----- ИЗМЕНЕНИЕ: Сделать класс публичным -----
    public class ActiveStatusEffect // Был private по умолчанию
    {
        public StatusEffectData Data { get; }
        public CharacterStats Caster { get; }
        public float TimeRemaining { get; set; }
        public float LastTickTime { get; set; }
        public List<StatusEffectData.AttributeModifier> AppliedModifiers { get; }
        public float AppliedSpeedMultiplier { get; set; } = 1.0f; // Запоминаем примененный множитель скорости
        public ActiveStatusEffect(StatusEffectData data, CharacterStats caster, float duration)
        {
            Data = data;
            Caster = caster;
            TimeRemaining = duration;
            LastTickTime = Time.time;
            AppliedModifiers = new List<StatusEffectData.AttributeModifier>();
        }
    }
    // -------------------------------------------

    void Awake()
    {
        _characterStats = GetComponent<CharacterStats>();
        if (_characterStats == null)
        {
            Debug.LogError($"CharacterStatusEffects на {gameObject.name} не может найти CharacterStats!", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (_activeEffects.Count == 0) return;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect effect = _activeEffects[i];

            if (effect.Data.restoreCondition == StatusEffectData.RestoreCondition.Timer)
            {
                effect.TimeRemaining -= Time.deltaTime;
                if (effect.TimeRemaining <= 0)
                {
                    RemoveStatus(effect, true);
                    continue;
                }
            }

            if (effect.Data.tickInterval > 0 && Time.time >= effect.LastTickTime + effect.Data.tickInterval)
            {
                ApplyTickEffect(effect);
                effect.LastTickTime = Time.time;
            }
        }
    }


    // --- ПОЛНЫЙ МЕТОД ApplyStatus ---
    public void ApplyStatus(StatusEffectData effectData, CharacterStats casterStats)
    {
        if (effectData == null || _characterStats == null) return;

        // Если статус не стакается, и такой статус уже активен, обновляем его длительность и выходим.
        if (!effectData.canStack && _activeEffects.Any(e => e.Data.statusID == effectData.statusID))
        {
            ActiveStatusEffect existingEffect = _activeEffects.First(e => e.Data.statusID == effectData.statusID);
            // Обновляем длительность, если это таймерный эффект.
            if (effectData.restoreCondition == StatusEffectData.RestoreCondition.Timer)
            {
                float newDuration = 0;
                if (effectData.durationAttribute != AssociatedAttribute.None)
                {
                    int attributeValue = (effectData.durationAttributeSource == StatusEffectData.DurationAttributeSource.Caster && casterStats != null)
                        ? casterStats.GetAttributeValue(effectData.durationAttribute)
                        : _characterStats.GetAttributeValue(effectData.durationAttribute);
                    newDuration = attributeValue * effectData.durationMultiplier;
                }
                else
                {
                    newDuration = effectData.fixedDuration;
                }
                existingEffect.TimeRemaining = Mathf.Max(existingEffect.TimeRemaining, newDuration); // Обновляем на большее значение
            }
            // Debug.Log($"Status '{effectData.statusName}' already active on {_characterStats.name}. Duration refreshed.");
            return; // Не применяем новый экземпляр, только обновили существующий
        }


        // Рассчитываем длительность для нового активного эффекта
        float duration = 0;
        if (effectData.restoreCondition == StatusEffectData.RestoreCondition.Timer)
        {
            if (effectData.durationAttribute != AssociatedAttribute.None)
            {
                int attributeValue = (effectData.durationAttributeSource == StatusEffectData.DurationAttributeSource.Caster && casterStats != null)
                    ? casterStats.GetAttributeValue(effectData.durationAttribute)
                    : _characterStats.GetAttributeValue(effectData.durationAttribute);
                duration = attributeValue * effectData.durationMultiplier;
            }
            else
            {
                duration = effectData.fixedDuration;
            }
            duration = Mathf.Max(0, duration);
        }

        // Если это таймерный статус с нулевой длительностью, нет тиков, и нет модификаторов "до отдыха", не применяем.
        bool hasRestModifiers = effectData.attributeModifiers.Any(m => m.modifierRestoreCondition == StatusEffectData.RestoreCondition.Rest);
        if (effectData.restoreCondition == StatusEffectData.RestoreCondition.Timer && duration <= 0 && effectData.tickInterval <= 0 && !hasRestModifiers)
        {
            Debug.LogWarning($"Status '{effectData.statusName}' on {_characterStats.name} not applied: Timer based, 0 duration, no ticks, no 'rest' modifiers. Check StatusEffectData config.");
            return;
        }


        ActiveStatusEffect newActiveEffect = new ActiveStatusEffect(effectData, casterStats, duration);
        _activeEffects.Add(newActiveEffect);

        // Применяем модификаторы атрибутов
        foreach (var mod in effectData.attributeModifiers)
        {
            var actualRestoreCondition = mod.modifierRestoreCondition;
            if (effectData.restoreCondition == StatusEffectData.RestoreCondition.Rest) // Если сам статус "до отдыха", все его модификаторы тоже "до отдыха"
            {
                actualRestoreCondition = StatusEffectData.RestoreCondition.Rest;
            }
            _characterStats.AddAttributeModifier(mod.targetAttribute, mod.modifierValue, actualRestoreCondition);
            newActiveEffect.AppliedModifiers.Add(mod);
        }

        // Применяем множитель скорости передвижения, если статус его имеет
        if (effectData.movementSpeedMultiplier != 1.0f)
        {
            _characterStats.ApplySpeedMultiplier(effectData.movementSpeedMultiplier);
            newActiveEffect.AppliedSpeedMultiplier = effectData.movementSpeedMultiplier; // Запоминаем примененный множитель
            // Debug.Log($"{_characterStats.name} received status '{effectData.statusName}'. Speed multiplier applied: {effectData.movementSpeedMultiplier}. Current cumulative: {_characterStats._movementSpeedMultiplierFromStatus}.");
        }

        // TODO: Создать VFX, если есть (effectData.statusVFXPrefab)
        if (effectData.statusVFXPrefab != null)
        {
            // Инстанцировать VFX и хранить ссылку, чтобы уничтожить при RemoveStatus
            // Например: GameObject vfx = Instantiate(effectData.statusVFXPrefab, _characterStats.transform);
            // newActiveEffect.VFXInstance = vfx; // Если добавить поле VFXInstance в ActiveStatusEffect
        }

        // Debug.Log($"{_characterStats.name} received status: {effectData.statusName} from {casterStats?.name}. Duration: {duration:F1}s.");
    }

    private void ApplyTickEffect(ActiveStatusEffect activeEffect)
    {
        StatusEffectData data = activeEffect.Data;
        if (data.baseDamagePerTick != 0)
        {
            int tickAmount = data.baseDamagePerTick;
            if (data.tickEffectScalingAttribute != AssociatedAttribute.None && activeEffect.Caster != null)
            {
                tickAmount += Mathf.FloorToInt(
                    activeEffect.Caster.GetAttributeValue(data.tickEffectScalingAttribute) * data.tickEffectScaleFactor
                );
            }

            if (tickAmount > 0)
                _characterStats.TakeDamage(tickAmount, activeEffect.Caster?.transform);
            else if (tickAmount < 0)
                _characterStats.Heal(Mathf.Abs(tickAmount));
        }
    }

    public void RemoveStatus(StatusEffectData statusDataToRemove)
    {
        if (statusDataToRemove == null) return;
        ActiveStatusEffect effectToRemove = _activeEffects.FirstOrDefault(e => e.Data == statusDataToRemove);
        if (effectToRemove != null)
        {
            // Вызываем наш основной метод удаления по инстансу
            RemoveStatus(effectToRemove, false);
        }
    }

    public void ClearStatusEffectsOnRest()
    {
        _characterStats.ClearRestAttributeModifiers();

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect currentEffect = _activeEffects[i];

            // Снимаем только те, что снимаются при отдыхе.
            // Timer и RequiresCure остаются.
            if (currentEffect.Data.restoreCondition == StatusEffectData.RestoreCondition.Rest)
            {
                // Этот вызов уже откатывает все модификаторы, так что он безопасен
                RemoveStatus(currentEffect, false);
            }
        }

        _characterStats.RecalculateAllStats();
    }

    // public void CureStatus(CureType cureType) { ... } // для RequiresCure

    // Старый метод для обратной совместимости или для удаления, когда все вызовы будут заменены.
    public void RemoveStatusByID(string statusID)
    {
        ActiveStatusEffect effectToRemove = _activeEffects.FirstOrDefault(e => e.Data.statusID == statusID);
        if (effectToRemove != null)
        {
            RemoveStatus(effectToRemove, false);
        }
    }
    
        private void RemoveStatus(ActiveStatusEffect effectInstance, bool expiredByTimer)
    {
        if (effectInstance == null || !_activeEffects.Contains(effectInstance)) return;

        foreach (var mod in effectInstance.AppliedModifiers)
        {
             var actualRestoreCondition = mod.modifierRestoreCondition;
             if (effectInstance.Data.restoreCondition == StatusEffectData.RestoreCondition.Rest)
             {
                 actualRestoreCondition = StatusEffectData.RestoreCondition.Rest;
             }
            _characterStats.RemoveAttributeModifier(mod.targetAttribute, mod.modifierValue, actualRestoreCondition);
        }
        
        if (effectInstance.AppliedSpeedMultiplier != 1.0f) 
        {
            _characterStats.RemoveSpeedMultiplier(effectInstance.AppliedSpeedMultiplier);
        }

        _activeEffects.Remove(effectInstance);
    }
}