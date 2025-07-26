using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Управляет экипированными предметами персонажа.
/// Взаимодействует с Inventory для перемещения предметов и с CharacterStats для применения модификаторов.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(Inventory))]
public class CharacterEquipment : MonoBehaviour
{
    private readonly Dictionary<EquipmentSlotType, InventoryItem> equippedItems = new Dictionary<EquipmentSlotType, InventoryItem>();
    private Inventory characterInventory;
    /// <summary>
    /// Суммарный вес всех экипированных предметов.
    /// </summary>
    public float CurrentWeight => equippedItems.Values
        .Where(item => item != null)
        .Distinct() // Убираем дубликаты для двуручных предметов
        .Sum(item => item.ItemData.Weight * item.Quantity);
    public event Action<EquipmentSlotType, InventoryItem> OnEquipmentChanged;

    private void Awake()
    {
        characterInventory = GetComponent<Inventory>();
        InitializeSlots();
    }

    /// <summary>
    /// Автоматически экипирует предмет, выбирая слот по умолчанию (например, правую руку для оружия).
    /// </summary>
    public void AutoEquip(InventoryItem itemToEquip, Inventory sourceInventory)
    {
        if (itemToEquip == null || itemToEquip.ItemData.ValidSlots == EquipmentSlotType.None) return;
        
        EquipmentSlotType targetSlot = GetDefaultSlotFor(itemToEquip.ItemData.ValidSlots);
        if (targetSlot == EquipmentSlotType.None) return;

        Equip(itemToEquip, targetSlot, sourceInventory);
    }

    /// <summary>
    /// Пытается экипировать предмет из указанного инвентаря в конкретный слот.
    /// </summary>
    public void Equip(InventoryItem itemToEquip, EquipmentSlotType targetSlot, Inventory sourceInventory)
    {
        if (!CanEquip(itemToEquip, targetSlot, sourceInventory)) return;
        
        // Сначала удаляем исходный предмет, чтобы освободить место для возвращаемых предметов
        sourceInventory.RemoveItem(itemToEquip);
        
        // Снимаем предметы, которые мешают, и возвращаем их в инвентарь персонажа
        UnequipBlockingItems(targetSlot, itemToEquip.ItemData);

        // Помещаем новый предмет в слот(ы)
        PlaceItemInSlot(itemToEquip, targetSlot);
    }

    /// <summary>
    /// Снимает предмет из указанного слота и пытается поместить его в инвентарь по указанным координатам.
    /// Если координаты невалидны, ищет первое свободное место.
    /// </summary>
    public void Unequip(EquipmentSlotType slotToUnequip, int targetX = -1, int targetY = -1)
    {
        if (equippedItems.TryGetValue(slotToUnequip, out InventoryItem itemToUnequip) && itemToUnequip != null)
        {
            // Сначала удаляет предмет из слотов экипировки
            RemoveItemFromSlot(itemToUnequip);

            // Пытается поместить его в указанную ячейку
            bool placedSuccessfully = false;
            if (targetX != -1 && targetY != -1)
            {
                placedSuccessfully = characterInventory.AddItemAt(itemToUnequip, targetX, targetY);
            }
            
            // Если не удалось (координаты не заданы или заняты), ищет первое свободное место
            if (!placedSuccessfully)
            {
                if (!characterInventory.AddItem(itemToUnequip.ItemData, itemToUnequip.Quantity))
                {
                    // Если места нет нигде, возвращает предмет в слот экипировки.
                    PlaceItemInSlot(itemToUnequip, slotToUnequip);
                    FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space in inventory to unequip.");
                }
            }
        }
    }

    /// <summary>
    /// Возвращает предмет, экипированный в указанном слоте.
    /// </summary>
    public InventoryItem GetItemInSlot(EquipmentSlotType slot)
    {
        equippedItems.TryGetValue(slot, out InventoryItem item);
        return item;
    }
    
    #region Private Logic
    private void InitializeSlots()
    {
        foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            if (slotType != EquipmentSlotType.None)
            {
                equippedItems[slotType] = null;
            }
        }
    }

    private bool CanEquip(InventoryItem item, EquipmentSlotType targetSlot, Inventory sourceInventory)
    {
        if (item == null || item.ItemData.ValidSlots == EquipmentSlotType.None || sourceInventory == null) return false;

        if (!item.ItemData.ValidSlots.HasFlag(targetSlot))
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Cannot equip this item in that slot.");
            return false;
        }
        List<InventoryItem> itemsToUnequip = GetBlockingItems(targetSlot, item.ItemData);
        Inventory targetInventory = (sourceInventory == characterInventory) ? characterInventory : sourceInventory;
        
        if (!CanInventoryAcceptSwap(targetInventory, itemsToUnequip, item))
        {
             FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space to swap items.");
             return false;
        }

        return true;
    }

    private void UnequipBlockingItems(EquipmentSlotType targetSlot, ItemData newItemData)
    {
        var itemsToReturn = GetBlockingItems(targetSlot, newItemData);
        foreach (var item in itemsToReturn)
        {
            // TODO: Убрать модификаторы статов от снимаемого предмета
            characterInventory.AddItem(item.ItemData, item.Quantity);
        }
    }

    private void PlaceItemInSlot(InventoryItem item, EquipmentSlotType slot)
    {
        bool isTwoHanded = item.ItemData.ValidSlots.HasFlag(EquipmentSlotType.Hands);
        if (isTwoHanded)
        {
            equippedItems[EquipmentSlotType.LeftHand] = item;
            equippedItems[EquipmentSlotType.RightHand] = item;
            OnEquipmentChanged?.Invoke(EquipmentSlotType.LeftHand, item);
            OnEquipmentChanged?.Invoke(EquipmentSlotType.RightHand, item);
        }
        else
        {
            equippedItems[slot] = item;
            OnEquipmentChanged?.Invoke(slot, item);
        }
        // TODO: Применить модификаторы статов от нового предмета
    }

    private void RemoveItemFromSlot(InventoryItem item)
    {
        bool isTwoHanded = item.ItemData.ValidSlots.HasFlag(EquipmentSlotType.Hands);
        if (isTwoHanded)
        {
            equippedItems[EquipmentSlotType.LeftHand] = null;
            equippedItems[EquipmentSlotType.RightHand] = null;
            OnEquipmentChanged?.Invoke(EquipmentSlotType.LeftHand, null);
            OnEquipmentChanged?.Invoke(EquipmentSlotType.RightHand, null);
        }
        else
        {
            var slot = GetSlotOfItem(item);
            if(slot.HasValue) 
            {
                equippedItems[slot.Value] = null;
                OnEquipmentChanged?.Invoke(slot.Value, null);
            }
        }
        // TODO: Убрать модификаторы статов от снятого предмета
    }

    private List<InventoryItem> GetBlockingItems(EquipmentSlotType targetSlot, ItemData newItemData)
    {
        var blockingItems = new List<InventoryItem>();
        bool isNewItemTwoHanded = newItemData.ValidSlots.HasFlag(EquipmentSlotType.Hands);

        if (isNewItemTwoHanded)
        {
            if (GetItemInSlot(EquipmentSlotType.LeftHand) != null) blockingItems.Add(GetItemInSlot(EquipmentSlotType.LeftHand));
            if (GetItemInSlot(EquipmentSlotType.RightHand) != null) blockingItems.Add(GetItemInSlot(EquipmentSlotType.RightHand));
        }
        else
        {
            var currentItem = GetItemInSlot(targetSlot);
            if (currentItem != null)
            {
                blockingItems.Add(currentItem);
                // Если текущий предмет двуручный, а новый - нет, нужно освободить и вторую руку
                if(currentItem.ItemData.ValidSlots.HasFlag(EquipmentSlotType.Hands))
                {
                    var otherHand = targetSlot == EquipmentSlotType.LeftHand ? EquipmentSlotType.RightHand : EquipmentSlotType.LeftHand;
                    if(GetItemInSlot(otherHand) != null) blockingItems.Add(GetItemInSlot(otherHand));
                }
            }
        }
        return blockingItems.Distinct().ToList();
    }
    
    private bool CanInventoryAcceptSwap(Inventory inventory, List<InventoryItem> itemsToAdd, InventoryItem itemToRemove)
    {
        int requiredSlots = itemsToAdd.Count;
        int currentItemCount = inventory.Items.Count;
        
        if (inventory.Items.Contains(itemToRemove))
        {
            currentItemCount--;
        }

        int totalGridCells = inventory.GridWidth * inventory.GridHeight;
        int occupiedCells = inventory.Items.Sum(item => item.ItemData.GridWidth * item.ItemData.GridHeight);
        if (inventory.Items.Contains(itemToRemove))
        {
            occupiedCells -= itemToRemove.ItemData.GridWidth * itemToRemove.ItemData.GridHeight;
        }

        // TODO: эта проверка не учитывает фрагментацию пространства, а только количество ячеек. Доработать.
        int requiredCells = itemsToAdd.Sum(item => item.ItemData.GridWidth * item.ItemData.GridHeight);
        return (totalGridCells - occupiedCells) >= requiredCells;
    }
    
    private EquipmentSlotType GetDefaultSlotFor(EquipmentSlotType ValidSlots)
    {
        if (ValidSlots.HasFlag(EquipmentSlotType.RightHand)) return EquipmentSlotType.RightHand;
        if (ValidSlots.HasFlag(EquipmentSlotType.LeftHand)) return EquipmentSlotType.LeftHand;
        if (ValidSlots.HasFlag(EquipmentSlotType.Armor)) return EquipmentSlotType.Armor;
        if (ValidSlots.HasFlag(EquipmentSlotType.Head)) return EquipmentSlotType.Head;
        if (ValidSlots.HasFlag(EquipmentSlotType.Chest)) return EquipmentSlotType.Chest;
        if (ValidSlots.HasFlag(EquipmentSlotType.Legs)) return EquipmentSlotType.Legs;
        if (ValidSlots.HasFlag(EquipmentSlotType.Feet)) return EquipmentSlotType.Feet;

        return EquipmentSlotType.None;
    }

    private EquipmentSlotType? GetSlotOfItem(InventoryItem item)
    {
        foreach(var pair in equippedItems)
        {
            if (pair.Value == item) return pair.Key;
        }
        return null;
    }
    #endregion
}