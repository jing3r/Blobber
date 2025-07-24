using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class AbilityExecutor
{
    public static void Execute(
        CharacterStats caster,
        AbilityData ability,
        CharacterStats primarySingleTarget,
        Transform primaryInteractableTarget,
        Vector3 initialCastPoint,
        FeedbackManager feedbackManager)
    {
        if (caster == null || ability == null)
        {
            Debug.LogError("AbilityExecutor: Caster or AbilityData is null.");
            return;
        }

        List<CharacterStats> targetsInAreaForAoE = new List<CharacterStats>();
        Vector3 pointForEffectApplication = initialCastPoint;

        // --- Логика определения целей для AoE ---
        if (ability.targetType == TargetType.AreaAroundCaster)
        {
            pointForEffectApplication = caster.transform.position;

            if (ability.affectsSelfInAoe && caster != null && !caster.IsDead)
            {
                targetsInAreaForAoE.Add(caster);
            }

            if (ability.affectsPartyMembersInAoe)
            {
                PartyManager partyMgr = caster.GetComponentInParent<PartyManager>();
                if (partyMgr != null)
                {
                    foreach (CharacterStats member in partyMgr.partyMembers)
                    {
                        if (member != null && member != caster && !member.IsDead && Vector3.Distance(pointForEffectApplication, member.transform.position) <= ability.areaOfEffectRadius)
                        {
                            if (!targetsInAreaForAoE.Contains(member)) targetsInAreaForAoE.Add(member);
                        }
                    }
                }
            }
            
            if (ability.affectsEnemiesInAoe || ability.affectsNeutralsInAoe || ability.affectsAlliesInAoe)
            {
                Collider[] collidersInArea = Physics.OverlapSphere(pointForEffectApplication, ability.areaOfEffectRadius, LayerMask.GetMask("Characters"));

                foreach (Collider col in collidersInArea)
                {
                    CharacterStats targetStats = col.GetComponent<CharacterStats>();
                    if (targetStats == null || targetStats == caster || targetsInAreaForAoE.Contains(targetStats) || targetStats.IsDead) continue;
                    
                    AIController targetAI = targetStats.GetComponent<AIController>();
                    if (targetAI == null) continue;

                    bool isEnemy = (targetAI.currentAlignment == AIController.Alignment.Hostile);
                    bool isNeutral = (targetAI.currentAlignment == AIController.Alignment.Neutral);
                    bool isAllyNPC = (targetAI.currentAlignment == AIController.Alignment.Friendly);
                    
                    if ((isEnemy && ability.affectsEnemiesInAoe) || (isNeutral && ability.affectsNeutralsInAoe) || (isAllyNPC && ability.affectsAlliesInAoe))
                    {
                        targetsInAreaForAoE.Add(targetStats);
                    }
                }
            }
        }
        
        // --- Применение эффектов и обработка фидбека ---
        string finalFeedback = null;

        foreach (AbilityEffectData effectData in ability.effectsToApply)
        {
            string currentFeedback = effectData.ApplyEffect(caster, ability, primarySingleTarget, primaryInteractableTarget, pointForEffectApplication, ref targetsInAreaForAoE);
            
            if (!string.IsNullOrEmpty(currentFeedback))
            {
                finalFeedback = currentFeedback;
            }
        }
        
        // --- НОВОЕ: Проверка на отсутствие эффекта ---
        // Если после всех эффектов фидбек остался null, значит, ни один эффект не нашел свою цель.
        // Это актуально для способностей, у которых только один эффект (как Flashbang).
        if (finalFeedback == null)
        {
            // Проверяем, что это не способность на себя, для которой отсутствие фидбека - норма.
            if (ability.targetType == TargetType.AreaAroundCaster || ability.targetType == TargetType.Point_GroundTargeted)
            {
                finalFeedback = $"{ability.abilityName} did not affect any targets.";
            }
            // Для Single_Target способностей сообщение "Target not found" выдается раньше, в AbilityCastingSystem,
            // поэтому здесь дополнительная проверка не нужна.
        }

        // Показываем финальное сообщение.
        if (!string.IsNullOrEmpty(finalFeedback))
        {
            feedbackManager?.ShowFeedbackMessage(finalFeedback);
        }
    }
}