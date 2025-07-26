using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Управляет активными статус-эффектами (баффами и дебаффами) на персонаже.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
public class CharacterStatusEffects : MonoBehaviour
{
    /// <summary>
    /// Внутренний класс для хранения состояния активного статус-эффекта.
    /// </summary>
    public class ActiveStatusEffect
    {
        public StatusEffectData Data { get; }
        public CharacterStats Caster { get; }
        public float TimeRemaining { get; set; }
        public float LastTickTime { get; set; }
        
        // Храним ссылки на примененные модификаторы, чтобы корректно их снять
        public List<StatusEffectData.AttributeModifier> AppliedModifiers { get; }
        public float AppliedSpeedMultiplier { get; set; }

        public ActiveStatusEffect(StatusEffectData data, CharacterStats caster, float duration)
        {
            Data = data;
            Caster = caster;
            TimeRemaining = duration;
            LastTickTime = Time.time;
            AppliedModifiers = new List<StatusEffectData.AttributeModifier>();
            AppliedSpeedMultiplier = 1.0f; // Значение по умолчанию
        }
    }

    private CharacterStats characterStats;
    private readonly List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    // TODO: Добавить публичное свойство IReadOnlyList<ActiveStatusEffect> для UI, если потребуется

    private void Awake()
    {
        characterStats = GetComponent<CharacterStats>();
    }

    private void Update()
    {
        if (activeEffects.Count == 0) return;

        // Итерируемся в обратном порядке, так как можем удалять элементы из списка
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            UpdateEffectTimer(effect);
            UpdateEffectTick(effect);
        }
    }

    /// <summary>
    /// Применяет новый статус-эффект к персонажу.
    /// </summary>
    /// <param name="effectData">Данные применяемого эффекта.</param>
    /// <param name="casterStats">Статы того, кто наложил эффект (может быть null).</param>
    public void ApplyStatus(StatusEffectData effectData, CharacterStats casterStats)
    {
        if (effectData == null || characterStats.IsDead) return;

        if (!effectData.CanStack)
        {
            var existingEffect = activeEffects.FirstOrDefault(e => e.Data == effectData);
            if (existingEffect != null)
            {
                RefreshExistingEffect(existingEffect, effectData, casterStats);
                return;
            }
        }

        float duration = CalculateDuration(effectData, casterStats);
        
        // Не применяем временные эффекты с нулевой длительностью, если у них нет других механик
        bool hasPersistentModifiers = effectData.AttributeModifiers.Any(m => m.modifierRestoreCondition != StatusEffectData.RestoreCondition.Timer);
        if (effectData.Condition == StatusEffectData.RestoreCondition.Timer && duration <= 0 && effectData.TickInterval <= 0 && !hasPersistentModifiers)
        {
            return;
        }

        var newActiveEffect = new ActiveStatusEffect(effectData, casterStats, duration);
        activeEffects.Add(newActiveEffect);
        
        ApplyAllComponentEffects(newActiveEffect);
        
        // TODO: Инстанцировать и управлять VFX для статуса
    }

    /// <summary>
    /// Проверяет, активен ли на персонаже указанный статус-эффект.
    /// </summary>
    public bool IsStatusActive(StatusEffectData statusData)
    {
        if (statusData == null) return false;
        return activeEffects.Any(e => e.Data == statusData);
    }
    
    /// <summary>
    /// Снимает все эффекты, которые должны исчезать после отдыха.
    /// </summary>
    public void ClearStatusEffectsOnRest()
    {
        characterStats.ClearRestAttributeModifiers();

        // Создаем копию списка, так как RemoveStatus будет его изменять
        var effectsToRemove = activeEffects
            .Where(e => e.Data.Condition == StatusEffectData.RestoreCondition.Rest)
            .ToList();
            
        foreach(var effect in effectsToRemove)
        {
            RemoveStatus(effect, false);
        }
    }
    /// <summary>
    /// Находит и удаляет активный статус-эффект по его данным (ScriptableObject).
    /// </summary>
    public void RemoveStatus(StatusEffectData statusDataToRemove)
    {
        if (statusDataToRemove == null) return;

        // Ищем активный эффект, соответствующий переданным данным
        var effectToRemove = activeEffects.FirstOrDefault(e => e.Data == statusDataToRemove);
        if (effectToRemove != null)
        {
            // Вызываем наш внутренний, приватный метод для безопасного удаления
            RemoveStatus(effectToRemove, false);
        }
    }

    #region Private Logic
    private void UpdateEffectTimer(ActiveStatusEffect effect)
    {
        if (effect.Data.Condition == StatusEffectData.RestoreCondition.Timer)
        {
            effect.TimeRemaining -= Time.deltaTime;
            if (effect.TimeRemaining <= 0)
            {
                RemoveStatus(effect, true);
            }
        }
    }

    private void UpdateEffectTick(ActiveStatusEffect effect)
    {
        if (effect.Data.TickInterval > 0 && Time.time >= effect.LastTickTime + effect.Data.TickInterval)
        {
            ApplyTickEffect(effect);
            effect.LastTickTime = Time.time;
        }
    }

    private void RefreshExistingEffect(ActiveStatusEffect existingEffect, StatusEffectData newData, CharacterStats caster)
    {
        if (newData.Condition == StatusEffectData.RestoreCondition.Timer)
        {
            float newDuration = CalculateDuration(newData, caster);
            existingEffect.TimeRemaining = Mathf.Max(existingEffect.TimeRemaining, newDuration);
        }
    }

    private void ApplyAllComponentEffects(ActiveStatusEffect effect)
    {
        // Применяем модификаторы атрибутов
        foreach (var mod in effect.Data.AttributeModifiers)
        {
            // Убеждаемся, что модификаторы статуса "до отдыха" всегда применяются как "до отдыха"
            var condition = (effect.Data.Condition == StatusEffectData.RestoreCondition.Rest) 
                ? StatusEffectData.RestoreCondition.Rest : mod.modifierRestoreCondition;
            
            characterStats.AddAttributeModifier(mod.targetAttribute, mod.modifierValue, condition);
            effect.AppliedModifiers.Add(mod);
        }

        // Применяем множитель скорости
        if (!Mathf.Approximately(effect.Data.MovementSpeedMultiplier, 1.0f))
        {
            characterStats.ApplySpeedMultiplier(effect.Data.MovementSpeedMultiplier);
            effect.AppliedSpeedMultiplier = effect.Data.MovementSpeedMultiplier;
        }
    }
    
    private void RemoveAllComponentEffects(ActiveStatusEffect effect)
    {
        // Снимаем модификаторы атрибутов
        foreach (var mod in effect.AppliedModifiers)
        {
            var condition = (effect.Data.Condition == StatusEffectData.RestoreCondition.Rest) 
                ? StatusEffectData.RestoreCondition.Rest : mod.modifierRestoreCondition;

            characterStats.RemoveAttributeModifier(mod.targetAttribute, mod.modifierValue, condition);
        }
        
        // Снимаем множитель скорости
        if (!Mathf.Approximately(effect.AppliedSpeedMultiplier, 1.0f))
        {
            characterStats.RemoveSpeedMultiplier(effect.AppliedSpeedMultiplier);
        }
    }
    
    private float CalculateDuration(StatusEffectData effectData, CharacterStats casterStats)
    {
        if (effectData.Condition != StatusEffectData.RestoreCondition.Timer) return float.PositiveInfinity;
        if (effectData.DurationAttribute == AssociatedAttribute.None) return effectData.FixedDuration;

        var sourceStats = (effectData.DurationSource == StatusEffectData.DurationAttributeSource.Caster && casterStats != null)
            ? casterStats
            : characterStats;
            
        int attributeValue = sourceStats.GetAttributeValue(effectData.DurationAttribute);
        return Mathf.Max(0, attributeValue * effectData.DurationMultiplier);
    }
    
    private void ApplyTickEffect(ActiveStatusEffect activeEffect)
    {
        var data = activeEffect.Data;
        if (data.BaseDamagePerTick == 0) return;

        int tickAmount = data.BaseDamagePerTick;
        if (data.TickEffectScalingAttribute != AssociatedAttribute.None && activeEffect.Caster != null)
        {
            int scalingAttributeValue = activeEffect.Caster.GetAttributeValue(data.TickEffectScalingAttribute);
            tickAmount += Mathf.FloorToInt(scalingAttributeValue * data.TickEffectScaleFactor);
        }

        if (tickAmount > 0)
            characterStats.TakeDamage(tickAmount, activeEffect.Caster?.transform);
        else if (tickAmount < 0)
            characterStats.Heal(Mathf.Abs(tickAmount));
    }

    private void RemoveStatus(ActiveStatusEffect effectInstance, bool expiredByTimer)
    {
        if (effectInstance == null || !activeEffects.Contains(effectInstance)) return;

        RemoveAllComponentEffects(effectInstance);
        activeEffects.Remove(effectInstance);
    }
    #endregion
}