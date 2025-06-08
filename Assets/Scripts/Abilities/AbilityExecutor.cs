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
        foreach (AbilityEffectData effectData in ability.effectsToApply)
        {
            EffectResult result = effectData.ApplyEffect(caster, ability, primarySingleTarget, primaryInteractableTarget, pointForEffectApplication, ref targetsInAreaForAoE);

            if (!result.WasApplied) continue;

            string feedbackMessage = null;
            
            // --- НОВАЯ ПРОВЕРКА на UseSimpleCasterFeedback ---
            if (result.UseSimpleCasterFeedback)
            {
                feedbackMessage = $"{caster.name} uses {ability.abilityName}.";
            }
            else
            {
                // Стандартная обработка
                switch (result.EffectType)
                {
                    case "Telekinesis":
                        feedbackMessage = FeedbackGenerator.TelekinesisInteract(result.TargetName);
                        break;
                    case "PlacementSuccess":
                        feedbackMessage = $"{caster.name} {result.TargetName} a {ability.abilityName}.";
                        break;
                    case "PlacementFailed":
                        feedbackMessage = $"Cannot place {ability.abilityName} there.";
                        break;
                    default: 
                        feedbackMessage = FeedbackGenerator.GenerateFeedback(ability, result);
                        break;
                }
            }
            
            if (!string.IsNullOrEmpty(feedbackMessage))
            {
                feedbackManager?.ShowFeedbackMessage(feedbackMessage);
            }
        }
    }
}