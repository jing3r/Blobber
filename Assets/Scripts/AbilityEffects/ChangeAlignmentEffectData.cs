using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ChangeAlignmentEffectData : AbilityEffectData
{
    [SerializeField] private AlignmentForEffect targetAlignment = AlignmentForEffect.Neutral;

    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (primaryTargetStats == null) return null;
        
        AIController targetAI = primaryTargetStats.GetComponent<AIController>();
        if (targetAI == null) return null;

        if (!sourceAbility.UsesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
        {
            if (Enum.TryParse(targetAlignment.ToString(), out AIController.Alignment alignment))
            {
                targetAI.SetAlignment(alignment);
                if (alignment == AIController.Alignment.Neutral)
                {
                    targetAI.ClearCurrentThreat();
                    targetAI.ChangeState(AIController.AIState.Idle);
                }
                return $"{primaryTargetStats.name}'s alignment is now {targetAlignment}.";
            }
        }
        else
        {
            return $"{primaryTargetStats.name} resisted the alignment change.";
        }
        
        return null;
    }
}