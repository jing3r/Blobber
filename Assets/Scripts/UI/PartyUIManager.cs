using System.Collections.Generic;
using UnityEngine;

public class PartyUIManager : MonoBehaviour
{
    [Header("Ссылки на префабы и контейнеры")]
    [Tooltip("Префаб UI-слота для отображения информации о члене партии (здоровье, опыт, атрибуты).")]
    public GameObject partyMemberSlotPrefab;
    [Tooltip("Объект-контейнер в UI, куда будут добавляться слоты информации о членах партии.")]
    public Transform partyMemberInfoContainer;

    [Header("Источник данных о партии")]
    public PartyManager partyManager;

    [Header("Настройки подсветки")]
    public Color activeMemberHighlightColor = Color.yellow;
    
    private List<PartyMemberUI> memberInfoUISlots = new List<PartyMemberUI>();

    void Start()
    {
        if (partyManager == null)
        {
            partyManager = FindObjectOfType<PartyManager>();
            if (partyManager == null)
            {
                Debug.LogError("PartyUIManager: PartyManager не найден в сцене! UI не будет инициализирован.", this);
                return;
            }
        }

        ValidatePrerequisites();

        // Подписываемся на событие смены активного персонажа
        partyManager.OnActiveMemberChanged += HandleActiveMemberChange;
        
        CreatePartyInfoSlotsUI();
        
        // Первоначальная подсветка
        HandleActiveMemberChange(null, partyManager.ActiveMember);
    }

    private void ValidatePrerequisites()
    {
        if (partyMemberSlotPrefab == null || partyMemberInfoContainer == null)
        {
            Debug.LogError("PartyUIManager: Не назначены префабы или контейнеры для UI слотов партии!", this);
            enabled = false;
        }
    }

    private void CreatePartyInfoSlotsUI()
    {
        foreach (Transform child in partyMemberInfoContainer)
        {
            Destroy(child.gameObject);
        }
        memberInfoUISlots.Clear();

        if (partyManager == null || partyManager.partyMembers.Count == 0) return;

        foreach (CharacterStats memberStats in partyManager.partyMembers)
        {
            if (memberStats == null) continue; 

            GameObject slotInstance = Instantiate(partyMemberSlotPrefab, partyMemberInfoContainer);
            PartyMemberUI memberUIComponent = slotInstance.GetComponent<PartyMemberUI>();

            if (memberUIComponent != null)
            {
                memberUIComponent.Setup(memberStats); 
                memberInfoUISlots.Add(memberUIComponent);

                UIPartyMemberTargetDetector targetDetector = slotInstance.GetComponent<UIPartyMemberTargetDetector>();
                if (targetDetector == null)
                {
                    targetDetector = slotInstance.AddComponent<UIPartyMemberTargetDetector>();
                }
                targetDetector.associatedMemberStats = memberStats;
            }
            else
            {
                Debug.LogError($"PartyUIManager: Префаб слота {partyMemberSlotPrefab.name} не содержит скрипт PartyMemberUI!", this);
                Destroy(slotInstance); 
            }
        }
    }

    private void HandleActiveMemberChange(CharacterStats oldMember, CharacterStats newMember)
    {
        if (oldMember != null)
        {
            var oldUI = memberInfoUISlots.Find(ui => ui.GetLinkedStats() == oldMember);
            if (oldUI != null)
            {
                oldUI.SetHighlight(false, activeMemberHighlightColor);
            }
        }
        
        if (newMember == null)
        {
            foreach (var slot in memberInfoUISlots)
            {
                slot.SetHighlight(false, activeMemberHighlightColor);
            }
        }
        else
        {
            var newUI = memberInfoUISlots.Find(ui => ui.GetLinkedStats() == newMember);
            if (newUI != null)
            {
                newUI.SetHighlight(true, activeMemberHighlightColor);
            }
        }
    }

    public void RefreshAllPartyMemberUIs()
    {
        // Этот метод теперь должен только пересоздавать слоты информации о партии
        CreatePartyInfoSlotsUI();
    }

    void OnDestroy()
    {
        if (partyManager != null)
        {
            partyManager.OnActiveMemberChanged -= HandleActiveMemberChange;
        }
    }
}