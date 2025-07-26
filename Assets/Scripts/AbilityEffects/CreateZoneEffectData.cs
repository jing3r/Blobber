using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

[Serializable]
public class CreateZoneEffectData : AbilityEffectData
{
    [Header("Настройки зоны")]
    [SerializeField] private GameObject zonePrefab;
    [SerializeField] private float baseZoneDuration = 10f;
    [SerializeField] private AssociatedAttribute durationScalingAttribute = AssociatedAttribute.Mind;
    [SerializeField] private float durationPerAttributePoint = 1.0f;

    [Header("Настройки размещения")]
    [SerializeField] private float maxPlacementHeightAboveGround = 1.5f;
    [SerializeField] private LayerMask groundPlacementMask;

    [Header("Фидбек")]
    [SerializeField] private string placementVerb = "creates";
    
    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (zonePrefab == null || casterStats == null || sourceAbility == null) return null;

        float finalDuration = baseZoneDuration;
        if (durationScalingAttribute != AssociatedAttribute.None)
        {
            finalDuration += casterStats.GetAttributeValue(durationScalingAttribute) * durationPerAttributePoint;
        }
        finalDuration = Mathf.Max(1.0f, finalDuration);

        Vector3 finalSpawnPosition;
        // Используем правильное имя переменной: castPoint
        if (Physics.Raycast(castPoint + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit, maxPlacementHeightAboveGround + 0.5f, groundPlacementMask))
        {
            finalSpawnPosition = groundHit.point;
        }
        else if (NavMesh.SamplePosition(castPoint, out var navHit, 2.0f, NavMesh.AllAreas))
        {
            finalSpawnPosition = navHit.position;
        }
        else
        {
            return $"Cannot place {sourceAbility.AbilityName} there.";
        }

        GameObject zoneInstance = GameObject.Instantiate(zonePrefab, finalSpawnPosition, Quaternion.identity);

        if (zoneInstance.GetComponent<SphereCollider>() is SphereCollider zoneTrigger)
        {
            zoneTrigger.radius = sourceAbility.AreaOfEffectRadius;
        }

        if (zoneInstance.GetComponent<ZoneEffectController>() is ZoneEffectController zoneController)
        {
            zoneController.Initialize(casterStats, finalDuration, sourceAbility);
        }
        else
        {
            GameObject.Destroy(zoneInstance, finalDuration);
        }

        return $"{casterStats.name} {placementVerb} a {sourceAbility.AbilityName}.";
    }
}