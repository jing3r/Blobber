using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Status Effect", menuName = "Abilities/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    public enum RestoreCondition { Timer, Rest }
    public enum DurationAttributeSource { Caster, Target } // Чей атрибут использовать для длительности

    [Header("General Info")]
    public string statusID; // Уникальный идентификатор, например "Poisoned_LV1"
    public string statusName; // Отображаемое имя, например "Отравление"
    [TextArea] public string description;
    public Sprite icon;
    public bool isBuff = false; // true - бафф, false - дебафф

    [Header("Duration & Stacking")]
    [Tooltip("Атрибут, влияющий на базовую длительность.")]
    public AssociatedAttribute durationAttribute = AssociatedAttribute.None;
    [Tooltip("Чей атрибут использовать для расчета длительности (кастера или цели).")]
    public DurationAttributeSource durationAttributeSource = DurationAttributeSource.Caster;
    [Tooltip("Множитель для значения атрибута при расчете длительности (атрибут * множитель = секунды).")]
    public float durationMultiplier = 1.0f;
    [Tooltip("Фиксированная длительность в секундах (если атрибут не используется).")]
    public float fixedDuration = 0f; // Используется, если durationAttribute = None
    public RestoreCondition restoreCondition = RestoreCondition.Timer;
    public bool canStack = false; // Пока не реализуем сложную логику стаков
    // public int maxStacks = 1;

    [Header("Periodic Effects (DoT/HoT)")]
    [Tooltip("Интервал в секундах между тиками. 0 - нет периодического эффекта.")]
    public float tickInterval = 0f;
    public int baseDamagePerTick = 0; // Отрицательное значение для HoT
    [Tooltip("Атрибут кастера, который скейлит урон/лечение за тик.")]
    public AssociatedAttribute tickEffectScalingAttribute = AssociatedAttribute.None;
    public float tickEffectScaleFactor = 0f;
    [Header("Movement Effects")]
    [Tooltip("Множитель скорости передвижения. 0.5 = замедление в 2 раза, 1.2 = ускорение на 20%. 1.0 = нет эффекта.")]
    public float movementSpeedMultiplier = 1.0f; 
    [Header("Attribute Modifiers")]
    public List<AttributeModifier> attributeModifiers = new List<AttributeModifier>();
    
    [System.Serializable]
    public class AttributeModifier
    {
        public AssociatedAttribute targetAttribute;
        public int modifierValue; // Отрицательное для дебаффа, положительное для баффа
        // public bool isPercentage = false; // Пока только абсолютные значения
        [Tooltip("Когда этот конкретный модификатор снимается (может отличаться от общего RestoreCondition статуса)")]
        public RestoreCondition modifierRestoreCondition = RestoreCondition.Timer; // По умолчанию наследует от статуса
    }

    [Header("Visuals (Optional)")]
    [Tooltip("Эффект частиц, который будет создан на цели при наложении статуса.")]
    public GameObject statusVFXPrefab;
}