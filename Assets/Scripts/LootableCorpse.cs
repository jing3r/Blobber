using UnityEngine;
using System.Collections.Generic;

public class LootableCorpse : Interactable
{
    [Header("Loot Settings")]
    public List<InventoryItem> loot = new List<InventoryItem>();
    [Tooltip("Сообщение, если ничего не взято (например, нет места).")]
    public string nothingTakenMessage = "Ничего не взято (нет места).";
    [Tooltip("Сообщение, если что-то было подобрано.")]
    public string itemsTakenMessage = "Вы подобрали несколько предметов.";
    [Tooltip("Задержка перед исчезновением трупа (в секундах).")]
    public float disappearDelay = 0.5f;

    private bool looted = false;

    public override string Interact()
    {
        if (looted) return "Здесь больше ничего нет.";
        looted = true;

        PartyManager partyManager = FindObjectOfType<PartyManager>(); // Находим один раз
        string finalLootMessage = nothingTakenMessage;
        bool anyItemSuccessfullyTaken = false;

        if (partyManager == null || partyManager.partyMembers.Count == 0)
        {
            Debug.LogError($"LootableCorpse ({gameObject.name}): PartyManager не найден или в партии нет участников!");
            Destroy(gameObject, disappearDelay);
            return "Ошибка: некому отдать лут.";
        }

        // Создаем копию списка лута, чтобы можно было изменять его по мере распределения
        List<InventoryItem> remainingLootToDistribute = new List<InventoryItem>(loot);
        List<InventoryItem> tempLootPile = new List<InventoryItem>(); // Для предметов, которые не влезли текущему персонажу

        // Попытка отдать лут первому живому члену партии (условный "активный")
        CharacterStats priorityMember = null;
        if (partyManager.partyMembers.Count > 0 && partyManager.partyMembers[0] != null && !partyManager.partyMembers[0].IsDead)
        {
            priorityMember = partyManager.partyMembers[0];
        }

        if (priorityMember != null)
        {
            Inventory targetInventory = priorityMember.GetComponent<Inventory>();
            if (targetInventory != null)
            {
                foreach (var itemEntry in remainingLootToDistribute)
                {
                    if (itemEntry.itemData == null || itemEntry.quantity <= 0) continue;
                    if (targetInventory.AddItem(itemEntry.itemData, itemEntry.quantity))
                    {
                        anyItemSuccessfullyTaken = true;
                    }
                    else
                    {
                        tempLootPile.Add(new InventoryItem(itemEntry.itemData, itemEntry.quantity)); // Не влезло, откладываем
                    }
                }
                remainingLootToDistribute = new List<InventoryItem>(tempLootPile); // Обновляем оставшийся лут
                tempLootPile.Clear();
            }
        }

        // Распределение оставшегося лута по другим членам партии
        if (remainingLootToDistribute.Count > 0)
        {
            foreach (var member in partyManager.partyMembers)
            {
                if (member == null || member.IsDead || member == priorityMember) continue; // Пропускаем уже обработанного, мертвого или null
                if (remainingLootToDistribute.Count == 0) break; // Весь лут распределен

                Inventory targetInventory = member.GetComponent<Inventory>();
                if (targetInventory != null)
                {
                    foreach (var itemEntry in remainingLootToDistribute) // Итерируемся по копии, чтобы можно было изменять оригинал
                    {
                         if (itemEntry.itemData == null || itemEntry.quantity <= 0) continue;
                         if (targetInventory.AddItem(itemEntry.itemData, itemEntry.quantity))
                         {
                             anyItemSuccessfullyTaken = true;
                             // Помечаем предмет как добавленный, чтобы не пытаться добавить его снова
                             // (простой способ - установить quantity в 0 или удалить из списка, но это сложнее при итерации)
                             // Вместо этого, будем собирать невлезшие в tempLootPile
                         }
                         else
                         {
                             tempLootPile.Add(new InventoryItem(itemEntry.itemData, itemEntry.quantity));
                         }
                    }
                    remainingLootToDistribute = new List<InventoryItem>(tempLootPile);
                    tempLootPile.Clear();
                }
            }
        }
        
        if (remainingLootToDistribute.Count > 0)
        {
            Debug.LogWarning($"LootableCorpse ({gameObject.name}): {remainingLootToDistribute.Count} типов предметов не влезло ни в один инвентарь.");
        }

        if (anyItemSuccessfullyTaken)
        {
            finalLootMessage = itemsTakenMessage;
        }

        Destroy(gameObject, disappearDelay);
        return finalLootMessage;
    }
}