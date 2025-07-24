using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

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
            return $"Cannot place {sourceAbility.abilityName} there.";
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
        
        return $"{casterStats.name} {placementVerb} a {sourceAbility.abilityName}.";
    }
}