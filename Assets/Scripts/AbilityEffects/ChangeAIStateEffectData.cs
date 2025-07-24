
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ChangeAIStateEffectData : AbilityEffectData
{
    public AIStateForEffect targetState = AIStateForEffect.Fleeing;

    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        // Этот эффект пока работает только на одиночную цель
        if (primaryTargetStats == null) return null;
        
        AIController targetAI = primaryTargetStats.GetComponent<AIController>();
        if (targetAI == null) return null;

        if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
        {
            if (Enum.TryParse(targetState.ToString(), out AIController.AIState aiState))
            {
                if (targetState == AIStateForEffect.Fleeing)
                {
                    targetAI.ForceFlee(casterStats.transform);
                }
                else
                {
                    targetAI.ChangeState(aiState);
                }
                // Возвращаем фидбек об успехе
                return $"{primaryTargetStats.name}'s state changed to {targetState}.";
            }
        }
        else
        {
            // Возвращаем фидбек о провале
            return $"{primaryTargetStats.name} resisted the state change.";
        }
        
        return null;
    }
}