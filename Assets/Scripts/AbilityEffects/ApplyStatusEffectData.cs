using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class ApplyStatusEffectData : AbilityEffectData
{
    public StatusEffectData statusEffectToApply;
    public bool applyToAllInAreaIfAoE = true;

    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (statusEffectToApply == null) return null;

        string feedback = null;

        if (applyToAllInAreaIfAoE && sourceAbility.targetType == TargetType.AreaAroundCaster)
        {
            if (allTargetsInArea == null) return null;

            int targetsHit = 0;
            int targetsAffected = 0;
            foreach (var target in allTargetsInArea)
            {
                if (target == null) continue;
                targetsHit++;
                if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, target, sourceAbility))
                {
                    target.GetComponent<CharacterStatusEffects>()?.ApplyStatus(statusEffectToApply, casterStats);
                    targetsAffected++;
                }
            }
            if (targetsHit > 0)
            {
                if (targetsAffected > 0)
                    feedback = $"{targetsAffected} of {targetsHit} targets are now {statusEffectToApply.statusName}.";
                else
                    feedback = $"All targets resisted {sourceAbility.abilityName}.";
            }
        }
        else if (primaryTargetStats != null)
        {
            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                primaryTargetStats.GetComponent<CharacterStatusEffects>()?.ApplyStatus(statusEffectToApply, casterStats);
                feedback = $"{primaryTargetStats.name} is now {statusEffectToApply.statusName}.";
            }
            else
            {
                feedback = $"{primaryTargetStats.name} resisted {statusEffectToApply.statusName}!";
            }
        }
        
        return feedback;
    }
}