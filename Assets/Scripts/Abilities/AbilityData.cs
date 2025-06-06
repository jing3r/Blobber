using UnityEngine;
using System.Collections.Generic; // Для List
using System.Linq;
using System;

[CreateAssetMenu(fileName = "New Ability", menuName = "Abilities/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("General Info")]
    public string abilityName = "New Ability";
    [TextArea(3, 5)]
    public string description = "Ability description.";
    public Sprite icon;

    [Header("Targeting & Mechanics")]
    public TargetType targetType = TargetType.Self;
    [Tooltip("Для Single_Creature/Single_Interactable. 0 для Self/AreaAroundCaster.")]
    public float range = 0f;
    [Tooltip("Для AreaAroundCaster.")]
    public float areaOfEffectRadius = 0f;

    [Header("Area Effect Filters (для AreaAroundCaster)")]
    public bool affectsSelfInAoe = false; // Если true, кастер сам попадет под AoE своей способности
    public bool affectsPartyMembersInAoe = true;
    public bool affectsAlliesInAoe = false;     // Дружественные NPC
    public bool affectsNeutralsInAoe = false;
    public bool affectsEnemiesInAoe = true;

    [Header("Usage")]
    public float cooldown = 1.0f;
    public int maxCharges = 1;
    public float castTime = 0f; // Пока не используется, но для будущего

    [Header("Contest Mechanics (если способность требует состязания)")]
    public bool usesContest = false;
    [Tooltip("Атрибуты атакующего для состязания (15%, 10%, 5%)")]
    public AssociatedAttribute attackerAttribute1 = AssociatedAttribute.None;
    public AssociatedAttribute attackerAttribute2 = AssociatedAttribute.None;
    public AssociatedAttribute attackerAttribute3 = AssociatedAttribute.None;
    [Tooltip("Атрибуты защищающегося для состязания")]
    public AssociatedAttribute defenderAttribute1 = AssociatedAttribute.None;
    public AssociatedAttribute defenderAttribute2 = AssociatedAttribute.None;
    public AssociatedAttribute defenderAttribute3 = AssociatedAttribute.None;

    [Header("Effects")]
    [SerializeReference] // Позволяет полиморфизм для списка ниже в инспекторе
    public List<AbilityEffectData> effectsToApply = new List<AbilityEffectData>();

    [Header("Visuals & Sound (Optional)")]
    public AudioClip castSound;
    public GameObject startVFXPrefab;
    public GameObject targetVFXPrefab;
    public GameObject areaVFXPrefab;

#if UNITY_EDITOR
    // Небольшой редакторский скрипт для удобного добавления эффектов в инспекторе
    [UnityEditor.CustomEditor(typeof(AbilityData))]
    public class AbilityDataEditor : UnityEditor.Editor
    {
        private Type effectTypeToAdd = typeof(HealEffectData); // Тип по умолчанию для добавления

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); // Отрисовываем стандартный инспектор

            AbilityData abilityData = (AbilityData)target;

            GUILayout.Space(10);
            GUILayout.Label("Add New Effect", UnityEditor.EditorStyles.boldLabel);

            // Выпадающий список для выбора типа эффекта
            // Собираем все типы, наследующие AbilityEffectData
            var effectTypes = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(AbilityEffectData)) && !type.IsAbstract)
                .ToList();
            
            string[] typeNames = effectTypes.Select(t => t.Name).ToArray();
            int currentTypeIndex = effectTypes.IndexOf(effectTypeToAdd);
            if (currentTypeIndex < 0) currentTypeIndex = 0;


            if (effectTypes.Count > 0)
            {
                 int newSelectedTypeIndex = UnityEditor.EditorGUILayout.Popup("Effect Type", currentTypeIndex, typeNames);
                if (newSelectedTypeIndex != currentTypeIndex)
                {
                    effectTypeToAdd = effectTypes[newSelectedTypeIndex];
                }

                if (GUILayout.Button("Add Effect"))
                {
                    AbilityEffectData newEffect = (AbilityEffectData)Activator.CreateInstance(effectTypeToAdd);
                    abilityData.effectsToApply.Add(newEffect);
                    UnityEditor.EditorUtility.SetDirty(abilityData); // Помечаем SO как измененный
                }
            }
            else
            {
                GUILayout.Label("No effect types found.");
            }
        }
    }
#endif
}