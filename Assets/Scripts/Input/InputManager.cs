using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private TargetingSystem targetingSystem;
    [SerializeField] private FeedbackManager feedbackManager;
    [SerializeField] private PlayerGlobalActions playerGlobalActions;
    [SerializeField] private InventoryUIManager inventoryUIManager;

    [Header("Настройки")]
    [Tooltip("Общая дистанция для действий, если не указано иное")]
    [SerializeField] private float actionDistance = 4f;
    [SerializeField] private LayerMask creatureLayerMask;
    [SerializeField] private LayerMask interactableLayerMask;
    [SerializeField] private LayerMask groundLayerMask;

    private Dictionary<string, int> abilityKeymap;
    private CharacterStats hoveredPartyMemberTarget;

    void Awake()
    {
        if (inventoryUIManager == null) inventoryUIManager = FindObjectOfType<InventoryUIManager>();
        if (partyManager == null) partyManager = GetComponent<PartyManager>();
        if (targetingSystem == null) targetingSystem = GetComponent<TargetingSystem>();
        if (feedbackManager == null) feedbackManager = FindObjectOfType<FeedbackManager>();
        if (playerGlobalActions == null) playerGlobalActions = GetComponent<PlayerGlobalActions>();

        InitializeAbilityKeymap();
    }

    private void InitializeAbilityKeymap()
    {
        abilityKeymap = new Dictionary<string, int>
        {
            {"Ability1", 0}, {"Ability2", 1}, {"Ability3", 2}, {"Ability4", 3},
            {"Ability5", 4}, {"Ability6", 5}, {"Ability7", 6}, {"Ability8", 7},
            {"Ability9", 8}, {"Ability10", 9}, {"Ability11", 10}, {"Ability12", 11}
        };
    }

    public void SetHoveredPartyMember(CharacterStats memberStats) { hoveredPartyMemberTarget = memberStats; }
    public void ClearHoveredPartyMember(CharacterStats memberStats) { if (hoveredPartyMemberTarget == memberStats) hoveredPartyMemberTarget = null; }

    #region Character Selection & Actions

    // --- Выбор персонажей (1-6) ---
    public void OnSelectCharacter1(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(0); }
    public void OnSelectCharacter2(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(1); }
    public void OnSelectCharacter3(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(2); }
    public void OnSelectCharacter4(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(3); }
    public void OnSelectCharacter5(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(4); }
    public void OnSelectCharacter6(InputAction.CallbackContext context) { if (context.performed) SelectCharacter(5); }
    private void SelectCharacter(int index)
    {
        // Если мы нажимаем на клавишу персонажа, который УЖЕ активен,
        // то мы открываем/закрываем его инвентарь.
        if (partyManager.ActiveMember != null && partyManager.partyMembers.IndexOf(partyManager.ActiveMember) == index)
        {
            // ИСПРАВЛЕННЫЙ ВЫЗОВ
            inventoryUIManager?.TogglePartyMemberInventory(index);
        }
        else // Иначе, мы просто меняем активного персонажа
        {
            partyManager.SetActiveMember(index);
        }
    }

    public void OnCycleNextReadyCharacter(InputAction.CallbackContext context) { if (context.performed) partyManager.CycleToNextReadyMember(); }

    // --- Форсированное ВЗАИМОДЕЙСТВИЕ (клавиша E) ---
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // --- ИЗМЕНЕНИЕ: Используем объединенную маску ---
        LayerMask combinedMask = creatureLayerMask | interactableLayerMask;

        // Используем общую дистанцию, чтобы достать до трупов
        if (targetingSystem.TryGetTarget(actionDistance, combinedMask, out RaycastHit hit))
        {
            var interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Успех! Взаимодействуем.
                feedbackManager?.ShowFeedbackMessage(interactable.Interact());
                return;
            }
        }

        // Если ничего интерактивного не найдено
        feedbackManager?.ShowFeedbackMessage("There is nothing to interact with.");
    }

    // --- Форсированная АТАКА (клавиша F) ---
    public void OnForceAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var activeController = partyManager.ActiveMember?.GetComponent<CharacterActionController>();
        if (activeController == null || activeController.CurrentState != CharacterActionController.ActionState.Ready)
        {
            feedbackManager?.ShowFeedbackMessage("Active character is not ready.");
            return;
        }

        if (targetingSystem.TryGetTarget(actionDistance, creatureLayerMask, out RaycastHit hit))
        {
            var targetStats = hit.collider.GetComponent<CharacterStats>();
            if (targetStats != null && !partyManager.partyMembers.Contains(targetStats))
            {
                activeController.TryAttack(targetStats);
                return;
            }
        }
        feedbackManager?.ShowFeedbackMessage("No valid target to attack.");
    }

    // --- Основное ("умное") ДЕЙСТВИЕ (ЛКМ) ---
    public void OnPrimaryAction(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // --- НОВАЯ, БОЛЕЕ НАДЕЖНАЯ ЛОГИКА ---

        // Сначала делаем один рейкаст, чтобы понять, на что мы вообще смотрим.
        // Используем объединенную маску.
        LayerMask combinedMask = creatureLayerMask | interactableLayerMask;
        if (targetingSystem.TryGetTarget(actionDistance, combinedMask, out RaycastHit hit))
        {
            CharacterStats targetCharacter = hit.collider.GetComponent<CharacterStats>();
            Interactable targetInteractable = hit.collider.GetComponent<Interactable>();

            // Сценарий 1: Цель - это СУЩЕСТВО (живое или мертвое)
            if (targetCharacter != null)
            {
                // Проверяем, не союзник ли это
                if (partyManager.partyMembers.Contains(targetCharacter))
                {
                    feedbackManager?.ShowFeedbackMessage("Cannot target a party member.");
                    return;
                }

                // Если цель ЖИВА, пытаемся атаковать
                if (!targetCharacter.IsDead)
                {
                    var activeController = partyManager.ActiveMember?.GetComponent<CharacterActionController>();
                    if (activeController != null && activeController.CurrentState == CharacterActionController.ActionState.Ready)
                    {
                        activeController.TryAttack(targetCharacter);
                    }
                    else
                    {
                        feedbackManager?.ShowFeedbackMessage("Active character is not ready.");
                    }
                    return; // Действие (или попытка) совершено.
                }
                // Если цель МЕРТВА, то она становится просто интерактивным объектом (трупом)
                // и будет обработана в следующем блоке.
            }

            // Сценарий 2: Цель - это ИНТЕРАКТИВНЫЙ ОБЪЕКТ (дверь, сундук, или труп из сценария 1)
            if (targetInteractable != null)
            {
                feedbackManager?.ShowFeedbackMessage(targetInteractable.Interact());
                return;
            }
        }

        // Если ни на что не попали
        feedbackManager?.ShowFeedbackMessage("Nothing to do here.");
    }

    #endregion

    #region Ability Casting
    public void OnUseAbility(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var activeController = partyManager.ActiveMember?.GetComponent<CharacterActionController>();
        if (activeController == null || activeController.CurrentState != CharacterActionController.ActionState.Ready) return;

        if (!abilityKeymap.TryGetValue(context.action.name, out int abilityIndex)) return;

        var abilitySlot = activeController.GetComponent<CharacterAbilities>()?.GetAbilitySlotByIndex(abilityIndex);
        if (abilitySlot == null || abilitySlot.abilityData == null) return;

        var ability = abilitySlot.abilityData;

        CharacterStats targetCreature = null;
        Transform targetInteractable = null;
        Vector3 targetPoint = Vector3.zero;
        float range = ability.range > 0 ? ability.range : actionDistance;

        bool targetFound = false;

        bool isCursorOnPartyUI = playerGlobalActions?.IsCursorFree ?? false;
        if (isCursorOnPartyUI && hoveredPartyMemberTarget != null && ability.targetType == TargetType.Single_Creature)
        {
            targetCreature = hoveredPartyMemberTarget;
            targetFound = true;
        }

        if (!targetFound)
        {
            switch (ability.targetType)
            {
                case TargetType.Self:
                case TargetType.AreaAroundCaster:
                    targetFound = true;
                    break;

                case TargetType.Single_Creature:
                    if (targetingSystem.TryGetTarget(range, creatureLayerMask, out RaycastHit creatureHit))
                    {
                        targetCreature = creatureHit.collider.GetComponent<CharacterStats>();
                        if (targetCreature != null) targetFound = true;
                    }
                    break;

                // --- КЛЮЧЕВОЕ ИЗМЕНЕНИЕ ЗДЕСЬ ---
                case TargetType.Single_Interactable:
                    // Ищем цель на ОБЪЕДИНЕННОЙ маске слоев
                    LayerMask combinedMask = creatureLayerMask | interactableLayerMask;
                    if (targetingSystem.TryGetTarget(range, combinedMask, out RaycastHit interactableHit))
                    {
                        // А теперь проверяем, есть ли на найденном объекте компонент Interactable
                        if (interactableHit.collider.GetComponent<Interactable>() != null)
                        {
                            // Нашли! Это может быть дверь, сундук или труп.
                            targetInteractable = interactableHit.transform;
                            targetFound = true;
                        }
                    }
                    break;

                case TargetType.Point_GroundTargeted:
                    if (targetingSystem.TryGetGroundPoint(range, groundLayerMask, out targetPoint))
                    {
                        targetFound = true;
                    }
                    break;
            }
        }

        if (targetFound)
        {
            activeController.TryUseAbility(abilityIndex, targetCreature, targetInteractable, targetPoint);
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: No valid target found.");
        }
    }

    #endregion

    #region UI Actions


    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var activeMember = partyManager.ActiveMember;
        if (activeMember != null)
        {
            // ИСПРАВЛЕННЫЙ ВЫЗОВ
            int activeIndex = partyManager.partyMembers.IndexOf(activeMember);
            inventoryUIManager?.TogglePartyMemberInventory(activeIndex);
        }
    }

    public void OnToggleAllPartyInventories(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        inventoryUIManager?.ToggleAllPartyWindows();
    }
    public void OnTakeAll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Этот метод должен вызвать логику "Взять всё" в InventoryUIManager
        inventoryUIManager?.TakeAllFromOpenContainer();
    }
    public void OnArrangeInventory(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Mouse.current.position.ReadValue();

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        InventoryGridUI gridUnderMouse = null;
        foreach (var result in results)
        {
            gridUnderMouse = result.gameObject.GetComponentInParent<InventoryGridUI>();
            if (gridUnderMouse != null) break;
        }

        if (gridUnderMouse != null)
        {
            // --- ИЗМЕНЕНИЕ: Вызываем новый метод-переключатель ---
            gridUnderMouse.GetLinkedInventory()?.ToggleArrange();
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage("Hover over an inventory to arrange it.");
        }
    }

    // Добавляем новый обработчик
public void OnCancel(InputAction.CallbackContext context)
{
    if (!context.performed) return;

    // Спрашиваем у UI менеджера, есть ли открытые окна
    if (inventoryUIManager.AreAnyWindowsOpen())
    {
        // Если есть, закрываем все
        inventoryUIManager.CloseAllWindows();
    }
    else
    {
        // Если окон нет, здесь в будущем будет открываться главное меню
    }
}
    #endregion
}