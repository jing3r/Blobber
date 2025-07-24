using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class DisplacementEffectData : AbilityEffectData
{
    public float baseDisplacementDistance = 3.0f;
    public AssociatedAttribute distanceScalingAttribute = AssociatedAttribute.Body;
    public float distancePerAttributePoint = 0.5f;
    public DisplacementDirectionType directionType = DisplacementDirectionType.AwayFromCaster;
    public bool targetIsCaster = false;
    public float displacementDuration = 0.3f;
    
public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
{
    // --- Логика для эффекта на себя (Charge/Dash) ---
    if (targetIsCaster)
    {
        // Убедимся, что кастер существует
        if (casterStats != null)
        {
            casterStats.StartCoroutine(DisplaceTargetCoroutine(casterStats, casterStats));
            return $"{casterStats.name} uses {sourceAbility.abilityName}.";
        }
    }
    // --- Логика для эффекта на другую цель (Толчок) ---
    else if (primaryTargetStats != null)
    {
        if (!sourceAbility.usesContest || CombatHelper.ResolveAttributeContest(casterStats, primaryTargetStats, sourceAbility))
        {
            primaryTargetStats.StartCoroutine(DisplaceTargetCoroutine(casterStats, primaryTargetStats));
            return $"{primaryTargetStats.name} is displaced.";
        }
        else
        {
            return $"{primaryTargetStats.name} resisted the displacement.";
        }
    }
    
    // Если ни одно из условий не выполнилось
    return null;
}
    
    // Корутина DisplaceTargetCoroutine остается без изменений
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
        UnityEngine.AI.NavMeshAgent agent = (aiMovement != null) ? aiMovement.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
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
                if (UnityEngine.AI.NavMesh.SamplePosition(targetController.transform.position, out UnityEngine.AI.NavMeshHit navHit, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    agent.isStopped = false;
                }
            }
        }
    }
}