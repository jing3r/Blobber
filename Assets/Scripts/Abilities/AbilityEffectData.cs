// AbilityEffectData.cs

using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// --- Вспомогательные перечисления ---
public enum AssociatedAttribute { Body, Agility, Spirit, Mind, Proficiency, None }
public enum TargetType { Self, Single_Creature, Single_Interactable, AreaAroundCaster, Point_GroundTargeted }
public enum AIStateForEffect { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
public enum AlignmentForEffect { Friendly, Neutral, Hostile }
public enum DisplacementDirectionType { AwayFromCaster, TowardsCaster, CasterForward, SpecificVector }

// --- Абстрактный базовый класс для всех эффектов ---
[Serializable]
public abstract class AbilityEffectData
{
    public virtual string GetDescription() => "Базовый эффект";

    /// <summary>
    /// Применяет эффект и возвращает структуру с результатами для системы фидбека.
    /// </summary>
    public abstract EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea);
}


// --- Конкретные реализации эффектов ---

[Serializable]
public class HealEffectData : AbilityEffectData
{
    public int baseHealAmount = 10;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Mind;
    public float scaleFactor = 2f;
    public bool applyToAllInAreaIfAoE = true;
    [Tooltip("Текстовое описание эффекта, например 'healed'")]
    public string effectVerb = "healed";

          
    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        int healAmount = baseHealAmount;
        if (scalingAttribute != AssociatedAttribute.None && casterStats != null)
        {
            healAmount += Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
        }

        EffectResult result = new EffectResult
        {
            WasApplied = true,
            EffectType = this.effectVerb
        };

        if (applyToAllInAreaIfAoE && allTargetsInArea != null && allTargetsInArea.Count > 0)
        {
            result.IsSingleTarget = false;
            foreach (var target in allTargetsInArea)
            {
                if (target != null && !target.IsDead)
                {
                    target.Heal(healAmount);
                    result.TargetsHit++;
                    result.TotalValue += healAmount;
                }
            }
            if (result.TargetsHit == 0) return EffectResult.None;
        }
        else if (primaryTargetStats != null && !primaryTargetStats.IsDead)
        {
            primaryTargetStats.Heal(healAmount);
            result.IsSingleTarget = true;
            result.TargetName = primaryTargetStats.name;
            result.TargetsHit = 1;
            result.TargetsAffected = 1;
            result.TotalValue = healAmount;
        }
        else
        {
            return EffectResult.None;
        }
        return result;
    }
}

[Serializable]
public class DamageEffectData : AbilityEffectData
{
    public int baseDamageAmount = 5;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Body;
    public float scaleFactor = 1f;
    public bool applyToAllInAreaIfAoE = true;
    [Tooltip("Текстовое описание, например 'damaged'")]
    public string effectVerb = "damaged";

          
    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        EffectResult result = new EffectResult
        {
            WasApplied = true,
            EffectType = this.effectVerb
        };

        int baseDamage = baseDamageAmount;
        if (scalingAttribute != AssociatedAttribute.None && casterStats != null)
        {
            baseDamage += Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
        }
        baseDamage = Mathf.Max(1, baseDamage);

        if (applyToAllInAreaIfAoE && allTargetsInArea != null && allTargetsInArea.Count > 0)
        {
            result.IsSingleTarget = false;
            foreach (var target in allTargetsInArea)
            {
                if (target != null && !target.IsDead)
                {
                    if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, target, sourceAbility))
                    {
                        target.TakeDamage(baseDamage, casterStats.transform);
                        result.TargetsHit++;
                        result.TotalValue += baseDamage;
                    }
                }
            }
            if (result.TargetsHit == 0) return EffectResult.None;
        }
        else if (primaryTargetStats != null && !primaryTargetStats.IsDead)
        {
            result.IsSingleTarget = true;
            result.TargetName = primaryTargetStats.name;
            result.TargetsHit = 1;
            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                primaryTargetStats.TakeDamage(baseDamage, casterStats.transform);
                result.TargetsAffected = 1;
                result.TotalValue = baseDamage;
            }
        }
        else
        {
            return EffectResult.None;
        }
        return result;
    }
}

[Serializable]
public class ApplyStatusEffectData : AbilityEffectData
{
    public StatusEffectData statusEffectToApply;
    public bool applyToAllInAreaIfAoE = true;
    [Tooltip("Текстовое описание эффекта для фидбека, например 'dazed' или 'feared'")]
    public string effectVerb = "affected";

          
    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (statusEffectToApply == null) return EffectResult.None;
        
        EffectResult result = new EffectResult
        {
            WasApplied = true,
            EffectType = this.effectVerb
        };
        
        if (applyToAllInAreaIfAoE && sourceAbility.targetType == TargetType.AreaAroundCaster)
        {
            result.IsSingleTarget = false;
            // ВАЖНО: Если список целей пуст, TargetsHit останется 0, и фидбек будет корректным.
            if (allTargetsInArea.Count == 0)
            {
                // Нечего делать, но сообщаем, что никого не задели
                return result; // WasApplied=true, TargetsHit=0
            }

            foreach (var target in allTargetsInArea)
            {
                if (target == null) continue;
                result.TargetsHit++;
                
                if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, target, sourceAbility))
                {
                    target.GetComponent<CharacterStatusEffects>()?.ApplyStatus(statusEffectToApply, casterStats);
                    result.TargetsAffected++;
                }
            }
        }
        else if (primaryTargetStats != null)
        {
            result.IsSingleTarget = true;
            result.TargetName = primaryTargetStats.name;
            result.TargetsHit = 1;

            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                primaryTargetStats.GetComponent<CharacterStatusEffects>()?.ApplyStatus(statusEffectToApply, casterStats);
                result.TargetsAffected = 1;
            }
        }
        else
        {
            // Если это не AoE и нет одиночной цели, то эффект не сработал.
            return EffectResult.None; 
        }

        return result;
    }
}

[Serializable]
public class ChangeAIStateEffectData : AbilityEffectData
{
    public AIStateForEffect targetState = AIStateForEffect.Fleeing;
    [Tooltip("Текстовое описание эффекта, например 'intimidated'")]
    public string effectVerb = "intimidated";

    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> allTargetsInArea)
    {
        // Примечание: этот эффект пока не поддерживает AoE. Логика ниже только для одиночной цели.
        if (primaryTargetStats != null)
        {
            AIController targetAI = primaryTargetStats.GetComponent<AIController>();
            if (targetAI == null) return EffectResult.None;

            EffectResult result = new EffectResult
            {
                WasApplied = true,
                IsSingleTarget = true,
                TargetName = primaryTargetStats.name,
                TargetsHit = 1,
                EffectType = this.effectVerb
            };

            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                if (Enum.TryParse(targetState.ToString(), out AIController.AIState aiState))
                {
                    if (targetState == AIStateForEffect.Fleeing)
                        targetAI.ForceFlee(casterStats.transform);
                    else
                        targetAI.ChangeState(aiState);
                    
                    result.TargetsAffected = 1;
                }
            }
            return result;
        }
        return EffectResult.None;
    }
}

[Serializable]
public class ChangeAlignmentEffectData : AbilityEffectData
{
    public AlignmentForEffect targetAlignment = AlignmentForEffect.Neutral;
    [Tooltip("Текстовое описание эффекта, например 'pacified'")]
    public string effectVerb = "pacified";

    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> allTargetsInArea)
    {
        // Примечание: этот эффект пока не поддерживает AoE.
        if (primaryTargetStats != null)
        {
            AIController targetAI = primaryTargetStats.GetComponent<AIController>();
            if (targetAI == null) return EffectResult.None;
            
            EffectResult result = new EffectResult
            {
                WasApplied = true,
                IsSingleTarget = true,
                TargetName = primaryTargetStats.name,
                TargetsHit = 1,
                EffectType = this.effectVerb
            };
            
            if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                if (Enum.TryParse(targetAlignment.ToString(), out AIController.Alignment alignment))
                {
                    targetAI.currentAlignment = alignment;
                    if (alignment == AIController.Alignment.Neutral)
                    {
                        targetAI.ClearCurrentThreat();
                        targetAI.ChangeState(AIController.AIState.Idle);
                    }
                    result.TargetsAffected = 1;
                }
            }
            return result;
        }
        return EffectResult.None;
    }
}

[Serializable]
public class InteractEffectData : AbilityEffectData
{
    public override string GetDescription() => "Взаимодействует с объектом на расстоянии.";

    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats _, Transform primaryTargetTransform, Vector3 __, ref List<CharacterStats> ___)
    {
        if (primaryTargetTransform != null)
        {
            Interactable interactable = primaryTargetTransform.GetComponent<Interactable>();
            if (interactable != null)
            {
                string interactionMessage = interactable.Interact();
                return new EffectResult { WasApplied = true, EffectType = "Telekinesis", TargetName = interactionMessage };
            }
        }
        return new EffectResult { WasApplied = true, EffectType = "Telekinesis", TargetName = "Nothing happened." };
    }
}

[Serializable]
public class DisplacementEffectData : AbilityEffectData
{
    public float baseDisplacementDistance = 3.0f;
    public AssociatedAttribute distanceScalingAttribute = AssociatedAttribute.Body;
    public float distancePerAttributePoint = 0.5f;
    public DisplacementDirectionType directionType = DisplacementDirectionType.AwayFromCaster;
    public bool targetIsCaster = false;
    public float displacementDuration = 0.3f;

    public override string GetDescription() => $"Перемещает цель на дистанцию, зависящую от Тела.";

    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        // Для этого эффекта состязание может быть важно, если, например, враг пытается оттолкнуть партию.
        // Пока кастером является игрок, состязание не применяется к самому себе.
        if (!targetIsCaster)
        {
            if (sourceAbility.usesContest && !CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
            {
                // Если состязание провалено (например, цель слишком "тяжелая"), возвращаем результат о провале.
                // Это можно будет обработать в Executor для сообщения "Target resisted the push".
                return new EffectResult { WasApplied = true, IsSingleTarget = true, TargetsHit = 1, TargetsAffected = 0, EffectType = "displacement", TargetName = primaryTargetStats.name };
            }
        }

        CharacterStats actualTargetStats = targetIsCaster ? casterStats : primaryTargetStats;
        if (actualTargetStats == null) return EffectResult.None;
        
        // Запускаем корутину перемещения
        actualTargetStats.StartCoroutine(DisplaceTargetCoroutine(casterStats, actualTargetStats));

        // Если эффект применяется к кастеру/партии (как в Charge/Dash)
        if (targetIsCaster)
        {
            // Возвращаем результат с флагом для простого фидбека
            return new EffectResult { WasApplied = true, UseSimpleCasterFeedback = true };
        }
        else // Если эффект на врага (например, "Толчок")
        {
            // Возвращаем стандартный результат
            return new EffectResult { WasApplied = true, IsSingleTarget = true, TargetsHit = 1, TargetsAffected = 1, EffectType = "displaced", TargetName = actualTargetStats.name };
        }
    
    }
        

    private IEnumerator DisplaceTargetCoroutine(CharacterStats casterStats, CharacterStats targetToDisplace)
    {
        CharacterController targetController = null;
        if (targetToDisplace != null)
        {
            targetController = targetToDisplace.GetComponent<CharacterController>();
            if (targetController == null && targetToDisplace.GetComponent<AIController>() == null)
            {
                PartyManager partyMgr = targetToDisplace.GetComponentInParent<PartyManager>();
                if (partyMgr != null) targetController = partyMgr.GetComponent<CharacterController>();
            }
        }

        if (targetController == null || !targetController.enabled)
        {
            yield break;
        }

        float finalDisplacementDistance = baseDisplacementDistance;
        if (distanceScalingAttribute != AssociatedAttribute.None && casterStats != null)
        {
            finalDisplacementDistance += casterStats.GetAttributeValue(distanceScalingAttribute) * distancePerAttributePoint;
        }
        finalDisplacementDistance = Mathf.Max(0.1f, finalDisplacementDistance);

        Vector3 moveDirection = Vector3.zero;
        switch (directionType)
        {
            case DisplacementDirectionType.AwayFromCaster:
                moveDirection = (targetToDisplace.transform.position - casterStats.transform.position).normalized;
                break;
            case DisplacementDirectionType.TowardsCaster:
                moveDirection = (casterStats.transform.position - targetToDisplace.transform.position).normalized;
                break;
            case DisplacementDirectionType.CasterForward:
                Transform lookTransform = casterStats.transform;
                if (casterStats.GetComponentInParent<PlayerMovement>() != null)
                    lookTransform = Camera.main.transform;
                moveDirection = lookTransform.forward;
                break;
        }
        moveDirection.y = 0;
        moveDirection = moveDirection.normalized;

        if (moveDirection == Vector3.zero) yield break;

        float elapsedTime = 0f;
        float speed = finalDisplacementDistance / Mathf.Max(0.01f, displacementDuration);

        AIMovement aiMovement = targetToDisplace.GetComponent<AIMovement>();
        NavMeshAgent agent = (aiMovement != null) ? aiMovement.GetComponent<NavMeshAgent>() : null;
        bool agentWasEnabled = false;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agentWasEnabled = true;
        }

        while (elapsedTime < displacementDuration)
        {
            Vector3 step = moveDirection * speed * Time.fixedDeltaTime;
            targetController.Move(step);
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (agentWasEnabled && agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
            else
            {
                if (NavMesh.SamplePosition(targetController.transform.position, out NavMeshHit navHit, 1.0f, NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    agent.isStopped = false;
                }
            }
        }
    }
}

[Serializable]
public class CreateZoneEffectData : AbilityEffectData
{
    [Tooltip("Префаб объекта-зоны, который будет создан.")]
    public GameObject zonePrefab;
    [Tooltip("Базовая длительность существования зоны в секундах.")]
    public float baseZoneDuration = 10f;
    [Tooltip("Атрибут кастера, влияющий на длительность зоны.")]
    public AssociatedAttribute durationScalingAttribute = AssociatedAttribute.Mind;
    [Tooltip("Сколько секунд добавляется к длительности за каждое очко атрибута.")]
    public float durationPerAttributePoint = 1.0f;

    [Header("Zone Placement Options")]
    [Tooltip("Максимальная высота над землей, на которой может быть создана зона.")]
    public float maxPlacementHeightAboveGround = 1.5f;
    [Tooltip("Слой(и), считающийся 'землей' для размещения зоны.")]
    public LayerMask groundPlacementMask;

    [Header("Feedback")]
    [Tooltip("Глагол для фидбека, описывающий действие. Например 'creates', 'places', 'summons'")]
    public string placementVerb = "creates";

    public override EffectResult ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats _, Transform __, Vector3 castPointReceived, ref List<CharacterStats> ___)
    {
        if (zonePrefab == null || casterStats == null || sourceAbility == null) return EffectResult.None;
        
        float finalDuration = baseZoneDuration;
        if (durationScalingAttribute != AssociatedAttribute.None)
        {
            finalDuration += casterStats.GetAttributeValue(durationScalingAttribute) * durationPerAttributePoint;
        }
        finalDuration = Mathf.Max(1.0f, finalDuration);

        Vector3 finalSpawnPosition;
        if (Physics.Raycast(castPointReceived + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit, maxPlacementHeightAboveGround + 0.5f, groundPlacementMask))
        {
            finalSpawnPosition = groundHit.point;
        }
        else if (UnityEngine.AI.NavMesh.SamplePosition(castPointReceived, out var navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            finalSpawnPosition = navHit.position;
        }
        else
        {
            // Возвращаем специальный результат для провала размещения
            return new EffectResult { WasApplied = true, EffectType = "PlacementFailed" };
        }

        GameObject zoneInstance = GameObject.Instantiate(zonePrefab, finalSpawnPosition, Quaternion.identity);

        if (zoneInstance.GetComponent<SphereCollider>() is SphereCollider zoneTrigger)
        {
            zoneTrigger.radius = sourceAbility.areaOfEffectRadius;
        }

        if (zoneInstance.GetComponent<ZoneEffectController>() is ZoneEffectController zoneController)
        {
            zoneController.Initialize(casterStats, finalDuration, sourceAbility);
        }
        else
        {
            GameObject.Destroy(zoneInstance, finalDuration);
        }
        
        // Возвращаем успешный результат для фидбека
        return new EffectResult
        {
            WasApplied = true,
            EffectType = "PlacementSuccess",
            TargetName = placementVerb // Используем TargetName для передачи глагола
        };
    }
}