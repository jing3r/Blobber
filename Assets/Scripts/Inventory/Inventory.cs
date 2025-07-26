using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Управляет сеткой предметов для сущности (игрока, сундука, трупа).
/// </summary>
public class Inventory : MonoBehaviour
{
    public enum SortAxis { Vertical, Horizontal }

    [Header("Настройки инвентаря")]
    [SerializeField] private int gridWidth = 6;
    [SerializeField] private int gridHeight = 12;

    [Header("Состояние")]
    [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();
    
    private CharacterStats ownerStats;
    private float currentMaxWeightCapacity;
    private SortAxis lastUsedSortAxis = SortAxis.Vertical;

    public IReadOnlyList<InventoryItem> Items => items.AsReadOnly();
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float MaxWeightCapacity => currentMaxWeightCapacity;
    public float CurrentWeight => items.Sum(item => item.ItemData != null ? item.ItemData.Weight * item.Quantity : 0);

    public event Action OnInventoryChanged;

    #region Unity Lifecycle & Initialization
    private void Awake()
    {
        ownerStats = GetComponent<CharacterStats>();
        if (ownerStats != null)
        {
            ownerStats.onAttributesChanged += UpdateMaxWeightFromStats;
            UpdateMaxWeightFromStats();
        }
        else
        {
            // Фоллбэк для инвентарей без статов (например, сундуков)
            currentMaxWeightCapacity = 500f;
        }
    }

    private void OnDestroy()
    {
        if (ownerStats != null)
        {
            ownerStats.onAttributesChanged -= UpdateMaxWeightFromStats;
        }
    }

    private void UpdateMaxWeightFromStats()
    {
        currentMaxWeightCapacity = ownerStats.CalculatedMaxCarryWeight;
        OnInventoryChanged?.Invoke();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Устанавливает размеры сетки. Должен вызываться до инициализации UI.
    /// </summary>
    public void SetGridSize(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
    }
    /// <summary>
    /// Пытается добавить предмет(ы) в инвентарь.
    /// </summary>
    /// <returns>True, если все предметы были успешно добавлены.</returns>
    public bool AddItem(ItemData data, int amountToAdd, InventoryItem itemToIgnore = null)
    {
        if (data == null || amountToAdd <= 0) return false;

        float weightToAdd = data.Weight * amountToAdd;
        if (ownerStats != null)
        {
            if(ownerStats.TotalCarriedWeight + weightToAdd > ownerStats.CalculatedMaxCarryWeight)
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                return false;
            }
        }
        else
        {
            if (CurrentWeight + weightToAdd > MaxWeightCapacity)
            {
                 FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Container is full.");
                 return false;
            }
        }

        int originalAmount = amountToAdd;

        // 1. Попытка объединить со существующими стаками
        if (data.MaxStackSize > 1)
        {
            amountToAdd = TryMergeWithExistingStacks(data, amountToAdd, itemToIgnore);
        }

        // 2. Попытка разместить оставшееся в новых слотах
        if (amountToAdd > 0)
        {
            amountToAdd = PlaceInNewSlots(data, amountToAdd, itemToIgnore);
        }

        bool success = amountToAdd == 0;
        if (!success && originalAmount > amountToAdd)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space for all items.");
        }
        else if (!success)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough space in inventory.");
        }

        OnInventoryChanged?.Invoke();
        return success;
    }
    
    /// <summary>
    /// Пытается добавить предмет в инвентарь по указанным координатам.
    /// </summary>
    /// <returns>True, если предмет был успешно добавлен.</returns>
    public bool AddItemAt(InventoryItem item, int x, int y)
    {
        if (item == null) return false;

        if (!IsAreaFree(x, y, item.ItemData.GridWidth, item.ItemData.GridHeight)) return false;

        float weightToAdd = item.ItemData.Weight * item.Quantity;
        if (ownerStats != null)
        {
            if (ownerStats.TotalCarriedWeight + weightToAdd > ownerStats.CalculatedMaxCarryWeight)
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                return false;
            }
        }
        else if (CurrentWeight + weightToAdd > MaxWeightCapacity)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Container is full.");
            return false;
        }

        // Добавляем предмет
        item.GridPositionX = x;
        item.GridPositionY = y;
        item.SetOwner(this);
        items.Add(item);

        OnInventoryChanged?.Invoke();
        return true;
    }
    /// <summary>
    /// Удаляет указанный предмет из инвентаря.
    /// </summary>
    public void RemoveItem(InventoryItem itemToRemove)
    {
        if (items.Contains(itemToRemove))
        {
            items.Remove(itemToRemove);
            OnInventoryChanged?.Invoke();
        }
    }
    /// <summary>
    /// Полностью очищает инвентарь от всех предметов.
    /// </summary>
    public void Clear()
    {
        items.Clear();
        OnInventoryChanged?.Invoke();
    }
    /// <summary>
    /// Разделяет стак на две части.
    /// </summary>
    public void SplitStack(InventoryItem itemToSplit, int amountToSplit = -1)
    {
        if (itemToSplit.Quantity <= 1) return;

        int quantityInNewStack = (amountToSplit == -1) 
            ? Mathf.FloorToInt(itemToSplit.Quantity / 2f) 
            : Mathf.Min(amountToSplit, itemToSplit.Quantity - 1);

        if (quantityInNewStack <= 0) return;
        
        if (FindFreeSpot(itemToSplit.ItemData.GridWidth, itemToSplit.ItemData.GridHeight, out Vector2Int position))
        {
            itemToSplit.Quantity -= quantityInNewStack;
            var newItem = new InventoryItem(itemToSplit.ItemData, quantityInNewStack, position.x, position.y, this);
            items.Add(newItem);
            OnInventoryChanged?.Invoke();
        }
        else
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space to split stack.");
        }
    }

    /// <summary>
    /// Переключает режим сортировки инвентаря (вертикальный/горизонтальный).
    /// </summary>
    public void ToggleArrange()
    {
        lastUsedSortAxis = (lastUsedSortAxis == SortAxis.Vertical) ? SortAxis.Horizontal : SortAxis.Vertical;
        ArrangeItems(lastUsedSortAxis);
    }
    
    /// <summary>
    /// Обрабатывает перетаскивание предмета на другой предмет (для объединения).
    /// </summary>
    public void HandleDropOntoItem(InventoryItem draggedItem, InventoryItem targetItem)
    {
        Inventory sourceInventory = draggedItem.GetOwnerInventory();
        Inventory targetInventory = targetItem.GetOwnerInventory();

        if (sourceInventory == null || targetInventory == null) return;
        if (sourceInventory != targetInventory && !IsInteractionInRange(sourceInventory, targetInventory)) return;

        if (draggedItem.ItemData == targetItem.ItemData && targetItem.Quantity < targetItem.ItemData.MaxStackSize)
        {
            int spaceInTarget = targetItem.ItemData.MaxStackSize - targetItem.Quantity;
            int amountToMove = Mathf.Min(draggedItem.Quantity, spaceInTarget);

            if (amountToMove > 0)
            {
                if (sourceInventory != targetInventory)
                {
                    float weightToAdd = draggedItem.ItemData.Weight * amountToMove;
                    if (targetInventory.CurrentWeight + weightToAdd > targetInventory.MaxWeightCapacity)
                    {
                        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                        return;
                    }
                }

                targetItem.Quantity += amountToMove;
                draggedItem.Quantity -= amountToMove;

                if (draggedItem.Quantity <= 0)
                {
                    sourceInventory.RemoveItem(draggedItem);
                }
                else
                {
                     sourceInventory.OnInventoryChanged?.Invoke();
                }

                if (sourceInventory != targetInventory)
                {
                    targetInventory.OnInventoryChanged?.Invoke();
                }
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает перетаскивание предмета на сетку (для перемещения).
    /// </summary>
    public void HandleDropOntoGrid(InventoryItem draggedItem, Inventory targetInventory, int targetX, int targetY)
    {
        Inventory sourceInventory = draggedItem.GetOwnerInventory();
        if (sourceInventory == null || targetInventory == null) return;
        if (sourceInventory != targetInventory && !IsInteractionInRange(sourceInventory, targetInventory)) return;

        sourceInventory.items.Remove(draggedItem);

        if (targetInventory.IsAreaFree(targetX, targetY, draggedItem.ItemData.GridWidth, draggedItem.ItemData.GridHeight))
        {
            if (sourceInventory != targetInventory)
            {
                float weightToAdd = draggedItem.ItemData.Weight * draggedItem.Quantity;
                if (targetInventory.CurrentWeight + weightToAdd > targetInventory.MaxWeightCapacity)
                {
                    sourceInventory.items.Add(draggedItem);
                    FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                    sourceInventory.OnInventoryChanged?.Invoke();
                    return;
                }
            }
            
            draggedItem.GridPositionX = targetX;
            draggedItem.GridPositionY = targetY;
            draggedItem.SetOwner(targetInventory);
            targetInventory.items.Add(draggedItem);
            
            targetInventory.OnInventoryChanged?.Invoke();
            if (sourceInventory != targetInventory)
            {
                sourceInventory.OnInventoryChanged?.Invoke();
            }
        }
        else
        {
            sourceInventory.items.Add(draggedItem);
            sourceInventory.OnInventoryChanged?.Invoke();
        }
    }
    
    public void TriggerInventoryChanged() => OnInventoryChanged?.Invoke();
    #endregion

    #region Private Logic
    private int TryMergeWithExistingStacks(ItemData data, int amountToAdd, InventoryItem itemToIgnore)
    {
        foreach (var stack in items)
        {
            if (amountToAdd == 0) break;
            if (stack == itemToIgnore || stack.ItemData != data || stack.Quantity >= data.MaxStackSize) continue;

            int spaceInStack = data.MaxStackSize - stack.Quantity;
            int amountToMove = Mathf.Min(amountToAdd, spaceInStack);

            stack.Quantity += amountToMove;
            amountToAdd -= amountToMove;

            if (itemToIgnore != null)
            {
                itemToIgnore.Quantity -= amountToMove;
            }
        }

        if (itemToIgnore != null && itemToIgnore.Quantity <= 0)
        {
            items.Remove(itemToIgnore);
        }

        return amountToAdd;
    }

    private int PlaceInNewSlots(ItemData data, int amountToAdd, InventoryItem itemToIgnore)
    {
        if (itemToIgnore != null)
        {
            if (FindFreeSpot(data.GridWidth, data.GridHeight, out Vector2Int pos))
            {
                itemToIgnore.GridPositionX = pos.x;
                itemToIgnore.GridPositionY = pos.y;
                return 0;
            }
            return amountToAdd;
        }

        while (amountToAdd > 0)
        {
            if (FindFreeSpot(data.GridWidth, data.GridHeight, out Vector2Int pos))
            {
                int quantityForNewStack = Mathf.Min(amountToAdd, data.MaxStackSize);
                var newItem = new InventoryItem(data, quantityForNewStack, pos.x, pos.y, this);
                items.Add(newItem);
                amountToAdd -= quantityForNewStack;
            }
            else
            {
                break;
            }
        }
        return amountToAdd;
    }

    private void ArrangeItems(SortAxis sortAxis)
    {
        ConsolidateStacks();
        List<InventoryItem> itemsToArrange = new List<InventoryItem>(items);
        items.Clear();

        // Полная логика сортировки
        itemsToArrange.Sort((a, b) =>
        {
            var dataA = a.ItemData;
            var dataB = b.ItemData;

            // Предметы, занимающие всю ширину, идут первыми
            bool isA_MaxWide = dataA.GridWidth == this.GridWidth;
            bool isB_MaxWide = dataB.GridWidth == this.GridWidth;
            if (isA_MaxWide != isB_MaxWide) return isB_MaxWide.CompareTo(isA_MaxWide);

            // "Очень высокие" предметы (например, оружие) идут следующими
            bool isA_VeryTall = dataA.GridHeight >= 6;
            bool isB_VeryTall = dataB.GridHeight >= 6;
            if (isA_VeryTall != isB_VeryTall) return isB_VeryTall.CompareTo(isA_VeryTall);
            if (isA_VeryTall && isB_VeryTall)
            {
                // Среди очень высоких сортируем по высоте
                if (dataA.GridHeight != dataB.GridHeight) return dataB.GridHeight.CompareTo(dataA.GridHeight);
            }

            // Сортируем по занимаемой площади (от большего к меньшему)
            int areaA = dataA.GridWidth * dataA.GridHeight;
            int areaB = dataB.GridWidth * dataB.GridHeight;
            if (areaA != areaB) return areaB.CompareTo(areaA);
            
            // Если площади равны, сортируем по максимальному измерению (от "квадратных" к "вытянутым")
            int maxDimA = Mathf.Max(dataA.GridWidth, dataA.GridHeight);
            int maxDimB = Mathf.Max(dataB.GridWidth, dataB.GridHeight);
            if (maxDimA != maxDimB) return maxDimB.CompareTo(maxDimA);

            return 0; // Если все параметры равны, порядок не важен
        });
        
        foreach (var item in itemsToArrange)
        {
            if (FindFreeSpotForAxis(item.ItemData.GridWidth, item.ItemData.GridHeight, sortAxis, out Vector2Int position))
            {
                item.GridPositionX = position.x;
                item.GridPositionY = position.y;
                items.Add(item);
            }
            else
            {
                Debug.LogError($"Could not place item {item.ItemData.ItemName} during arrangement. This should not happen.");
                items.Add(item); // Возвращаем, чтобы не потерять
            }
        }
        
        OnInventoryChanged?.Invoke();
        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Inventory arranged.");
    }

    private void ConsolidateStacks()
    {
        var itemGroups = items.GroupBy(item => item.ItemData)
                           .Where(group => group.Count() > 1 && group.Key.MaxStackSize > 1)
                           .ToList();

        foreach (var group in itemGroups)
        {
            var stacks = group.OrderBy(item => item.Quantity).ToList();
            for (int i = 0; i < stacks.Count; i++)
            {
                var sourceStack = stacks[i];
                if (sourceStack.Quantity == 0) continue;

                for (int j = stacks.Count - 1; j > i; j--)
                {
                    var targetStack = stacks[j];
                    int spaceInTarget = targetStack.ItemData.MaxStackSize - targetStack.Quantity;
                    int amountToMove = Mathf.Min(sourceStack.Quantity, spaceInTarget);

                    if (amountToMove > 0)
                    {
                        targetStack.Quantity += amountToMove;
                        sourceStack.Quantity -= amountToMove;
                    }
                    if (sourceStack.Quantity == 0) break;
                }
            }
        }
        items.RemoveAll(item => item.Quantity == 0);
    }
    
    private bool IsInteractionInRange(Inventory invA, Inventory invB)
    {
        var partyManager = FindObjectOfType<PartyManager>();
        if (partyManager == null) return true;
        
        bool isAInParty = invA.GetComponentInParent<PartyManager>() != null;
        bool isBInParty = invB.GetComponentInParent<PartyManager>() != null;

        if (isAInParty == isBInParty) return true;

        Inventory worldContainer = isAInParty ? invB : invA;
        if (Vector3.Distance(partyManager.transform.position, worldContainer.transform.position) > partyManager.MaxLootDistance)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("The container is too far away.");
            return false;
        }
        return true;
    }
    
    private bool IsAreaFree(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x + width > GridWidth || y + height > GridHeight) return false;

        foreach (var item in items)
        {
            if (x < item.GridPositionX + item.ItemData.GridWidth && x + width > item.GridPositionX &&
                y < item.GridPositionY + item.ItemData.GridHeight && y + height > item.GridPositionY)
            {
                return false;
            }
        }
        return true;
    }
    
    private bool FindFreeSpot(int itemWidth, int itemHeight, out Vector2Int position)
    {
        return FindFreeSpotForAxis(itemWidth, itemHeight, SortAxis.Vertical, out position);
    }

    private bool FindFreeSpotForAxis(int itemWidth, int itemHeight, SortAxis axis, out Vector2Int position)
    {
        position = Vector2Int.zero;
        if (axis == SortAxis.Vertical)
        {
            for (int y = 0; y <= GridHeight - itemHeight; y++)
            for (int x = 0; x <= GridWidth - itemWidth; x++)
            {
                if (IsAreaFree(x, y, itemWidth, itemHeight))
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        else // Horizontal
        {
            for (int x = 0; x <= GridWidth - itemWidth; x++)
            for (int y = 0; y <= GridHeight - itemHeight; y++)
            {
                if (IsAreaFree(x, y, itemWidth, itemHeight))
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        return false;
    }
    #endregion
}