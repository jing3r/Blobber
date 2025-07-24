using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Добавляем, чтобы использовать LINQ

public class InventoryUIManager : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private GameObject inventoryWindowPrefab;

    [Header("Контейнеры")]
    [SerializeField] private Transform partyInventoriesContainer; // Для инвентарей партии

    [SerializeField] private Transform worldInventoriesContainer; // Для трупов, сундуков и т.д.


    [Header("Источники данных")]
    [SerializeField] private PartyManager partyManager;

    // Теперь храним окна по их источнику-инвентарю
    private Dictionary<Inventory, InventoryUI> openWindows = new Dictionary<Inventory, InventoryUI>();
    public static InventoryGridUI GridUnderMouse { get; private set; }

    public static void SetGridUnderMouse(InventoryGridUI grid)
    {
        GridUnderMouse = grid;
    }

    public static void ClearGridUnderMouse()
    {
        GridUnderMouse = null;
    }
    // Вызывается из InputManager по нажатию 1-6
    public void TogglePartyMemberInventory(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= partyManager.partyMembers.Count) return;
        var character = partyManager.partyMembers[characterIndex];
        if (character == null) return;

        var inventory = character.GetComponent<Inventory>();
        if (inventory != null)
        {
            // Используем контейнер партии
            ToggleWindowFor(inventory, partyInventoriesContainer, null);
        }
    }

    // Вызывается из LootableCorpse.Interact()
    public void ToggleCorpseInventoryWindow(Inventory corpseInventory, string windowTitle)
    {
        // Используем мировой контейнер
        ToggleWindowFor(corpseInventory, worldInventoriesContainer, windowTitle);
    }

    // Универсальный метод для открытия/закрытия окна
    private void ToggleWindowFor(Inventory inventory, Transform container, string overrideName = null)
    {
        if (inventory == null) return;

        if (openWindows.TryGetValue(inventory, out InventoryUI existingWindow))
        {
            Destroy(existingWindow.gameObject);
            openWindows.Remove(inventory);
        }
        else
        {
            // Создаем окно в указанном контейнере
            var windowGO = Instantiate(inventoryWindowPrefab, container);
            var inventoryUI = windowGO.GetComponent<InventoryUI>();

            inventoryUI.Initialize(inventory, overrideName);
            openWindows.Add(inventory, inventoryUI);
        }
    }
    public void ToggleAllPartyWindows()
    {
        // Проверяем, открыто ли хотя бы одно окно инвентаря партии
        bool anyPartyInventoryOpen = false;
        foreach (var member in partyManager.partyMembers)
        {
            var inventory = member.GetComponent<Inventory>();
            if (inventory != null && openWindows.ContainsKey(inventory))
            {
                anyPartyInventoryOpen = true;
                break;
            }
        }

        // Если хотя бы одно открыто -> закрываем ВСЕ окна (и партийные, и мировые)
        if (anyPartyInventoryOpen)
        {
            // Создаем копию ключей, так как мы будем изменять словарь в цикле
            List<Inventory> keys = new List<Inventory>(openWindows.Keys);
            foreach (var inventoryKey in keys)
            {
                if (openWindows.TryGetValue(inventoryKey, out InventoryUI window))
                {
                    Destroy(window.gameObject);
                    openWindows.Remove(inventoryKey);
                }
            }
        }
        else // Если все закрыты -> открываем инвентари для КАЖДОГО члена партии
        {
            for (int i = 0; i < partyManager.partyMembers.Count; i++)
            {
                TogglePartyMemberInventory(i);
            }
        }
    }
    public void TakeAllFromOpenContainer()
    {
        // 1. Собираем список всех открытых "мировых" инвентарей
        List<Inventory> openWorldInventories = new List<Inventory>();
        foreach (var windowPair in openWindows)
        {
            // Владелец инвентаря - это GameObject, на котором висит компонент Inventory
            var inventoryOwner = windowPair.Key.gameObject;

            // Проверяем, что это не инвентарь члена партии
            if (inventoryOwner.GetComponentInParent<PartyManager>() == null)
            {
                openWorldInventories.Add(windowPair.Key);
            }
        }

        // 2. Если нашли хотя бы один, передаем весь список в PartyManager
        if (openWorldInventories.Count > 0)
        {
            partyManager.LootAllFromSources(openWorldInventories); // Обрати внимание на 's' в конце
        }
        else
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No containers are open.");
        }
    }

    public bool AreAnyWindowsOpen()
    {
        return openWindows.Count > 0;
    }

    public void CloseAllWindows()
    {
        if (openWindows.Count == 0) return;

        List<Inventory> keys = new List<Inventory>(openWindows.Keys);
        foreach (var inventoryKey in keys)
        {
            if (openWindows.TryGetValue(inventoryKey, out InventoryUI window))
            {
                Destroy(window.gameObject);
            }
        }
        openWindows.Clear();
    }
    // --- НАЧАЛО ИЗМЕНЕНИЯ ---
    public void HandleFastItemTransfer(InventoryItem itemToTransfer)
    {
        Inventory sourceInventory = itemToTransfer.GetOwnerInventory();
        if (sourceInventory == null) return;

        bool isSourceInParty = sourceInventory.GetComponentInParent<PartyManager>() != null;

        if (isSourceInParty)
        {
            // Случай: Из инвентаря партии -> в мир или другому члену партии
            TransferFromParty(itemToTransfer, sourceInventory);
        }
        else
        {
            // Случай: Из мирового контейнера -> в инвентарь партии
            TransferToParty(itemToTransfer, sourceInventory);
        }
    }

    private void TransferToParty(InventoryItem item, Inventory sourceInventory)
    {
        // Собираем список всех членов партии в правильном порядке для получения лута
        List<CharacterStats> receivers = new List<CharacterStats>();
        // Сначала активный член, если он есть
        if (partyManager.ActiveMember != null && !partyManager.ActiveMember.IsDead)
        {
            receivers.Add(partyManager.ActiveMember);
        }
        // Затем все остальные по порядку
        foreach (var member in partyManager.partyMembers)
        {
            if (member != null && !member.IsDead && !receivers.Contains(member))
            {
                receivers.Add(member);
            }
        }

        // Пытаемся передать предмет
        foreach (var member in receivers)
        {
            var targetInventory = member.GetComponent<Inventory>();
            if (targetInventory != null && targetInventory.AddItem(item.itemData, item.quantity))
            {
                // Успех, удаляем предмет из источника
                sourceInventory.RemoveItem(item);
                return; // Выходим
            }
        }

        // Если дошли сюда, значит, никому не удалось передать предмет
        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space in party inventories.");
    }

    private void TransferFromParty(InventoryItem item, Inventory sourceInventory)
    {
        // Ищем открытые мировые контейнеры
        var openWorldInventories = openWindows.Keys
            .Where(inv => inv.GetComponentInParent<PartyManager>() == null)
            .ToList();

        // Правило 2.2: Если открыт ровно один мировой контейнер, переносим в него
        if (openWorldInventories.Count == 1)
        {
            var targetInventory = openWorldInventories[0];
            if (targetInventory.AddItem(item.itemData, item.quantity))
            {
                sourceInventory.RemoveItem(item);
            }
            else
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space in container.");
            }
            return;
        }

        // Правило 2.3: Если мировых контейнеров нет, переносим активному персонажу
        if (openWorldInventories.Count == 0 && partyManager.ActiveMember != null)
        {
            var activeMemberInventory = partyManager.ActiveMember.GetComponent<Inventory>();
            // Передаем только если это не мы сами
            if (activeMemberInventory != sourceInventory)
            {
                if (activeMemberInventory.AddItem(item.itemData, item.quantity))
                {
                    sourceInventory.RemoveItem(item);
                }
                else
                {
                    FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Active member has no space.");
                }
            }
        }
    }
    // --- КОНЕЦ ИЗМЕНЕНИЯ ---
        // --- НАЧАЛО ИЗМЕНЕНИЯ ---
public void HandleDirectItemTransfer(InventoryItem item, int targetCharacterIndex)
    {
        Inventory sourceInventory = item.GetOwnerInventory();
        if (sourceInventory == null) return;

        if (targetCharacterIndex < 0 || targetCharacterIndex >= partyManager.partyMembers.Count) return;

        var targetCharacter = partyManager.partyMembers[targetCharacterIndex];
        if (targetCharacter == null || targetCharacter.IsDead)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Target character is unavailable.");
            return;
        }

        var targetInventory = targetCharacter.GetComponent<Inventory>();
        if (targetInventory == null) return;

        // --- НАЧАЛО ИЗМЕНЕНИЯ ---

        // Если это один и тот же инвентарь, используем новую логику
        if (sourceInventory == targetInventory)
        {
            // Мы не удаляем предмет. Мы пытаемся "добавить" его, игнорируя его же при поиске стаков.
            // Если он найдет другой стак, он объединится. Если нет, он найдет первую свободную ячейку и переместится туда.
            if (targetInventory.AddItem(item.itemData, item.quantity, item))
            {
                // Успех. AddItem сам вызовет OnInventoryChanged.
            }
            else
            {
                // Этого не должно происходить, так как предмет уже в инвентаре,
                // но на всякий случай оставим фидбек.
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Could not rearrange item.");
            }
        }
        else // Если инвентари разные, используем старую, работающую логику
        {
            if (targetInventory.AddItem(item.itemData, item.quantity))
            {
                sourceInventory.RemoveItem(item);
            }
            else
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage($"{targetCharacter.name} has no space.");
            }
        }
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---
    }
}