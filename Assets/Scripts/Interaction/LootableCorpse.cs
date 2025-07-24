using UnityEngine;

[RequireComponent(typeof(Inventory))]
public class LootableCorpse : Interactable
{
    private Inventory corpseInventory;
    private InventoryUIManager inventoryUIManager;

    void Awake()
    {
        corpseInventory = GetComponent<Inventory>();
        inventoryUIManager = FindObjectOfType<InventoryUIManager>();
        
        // Устанавливаем базовые размеры для инвентаря трупа, если они не заданы
        if(corpseInventory.gridWidth <= 0) corpseInventory.gridWidth = 5;
        if(corpseInventory.gridHeight <= 0) corpseInventory.gridHeight = 4;
    }

    public override string Interact()
    {
        // Проверяем, пуст ли инвентарь. Если да, то он "залутан".
        if (corpseInventory.items.Count == 0)
        {
            // Можно обновить подсказку здесь на случай, если игрок залутал все, не закрывая окно
            interactionPrompt = "Searched";
            return "There is nothing left here.";
        }

        if (inventoryUIManager != null && corpseInventory != null)
        {
            inventoryUIManager.ToggleCorpseInventoryWindow(corpseInventory, gameObject.name);
            return null;
        }
        return "Cannot open.";
    }
}