using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

/// <summary>
/// ScriptableObject, определяющий все свойства и эффекты способности.
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "Abilities/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("Основная информация")]
    [SerializeField] private string abilityName = "New Ability";
    [SerializeField] [TextArea(3, 5)] private string description = "Ability description.";
    [SerializeField] private Sprite icon;
    
    [Header("Механика и цели")]
    [SerializeField] private TargetType targetType = TargetType.Self;
    [SerializeField] [Tooltip("Для Single_Creature/Single_Interactable. 0 для Self/AreaAroundCaster.")] private float range = 0f;
    [SerializeField] [Tooltip("Для AreaAroundCaster.")] private float areaOfEffectRadius = 0f;

    [Header("Фильтры для AoE")]
    [SerializeField] private bool affectsSelfInAoe = false;
    [SerializeField] private bool affectsPartyMembersInAoe = true;
    [SerializeField] private bool affectsAlliesInAoe = false;
    [SerializeField] private bool affectsNeutralsInAoe = false;
    [SerializeField] private bool affectsEnemiesInAoe = true;

    [Header("Использование")]
    [SerializeField] [Min(0)] private float cooldown = 1.0f;
    [SerializeField] [Min(0)] private int maxCharges = 1;
    [SerializeField] [Min(0)] private float castTime = 0f;

    [Header("Механика состязания")]
    [SerializeField] private bool usesContest = false;
    [SerializeField] private AssociatedAttribute attackerAttribute1 = AssociatedAttribute.None;
    [SerializeField] private AssociatedAttribute attackerAttribute2 = AssociatedAttribute.None;
    [SerializeField] private AssociatedAttribute attackerAttribute3 = AssociatedAttribute.None;
    [SerializeField] private AssociatedAttribute defenderAttribute1 = AssociatedAttribute.None;
    [SerializeField] private AssociatedAttribute defenderAttribute2 = AssociatedAttribute.None;
    [SerializeField] private AssociatedAttribute defenderAttribute3 = AssociatedAttribute.None;
    
    [Header("Эффекты")]
    [SerializeReference] private List<AbilityEffectData> effectsToApply = new List<AbilityEffectData>();

    [Header("Визуальные эффекты и звук")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private GameObject startVFXPrefab;
    [SerializeField] private GameObject targetVFXPrefab;
    [SerializeField] private GameObject areaVFXPrefab;
    
    // Публичные свойства для доступа только на чтение
    public string AbilityName => abilityName;
    public string Description => description;
    public Sprite Icon => icon;
    public TargetType TargetType => targetType;
    public float Range => range;
    public float AreaOfEffectRadius => areaOfEffectRadius;
    public bool AffectsSelfInAoe => affectsSelfInAoe;
    public bool AffectsPartyMembersInAoe => affectsPartyMembersInAoe;
    public bool AffectsAlliesInAoe => affectsAlliesInAoe;
    public bool AffectsNeutralsInAoe => affectsNeutralsInAoe;
    public bool AffectsEnemiesInAoe => affectsEnemiesInAoe;
    public float Cooldown => cooldown;
    public int MaxCharges => maxCharges;
    public float CastTime => castTime;
    public bool UsesContest => usesContest;
    public AssociatedAttribute AttackerAttribute1 => attackerAttribute1;
    public AssociatedAttribute AttackerAttribute2 => attackerAttribute2;
    public AssociatedAttribute AttackerAttribute3 => attackerAttribute3;
    public AssociatedAttribute DefenderAttribute1 => defenderAttribute1;
    public AssociatedAttribute DefenderAttribute2 => defenderAttribute2;
    public AssociatedAttribute DefenderAttribute3 => defenderAttribute3;
    public IReadOnlyList<AbilityEffectData> EffectsToApply => effectsToApply.AsReadOnly();
    public AudioClip CastSound => castSound;
    public GameObject StartVFXPrefab => startVFXPrefab;
    public GameObject TargetVFXPrefab => targetVFXPrefab;
    public GameObject AreaVFXPrefab => areaVFXPrefab;
    

#if UNITY_EDITOR
    /// <summary>
    /// Пользовательский редактор для удобного добавления полиморфных эффектов в инспекторе.
    /// </summary>
    [CustomEditor(typeof(AbilityData))]
    public class AbilityDataEditor : Editor
    {
        private Type effectTypeToAdd;
        private List<Type> effectTypes;
        private string[] effectTypeNames;

        private void OnEnable()
        {
            // Кэшируем типы при включении, чтобы не искать их каждый кадр
            effectTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(AbilityEffectData)) && !type.IsAbstract)
                .ToList();
            
            effectTypeNames = effectTypes.Select(t => t.Name).ToArray();
            effectTypeToAdd = effectTypes.FirstOrDefault();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (effectTypeToAdd == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add New Effect", EditorStyles.boldLabel);
            
            int currentTypeIndex = effectTypes.IndexOf(effectTypeToAdd);
            int newSelectedTypeIndex = EditorGUILayout.Popup("Effect Type", currentTypeIndex, effectTypeNames);
            if (newSelectedTypeIndex != currentTypeIndex)
            {
                effectTypeToAdd = effectTypes[newSelectedTypeIndex];
            }

            if (GUILayout.Button("Add Effect"))
            {
                var abilityData = (AbilityData)target;
                var newEffect = (AbilityEffectData)Activator.CreateInstance(effectTypeToAdd);
                abilityData.EffectsToApply.ToList().Add(newEffect);
                EditorUtility.SetDirty(abilityData);
            }
        }
    }
#endif
}