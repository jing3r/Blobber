using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class ApplyStatusEffectData : AbilityEffectData
{
    [SerializeField] private StatusEffectData statusEffectToApply;
    [SerializeField] private bool applyToAllInAreaIfAoE = true;
    public StatusEffectData StatusEffectToApply => statusEffectToApply;
    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (StatusEffectToApply == null) return null;

        string feedback = null;

        if (applyToAllInAreaIfAoE && sourceAbility.TargetType == TargetType.AreaAroundCaster)
        {
            if (allTargetsInArea == null) return null;

            int targetsHit = 0;
            int targetsAffected = 0;
            foreach (var target in allTargetsInArea)
            {
                if (target == null) continue;
                targetsHit++;
                if (!sourceAbility.UsesContest || CombatHelper.ResolveAttributeContest(casterStats, target, sourceAbility))
                {
                    target.GetComponent<CharacterStatusEffects>()?.ApplyStatus(StatusEffectToApply, casterStats);
                    targetsAffected++;
                }
            }
            if (targetsHit > 0)
            {
                if (targetsAffected > 0)
                    feedback = $"{targetsAffected} of {targetsHit} targets are now {StatusEffectToApply.StatusName}.";
                else
                    feedback = $"All targets resisted {sourceAbility.AbilityName}.";
            }
        }
        else if (primaryTargetStats != null)
        {
            if (!sourceAbility.UsesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                primaryTargetStats.GetComponent<CharacterStatusEffects>()?.ApplyStatus(StatusEffectToApply, casterStats);
                feedback = $"{primaryTargetStats.name} is now {StatusEffectToApply.StatusName}.";
            }
            else
            {
                feedback = $"{primaryTargetStats.name} resisted {StatusEffectToApply.StatusName}!";
            }
        }
        
        return feedback;
    }
}