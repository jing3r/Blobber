using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Inventory : MonoBehaviour
{
    // Убираем public float maxWeightCapacity;
    // public int totalGridCapacity = 40; // Оставляем, если будет использоваться

    [Header("Настройки инвентаря")]
    // public float maxWeightCapacity = 50f; // Удалено, будет браться из CharacterStats
    public int totalGridCapacity = 40; // Условная общая вместимость сетки

    [Header("Состояние")]
    public List<InventoryItem> items = new List<InventoryItem>();

    public event System.Action OnInventoryChanged;

    private CharacterStats ownerStats;
    private float currentMaxWeightCapacity; // Внутреннее поле для хранения актуального макс. веса

    public float MaxWeightCapacity => currentMaxWeightCapacity; // Публичное свойство для чтения

    public float CurrentWeight
    {
        get
        {
            float weight = 0f;
            foreach (var slot in items)
            {
                if (slot.itemData != null)
                {
                    weight += slot.itemData.weight * slot.quantity;
                }
            }
            return weight;
        }
    }

    public int CurrentGridOccupancy
    {
        get
        {
            int occupancy = 0;
            foreach (var slot in items)
            {
                if (slot.itemData != null)
                {
                    occupancy += slot.itemData.gridWidth * slot.itemData.gridHeight;
                }
            }
            return occupancy;
        }
    }

    void Awake()
    {
        ownerStats = GetComponent<CharacterStats>();
        if (ownerStats == null)
        {
            Debug.LogError($"Inventory ({gameObject.name}): CharacterStats не найден на этом объекте! Лимит веса не будет работать корректно.", this);
            // Установим дефолтное значение, если статы не найдены, чтобы избежать NullReferenceException
            currentMaxWeightCapacity = 50f; // Например, какое-то базовое значение
        }
        else
        {
            // Подписываемся на изменение атрибутов, чтобы обновить лимит веса
            ownerStats.onAttributesChanged += UpdateMaxWeightFromStats;
            UpdateMaxWeightFromStats(); // Первоначальная установка
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
            // Debug.Log($"Inventory ({gameObject.name}): Max weight updated to {currentMaxWeightCapacity}");
            OnInventoryChanged?.Invoke(); // Сообщаем UI и другим системам, что инвентарь (лимиты) изменился
        }
    }

    public bool AddItem(ItemData data, int amountToAdd)
    {
        if (data == null || amountToAdd <= 0)
        {
            // Debug.LogWarning($"Inventory ({gameObject.name}): Попытка добавить невалидный предмет ({data?.itemName}) или количество ({amountToAdd}).");
            return false;
        }

        bool changed = false;
        int remainingAmount = amountToAdd;

        // 1. Попытка добавить в существующие стеки
        if (data.maxStackSize > 1)
        {
            List<InventoryItem> existingStacks = items.Where(slot => slot.itemData == data && slot.quantity < data.maxStackSize).ToList();
            foreach (var stack in existingStacks)
            {
                if (remainingAmount <= 0) break;

                int spaceInStack = data.maxStackSize - stack.quantity;
                int amountToStack = Mathf.Min(remainingAmount, spaceInStack);

                // Проверка веса перед добавлением в стек
                float weightOfItemsToStack = data.weight * amountToStack;
                if (CurrentWeight + weightOfItemsToStack > MaxWeightCapacity) // Используем свойство MaxWeightCapacity
                {
                    // Пытаемся добавить столько, сколько влезет по весу
                    int maxCanAddByWeight = Mathf.FloorToInt((MaxWeightCapacity - CurrentWeight) / data.weight);
                    if (maxCanAddByWeight <= 0) continue; // Вообще ничего не влезает в этот стек по весу
                    
                    amountToStack = Mathf.Min(amountToStack, maxCanAddByWeight);
                    if(amountToStack <= 0) continue; // Если после всех расчетов добавлять нечего
                }
                
                if (amountToStack > 0)
                {
                    stack.quantity += amountToStack;
                    remainingAmount -= amountToStack;
                    changed = true;
                }
            }
        }

        // 2. Попытка добавить оставшееся количество в новые стеки/слоты
        while (remainingAmount > 0)
        {
            int amountForNewStack = (data.maxStackSize > 1) ? Mathf.Min(remainingAmount, data.maxStackSize) : 1;
            // Для нестекируемых всегда 1, даже если пытаются добавить больше (каждый предмет - новый слот)

            float newStackWeight = data.weight * amountForNewStack;
            if (CurrentWeight + newStackWeight > MaxWeightCapacity) // Используем свойство MaxWeightCapacity
            {
                // Если даже один предмет нового стека не влезает по весу, пытаемся уменьшить количество в этом "новом стеке"
                int maxCanAddByWeight = Mathf.FloorToInt((MaxWeightCapacity - CurrentWeight) / data.weight);
                if (maxCanAddByWeight <= 0) {
                    // Debug.LogWarning($"Inventory ({gameObject.name}): Недостаточно грузоподъемности для нового предмета/стека {data.itemName}. Осталось добавить: {remainingAmount}");
                    break; 
                }
                amountForNewStack = Mathf.Min(amountForNewStack, maxCanAddByWeight);
                if(amountForNewStack <=0) break;
            }


            int newStackGridSize = data.gridWidth * data.gridHeight;
            if (CurrentGridOccupancy + newStackGridSize > totalGridCapacity)
            {
                // Debug.LogWarning($"Inventory ({gameObject.name}): Недостаточно места в сетке для нового стека {data.itemName}. Осталось добавить: {remainingAmount}");
                break; 
            }
            
            if (amountForNewStack > 0)
            {
                items.Add(new InventoryItem(data, amountForNewStack));
                remainingAmount -= amountForNewStack;
                changed = true;
            }
            else // Если после всех проверок оказалось, что добавлять нечего (например, из-за веса)
            {
                break;
            }


            if (data.maxStackSize == 1 && remainingAmount > 0)
            {
                 continue;
            }
            else if (data.maxStackSize > 1 && remainingAmount > 0)
            {
                continue;
            }
            else
            {
                break;
            }
        }

        if (changed)
        {
            OnInventoryChanged?.Invoke();
        }
        // Возвращаем true, если удалось добавить хотя бы ЧАСТЬ запрошенного количества
        return amountToAdd > remainingAmount;
    }
}