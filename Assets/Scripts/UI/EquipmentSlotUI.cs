using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EquipmentSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Настройки Слота")]
    [Tooltip("Тип слота, который представляет этот UI элемент.")]
    public EquipmentSlotType slotType;

    [Header("UI Ссылки")]
    [Tooltip("Изображение для иконки экипированного предмета.")]
    [SerializeField] private Image itemIcon;
    [Tooltip("Изображение-заглушка, когда слот пуст.")]
    [SerializeField] private Image placeholderIcon;

    private CharacterEquipment characterEquipment;

    public void Initialize(CharacterEquipment equipment)
    {
        characterEquipment = equipment;
        // Подписываемся на событие изменения экипировки
        characterEquipment.OnEquipmentChanged += HandleEquipmentChange;
        // Обновляем отображение при инициализации
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
        // Реагируем только на изменения в нашем слоте
        if (changedSlot == this.slotType)
        {
            UpdateSlotVisuals(newItem);
        }
    }

    private void UpdateSlotVisuals(InventoryItem itemInSlot)
    {
        if (itemInSlot != null)
        {
            itemIcon.gameObject.SetActive(true);
            placeholderIcon.gameObject.SetActive(false);
            itemIcon.sprite = itemInSlot.itemData.icon;
        }
        else
        {
            itemIcon.gameObject.SetActive(false);
            placeholderIcon.gameObject.SetActive(true);
            itemIcon.sprite = null;
        }
    }

    // Обработка перетаскивания предмета ИЗ инвентаря НА этот слот
public void OnDrop(PointerEventData eventData)
    {
        var itemUI = eventData.pointerDrag?.GetComponent<InventoryItemUI>();
        if (itemUI != null)
        {
            // --- НАЧАЛО ИЗМЕНЕНИЯ: Отладочная проверка ---
            InventoryItem itemToEquip = itemUI.GetLinkedItem();
            if (itemToEquip == null)
            {
               return;
            }
            // Получаем исходный инвентарь и передаем его
            Inventory sourceInventory = itemToEquip.GetOwnerInventory();
            characterEquipment.Equip(itemToEquip, this.slotType, sourceInventory);
        }
    }

    // Обработка клика ПО слоту (для снятия экипировки)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 1)
        {
            // Простой клик снимает предмет
            characterEquipment.Unequip(this.slotType);
        }
    }
}