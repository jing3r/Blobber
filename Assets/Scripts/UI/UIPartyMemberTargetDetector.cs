using UnityEngine;
using UnityEngine.EventSystems;

public class UIPartyMemberTargetDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CharacterStats associatedMemberStats; // Устанавливается из PartyUIManager

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AbilityCastingSystem.Instance != null) // Проверяем, что Instance существует
        {
            AbilityCastingSystem.Instance.SetHoveredPartyMember(associatedMemberStats);
        }
        // Debug.Log("Hovering over: " + (associatedMemberStats != null ? associatedMemberStats.gameObject.name : "null"));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (AbilityCastingSystem.Instance != null) // Проверяем, что Instance существует
        {
            AbilityCastingSystem.Instance.ClearHoveredPartyMemberIfCurrent(associatedMemberStats);
        }
        // Debug.Log("Exited: " + (associatedMemberStats != null ? associatedMemberStats.gameObject.name : "null"));
    }
}