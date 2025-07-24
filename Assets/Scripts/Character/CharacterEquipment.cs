using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(CharacterStats))] 
[RequireComponent(typeof(Inventory))]
public class CharacterEquipment : MonoBehaviour
{
    // Словарь для хранения экипированных предметов
    private Dictionary<EquipmentSlotType, InventoryItem> equippedItems = new Dictionary<EquipmentSlotType, InventoryItem>();
    
    private Inventory characterInventory;

    // Событие для обновления UI
    public event Action<EquipmentSlotType, InventoryItem> OnEquipmentChanged;

    void Awake()
    {
        characterInventory = GetComponent<Inventory>();
        // Инициализируем словарь пустыми значениями
        foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            if (slot != EquipmentSlotType.None)
            {
                equippedItems.Add(slot, null);
            }
        }
    }
/// <summary>
    /// Определяет слот по умолчанию для предмета на основе его валидных слотов.
    /// </summary>
    private EquipmentSlotType GetDefaultSlot(EquipmentSlotType validSlots)
    {
        // Приоритеты для автоматической экипировки
        if (validSlots.HasFlag(EquipmentSlotType.RightHand)) return EquipmentSlotType.RightHand;
        if (validSlots.HasFlag(EquipmentSlotType.LeftHand)) return EquipmentSlotType.LeftHand;
        if (validSlots.HasFlag(EquipmentSlotType.Armor)) return EquipmentSlotType.Armor;
        if (validSlots.HasFlag(EquipmentSlotType.Head)) return EquipmentSlotType.Head;
        if (validSlots.HasFlag(EquipmentSlotType.Chest)) return EquipmentSlotType.Chest;
        if (validSlots.HasFlag(EquipmentSlotType.Legs)) return EquipmentSlotType.Legs;
        if (validSlots.HasFlag(EquipmentSlotType.Feet)) return EquipmentSlotType.Feet;

        return EquipmentSlotType.None;
    }
    // --- КОНЕЦ ИЗМЕНЕНИЯ ---
    /// <summary>
    /// Автоматически экипирует предмет, выбирая слот по умолчанию.
    /// </summary>
    public void AutoEquip(InventoryItem itemToEquip, Inventory sourceInventory)
    {
        if (itemToEquip == null || itemToEquip.itemData.validSlots == EquipmentSlotType.None) return;
        EquipmentSlotType targetSlot = GetDefaultSlot(itemToEquip.itemData.validSlots);
        if (targetSlot == EquipmentSlotType.None) return;
        Equip(itemToEquip, targetSlot, sourceInventory);
    }

    public void Equip(InventoryItem itemToEquip, EquipmentSlotType targetSlot, Inventory sourceInventory)
    {
        if (itemToEquip == null || itemToEquip.itemData.validSlots == EquipmentSlotType.None || sourceInventory == null) return;

        // 1. Проверяем, валиден ли слот для этого предмета
        if ((itemToEquip.itemData.validSlots & targetSlot) == 0)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Cannot equip this item in that slot.");
            return;
        }

        // 2. Проверяем, является ли предмет двуручным
        bool isTwoHanded = itemToEquip.itemData.validSlots.HasFlag(EquipmentSlotType.Hands);

        // ВРЕМЕННО сохраняем предметы, которые будут сняты
        InventoryItem itemInTargetSlot = GetItemInSlot(targetSlot);
        InventoryItem itemInOtherHand = null;

        if (isTwoHanded)
        {
             var otherHand = targetSlot == EquipmentSlotType.LeftHand ? EquipmentSlotType.RightHand : EquipmentSlotType.LeftHand;
             itemInOtherHand = GetItemInSlot(otherHand);
        } else if (itemInTargetSlot != null && itemInTargetSlot.itemData.validSlots.HasFlag(EquipmentSlotType.Hands)) {
            // Если мы пытаемся надеть одноручное в слот, где уже есть двуручное
            var otherHand = targetSlot == EquipmentSlotType.LeftHand ? EquipmentSlotType.RightHand : EquipmentSlotType.LeftHand;
            itemInOtherHand = GetItemInSlot(otherHand);
        }

        // 3. ПРЕДВАРИТЕЛЬНАЯ ПРОВЕРКА МЕСТА В ИНВЕНТАРЕ
        // Прежде чем что-либо делать, симулируем обмен и проверяем, хватит ли места в инвентаре источника.
        if (sourceInventory != this.characterInventory)
        {
            // Если мы тащим из сундука в экипировку, то снятые вещи пойдут в инвентарь персонажа.
            if (!CanInventoryAcceptItems(this.characterInventory, itemInTargetSlot, itemInOtherHand))
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space to swap items.");
                return;
            }
        }
        else // Если мы тащим из своего инвентаря в свою экипировку
        {
            // То снятые вещи вернутся в тот же инвентарь.
            if (!CanInventoryAcceptItems(this.characterInventory, itemInTargetSlot, itemInOtherHand, itemToEquip))
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space to swap items.");
                return;
            }
        }
        
        // 4. ПРОВОДИМ ОПЕРАЦИЮ (теперь она безопасна)
        // Сначала удаляем предмет из источника
        sourceInventory.RemoveItem(itemToEquip);

        // Снимаем старые предметы и возвращаем их в инвентарь персонажа
        if(itemInTargetSlot != null) UnequipAndReturnToInventory(itemInTargetSlot);
        if(itemInOtherHand != null && itemInOtherHand != itemInTargetSlot) UnequipAndReturnToInventory(itemInOtherHand);
        
        // Экипируем новый предмет
        PlaceItemInSlot(itemToEquip, targetSlot, true);
    }
    
    /// <summary>
    /// Проверяет, может ли инвентарь принять указанные предметы. Игнорирует предмет, который будет удален.
    /// </summary>
    private bool CanInventoryAcceptItems(Inventory inventory, InventoryItem item1, InventoryItem item2, InventoryItem itemToIgnore = null)
    {
        // Это упрощенная симуляция. Для 100% точности нужен более сложный алгоритм,
        // но для большинства случаев этого хватит.
        // Мы просто проверяем, что в инвентаре достаточно свободных ячеек.
        int requiredSlots = 0;
        if (item1 != null) requiredSlots++;
        if (item2 != null && item2 != item1) requiredSlots++;

        int currentItemCount = inventory.items.Count;
        if (itemToIgnore != null) currentItemCount--;

        int freeSlots = (inventory.gridWidth * inventory.gridHeight) - currentItemCount;
        
        return freeSlots >= requiredSlots;
    }
    
    /// <summary>
    /// Снимает предмет со слота и помещает его в инвентарь персонажа.
    /// Это "внутренний" метод, который не делает проверок.
    /// </summary>
    private void UnequipAndReturnToInventory(InventoryItem item)
    {
        if (item == null) return;
        
        bool isTwoHanded = item.itemData.validSlots.HasFlag(EquipmentSlotType.Hands);

        if (isTwoHanded)
        {
            equippedItems[EquipmentSlotType.LeftHand] = null;
            equippedItems[EquipmentSlotType.RightHand] = null;
        }
        else
        {
            // Находим, в каком слоте лежит предмет
            EquipmentSlotType? slot = GetSlotOfItem(item);
            if(slot.HasValue) equippedItems[slot.Value] = null;
        }

        this.characterInventory.AddItem(item.itemData, item.quantity);
        // TODO: Убрать модификаторы статов
    }

    /// <summary>
    /// Вспомогательный метод для поиска слота по предмету
    /// </summary>
    private EquipmentSlotType? GetSlotOfItem(InventoryItem item)
    {
        foreach(var pair in equippedItems)
        {
            if (pair.Value == item) return pair.Key;
        }
        return null;
    }

    /// <summary>
    /// Вспомогательный метод для фактического размещения предмета в слоте и обновления UI.
    /// </summary>
    private void PlaceItemInSlot(InventoryItem item, EquipmentSlotType slot, bool updateUI)
    {
        equippedItems[slot] = item;
        
        // TODO: Применить модификаторы статов
        
        if (updateUI)
        {
            OnEquipmentChanged?.Invoke(slot, item);
            if(item.itemData.validSlots.HasFlag(EquipmentSlotType.Hands))
            {
                // Если предмет двуручный, нужно обновить и второй слот в UI
                var otherHand = slot == EquipmentSlotType.LeftHand ? EquipmentSlotType.RightHand : EquipmentSlotType.LeftHand;
                OnEquipmentChanged?.Invoke(otherHand, item);
            }
        }
    }

    /// <summary>
    /// Снимает предмет из указанного слота и возвращает его в инвентарь.
    /// </summary>
    public void Unequip(EquipmentSlotType slotToUnequip)
    {
        if (equippedItems.TryGetValue(slotToUnequip, out InventoryItem itemToUnequip) && itemToUnequip != null)
        {
            // Если предмет двуручный, нужно освободить оба слота
            bool isTwoHanded = itemToUnequip.itemData.validSlots.HasFlag(EquipmentSlotType.Hands);

            // Пытаемся вернуть предмет в инвентарь (это происходит только один раз)
            if (characterInventory.AddItem(itemToUnequip.itemData, itemToUnequip.quantity))
            {
                if (isTwoHanded)
                {
                    equippedItems[EquipmentSlotType.LeftHand] = null;
                    equippedItems[EquipmentSlotType.RightHand] = null;
                    
                    // TODO: Убрать модификаторы статов
                    
                    OnEquipmentChanged?.Invoke(EquipmentSlotType.LeftHand, null);
                    OnEquipmentChanged?.Invoke(EquipmentSlotType.RightHand, null);
                }
                else
                {
                    equippedItems[slotToUnequip] = null;
                    
                    // TODO: Убрать модификаторы статов
                    
                    OnEquipmentChanged?.Invoke(slotToUnequip, null);
                }
            }
            else
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space in inventory to unequip.");
            }
        }
    }
    
    /// <summary>
    /// Возвращает предмет, экипированный в указанном слоте.
    /// </summary>
    public InventoryItem GetItemInSlot(EquipmentSlotType slot)
    {
        if (equippedItems.TryGetValue(slot, out InventoryItem item))
        {
            return item;
        }
        return null;
    }
}