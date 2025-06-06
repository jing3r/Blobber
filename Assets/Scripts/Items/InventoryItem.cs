using System; // Для Serializable
using UnityEngine; // Для ItemData

[Serializable]
public class InventoryItem
{
    public ItemData itemData;
    public int quantity;

    public InventoryItem(ItemData data, int initialQuantity = 1)
    {
        if (data == null)
        {
            Debug.LogError("InventoryItem: Попытка создать слот с null ItemData!");
            // Можно либо бросить исключение, либо установить itemData в null и обработать позже
            // Для простоты оставляем как есть, но проверки на itemData != null в Inventory важны.
        }
        itemData = data;
        quantity = Mathf.Max(1, initialQuantity); // Гарантируем, что количество хотя бы 1, если предмет создается
                                                 // Хотя при добавлении в инвентарь может быть 0, если ничего не влезло.
                                                 // Лучше оставить initialQuantity как есть, а проверки делать в AddItem.
        this.quantity = initialQuantity;

    }
}