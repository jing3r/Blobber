using UnityEngine;
using System.Collections.Generic;
using System; // Для Serializable

/// <summary>
/// ScriptableObject, определяющий все свойства статус-эффекта (баффа или дебаффа).
/// </summary>
[CreateAssetMenu(fileName = "New Status Effect", menuName = "Abilities/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    public enum RestoreCondition { Timer, Rest, RequiresCure }
    public enum DurationAttributeSource { Caster, Target }

    [Serializable]
    public class AttributeModifier
    {
        public AssociatedAttribute targetAttribute;
        [Tooltip("Отрицательное для дебаффа, положительное для баффа.")]
        public int modifierValue;
        [Tooltip("Когда этот конкретный модификатор снимается (может отличаться от общего статуса).")]
        public RestoreCondition modifierRestoreCondition = RestoreCondition.Timer;
    }

    [Header("Основная информация")]
    [SerializeField] private string statusID;
    [SerializeField] private string statusName;
    [SerializeField] [TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private bool isBuff = false;

    [Header("Длительность и стаки")]
    [SerializeField] private AssociatedAttribute durationAttribute = AssociatedAttribute.None;
    [SerializeField] private DurationAttributeSource durationAttributeSource = DurationAttributeSource.Caster;
    [SerializeField] private float durationMultiplier = 1.0f;
    [SerializeField] private float fixedDuration = 0f;
    [SerializeField] private RestoreCondition restoreCondition = RestoreCondition.Timer;
    [SerializeField] private bool canStack = false;

    [Header("Периодические эффекты (DoT/HoT)")]
    [SerializeField] [Tooltip("Интервал в секундах между тиками. 0 - нет эффекта.")] private float tickInterval = 0f;
    [SerializeField] private int baseDamagePerTick = 0;
    [SerializeField] private AssociatedAttribute tickEffectScalingAttribute = AssociatedAttribute.None;
    [SerializeField] private float tickEffectScaleFactor = 0f;
    
    [Header("Эффекты на движение")]
    [SerializeField] [Tooltip("Множитель скорости. <1 для замедления, >1 для ускорения.")] private float movementSpeedMultiplier = 1.0f;
    
    [Header("Модификаторы атрибутов")]
    [SerializeField] private List<AttributeModifier> attributeModifiers = new List<AttributeModifier>();
    
    [Header("Визуальные эффекты")]
    [SerializeField] private GameObject statusVFXPrefab;
    
    public string StatusID => statusID;
    public string StatusName => statusName;
    public string Description => description;
    public Sprite Icon => icon;
    public bool IsBuff => isBuff;
    public AssociatedAttribute DurationAttribute => durationAttribute;
    public DurationAttributeSource DurationSource => durationAttributeSource;
    public float DurationMultiplier => durationMultiplier;
    public float FixedDuration => fixedDuration;
    public RestoreCondition Condition => restoreCondition;
    public bool CanStack => canStack;
    public float TickInterval => tickInterval;
    public int BaseDamagePerTick => baseDamagePerTick;
    public AssociatedAttribute TickEffectScalingAttribute => tickEffectScalingAttribute;
    public float TickEffectScaleFactor => tickEffectScaleFactor;
    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public IReadOnlyList<AttributeModifier> AttributeModifiers => attributeModifiers.AsReadOnly();
    public GameObject StatusVFXPrefab => statusVFXPrefab;
}