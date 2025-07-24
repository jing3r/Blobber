using UnityEngine;
using UnityEngine.EventSystems;

public class UIPartyMemberTargetDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CharacterStats associatedMemberStats;
    private InputManager inputManager; // Ссылка на новый менеджер

    void Start()
    {
        // Находим InputManager один раз
        inputManager = FindObjectOfType<InputManager>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inputManager != null)
        {
            // Сообщаем InputManager, что мы навели курсор на этого члена партии
            inputManager.SetHoveredPartyMember(associatedMemberStats);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inputManager != null)
        {
            // Сообщаем, что курсор ушел с этого члена партии
            inputManager.ClearHoveredPartyMember(associatedMemberStats);
        }
    }
}