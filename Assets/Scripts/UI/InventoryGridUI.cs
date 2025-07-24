using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryGridUI : MonoBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject gridSlotPrefab;
    [SerializeField] private GameObject inventoryItemPrefab;

    [Header("Контейнеры")]
    [SerializeField] private Transform itemsContainer;
    public Transform GetItemsContainer() => itemsContainer; // Геттер для ItemUI

    private Inventory linkedInventory;
    private GridLayoutGroup gridLayout;
    private List<InventoryItemUI> itemUIs = new List<InventoryItemUI>();
    
    public Inventory GetLinkedInventory() => linkedInventory;
    public GridLayoutGroup GetGridLayoutGroup() => gridLayout;

    public void Initialize(Inventory inventoryToLink)
    {
        linkedInventory = inventoryToLink;
        gridLayout = GetComponent<GridLayoutGroup>();
        linkedInventory.OnInventoryChanged += RedrawAll;
        GenerateGridCells();
        RedrawAll();
    }
    public void OnPointerEnterGrid()
    {
        InventoryUIManager.SetGridUnderMouse(this);
    }

    public void OnPointerExitGrid()
    {
        InventoryUIManager.ClearGridUnderMouse();
    }
    private void OnDestroy()
    {
        if (linkedInventory != null) linkedInventory.OnInventoryChanged -= RedrawAll;
    }
    
    private void GenerateGridCells()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        
        if (linkedInventory != null && gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = linkedInventory.gridWidth; 
        }

        int totalCells = linkedInventory.gridWidth * linkedInventory.gridHeight;
        for (int i = 0; i < totalCells; i++)
        {
            int x = i % linkedInventory.gridWidth;
            int y = i / linkedInventory.gridWidth;
            var slotGO = Instantiate(gridSlotPrefab, transform);
            slotGO.GetComponent<GridSlotUI>()?.SetCoordinates(x, y);
        }
    }

    // Переименовываем Redraw в RedrawAll для ясности
    public void RedrawAll()
    {
        foreach (var itemUI in itemUIs) Destroy(itemUI.gameObject);
        itemUIs.Clear();

        foreach (var item in linkedInventory.items)
        {
            var itemUIGO = Instantiate(inventoryItemPrefab, itemsContainer);
            var itemUIComponent = itemUIGO.GetComponent<InventoryItemUI>();
            
            // --- ИЗМЕНЕНИЕ: Передаем ссылку на себя ---
            itemUIComponent.Initialize(item, this);
            
            itemUIs.Add(itemUIComponent);
            
            var rectTransform = itemUIGO.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = GetPositionForItem(item);
        }
    }

    public Vector2 GetPositionForItem(InventoryItem item)
    {
        if (gridLayout == null) return Vector2.zero;
        return new Vector2(
            item.gridPositionX * gridLayout.cellSize.x + item.gridPositionX * gridLayout.spacing.x,
            -item.gridPositionY * gridLayout.cellSize.y - item.gridPositionY * gridLayout.spacing.y
        );
    }
}