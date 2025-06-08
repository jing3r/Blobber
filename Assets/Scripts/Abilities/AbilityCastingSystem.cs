using UnityEngine;
using UnityEngine.InputSystem; // Для новой системы ввода
using System.Collections.Generic; // Для List<AbilityData>
using System.Linq; // Для FirstOrDefault

public class AbilityCastingSystem : MonoBehaviour
{
    public static AbilityCastingSystem Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private PlayerGlobalActions playerGlobalActions;
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private FeedbackManager feedbackManager;
    [SerializeField] private TargetingSystem targetingSystem;

    [Header("Layer Masks for Targeting")]
    public LayerMask interactableLayerMask; // Назначь сюда слой Interactable
    public LayerMask characterLayerMask;    // Назначь сюда слой Characters
    public LayerMask defaultRaycastLayers;  // Все остальное, если нужно
    [Header("Targeting Layers")]
    public LayerMask groundLayerMask = 1; // Слой по умолчанию (Everything), нужно настроить на слой земли
    public LayerMask generalObstacleLayerMask;

    // Ссылки на CharacterAbilities активного персонажа (или первого в партии)
    // Это нужно, чтобы знать, какие способности есть у кастера
    private CharacterAbilities currentCasterAbilities;
    private CharacterStats currentCasterStats;

    // Для хранения CharacterStats члена партии, над UI которого наведен курсор
    private CharacterStats hoveredPartyMemberTarget;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerGlobalActions == null) Debug.LogError("AbilityCastingSystem: PlayerGlobalActions не назначен!", this);
        if (partyManager == null) Debug.LogError("AbilityCastingSystem: PartyManager не назначен!", this);
        // FeedbackManager опционален
        if (targetingSystem == null) targetingSystem = GetComponent<TargetingSystem>();
        if (targetingSystem == null) Debug.LogError("AbilityCastingSystem: TargetingSystem не назначен!", this);
    }

    void Start()
    {
        // Предполагаем, что кастер - это первый живой член партии.
        // Эту логику можно будет усложнить (например, выбор активного персонажа).
        UpdateCurrentCaster();
    }

    void Update()
    {
        // Можно обновлять кастера, если состав партии может меняться или активный персонаж
        // UpdateCurrentCaster(); 
        // Но пока это не требуется каждый кадр, делаем в Start и при смене партии (если будет такой эвент)
    }

    private void UpdateCurrentCaster()
    {
        if (partyManager != null && partyManager.partyMembers.Count > 0)
        {
            // Ищем первого живого члена партии
            currentCasterStats = partyManager.partyMembers.FirstOrDefault(m => m != null && !m.IsDead);
            if (currentCasterStats != null)
            {
                currentCasterAbilities = currentCasterStats.GetComponent<CharacterAbilities>();
                if (currentCasterAbilities == null)
                {
                    // Debug.LogError($"AbilityCastingSystem: У {currentCasterStats.gameObject.name} отсутствует CharacterAbilities!", this);
                }
            }
            else
            {
                currentCasterAbilities = null; // Нет живых кастеров
            }
        }
    }


    // --- Публичные методы для UIPartyMemberTargetDetector ---
    public void SetHoveredPartyMember(CharacterStats memberStats)
    {
        hoveredPartyMemberTarget = memberStats;
    }

    public void ClearHoveredPartyMemberIfCurrent(CharacterStats memberStats)
    {
        if (hoveredPartyMemberTarget == memberStats)
        {
            hoveredPartyMemberTarget = null;
        }
    }

    // --- Методы для обработки ввода способностей (от PlayerInput компонента) ---
    // Эти методы будут вызываться из компонента PlayerInput на объекте игрока,
    // когда соответствующие действия (Ability1, Ability2, ..., Ability0) срабатывают.

    public void OnAbility1(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(0); }
    public void OnAbility2(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(1); }
    public void OnAbility3(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(2); }
    public void OnAbility4(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(3); }
    public void OnAbility5(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(4); }
    public void OnAbility6(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(5); }
    public void OnAbility7(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(6); }
    public void OnAbility8(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(7); }
    public void OnAbility9(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(8); }
    public void OnAbility0(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(9); }


    private void TryCastAbility(int abilityIndex)
    {
        UpdateCurrentCaster();

        if (currentCasterAbilities == null || currentCasterStats == null || currentCasterStats.IsDead)
        {
            feedbackManager?.ShowFeedbackMessage("Cannot use ability: No valid caster.");
            return;
        }

        AbilitySlot abilitySlot = currentCasterAbilities.GetAbilitySlotByIndex(abilityIndex);
        if (abilitySlot == null || abilitySlot.abilityData == null) return;

        AbilityData ability = abilitySlot.abilityData;

        if (!currentCasterAbilities.CanUseAbility(abilitySlot))
        {
            string reason = abilitySlot.currentCharges <= 0 ? "No charges left" : "Ability on cooldown";
            feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: {reason}.");
            return;
        }

        // --- НОВАЯ, УПРОЩЕННАЯ ЛОГИКА ОПРЕДЕЛЕНИЯ ЦЕЛИ ---
        DetermineTargetAndCast(abilitySlot);
    }

private void DetermineTargetAndCast(AbilitySlot abilitySlot)
{
    AbilityData ability = abilitySlot.abilityData;
    Transform primaryTargetTransform = null;
    CharacterStats primaryTargetStats = null;
    Vector3 castPoint = Vector3.zero;

    bool targetProcessSuccess = false;

    switch (ability.targetType)
    {
        case TargetType.Self:
            primaryTargetTransform = currentCasterStats.transform;
            primaryTargetStats = currentCasterStats;
            castPoint = currentCasterStats.transform.position;
            targetProcessSuccess = true;
            break;

        case TargetType.AreaAroundCaster:
            // ВАЖНО: Для AoE вокруг кастера, primaryTarget остается null.
            // Центром будет позиция кастера, но это не "цель" в том же смысле.
            castPoint = currentCasterStats.transform.position;
            targetProcessSuccess = true;
            break;

        // ... (остальные case'ы остаются без изменений) ...
        case TargetType.Point_GroundTargeted:
            float groundRange = ability.range > 0 ? ability.range : 30f;
            if (targetingSystem.TryGetGroundPoint(groundRange, playerGlobalActions.interactionLayerMask, out castPoint))
            {
                targetProcessSuccess = true;
            }
            break;

        case TargetType.Single_Creature:
        case TargetType.Single_Interactable:
            if (playerGlobalActions.IsCursorFree && hoveredPartyMemberTarget != null)
            {
                primaryTargetTransform = hoveredPartyMemberTarget.transform;
            }
            else
            {
                float singleTargetRange = ability.range > 0 ? ability.range : playerGlobalActions.actionDistance;
                if (targetingSystem.TryGetTarget(singleTargetRange, GetLayerMaskForAbility(ability), out RaycastHit hit))
                {
                    primaryTargetTransform = hit.transform;
                }
            }

            if (primaryTargetTransform != null)
            {
                primaryTargetStats = primaryTargetTransform.GetComponent<CharacterStats>();
                castPoint = primaryTargetTransform.position;
                if (ValidateSingleTarget(ability, primaryTargetTransform, primaryTargetStats))
                {
                    targetProcessSuccess = true;
                }
            }
            break;
    }

    if (targetProcessSuccess)
    {
        currentCasterAbilities.TryUseAbility(
            abilitySlot, currentCasterStats, feedbackManager, 
            primaryTargetTransform, castPoint, ability
        );
    }
    else
    {
        feedbackManager?.ShowFeedbackMessage(FeedbackGenerator.TargetNotFound(ability.abilityName));
    }
}
    // Вспомогательный метод для валидации цели (пока оставляем здесь)
    private bool ValidateSingleTarget(AbilityData ability, Transform targetTransform, CharacterStats targetStats)
    {
        // НОВАЯ ПРОВЕРКА: ищем эффект взаимодействия
        bool isInteractionAbility = ability.effectsToApply.Any(effect => effect is InteractEffectData);

        if (isInteractionAbility)
        {
            Interactable interactableComponent = targetTransform.GetComponent<Interactable>();
            if (interactableComponent == null) return false;
            
            // Телекинез/взаимодействие не должно работать на живых существ, которые не являются трупами
            if (targetStats != null && !targetStats.IsDead && targetTransform.GetComponent<LootableCorpse>() == null)
            {
                return false;
            }
            return true;
        }

        // Обычная проверка для Single_Creature
        if (ability.targetType == TargetType.Single_Creature && targetStats == null)
        {
            feedbackManager?.ShowFeedbackMessage(FeedbackGenerator.InvalidTarget(ability.abilityName));
            return false;
        }
        
        return true;
    }



    // Этот метод теперь тоже можно упростить или убрать, если маски будут в TargetingSystem
    private LayerMask GetLayerMaskForAbility(AbilityData ability)
    {
        if (ability.targetType == TargetType.Single_Interactable)
        {
            if (ability.abilityName == "Telekinesis")
                return LayerMask.GetMask("Interactable", "Characters");
            else
                return LayerMask.GetMask("Interactable");
        }
        if (ability.targetType == TargetType.Single_Creature)
        {
            return LayerMask.GetMask("Characters");
        }
        return playerGlobalActions.interactionLayerMask; // Дефолтная маска
    }
    private Ray GetRayFromScreen()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("AbilityCastingSystem: Main Camera not found for Raycast!");
            return new Ray(transform.position, transform.forward); // Фоллбэк, но это плохо
        }
        if (playerGlobalActions != null && playerGlobalActions.IsCursorFree)
        {
            return mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        }
    }
}