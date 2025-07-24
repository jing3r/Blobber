
using System;
using UnityEngine;

[Serializable]
public class InventoryItem
{
        [NonSerialized] private Inventory ownerInventory;
    public ItemData itemData;
    public int quantity;

    // --- НОВЫЕ ПОЛЯ ---
    public int gridPositionX;
    public int gridPositionY;

    // Конструктор теперь тоже должен принимать позицию
    public InventoryItem(ItemData data, int initialQuantity = 1, int x = 0, int y = 0, Inventory owner = null)
    {
        itemData = data;
        quantity = initialQuantity;
        gridPositionX = x;
        gridPositionY = y;
        ownerInventory = owner;
    }

    public void SetOwner(Inventory owner)
    {
        ownerInventory = owner;
    }
    
    public Inventory GetOwnerInventory()
    {
        return ownerInventory;
    }
}