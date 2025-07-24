using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Inventory : MonoBehaviour
{
    [Header("Настройки инвентаря")]
    public int gridWidth = 6;
    public int gridHeight = 12;

    [Header("Состояние")]
    public List<InventoryItem> items = new List<InventoryItem>();

    public event System.Action OnInventoryChanged;

    private CharacterStats ownerStats;
    private float currentMaxWeightCapacity;
    public float MaxWeightCapacity => currentMaxWeightCapacity;

    public float CurrentWeight => items.Sum(slot => slot.itemData != null ? slot.itemData.weight * slot.quantity : 0);
    public int TotalGridCapacity => gridWidth * gridHeight;

    // Добавляем enum и поле для хранения состояния
    public enum SortAxis { Vertical, Horizontal }
    private SortAxis lastUsedSortAxis = SortAxis.Vertical;

    void Awake()
    {
        ownerStats = GetComponent<CharacterStats>();
        if (ownerStats != null)
        {
            ownerStats.onAttributesChanged += UpdateMaxWeightFromStats;
            UpdateMaxWeightFromStats();
        }
        else
        {
            currentMaxWeightCapacity = 50f;
        }
    }

    void OnDestroy()
    {
        if (ownerStats != null)
        {
            ownerStats.onAttributesChanged -= UpdateMaxWeightFromStats;
        }
    }

    private void UpdateMaxWeightFromStats()
    {
        if (ownerStats != null)
        {
            currentMaxWeightCapacity = ownerStats.CalculatedMaxCarryWeight;
            OnInventoryChanged?.Invoke();
        }
    }

    private bool IsAreaFree(int startX, int startY, int width, int height)
    {
        if (startX < 0 || startY < 0 || startX + width > gridWidth || startY + height > gridHeight)
        {
            return false;
        }

        foreach (var item in items)
        {
            if (startX < item.gridPositionX + item.itemData.gridWidth &&
                startX + width > item.gridPositionX &&
                startY < item.gridPositionY + item.itemData.gridHeight &&
                startY + height > item.gridPositionY)
            {
                return false;
            }
        }
        return true;
    }

    // Этот метод нам все еще нужен для AddItem, возвращаем его
    private bool FindFreeSpot(int itemWidth, int itemHeight, out Vector2Int position)
    {
        // По умолчанию ищем по вертикали
        return FindFreeSpotForAxis(itemWidth, itemHeight, SortAxis.Vertical, out position);
    }

    private bool FindFreeSpotForAxis(int itemWidth, int itemHeight, SortAxis axis, out Vector2Int position)
    {
        if (axis == SortAxis.Vertical)
        {
            for (int y = 0; y <= gridHeight - itemHeight; y++)
            {
                for (int x = 0; x <= gridWidth - itemWidth; x++)
                {
                    if (IsAreaFree(x, y, itemWidth, itemHeight))
                    {
                        position = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
        }
        else
        {
            for (int x = 0; x <= gridWidth - itemWidth; x++)
            {
                for (int y = 0; y <= gridHeight - itemHeight; y++)
                {
                    if (IsAreaFree(x, y, itemWidth, itemHeight))
                    {
                        position = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
        }

        position = Vector2Int.zero;
        return false;
    }

public bool AddItem(ItemData data, int amountToAdd, InventoryItem itemToIgnore = null)
    {
        if (data == null || amountToAdd <= 0) return false;
        
        // ... проверка веса ...

        // 1. Попытка объединения
        if (data.maxStackSize > 1)
        {
            foreach (var stack in items.Where(slot => slot != itemToIgnore && slot.itemData == data && slot.quantity < data.maxStackSize))
            {
                int canAdd = data.maxStackSize - stack.quantity;
                int toAdd = Mathf.Min(amountToAdd, canAdd);
                
                stack.quantity += toAdd;
                amountToAdd -= toAdd;

                if (itemToIgnore != null) // Если это была пересортировка
                {
                    itemToIgnore.quantity -= toAdd;
                }
                
                if (amountToAdd == 0)
                {
                    if (itemToIgnore != null && itemToIgnore.quantity <= 0)
                    {
                        items.Remove(itemToIgnore); // Удаляем исходный стак, если он полностью перешел в другой
                    }
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }
        
        // Если после объединения что-то осталось (или предмет не стакается), ищем новую ячейку
        if (amountToAdd > 0)
        {
            // Если это была пересортировка, мы не создаем новый предмет, а перемещаем старый
            if (itemToIgnore != null)
            {
                if(FindFreeSpot(data.gridWidth, data.gridHeight, out Vector2Int position))
                {
                    // Мы не можем просто удалить и добавить, т.к. потеряем ссылку. Меняем позицию.
                    itemToIgnore.gridPositionX = position.x;
                    itemToIgnore.gridPositionY = position.y;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
            else // Если это добавление нового предмета извне
            {
                while (amountToAdd > 0)
                {
                    if (FindFreeSpot(data.gridWidth, data.gridHeight, out Vector2Int position))
                    {
                        int quantityForNewStack = Mathf.Min(amountToAdd, data.maxStackSize);
                        items.Add(new InventoryItem(data, quantityForNewStack, position.x, position.y, this));
                        amountToAdd -= quantityForNewStack;
                    }
                    else
                    {
                        Debug.LogWarning("Not enough space in inventory grid.");
                        OnInventoryChanged?.Invoke();
                        return false;
                    }
                }
            }
        }
        
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    public bool MoveItem(InventoryItem itemToMove, int newX, int newY)
    {
        items.Remove(itemToMove);
        if (IsAreaFree(newX, newY, itemToMove.itemData.gridWidth, itemToMove.itemData.gridHeight))
        {
            itemToMove.gridPositionX = newX;
            itemToMove.gridPositionY = newY;
            items.Add(itemToMove);
            OnInventoryChanged?.Invoke();
            return true;
        }
        else
        {
            items.Add(itemToMove);
            return false;
        }
    }

    public void RemoveItem(InventoryItem itemToRemove)
    {
        if (items.Contains(itemToRemove))
        {
            items.Remove(itemToRemove);
            OnInventoryChanged?.Invoke();
        }
    }

    // public bool TryAddItemAtPosition(InventoryItem itemToAdd, int x, int y)
    // {
    //     if (CurrentWeight + itemToAdd.itemData.weight * itemToAdd.quantity > MaxWeightCapacity) return false;
        
    //     if (IsAreaFree(x, y, itemToAdd.itemData.gridWidth, itemToAdd.itemData.gridHeight))
    //     {
    //         itemToAdd.gridPositionX = x;
    //         itemToAdd.gridPositionY = y;
    //         items.Add(itemToAdd);
    //         OnInventoryChanged?.Invoke();
    //         return true;
    //     }

    //     return false;
    // }
public void SplitStack(InventoryItem itemToSplit, int amountToSplit = -1)
{
    // Нельзя разделить стак из одного предмета
    if (itemToSplit.quantity <= 1) return;

    // Если количество не указано, делим пополам. Иначе, берем указанное количество.
    int quantityInNewStack = (amountToSplit == -1) 
        ? Mathf.FloorToInt(itemToSplit.quantity / 2f) 
        : Mathf.Min(amountToSplit, itemToSplit.quantity - 1); // Нельзя отделить весь стак

    if (quantityInNewStack <= 0) return;

    // Ищем свободное место для нового стака
    if (FindFreeSpot(itemToSplit.itemData.gridWidth, itemToSplit.itemData.gridHeight, out Vector2Int position))
    {
        // Уменьшаем количество в исходном стаке
        itemToSplit.quantity -= quantityInNewStack;
        
        // Создаем новый предмет с отделенным количеством
        // Используем наш существующий метод AddItem, но нам нужно создать новый для размещения в конкретной ячейке
        // Давай создадим приватный метод для этого
        PlaceNewItem(itemToSplit.itemData, quantityInNewStack, position.x, position.y);
        
        // Оповещаем UI
        OnInventoryChanged?.Invoke();
    }
    else
    {
        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space to split stack.");
    }
}

// Вспомогательный приватный метод для чистоты кода
private void PlaceNewItem(ItemData data, int quantity, int x, int y)
{
    items.Add(new InventoryItem(data, quantity, x, y, this));
}
    // --- ИСПРАВЛЕННЫЙ БЛОК СОРТИРОВКИ ---

    public void ToggleArrange()
    {
        SortAxis nextAxis = (lastUsedSortAxis == SortAxis.Vertical) ? SortAxis.Horizontal : SortAxis.Vertical;
        // ОШИБКА №2 БЫЛА ЗДЕСЬ: Нужно было передавать параметр в приватный метод.
        ArrangeItems(nextAxis); 
        lastUsedSortAxis = nextAxis;
    }

    private void ArrangeItems(SortAxis sortAxis)
    {
        ConsolidateStacks();
        List<InventoryItem> itemsToArrange = new List<InventoryItem>(items);
        items.Clear();

        itemsToArrange.Sort((a, b) =>
        {
            var dataA = a.itemData;
            var dataB = b.itemData;

            bool isA_MaxWide = dataA.gridWidth == this.gridWidth;
            bool isB_MaxWide = dataB.gridWidth == this.gridWidth;
            if (isA_MaxWide != isB_MaxWide) return isB_MaxWide.CompareTo(isA_MaxWide);

            bool isA_VeryTall = dataA.gridHeight >= 6;
            bool isB_VeryTall = dataB.gridHeight >= 6;
            if (isA_VeryTall != isB_VeryTall) return isB_VeryTall.CompareTo(isA_VeryTall);
            if (isA_VeryTall && isB_VeryTall)
            {
                if (dataA.gridHeight != dataB.gridHeight) return dataB.gridHeight.CompareTo(dataA.gridHeight);
            }

            int areaA = dataA.gridWidth * dataA.gridHeight;
            int areaB = dataB.gridWidth * dataB.gridHeight;
            if (areaA != areaB) return areaB.CompareTo(areaA);
            
            int maxDimA = Mathf.Max(dataA.gridWidth, dataA.gridHeight);
            int maxDimB = Mathf.Max(dataB.gridWidth, dataB.gridHeight);
            if (maxDimA != maxDimB) return maxDimB.CompareTo(maxDimA);

            return 0;
        });
        
        bool allItemsPlaced = true;
        foreach (var item in itemsToArrange)
        {
            // ОШИБКА №3 БЫЛА ЗДЕСЬ: Использовалась несуществующая переменная.
            if (FindFreeSpotForAxis(item.itemData.gridWidth, item.itemData.gridHeight, sortAxis, out Vector2Int position))
            {
                item.gridPositionX = position.x;
                item.gridPositionY = position.y;
                items.Add(item);
            }
            else
            {
                Debug.LogError($"Could not place item {item.itemData.name} during arrangement. This should not happen.");
                item.gridPositionX = -1; 
                item.gridPositionY = -1;
                items.Add(item);
                allItemsPlaced = false;
            }
        }

        OnInventoryChanged?.Invoke();
        
        var feedback = FindObjectOfType<FeedbackManager>();
        if (feedback != null)
        {
            if (allItemsPlaced)
                feedback.ShowFeedbackMessage("Inventory arranged.");
            else
                feedback.ShowFeedbackMessage("Could not arrange all items perfectly.");
        }
    }
    public void ConsolidateStacks()
{
    // Группируем все предметы по их ItemData
    var itemGroups = items.GroupBy(item => item.itemData)
                           .Where(group => group.Count() > 1 && group.Key.maxStackSize > 1)
                           .ToList();

    foreach (var group in itemGroups)
    {
        var stacks = group.OrderBy(item => item.quantity).ToList();
        
        // Переносим предметы из меньших стаков в большие, пока возможно
        for (int i = 0; i < stacks.Count - 1; i++)
        {
            var sourceStack = stacks[i];
            for (int j = i + 1; j < stacks.Count; j++)
            {
                var targetStack = stacks[j];
                int spaceInTarget = targetStack.itemData.maxStackSize - targetStack.quantity;
                int amountToMove = Mathf.Min(sourceStack.quantity, spaceInTarget);

                if (amountToMove > 0)
                {
                    targetStack.quantity += amountToMove;
                    sourceStack.quantity -= amountToMove;
                }
                if (sourceStack.quantity == 0) break;
            }
        }
        
        // Удаляем все опустевшие стаки
        items.RemoveAll(item => item.quantity == 0);
    }
    // Вызываем OnInventoryChanged, если он не будет вызван позже
    // OnInventoryChanged?.Invoke(); 
    // Он будет вызван в конце ArrangeItems, так что здесь не нужен.
}

    public void TryMergeStacks(InventoryItem sourceItem, InventoryItem targetItem)
    {
        // Проверяем, что это один и тот же тип предмета и целевой стак не полон
        if (sourceItem.itemData == targetItem.itemData && targetItem.quantity < targetItem.itemData.maxStackSize)
        {
            int spaceInTarget = targetItem.itemData.maxStackSize - targetItem.quantity;
            int amountToMove = Mathf.Min(sourceItem.quantity, spaceInTarget);

            // Перемещаем количество
            targetItem.quantity += amountToMove;
            sourceItem.quantity -= amountToMove;

            // Если исходный стак опустел, удаляем его
            if (sourceItem.quantity <= 0)
            {
                items.Remove(sourceItem);
            }

            // Оповещаем UI об изменениях
            OnInventoryChanged?.Invoke();
        }
    }


    // public void HandleDrop(InventoryItem draggedItem, Inventory targetInventory, int targetX, int targetY)
    // {
    //     // Находим, есть ли на целевой позиции какой-то предмет
    //     InventoryItem targetItem = targetInventory.GetItemAt(targetX, targetY);

    //     // Сценарий 1: Объединение стаков
    //     if (targetItem != null && targetItem != draggedItem && targetItem.itemData == draggedItem.itemData)
    //     {
    //         // Если это тот же инвентарь
    //         if (this == targetInventory)
    //         {
    //             TryMergeStacks(draggedItem, targetItem);
    //         }
    //         else // Если это другой инвентарь
    //         {
    //             // Переносим стак
    //             int spaceInTarget = targetItem.itemData.maxStackSize - targetItem.quantity;
    //             int amountToMove = Mathf.Min(draggedItem.quantity, spaceInTarget);

    //             if (amountToMove > 0)
    //             {
    //                 targetItem.quantity += amountToMove;
    //                 draggedItem.quantity -= amountToMove;

    //                 if (draggedItem.quantity <= 0)
    //                 {
    //                     this.RemoveItem(draggedItem);
    //                 }

    //                 // Вызываем OnInventoryChanged для обоих инвентарей
    //                 this.OnInventoryChanged?.Invoke();
    //                 targetInventory.OnInventoryChanged?.Invoke();
    //             }
    //         }
    //         return;
    //     }

    //     // Сценарий 2: Перемещение или передача в пустую ячейку
    //     if (this == targetInventory)
    //     {
    //         // Простое перемещение внутри одного инвентаря
    //         MoveItem(draggedItem, targetX, targetY);
    //     }
    //     else
    //     {
    //         // Передача в другой инвентарь
    //         this.RemoveItem(draggedItem);
    //         if (!targetInventory.TryAddItemAtPosition(draggedItem, targetX, targetY))
    //         {
    //             // Если не получилось, возвращаем предмет обратно
    //             this.AddItem(draggedItem.itemData, draggedItem.quantity);
    //         }
    //     }
    // }
    
    /// <summary>
    /// Обрабатывает бросок перетаскиваемого предмета на другой предмет (для объединения).
    /// </summary>
    public void HandleDropOntoItem(InventoryItem draggedItem, InventoryItem targetItem)
    {
        Inventory sourceInventory = draggedItem.GetOwnerInventory();
        Inventory targetInventory = targetItem.GetOwnerInventory();

        if (sourceInventory == null || targetInventory == null) return;

        if (sourceInventory != targetInventory && !CheckInteractionDistance(sourceInventory, targetInventory))
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("The container is too far away.");
            // Вызываем OnInventoryChanged, чтобы UI вернулся в исходное состояние, так как предмет не переместился
            sourceInventory.OnInventoryChanged?.Invoke();
            return;
        }

        // Проверяем, что это один и тот же тип предмета и целевой стак не полон
        if (draggedItem.itemData == targetItem.itemData && targetItem.quantity < targetItem.itemData.maxStackSize)
        {
            int spaceInTarget = targetItem.itemData.maxStackSize - targetItem.quantity;
            int amountToMove = Mathf.Min(draggedItem.quantity, spaceInTarget);

            if (amountToMove > 0)
            {
                // Проверяем вес, если переносим в другой инвентарь
                if (sourceInventory != targetInventory)
                {
                    float weightToAdd = draggedItem.itemData.weight * amountToMove;
                    if (targetInventory.CurrentWeight + weightToAdd > targetInventory.MaxWeightCapacity)
                    {
                        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                        return; // Прерываем операцию
                    }
                }

                targetItem.quantity += amountToMove;
                draggedItem.quantity -= amountToMove;

                if (draggedItem.quantity <= 0)
                {
                    sourceInventory.RemoveItem(draggedItem);
                }

                // Оповещаем UI обоих инвентарей
                sourceInventory.OnInventoryChanged?.Invoke();
                if (sourceInventory != targetInventory)
                {
                    targetInventory.OnInventoryChanged?.Invoke();
                }
            }
        }
    }
    /// <summary>
    /// Обрабатывает бросок перетаскиваемого предмета на пустую ячейку сетки (для перемещения).
    /// </summary>
    public void HandleDropOntoGrid(InventoryItem draggedItem, Inventory targetInventory, int targetX, int targetY)
    {
        Inventory sourceInventory = draggedItem.GetOwnerInventory();
        if (sourceInventory == null || targetInventory == null) return;

        if (sourceInventory != targetInventory && !CheckInteractionDistance(sourceInventory, targetInventory))
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("The container is too far away.");
            sourceInventory.OnInventoryChanged?.Invoke();
            return;
        }
        // Удаляем предмет из исходного инвентаря для проверки места
        sourceInventory.items.Remove(draggedItem);

        // Проверяем, свободно ли место в целевом инвентаре
        if (targetInventory.IsAreaFree(targetX, targetY, draggedItem.itemData.gridWidth, draggedItem.itemData.gridHeight))
        {
            // Проверяем вес, если это другой инвентарь
            if (sourceInventory != targetInventory)
            {
                float weightToAdd = draggedItem.itemData.weight * draggedItem.quantity;
                if (targetInventory.CurrentWeight + weightToAdd > targetInventory.MaxWeightCapacity)
                {
                    // Недостаточно места по весу, возвращаем предмет обратно
                    sourceInventory.items.Add(draggedItem);
                    FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Not enough carry capacity.");
                    sourceInventory.OnInventoryChanged?.Invoke(); // Обновляем UI, т.к. ничего не изменилось
                    return;
                }
            }

            // Место свободно, перемещаем предмет
            draggedItem.gridPositionX = targetX;
            draggedItem.gridPositionY = targetY;
            targetInventory.items.Add(draggedItem);

            // Обновляем оба инвентаря
            sourceInventory.OnInventoryChanged?.Invoke();
            if (sourceInventory != targetInventory)
            {
                targetInventory.OnInventoryChanged?.Invoke();
            }
        }
        else
        {
            // Место занято, возвращаем предмет обратно
            sourceInventory.items.Add(draggedItem);
            // Так как ничего не поменялось, можно не вызывать OnInventoryChanged,
            // но для надежности можно и вызвать, чтобы UI точно вернулся в исходное состояние.
            sourceInventory.OnInventoryChanged?.Invoke();
        }
    }
    /// <summary>
    /// Проверяет, допустима ли дистанция для взаимодействия между двумя инвентарями.
    /// </summary>
    /// <returns>True, если взаимодействие разрешено.</returns>
    private bool CheckInteractionDistance(Inventory invA, Inventory invB)
    {
        var partyManager = FindObjectOfType<PartyManager>();
        if (partyManager == null) return true; // Если нет PartyManager, проверку не проводим

        float maxLootDistance = partyManager.maxLootDistance;

        // Проверяем, принадлежат ли инвентари к разным "мирам" (партия vs мир)
        bool isAInParty = invA.GetComponentInParent<PartyManager>() != null;
        bool isBInParty = invB.GetComponentInParent<PartyManager>() != null;

        // Если оба в партии или оба в мире - дистанцию не проверяем
        if (isAInParty == isBInParty)
        {
            return true;
        }

        // Определяем, кто из них мировой контейнер, а кто - игрок
        Inventory worldContainer = isAInParty ? invB : invA;
        
        // Находим объект игрока для измерения дистанции
        var playerObject = FindObjectOfType<PartyManager>()?.gameObject;
        if (playerObject == null)
        {
            // Не смогли найти игрока, на всякий случай разрешаем действие
            return true; 
        }

        float distance = Vector3.Distance(playerObject.transform.position, worldContainer.transform.position);

        return distance <= maxLootDistance;
    }
    // Вспомогательный метод, который нам понадобится
    public InventoryItem GetItemAt(int x, int y)
    {
        foreach (var item in items)
        {
            // Проверяем, попадает ли точка (x, y) в прямоугольник предмета
            if (x >= item.gridPositionX && x < item.gridPositionX + item.itemData.gridWidth &&
                y >= item.gridPositionY && y < item.gridPositionY + item.itemData.gridHeight)
            {
                return item;
            }
        }
        return null;
    }
    public void TriggerInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}