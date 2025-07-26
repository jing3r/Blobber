using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Управляет созданием и обновлением UI-элементов для всей партии.
/// </summary>
public class PartyUIManager : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private GameObject partyMemberSlotPrefab;
    [SerializeField] private Transform partyMemberInfoContainer;
    
    [Header("Настройки")]
    [SerializeField] private Color activeMemberHighlightColor = Color.yellow;
    
    private PartyManager partyManager;
    private List<PartyMemberUI> memberUISlots = new List<PartyMemberUI>();

    private void Start()
    {
        partyManager = FindObjectOfType<PartyManager>();
        if (partyManager == null)
        {
            Debug.LogError($"[{nameof(PartyUIManager)}] PartyManager not found in scene. Disabling component.", this);
            enabled = false;
            return;
        }

        CreatePartyMemberUI();
        SubscribeToEvents();
        UpdateActiveMemberHighlight();
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    /// <summary>
    /// Полностью пересоздает UI для всех членов партии.
    /// Вызывается при загрузке или изменении состава партии.
    /// </summary>
    public void RefreshAllPartyMemberUIs()
    {
        CreatePartyMemberUI();
        UpdateActiveMemberHighlight();
    }
    
    private void SubscribeToEvents()
    {
        if (partyManager != null)
        {
            partyManager.OnActiveMemberChanged += HandleActiveMemberChange;
            // PartyManager.OnPartyCompositionChanged += RefreshAllPartyMemberUIs; // Для будущего использования
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (partyManager != null)
        {
            partyManager.OnActiveMemberChanged -= HandleActiveMemberChange;
            // PartyManager.OnPartyCompositionChanged -= RefreshAllPartyMemberUIs;
        }
    }
    
    private void CreatePartyMemberUI()
    {
        foreach (Transform child in partyMemberInfoContainer)
        {
            Destroy(child.gameObject);
        }
        memberUISlots.Clear();

        if (partyManager == null) return;

        foreach (var memberStats in partyManager.PartyMembers)
        {
            if (memberStats == null) continue; 

            var slotInstance = Instantiate(partyMemberSlotPrefab, partyMemberInfoContainer);
            if (slotInstance.TryGetComponent<PartyMemberUI>(out var memberUI))
            {
                memberUI.Setup(memberStats); 
                memberUISlots.Add(memberUI);

                // Добавляем детектор наведения для таргетинга способностей
                var targetDetector = slotInstance.GetComponent<UIPartyMemberTargetDetector>() ?? slotInstance.AddComponent<UIPartyMemberTargetDetector>();
                // targetDetector.Setup(memberStats); // TODO: Реализовать и вызывать метод Setup в UIPartyMemberTargetDetector для более надёжной инициализации
            }
        }
    }

    private void HandleActiveMemberChange(CharacterStats oldMember, CharacterStats newMember)
    {
        UpdateActiveMemberHighlight();
    }

    private void UpdateActiveMemberHighlight()
    {
        var activeMember = partyManager.ActiveMember;
        foreach (var slot in memberUISlots)
        {
            slot.SetHighlight(slot.GetLinkedStats() == activeMember);
        }
    }
}