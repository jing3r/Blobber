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
        public abstract string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea);
}