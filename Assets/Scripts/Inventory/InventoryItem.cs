using System;

/// <summary>
/// Представляет экземпляр предмета в инвентаре.
/// Содержит ссылку на ItemData и изменяемые данные (количество, позиция).
/// </summary>
[Serializable]
public class InventoryItem
{
    public ItemData ItemData;
    public int Quantity;
    public int GridPositionX;
    public int GridPositionY;

    // Ссылка на инвентарь-владелец. Не сериализуется, устанавливается в рантайме.
    [NonSerialized] private Inventory ownerInventory;

    public InventoryItem(ItemData data, int quantity, int x, int y, Inventory owner)
    {
        ItemData = data;
        Quantity = quantity;
        GridPositionX = x;
        GridPositionY = y;
        ownerInventory = owner;
    }

    public Inventory GetOwnerInventory() => ownerInventory;
    public void SetOwner(Inventory owner) => ownerInventory = owner;
}