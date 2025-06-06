using UnityEngine;
using System.Collections.Generic;
using TMPro; // Для TextMeshPro

public class PartyUIManager : MonoBehaviour
{
    [Header("Ссылки на префабы и контейнеры")]
    [Tooltip("Префаб UI-слота для отображения информации о члене партии (здоровье, опыт, атрибуты).")]
    public GameObject partyMemberSlotPrefab;
    [Tooltip("Объект-контейнер в UI, куда будут добавляться слоты информации о членах партии.")]
    public Transform partyMemberInfoContainer;

    [Header("Источник данных о партии")]
    public PartyManager partyManager;

    [Header("UI для инвентаря партии")]
    [Tooltip("Текстовые поля для отображения сводки инвентаря каждого члена партии. Количество должно соответствовать макс. размеру партии.")]
    public List<TextMeshProUGUI> memberInventorySummaryTexts = new List<TextMeshProUGUI>();

    // Внутренние списки для управления созданными UI элементами и подписками
    private List<PartyMemberUI> memberInfoUISlots = new List<PartyMemberUI>(); // Для слотов здоровья/опыта/атрибутов
    private List<Inventory> trackedInventories = new List<Inventory>();      // Для инвентарей, на которые подписаны

    void Start()
    {
        if (partyManager == null)
        {
            partyManager = FindObjectOfType<PartyManager>();
            if (partyManager == null)
            {
                Debug.LogError("PartyUIManager: PartyManager не найден в сцене! UI не будет инициализирован.", this);
                return;
            }
        }

        ValidatePrerequisites(); // Проверка остальных необходимых ссылок

        CreatePartyInfoSlotsUI(); // Создаем UI для отображения здоровья, опыта, атрибутов
        SetupInventorySummaryUI();  // Настраиваем UI для отображения сводки инвентарей
    }

    private void ValidatePrerequisites()
    {
        if (partyMemberSlotPrefab == null)
        {
            Debug.LogError("PartyUIManager: Не назначен префаб слота информации о члене партии (partyMemberSlotPrefab)!", this);
            enabled = false; // Отключаем менеджер, если основное не настроено
            return;
        }
        if (partyMemberInfoContainer == null)
        {
            Debug.LogError("PartyUIManager: Не назначен контейнер для слотов информации (partyMemberInfoContainer)!", this);
            enabled = false;
            return;
        }

        if (memberInventorySummaryTexts.Count == 0)
        {
            Debug.LogWarning("PartyUIManager: Не назначены текстовые поля для сводки инвентаря (memberInventorySummaryTexts). Отображение инвентаря не будет работать.", this);
        }
        else if (partyManager != null && partyManager.partyMembers.Count > memberInventorySummaryTexts.Count)
        {
            Debug.LogWarning($"PartyUIManager: Количество текстовых полей для инвентаря ({memberInventorySummaryTexts.Count}) меньше, чем членов партии ({partyManager.partyMembers.Count}). Инвентарь не для всех будет отображен.", this);
        }
    }

    private void CreatePartyInfoSlotsUI()
    {
        // Очищаем старые UI слоты информации о членах партии
        foreach (Transform child in partyMemberInfoContainer)
        {
            Destroy(child.gameObject);
        }
        memberInfoUISlots.Clear();

        if (partyManager == null || partyManager.partyMembers.Count == 0) return;

        foreach (CharacterStats memberStats in partyManager.partyMembers)
        {
            if (memberStats == null) continue; 

            GameObject slotInstance = Instantiate(partyMemberSlotPrefab, partyMemberInfoContainer);
            PartyMemberUI memberUIComponent = slotInstance.GetComponent<PartyMemberUI>();

            if (memberUIComponent != null)
            {
                memberUIComponent.Setup(memberStats); 
                memberInfoUISlots.Add(memberUIComponent);

                // ----- НОВОЕ: Добавляем и настраиваем UIPartyMemberTargetDetector -----
                UIPartyMemberTargetDetector targetDetector = slotInstance.GetComponent<UIPartyMemberTargetDetector>();
                if (targetDetector == null) // Если его нет на префабе, добавляем
                {
                    targetDetector = slotInstance.AddComponent<UIPartyMemberTargetDetector>();
                }
                targetDetector.associatedMemberStats = memberStats;
                // --------------------------------------------------------------------
            }
            else
            {
                Debug.LogError($"PartyUIManager: Префаб слота {partyMemberSlotPrefab.name} не содержит скрипт PartyMemberUI!", this);
                Destroy(slotInstance); 
            }
        }
    }

    private void SetupInventorySummaryUI()
    {
        // Отписываемся от событий предыдущих отслеживаемых инвентарей
        foreach (var inv in trackedInventories)
        {
            if (inv != null) inv.OnInventoryChanged -= HandleAnyInventoryChanged;
        }
        trackedInventories.Clear();

        if (partyManager == null || partyManager.partyMembers.Count == 0) return;

        for (int i = 0; i < partyManager.partyMembers.Count; i++)
        {
            // Проверяем, есть ли текстовое поле UI для инвентаря этого члена партии
            if (i < memberInventorySummaryTexts.Count && memberInventorySummaryTexts[i] != null)
            {
                CharacterStats member = partyManager.partyMembers[i];
                if (member != null)
                {
                    Inventory memberInventory = member.GetComponent<Inventory>();
                    if (memberInventory != null)
                    {
                        trackedInventories.Add(memberInventory);
                        memberInventory.OnInventoryChanged += HandleAnyInventoryChanged; // Все подписываются на один обработчик
                        UpdateSpecificInventorySummaryUI(memberInventory, memberInventorySummaryTexts[i]); // Первичное обновление
                    }
                    else
                    {
                        // Если у члена партии нет инвентаря, отображаем это
                        memberInventorySummaryTexts[i].text = $"{member.gameObject.name}:\nИнвентарь отсутствует";
                    }
                }
                // Если member == null, но текстовое поле есть, можно его очистить или скрыть
                else if (memberInventorySummaryTexts[i] != null)
                {
                     memberInventorySummaryTexts[i].text = ""; // Очищаем, если члена партии нет
                }
            }
            else if (i < memberInventorySummaryTexts.Count && memberInventorySummaryTexts[i] == null)
            {
                 Debug.LogWarning($"PartyUIManager: Текстовое поле для инвентаря члена партии {i} не назначено (null).", this);
            }
        }
    }

    // Обработчик события изменения ЛЮБОГО из отслеживаемых инвентарей
    private void HandleAnyInventoryChanged()
    {
        // Перебираем все отслеживаемые инвентари и обновляем соответствующий им UI
        for (int i = 0; i < trackedInventories.Count; i++)
        {
            if (i < memberInventorySummaryTexts.Count && // Есть ли UI элемент для этого индекса
                trackedInventories[i] != null &&        // Есть ли отслеживаемый инвентарь для этого индекса
                memberInventorySummaryTexts[i] != null) // Назначен ли сам UI Text элемент
            {
                UpdateSpecificInventorySummaryUI(trackedInventories[i], memberInventorySummaryTexts[i]);
            }
        }
    }

    // Обновление UI для конкретного инвентаря и его текстового поля
    private void UpdateSpecificInventorySummaryUI(Inventory inventoryToDisplay, TextMeshProUGUI uiTextElement)
    {
        if (inventoryToDisplay == null || uiTextElement == null) return;

        string summary = $"Инвентарь ({inventoryToDisplay.gameObject.name}):\n";
        summary += $"Вес: {inventoryToDisplay.CurrentWeight:F1}/{inventoryToDisplay.MaxWeightCapacity} кг\n";
        summary += $"Место: {inventoryToDisplay.CurrentGridOccupancy}/{inventoryToDisplay.totalGridCapacity} клеток\n";
        summary += "Предметы:\n";

        if (inventoryToDisplay.items.Count > 0)
        {
            foreach (var itemSlot in inventoryToDisplay.items)
            {
                if (itemSlot.itemData != null)
                {
                    summary += $"- {itemSlot.itemData.itemName} x{itemSlot.quantity} (Вес: {itemSlot.itemData.weight * itemSlot.quantity:F1})\n";
                }
            }
        }
        else
        {
            summary += "- Пусто\n";
        }
        uiTextElement.text = summary;
    }

    // Метод для принудительного обновления всего UI, управляемого этим менеджером (например, после загрузки игры)
    public void RefreshAllPartyMemberUIs()
    {
        Debug.Log("PartyUIManager: Refreshing all party member UIs.");
        CreatePartyInfoSlotsUI();  // Пересоздаст слоты здоровья/опыта/атрибутов и их подписки
        SetupInventorySummaryUI(); // Пересоздаст подписки для инвентарей и обновит их текст
    }

    void OnDestroy()
    {
        // Отписываемся от событий инвентарей
        foreach (var inv in trackedInventories)
        {
            if (inv != null)
            {
                inv.OnInventoryChanged -= HandleAnyInventoryChanged;
            }
        }
        // PartyMemberUI сами отписываются от событий CharacterStats в своем OnDestroy
    }
}