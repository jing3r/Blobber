using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Управляет созданием, отображением и жизненным циклом всех окон инвентаря в игре.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    [Header("Префабы и контейнеры")]
    [SerializeField] private GameObject inventoryWindowPrefab;
    [SerializeField] private Transform partyInventoriesContainer;
    [SerializeField] private Transform worldInventoriesContainer;
    public static InventoryGridUI GridUnderMouse { get; private set; }
    public static void SetGridUnderMouse(InventoryGridUI grid)
    {
        GridUnderMouse = grid;
    }
    public static void ClearGridUnderMouse(InventoryGridUI grid)
    {
        // Очищаем, только если курсор действительно ушел с ЭТОЙ сетки
        if (GridUnderMouse == grid)
        {
            GridUnderMouse = null;
        }
    }
    private PartyManager partyManager;
    private readonly Dictionary<Inventory, InventoryUI> openWindows = new Dictionary<Inventory, InventoryUI>();

    private void Awake()
    {
        partyManager = FindObjectOfType<PartyManager>();
    }

    #region Window Management
    /// <summary>
    /// Открывает или закрывает окно инвентаря для указанного члена партии.
    /// </summary>
    public void TogglePartyMemberInventory(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= partyManager.PartyMembers.Count) return;
        
        var character = partyManager.PartyMembers[characterIndex];
        if (character != null && character.TryGetComponent<Inventory>(out var inventory))
        {
            ToggleWindowFor(inventory, partyInventoriesContainer, null);
        }
    }

    /// <summary>
    /// Открывает или закрывает окно инвентаря для мирового контейнера (например, трупа).
    /// </summary>
    public void ToggleCorpseInventoryWindow(Inventory corpseInventory, string windowTitle)
    {
        ToggleWindowFor(corpseInventory, worldInventoriesContainer, windowTitle);
    }
    
    /// <summary>
    /// Открывает или закрывает все окна инвентаря для членов партии.
    /// </summary>
    public void ToggleAllPartyWindows()
    {
        bool anyPartyWindowOpen = openWindows.Keys.Any(inv => inv.GetComponentInParent<PartyManager>() != null);

        if (anyPartyWindowOpen)
        {
            CloseAllWindows();
        }
        else
        {
            for (int i = 0; i < partyManager.PartyMembers.Count; i++)
            {
                TogglePartyMemberInventory(i);
            }
        }
    }
    
    /// <summary>
    /// Закрывает все открытые окна инвентаря.
    /// </summary>
    public void CloseAllWindows()
    {
        // Создаем копию ключей, так как словарь будет изменяться во время итерации
        foreach (var window in openWindows.Values.ToList())
        {
            Destroy(window.gameObject);
        }
        openWindows.Clear();
    }
    
    public bool AreAnyWindowsOpen() => openWindows.Count > 0;

    /// <summary>
    /// Закрывает конкретное окно инвентаря.
    /// </summary>
    public void CloseWindowFor(Inventory inventory)
    {
        if (openWindows.TryGetValue(inventory, out var windowToClose))
        {
            Destroy(windowToClose.gameObject);
            openWindows.Remove(inventory);
        }
    }
    private void ToggleWindowFor(Inventory inventory, Transform container, string overrideName)
    {
        if (inventory == null) return;

        if (openWindows.ContainsKey(inventory))
        {
            CloseWindowFor(inventory);
        }
        else
        {
            var windowGO = Instantiate(inventoryWindowPrefab, container);
            var inventoryUI = windowGO.GetComponent<InventoryUI>();
            inventoryUI.Initialize(inventory, overrideName);
            openWindows.Add(inventory, inventoryUI);
        }
    }
    #endregion
    
    #region Item Transfer Logic
    /// <summary>
    /// Собирает все предметы из всех открытых мировых контейнеров.
    /// </summary>
    public void TakeAllFromOpenContainer()
    {
        var openWorldInventories = openWindows.Keys
            .Where(inv => inv.GetComponentInParent<PartyManager>() == null)
            .ToList();

        if (openWorldInventories.Count > 0)
        {
            partyManager.LootAllFromSources(openWorldInventories);
        }
        else
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No containers are open.");
        }
    }
    
    /// <summary>
    /// Обрабатывает быстрое перемещение предмета (Shift+Click).
    /// </summary>
    public void HandleFastItemTransfer(InventoryItem itemToTransfer)
    {
        var sourceInventory = itemToTransfer.GetOwnerInventory();
        if (sourceInventory == null) return;

        bool isSourceInParty = sourceInventory.GetComponentInParent<PartyManager>() != null;
        if (isSourceInParty)
        {
            TransferFromParty(itemToTransfer, sourceInventory);
        }
        else
        {
            TransferToParty(itemToTransfer, sourceInventory);
        }
    }
    
    /// <summary>
    /// Обрабатывает прямую передачу предмета конкретному персонажу (NumPad-клик).
    /// </summary>
    public void HandleDirectItemTransfer(InventoryItem item, int targetCharacterIndex)
    {
        var sourceInventory = item.GetOwnerInventory();
        if (sourceInventory == null) return;
        if (targetCharacterIndex < 0 || targetCharacterIndex >= partyManager.PartyMembers.Count) return;

        var targetCharacter = partyManager.PartyMembers[targetCharacterIndex];
        if (targetCharacter == null || targetCharacter.IsDead)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("Target character is unavailable.");
            return;
        }

        var targetInventory = targetCharacter.GetComponent<Inventory>();
        if (targetInventory == null) return;
        
        // Используем перегрузку AddItem для корректной обработки перемещения внутри одного инвентаря
        var itemToIgnore = (sourceInventory == targetInventory) ? item : null;
        
        if (targetInventory.AddItem(item.ItemData, item.Quantity, itemToIgnore))
        {
            sourceInventory.RemoveItem(item);
        }
        else
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage($"{targetCharacter.name} has no space.");
        }
    }

    private void TransferToParty(InventoryItem item, Inventory sourceInventory)
    {
        var receivers = new List<CharacterStats>();
        if (partyManager.ActiveMember != null && !partyManager.ActiveMember.IsDead) receivers.Add(partyManager.ActiveMember);
        foreach (var member in partyManager.PartyMembers)
        {
            if (member != null && !member.IsDead && !receivers.Contains(member)) receivers.Add(member);
        }

        foreach (var member in receivers)
        {
            if (member.TryGetComponent<Inventory>(out var targetInv) && targetInv.AddItem(item.ItemData, item.Quantity))
            {
                sourceInventory.RemoveItem(item);
                return;
            }
        }
        
        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space in party inventories.");
    }

private void TransferFromParty(InventoryItem item, Inventory sourceInventory)
    {
        var openWorldInventories = openWindows.Keys.Where(inv => inv.GetComponentInParent<PartyManager>() == null).ToList();

        // Приоритет 1: Переместить в единственный открытый мировой контейнер.
        if (openWorldInventories.Count == 1)
        {
            var targetInv = openWorldInventories[0];
            if (targetInv.AddItem(item.ItemData, item.Quantity))
            {
                sourceInventory.RemoveItem(item);
            }
            else
            {
                FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No space in container.");
            }
            return;
        }

        // Если мировых контейнеров 0 или >1, пытаемся передать предмет другому члену партии.
        if (openWorldInventories.Count != 1)
        {
            CharacterStats targetCharacter = null;
            var sourceCharacter = sourceInventory.GetComponent<CharacterStats>();

            // Цель по умолчанию - активный персонаж, если это не сам источник.
            if (partyManager.ActiveMember != null && partyManager.ActiveMember != sourceCharacter)
            {
                targetCharacter = partyManager.ActiveMember;
            }
            else
            {
                // В противном случае, ищем первого доступного персонажа в партии, который не является источником.
                // Это создает предсказуемое "резервное" поведение.
                targetCharacter = partyManager.PartyMembers.FirstOrDefault(m => m != null && !m.IsDead && m != sourceCharacter);
            }
            
            if (targetCharacter != null)
            {
                var targetInventory = targetCharacter.GetComponent<Inventory>();
                if (targetInventory.AddItem(item.ItemData, item.Quantity))
                {
                    sourceInventory.RemoveItem(item);
                }
                else
                {
                    FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage($"{targetCharacter.name} has no space.");
                }
            }
            // Если подходящий персонаж не найден (например, в партии всего один), действие не выполняется.
        }
    }
    #endregion
}