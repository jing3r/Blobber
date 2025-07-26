using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Управляет визуальным представлением сетки инвентаря.
/// Отвечает за создание ячеек и размещение UI-элементов предметов.
/// </summary>
public class InventoryGridUI : MonoBehaviour
{
    [Header("Префабы и контейнеры")]
    [SerializeField] private GameObject gridSlotPrefab;
    [SerializeField] private GameObject inventoryItemPrefab;
    [SerializeField] private Transform itemsContainer;

    private Inventory linkedInventory;
    private GridLayoutGroup gridLayout;
    private List<InventoryItemUI> itemUIInstances = new List<InventoryItemUI>();
    
    public Inventory GetLinkedInventory() => linkedInventory;
    public GridLayoutGroup GetGridLayoutGroup() => gridLayout;

    /// <summary>
    /// Инициализирует сетку, связывая ее с логикой инвентаря.
    /// </summary>
    public void Initialize(Inventory inventoryToLink)
    {
        linkedInventory = inventoryToLink;
        gridLayout = GetComponent<GridLayoutGroup>();
        
        linkedInventory.OnInventoryChanged += RedrawAllItems;
        
        GenerateGridCells();
        RedrawAllItems();
    }
    /// <summary>
    /// Рассчитывает локальную позицию для UI-элемента предмета внутри контейнера itemsContainer.
    /// </summary>
    public Vector2 GetPositionForItem(InventoryItem item)
    {
        return new Vector2(
            item.GridPositionX * (gridLayout.cellSize.x + gridLayout.spacing.x),
            -item.GridPositionY * (gridLayout.cellSize.y + gridLayout.spacing.y)
        );
    }

    private void OnDestroy()
    {
        if (linkedInventory != null)
        {
            linkedInventory.OnInventoryChanged -= RedrawAllItems;
        }
    }
    
    private void GenerateGridCells()
    {
        foreach (Transform child in transform)
        {
            if(child != itemsContainer) Destroy(child.gameObject);
        }

        if (linkedInventory == null) return;
        
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = linkedInventory.GridWidth;

        int totalCells = linkedInventory.GridWidth * linkedInventory.GridHeight;
        for (int i = 0; i < totalCells; i++)
        {
            var slotGO = Instantiate(gridSlotPrefab, transform);
            slotGO.name = $"Slot_{i % linkedInventory.GridWidth}_{i / linkedInventory.GridWidth}";
            if (slotGO.TryGetComponent<GridSlotUI>(out var slotUI))
            {
                 slotUI.SetCoordinates(i % linkedInventory.GridWidth, i / linkedInventory.GridWidth);
            }
        }
    }

    private void RedrawAllItems()
    {
        foreach (var itemUI in itemUIInstances)
        {
            Destroy(itemUI.gameObject);
        }
        itemUIInstances.Clear();

        if (linkedInventory == null) return;

        foreach (var item in linkedInventory.Items)
        {
            var itemUIGO = Instantiate(inventoryItemPrefab, itemsContainer);
            var itemUIComponent = itemUIGO.GetComponent<InventoryItemUI>();
            
            itemUIComponent.Initialize(item, this);
            itemUIInstances.Add(itemUIComponent);
            
            var rectTransform = itemUIGO.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = GetPositionForItem(item);
        }
    }
}