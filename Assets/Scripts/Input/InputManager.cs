using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Linq;

/// <summary>
/// Центральный обработчик ввода игрока.
/// Получает события от PlayerInput и делегирует их соответствующим системам (PartyManager, TargetingSystem и т.д.).
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("Настройки действий")]
    [SerializeField] private float actionDistance = 4f;
    [SerializeField] private LayerMask creatureLayerMask;
    [SerializeField] private LayerMask interactableLayerMask;
    [SerializeField] private LayerMask groundLayerMask;

    private PartyManager partyManager;
    private TargetingSystem targetingSystem;
    private FeedbackManager feedbackManager;
    private PlayerGlobalActions playerGlobalActions;
    private InventoryUIManager inventoryUIManager;

    private Dictionary<string, int> abilityKeymap;
    private CharacterStats hoveredPartyMemberTarget;

    #region Unity Lifecycle & Initialization
    private void Awake()
    {
        partyManager = GetComponentInParent<PartyManager>();
        targetingSystem = GetComponentInParent<TargetingSystem>();
        playerGlobalActions = GetComponentInParent<PlayerGlobalActions>();
        inventoryUIManager = FindObjectOfType<InventoryUIManager>();
        feedbackManager = FindObjectOfType<FeedbackManager>();

        InitializeAbilityKeymap();
    }

    private void InitializeAbilityKeymap()
    {
        abilityKeymap = new Dictionary<string, int>();
        for (int i = 1; i <= 12; i++)
        {
            abilityKeymap.Add($"Ability{i}", i - 1);
        }
    }
    #endregion

    #region Public Setters (Called by UI events)
    public void SetHoveredPartyMember(CharacterStats memberStats) => hoveredPartyMemberTarget = memberStats;
    public void ClearHoveredPartyMember(CharacterStats memberStats)
    {
        if (hoveredPartyMemberTarget == memberStats)
        {
            hoveredPartyMemberTarget = null;
        }
    }
    #endregion

    #region Character Selection Handlers
    public void OnSelectCharacter1(InputAction.CallbackContext context) => HandleCharacterSelection(context, 0);
    public void OnSelectCharacter2(InputAction.CallbackContext context) => HandleCharacterSelection(context, 1);
    public void OnSelectCharacter3(InputAction.CallbackContext context) => HandleCharacterSelection(context, 2);
    public void OnSelectCharacter4(InputAction.CallbackContext context) => HandleCharacterSelection(context, 3);
    public void OnSelectCharacter5(InputAction.CallbackContext context) => HandleCharacterSelection(context, 4);
    public void OnSelectCharacter6(InputAction.CallbackContext context) => HandleCharacterSelection(context, 5);

    private void HandleCharacterSelection(InputAction.CallbackContext context, int index)
    {
        if (!context.performed) return;

        // Повторное нажатие на клавишу активного персонажа открывает/закрывает его инвентарь
        if (partyManager.ActiveMember != null && partyManager.PartyMembers.ToList().IndexOf(partyManager.ActiveMember) == index)
        {
            inventoryUIManager?.TogglePartyMemberInventory(index);
        }
        else
        {
            partyManager.SetActiveMember(index);
        }
    }

    public void OnCycleNextReadyCharacter(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            partyManager.CycleToNextReadyMember();
        }
    }
    #endregion

    #region World Interaction Handlers
    public void OnPrimaryAction(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (IsPointerOverUI()) return;
        LayerMask combinedMask = creatureLayerMask | interactableLayerMask;
        if (targetingSystem.TryGetTarget(actionDistance, combinedMask, out var hit))
        {
            if (hit.collider.TryGetComponent<CharacterStats>(out var targetStats) && !partyManager.PartyMembers.ToList().Contains(targetStats))
            {
                if (!targetStats.IsDead)
                {
                    partyManager.ActiveMember?.GetComponent<CharacterActionController>()?.TryAttack(targetStats);
                    return;
                }
            }
            
            if (hit.collider.TryGetComponent<Interactable>(out var interactable))
            {
                feedbackManager?.ShowFeedbackMessage(interactable.Interact());
                return;
            }
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage("Nothing to do here.");
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (IsPointerOverUI()) return;
        LayerMask combinedMask = interactableLayerMask | creatureLayerMask;
        if (targetingSystem.TryGetTarget(actionDistance, combinedMask, out var hit) && hit.collider.TryGetComponent<Interactable>(out var interactable))
        {
            feedbackManager?.ShowFeedbackMessage(interactable.Interact());
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage("There is nothing to interact with.");
        }
    }

    public void OnForceAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (IsPointerOverUI()) return;
        if (targetingSystem.TryGetTarget(actionDistance, creatureLayerMask, out var hit) && hit.collider.TryGetComponent<CharacterStats>(out var targetStats))
        {
            if (!partyManager.PartyMembers.ToList().Contains(targetStats))
            {
                partyManager.ActiveMember?.GetComponent<CharacterActionController>()?.TryAttack(targetStats);
                return;
            }
        }

        feedbackManager?.ShowFeedbackMessage("No valid target to attack.");
    }
    #endregion

    #region Ability Casting Handler
    public void OnUseAbility(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var activeController = partyManager.ActiveMember?.GetComponent<CharacterActionController>();
        if (activeController == null || activeController.CurrentState != CharacterActionController.ActionState.Ready) return;

        if (!abilityKeymap.TryGetValue(context.action.name, out int abilityIndex)) return;

        var abilitySlot = activeController.GetComponent<CharacterAbilities>()?.GetAbilitySlotByIndex(abilityIndex);
        if (abilitySlot == null) return;

        ResolveAbilityTargetAndCast(activeController, abilityIndex, abilitySlot.AbilityData);
    }

    private void ResolveAbilityTargetAndCast(CharacterActionController caster, int index, AbilityData ability)
    {
        float range = ability.Range > 0 ? ability.Range : actionDistance;
        CharacterStats targetCreature = null;
        Transform targetInteractable = null;
        Vector3 targetPoint = Vector3.zero;

        // 1. Проверяем цель по наведению курсора на UI партии
        if (playerGlobalActions.IsCursorFree && hoveredPartyMemberTarget != null)
        {
            targetCreature = hoveredPartyMemberTarget;
        }
        else // 2. Если не на UI, ищем цель в мире
        {
            switch (ability.TargetType)
            {
                case TargetType.Single_Creature:
                    if (targetingSystem.TryGetTarget(range, creatureLayerMask, out var hit))
                        targetCreature = hit.collider.GetComponent<CharacterStats>();
                    break;
                case TargetType.Single_Interactable:
                    if (targetingSystem.TryGetTarget(range, interactableLayerMask | creatureLayerMask, out hit))
                        targetInteractable = hit.transform;
                    break;
                case TargetType.Point_GroundTargeted:
                    targetingSystem.TryGetGroundPoint(range, groundLayerMask, out targetPoint);
                    break;
            }
        }

        // 3. Выполняем действие
        if (IsTargetValidForAbility(ability.TargetType, targetCreature, targetInteractable, targetPoint))
        {
            caster.TryUseAbility(index, targetCreature, targetInteractable, targetPoint);
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage($"{ability.AbilityName}: No valid target found.");
        }
    }

    private bool IsTargetValidForAbility(TargetType type, CharacterStats creature, Transform interactable, Vector3 point)
    {
        switch (type)
        {
            case TargetType.Self:
            case TargetType.AreaAroundCaster:
                return true;
            case TargetType.Single_Creature:
                return creature != null;
            case TargetType.Single_Interactable:
                return interactable != null && interactable.GetComponent<Interactable>() != null;
            case TargetType.Point_GroundTargeted:
                return point != Vector3.zero; // TryGetGroundPoint вернет zero в случае неудачи
            default:
                return false;
        }
    }
    #endregion

    #region UI Action Handlers
    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (!context.performed || partyManager.ActiveMember == null) return;

        int activeIndex = partyManager.PartyMembers.ToList().IndexOf(partyManager.ActiveMember);
        if (activeIndex != -1)
        {
            inventoryUIManager?.TogglePartyMemberInventory(activeIndex);
        }
    }

    public void OnToggleAllPartyInventories(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            inventoryUIManager?.ToggleAllPartyWindows();
        }
    }

    public void OnTakeAll(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            inventoryUIManager?.TakeAllFromOpenContainer();
        }
    }

    public void OnArrangeInventory(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (InventoryUIManager.GridUnderMouse != null)
        {
            InventoryUIManager.GridUnderMouse.GetLinkedInventory()?.ToggleArrange();
        }
        else
        {
            feedbackManager?.ShowFeedbackMessage("Hover over an inventory to arrange.");
        }
    }

    public void OnCancel(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (inventoryUIManager != null && inventoryUIManager.AreAnyWindowsOpen())
        {
            inventoryUIManager.CloseAllWindows();
        }
        else
        {
            // TODO: Открыть главное меню/меню паузы
        }
    }
    
        private bool IsPointerOverUI()
    {
        var eventData = new PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();
        
        var results = new List<RaycastResult>();
        
        EventSystem.current.RaycastAll(eventData, results);
        
        return results.Count > 0;
    }
    #endregion
}