using UnityEngine;
using System;
using System.Collections.Generic;

// --- Вспомогательные перечисления, используемые в эффектах ---

public enum AssociatedAttribute { Body, Agility, Spirit, Mind, Proficiency, None }
public enum TargetType { Self, Single_Creature, Single_Interactable, AreaAroundCaster, Point_GroundTargeted }
public enum AIStateForEffect { Idle, Wandering, Chasing, Attacking, Fleeing, Dead }
public enum AlignmentForEffect { Friendly, Neutral, Hostile }
public enum DisplacementDirectionType { AwayFromCaster, TowardsCaster, CasterForward, SpecificVector }

/// <summary>
/// Абстрактный базовый класс для всех эффектов, которые может иметь способность.
/// </summary>
[Serializable]
public abstract class AbilityEffectData
{
    /// <summary>
    /// Применяет логику эффекта к целям.
    /// </summary>
    /// <param name="casterStats">Статы создателя эффекта.</param>
    /// <param name="sourceAbility">Способность-источник.</param>
    /// <param name="primaryTargetStats">Основная одиночная цель (существо).</param>
    /// <param name="primaryTargetTransform">Основная одиночная цель (любой трансформ, включая интерактивные объекты).</param>
    /// <param name="castPoint">Точка в мире, где был применен эффект (для AoE по земле).</param>
    /// <param name="allTargetsInArea">Список всех целей в области действия (для AoE вокруг кастера).</param>
    /// <returns>Строка с фидбеком для игрока, или null.</returns>
    public abstract string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, 
                                     CharacterStats primaryTargetStats, Transform primaryTargetTransform, 
                                     Vector3 castPoint, ref List<CharacterStats> allTargetsInArea);
}