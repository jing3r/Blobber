using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class DamageEffectData : AbilityEffectData
{
    public int baseDamageAmount = 5;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Body;
    public float scaleFactor = 1f;
    public bool applyToAllInAreaIfAoE = true;

    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        string feedback = null;
        int totalDamageDealt = 0;
        int targetsAffected = 0;

        if (applyToAllInAreaIfAoE && sourceAbility.targetType == TargetType.AreaAroundCaster)
        {
            if (allTargetsInArea == null || allTargetsInArea.Count == 0) return null;

            foreach (var target in allTargetsInArea)
            {
                if (target == null || target.IsDead) continue;
                
                if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, target, sourceAbility))
                {
                    int damageAmount = baseDamageAmount + Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
                    damageAmount = Mathf.Max(1, damageAmount);
                    
                    target.TakeDamage(damageAmount, casterStats.transform);
                    totalDamageDealt += damageAmount;
                    targetsAffected++;
                }
            }
            if (targetsAffected > 0)
            {
                int avgDamage = Mathf.CeilToInt((float)totalDamageDealt / targetsAffected);
                feedback = $"{sourceAbility.abilityName} hits {targetsAffected} targets for an average of {avgDamage} damage.";
            }
        }
        else if (primaryTargetStats != null)
        {
            if (primaryTargetStats.IsDead) return null;

            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                int damageAmount = baseDamageAmount + Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
                damageAmount = Mathf.Max(1, damageAmount);
                
                primaryTargetStats.TakeDamage(damageAmount, casterStats.transform);
                feedback = $"{primaryTargetStats.name} takes {damageAmount} damage.";
            }
            else
            {
                feedback = $"{primaryTargetStats.name} dodged the attack!";
            }
        }

        return feedback;
    }
}