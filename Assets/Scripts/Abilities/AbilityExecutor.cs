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
        Vector3 initialCastPoint, // Переименовал для ясности - это точка, куда целились или позиция одиночной цели
        FeedbackManager feedbackManager)
    {
        if (caster == null || ability == null)
        {
            Debug.LogError("AbilityExecutor: Caster or AbilityData is null.");
            return;
        }

        bool contestResult = true;
        if (ability.usesContest && caster.GetComponent<AIController>() == null)
        {
            if (primarySingleTarget != null)
            {
                contestResult = CombatHelper.ResolveAttributeContest(caster, primarySingleTarget, ability);
                if (!contestResult)
                {
                    feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: Contest with {primarySingleTarget.name} failed!");
                    return;
                }
                else
                {
                    feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: Contest with {primarySingleTarget.name} won!");
                }
            }
            // Если usesContest = true, а primarySingleTarget = null, то состязание не проводится.
            // contestResult останется true. Это нужно учитывать, если AoE дебафф с состязанием должен работать на каждую цель.
            // Пока что состязание только против основной одиночной цели.
        }

        List<CharacterStats> targetsInAreaForAoE = new List<CharacterStats>();
        Vector3 pointForEffectApplication = initialCastPoint; // Точка, которая будет передана в эффекты.
                                                              // Для Single_Target и Point_GroundTargeted это initialCastPoint.
                                                              // Для AreaAroundCaster это будет позиция кастера.

        if (ability.targetType == TargetType.AreaAroundCaster)
        {
            pointForEffectApplication = caster.transform.position; // Центр AoE - это кастер

            // 1. Кастер (если affectsSelfInAoe)
            if (ability.affectsSelfInAoe && caster != null && !caster.IsDead)
            {
                targetsInAreaForAoE.Add(caster);
            }

            // 2. Члены партии (если affectsPartyMembersInAoe)
            if (ability.affectsPartyMembersInAoe)
            {
                PartyManager partyMgr = caster.GetComponentInParent<PartyManager>(); // Предполагаем, что кастер - член партии или его объект имеет доступ к PartyManager
                if (partyMgr != null)
                {
                    foreach (CharacterStats member in partyMgr.partyMembers)
                    {
                        if (member != null && member != caster && !member.IsDead && 
                            Vector3.Distance(pointForEffectApplication, member.transform.position) <= ability.areaOfEffectRadius)
                        {
                            if (!targetsInAreaForAoE.Contains(member)) // Избегаем дублирования, если кастер уже добавлен
                            {
                                targetsInAreaForAoE.Add(member);
                            }
                        }
                    }
                }
            }

            // 3. NPC (если способность действует на них)
            if (ability.affectsEnemiesInAoe || ability.affectsNeutralsInAoe || ability.affectsAlliesInAoe)
            {
                int characterLayer = LayerMask.NameToLayer("Characters");
                if (characterLayer != -1) {
                    LayerMask npcMask = 1 << characterLayer;
                    Collider[] collidersInArea = Physics.OverlapSphere(pointForEffectApplication, ability.areaOfEffectRadius, npcMask);

                    foreach (Collider col in collidersInArea)
                    {
                        CharacterStats targetStats = col.GetComponent<CharacterStats>();
                        if (targetStats == null || targetStats == caster || targetsInAreaForAoE.Contains(targetStats)) continue;

                        AIController targetAI = targetStats.GetComponent<AIController>();
                        if (targetAI == null) continue; // Не NPC

                        bool isEnemy = (targetAI.currentAlignment == AIController.Alignment.Hostile);
                        bool isNeutral = (targetAI.currentAlignment == AIController.Alignment.Neutral);
                        bool isAllyNPC = (targetAI.currentAlignment == AIController.Alignment.Friendly);
                        bool addThisNpc = false;

                        if (isEnemy && ability.affectsEnemiesInAoe) addThisNpc = true;
                        else if (isNeutral && ability.affectsNeutralsInAoe) addThisNpc = true;
                        else if (isAllyNPC && ability.affectsAlliesInAoe) addThisNpc = true;

                        if (addThisNpc && !targetStats.IsDead)
                        {
                            targetsInAreaForAoE.Add(targetStats);
                        }
                    }
                }
            }
        }
        // Если тип цели НЕ AreaAroundCaster (т.е. Point_GroundTargeted, Single_Creature, Single_Interactable, Self),
        // то pointForEffectApplication остается равным initialCastPoint.
        // А список targetsInAreaForAoE будет пуст (или содержать только кастера для Self, если эффекты это ожидают).
        // Для Self-способностей, primarySingleTarget обычно устанавливается в кастера в AbilityCastingSystem.

        // Применение эффектов
        foreach (AbilityEffectData effectData in ability.effectsToApply)
        {
            // ВАЖНО: Передаем pointForEffectApplication как точку для применения этого эффекта.
            // Эффекты сами должны решить, как ее использовать.
            // CreateZoneEffectData будет использовать ее как точку создания.
            // HealEffectData/ApplyStatusEffectData с флагом applyToAllInAreaIfAoE будет использовать targetsInAreaForAoE.
            // Эффекты на одиночную цель будут использовать primarySingleTarget.
            
            effectData.ApplyEffect(caster, ability, primarySingleTarget, primaryInteractableTarget, pointForEffectApplication, ref targetsInAreaForAoE, contestResult);
        }
        
        if (contestResult) 
        {
            // Общий фидбек можно улучшить, чтобы он был более контекстным
            // Например, если это была атака на цель, то "(Кастер) использует (Способность) на (Цель)."
            // Если это AoE, то просто "(Кастер) использует (Способность)."
            // feedbackManager?.ShowFeedbackMessage($"{caster.name} uses {ability.abilityName}.");
        }
    }
}