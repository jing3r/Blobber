using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Обнаруживает наведение курсора на UI-элемент члена партии
/// и сообщает об этом в InputManager для таргетинга способностей.
/// </summary>
public class UIPartyMemberTargetDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CharacterStats associatedMemberStats;
    private InputManager inputManager;

    private void Start()
    {
        inputManager = FindObjectOfType<InputManager>();
        if (associatedMemberStats == null)
        {
            // Попытка найти статы на родительском объекте, если не задано вручную
            associatedMemberStats = GetComponentInParent<PartyMemberUI>()?.GetLinkedStats();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inputManager?.SetHoveredPartyMember(associatedMemberStats);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inputManager?.ClearHoveredPartyMember(associatedMemberStats);
    }
}