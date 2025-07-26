using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Управляет поведением зоны с длительным эффектом (например, лужа кислоты).
/// Периодически применяет эффекты к сущностям внутри своего триггера.
/// </summary>
public class ZoneEffectController : MonoBehaviour
{
    private CharacterStats zoneCaster;
    private AbilityData sourceAbilityData;
    private float effectTickRate = 0.5f;
    
    private float damagePerTick;
    private StatusEffectData StatusEffectToApply;

    private float nextEffectTickTime;
    private readonly List<CharacterStats> targetsInZone = new List<CharacterStats>();

    /// <summary>
    /// Инициализирует зону с параметрами из способности-источника.
    /// </summary>
    public void Initialize(CharacterStats caster, float duration, AbilityData sourceAbility)
    {
        zoneCaster = caster;
        sourceAbilityData = sourceAbility;
        
        ExtractEffectsFromAbility(sourceAbility);
        
        nextEffectTickTime = Time.time;
        Destroy(gameObject, duration);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<CharacterStats>(out var targetStats) && CanAffectTarget(targetStats))
        {
            if (!targetsInZone.Contains(targetStats))
            {
                targetsInZone.Add(targetStats);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (Time.time < nextEffectTickTime) return;
        nextEffectTickTime = Time.time + effectTickRate;

        if (other.TryGetComponent<CharacterStats>(out var targetStats) && targetsInZone.Contains(targetStats))
        {
            ApplyZoneEffects(targetStats);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<CharacterStats>(out var targetStats) && targetsInZone.Contains(targetStats))
        {
            targetsInZone.Remove(targetStats);
            
            // Снимаем статус-эффект при выходе из зоны, если он был
            if (StatusEffectToApply != null)
            {
                targetStats.GetComponent<CharacterStatusEffects>()?.RemoveStatus(StatusEffectToApply);
            }
        }
    }
    
    private void ExtractEffectsFromAbility(AbilityData ability)
    {
        // Ищем и кэшируем эффект урона
        var damageEffect = ability.EffectsToApply.OfType<DamageEffectData>().FirstOrDefault();
        if (damageEffect != null)
        {
            int casterBonus = zoneCaster != null ? Mathf.FloorToInt(zoneCaster.GetAttributeValue(damageEffect.ScalingAttribute) * damageEffect.ScaleFactor) : 0;
            damagePerTick = Mathf.Max(1, damageEffect.BaseDamageAmount + casterBonus);
        }

        // Ищем и кэшируем статусный эффект
        var statusEffect = ability.EffectsToApply.OfType<ApplyStatusEffectData>().FirstOrDefault();
        if (statusEffect != null)
        {
            StatusEffectToApply = statusEffect.StatusEffectToApply;
        }
    }
    
    private void ApplyZoneEffects(CharacterStats target)
    {
        // 1. Наносим урон, если он есть
        if (damagePerTick > 0)
        {
            target.TakeDamage(Mathf.CeilToInt(damagePerTick), zoneCaster?.transform);
        }

        // 2. Накладываем/обновляем статус, если он есть
        var targetStatusEffects = target.GetComponent<CharacterStatusEffects>();
        if (targetStatusEffects != null && StatusEffectToApply != null)
        {
            bool canApply = !sourceAbilityData.UsesContest || CombatHelper.ResolveAttributeContest(zoneCaster, target, sourceAbilityData);
            if (canApply)
            {
                targetStatusEffects.ApplyStatus(StatusEffectToApply, zoneCaster);
            }
        }
    }

    private bool CanAffectTarget(CharacterStats targetStats)
    {
        if (targetStats == null || targetStats.IsDead) return false;
        
        // Используем фильтры из AbilityData для определения, можно ли воздействовать на цель
        bool isPlayerCharacter = targetStats.GetComponent<AIController>() == null;
        if (isPlayerCharacter)
        {
            bool isSelf = (targetStats == zoneCaster);
            // Если цель - сам кастер или член его партии
            if (isSelf) return sourceAbilityData.AffectsSelfInAoe;
            return sourceAbilityData.AffectsPartyMembersInAoe;
        }
        else
        {
            // Если цель - NPC
            var targetAI = targetStats.GetComponent<AIController>();
            if (targetAI == null) return false;
            
            switch (targetAI.CurrentAlignment)
            {
                case AIController.Alignment.Hostile: return sourceAbilityData.AffectsEnemiesInAoe;
                case AIController.Alignment.Neutral: return sourceAbilityData.AffectsNeutralsInAoe;
                case AIController.Alignment.Friendly: return sourceAbilityData.AffectsAlliesInAoe;
                default: return false;
            }
        }
    }
}