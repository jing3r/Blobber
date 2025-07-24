using UnityEngine;
using System;
using System.Collections.Generic; // Добавляем на всякий случай, если еще нет
using System.Linq;

[Serializable]
public class HealEffectData : AbilityEffectData
{
    public int baseHealAmount = 10;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Mind;
    [Tooltip("Коэффициент для атрибута, например, 2 означает +2 ХП за каждое очко атрибута")]
    public float scaleFactor = 2f;
    [Tooltip("Если true и TargetType способности - AreaAroundCaster, эффект применится ко всем целям в allTargetsInArea. Иначе - только к primaryTargetStats.")]
    public bool applyToAllInAreaIfAoE = true;

    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        int totalHealedAmount = 0;
        int targetsAffected = 0;
        string feedback = null;

        // Логика для AoE
        if (applyToAllInAreaIfAoE && sourceAbility.targetType == TargetType.AreaAroundCaster)
        {
            if (allTargetsInArea == null || allTargetsInArea.Count == 0) return null;

            foreach (var target in allTargetsInArea)
            {
                if (target == null || target.IsDead) continue;
                
                int healAmount = baseHealAmount + Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
                target.Heal(healAmount);
                totalHealedAmount += healAmount;
                targetsAffected++;
            }

            if (targetsAffected > 0)
            {
                // Если затронута только одна цель (например, кастер в AoE), говорим конкретно
                if (targetsAffected == 1)
                {
                    feedback = $"{allTargetsInArea.First(t => t != null && !t.IsDead).name} is healed for {totalHealedAmount} HP.";
                }
                else // Если целей несколько, говорим в среднем
                {
                    int avgHeal = Mathf.CeilToInt((float)totalHealedAmount / targetsAffected);
                    feedback = $"{sourceAbility.abilityName} heals {targetsAffected} targets for an average of {avgHeal} HP.";
                }
            }
        }
        // Логика для одиночной цели
        else if (primaryTargetStats != null)
        {
            if (primaryTargetStats.IsDead) return null;

            int healAmount = baseHealAmount + Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
            primaryTargetStats.Heal(healAmount);
            feedback = $"{primaryTargetStats.name} is healed for {healAmount} HP.";
        }

        return feedback;
    }
}