using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Управляет одним слотом экипировки в UI.
/// </summary>
public class EquipmentSlotUI : MonoBehaviour, IDropHandler
{
    [SerializeField] private EquipmentSlotType slotType;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image placeholderIcon;
    [SerializeField] private GameObject inventoryItemPrefab; 

    private CharacterEquipment characterEquipment;
    private InventoryItemUI currentItemUI;

    /// <summary>
    /// Инициализирует слот, связывая его с логикой экипировки персонажа.
    /// </summary>
    public void Initialize(CharacterEquipment equipment)
    {
        characterEquipment = equipment;
        characterEquipment.OnEquipmentChanged += HandleEquipmentChange;
        UpdateSlotVisuals(characterEquipment.GetItemInSlot(slotType));
    }

    private void OnDestroy()
    {
        if (characterEquipment != null)
        {
            characterEquipment.OnEquipmentChanged -= HandleEquipmentChange;
        }
    }

    private void HandleEquipmentChange(EquipmentSlotType changedSlot, InventoryItem newItem)
    {
        bool isTwoHandedSlot = this.slotType.HasFlag(EquipmentSlotType.Hands);
        bool changedSlotIsTwoHanded = changedSlot.HasFlag(EquipmentSlotType.Hands);

        if (changedSlot == this.slotType || (isTwoHandedSlot && changedSlotIsTwoHanded))
        {
            UpdateSlotVisuals(characterEquipment.GetItemInSlot(this.slotType));
        }
    }

    private void UpdateSlotVisuals(InventoryItem itemInSlot)
    {
        if (currentItemUI != null)
        {
            Destroy(currentItemUI.gameObject);
            currentItemUI = null;
        }

        if (itemInSlot != null)
        {
            var itemUIGO = Instantiate(inventoryItemPrefab, transform);
            currentItemUI = itemUIGO.GetComponent<InventoryItemUI>();
            
            currentItemUI.InitializeAsEquipped(itemInSlot);

            placeholderIcon.gameObject.SetActive(false);
        }
        else
        {
            placeholderIcon.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Вызывается, когда предмет из инвентаря перетаскивают на этот слот.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (characterEquipment == null) return;

        if (eventData.pointerDrag.TryGetComponent<InventoryItemUI>(out var itemUI))
        {
            var itemToEquip = itemUI.GetLinkedItem();
            var sourceInventory = itemToEquip.GetOwnerInventory();
            characterEquipment.Equip(itemToEquip, this.slotType, sourceInventory);
        }
    }
}