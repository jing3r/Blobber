using UnityEngine;
using System.Linq;

/// <summary>
/// Реализует логику взаимодействия с трупом, открывая его инвентарь.
/// </summary>
[RequireComponent(typeof(Inventory))]
public class LootableCorpse : Interactable
{
    private Inventory corpseInventory;
    private InventoryUIManager inventoryUIManager;

    private void Awake()
    {
        corpseInventory = GetComponent<Inventory>();
        inventoryUIManager = FindObjectOfType<InventoryUIManager>();

        // Устанавливаем базовые размеры для инвентаря трупа, если они не заданы в инспекторе.
        if (corpseInventory.GridWidth <= 0 || corpseInventory.GridHeight <= 0)
        {
            corpseInventory.SetGridSize(5, 4);
        }
    }

    /// <summary>
    /// При взаимодействии открывает UI-окно инвентаря трупа.
    /// </summary>
    public override string Interact()
    {
        // При каждом взаимодействии обновляем подсказку на случай, если труп был обыскан
        UpdateInteractionPrompt();

        if (corpseInventory.Items.Count == 0)
        {
            return "There is nothing left here.";
        }

        if (inventoryUIManager != null)
        {
            inventoryUIManager.ToggleCorpseInventoryWindow(corpseInventory, gameObject.name);
            return null; // Возвращаем null, так как действие - открытие окна, а не текстовый фидбек
        }
        
        Debug.LogError($"[LootableCorpse] InventoryUIManager not found on object '{gameObject.name}'.");
        return "Cannot open container.";
    }

    private void UpdateInteractionPrompt()
    {
        // Используем рефлексию для доступа к приватному полю 'interactionPrompt' базового класса Interactable.
        // Это позволяет изменять текст подсказки, не нарушая инкапсуляцию базового класса.
        var promptField = this.GetType().BaseType.GetField("interactionPrompt", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (promptField != null)
        {
             promptField.SetValue(this, corpseInventory.Items.Any() ? $"Search {gameObject.name}" : "Searched");
        }
    }
}