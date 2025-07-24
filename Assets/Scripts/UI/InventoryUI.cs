using UnityEngine;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI weightInfoText;
    [SerializeField] private InventoryGridUI inventoryGridUI;
    [SerializeField] private EquipmentPanelUI equipmentPanelUI;

    [Header("Настройки инвентаря")]
    public int gridWidth = 6;

    public int gridHeight = 12;

    private Inventory linkedInventory;
    private CharacterEquipment linkedEquipment;

public void Initialize(Inventory inventoryToLink, string overrideName = null)
    {
        linkedInventory = inventoryToLink;
        linkedEquipment = inventoryToLink.GetComponent<CharacterEquipment>();
        linkedInventory.OnInventoryChanged += UpdateUI;
        inventoryGridUI.Initialize(inventoryToLink);
        if (equipmentPanelUI != null)
        {
            // Если у сущности есть экипировка - инициализируем.
            if (linkedEquipment != null) 
            {
                equipmentPanelUI.Initialize(linkedEquipment);
            }
            // Если нет - панель просто останется пустой/неинтерактивной
            // (в зависимости от того, как мы решим ее отображать для сундуков).
            // Пока что можно ничего не делать.
        }
        // Используем переданное имя или имя объекта по умолчанию
        characterNameText.text = overrideName ?? linkedInventory.gameObject.name;

        UpdateUI();
        gameObject.SetActive(true);
    }

private void UpdateUI()
{
    if (linkedInventory == null) return;
    // Имя обновлять не нужно, оно задается при инициализации. Обновляем только вес.
    weightInfoText.text = $"{linkedInventory.CurrentWeight:F1} / {linkedInventory.MaxWeightCapacity:F1} kg";
}

    private void OnDestroy()
    {
        // Важно отписаться, чтобы избежать утечек памяти
        if (linkedInventory != null)
        {
            linkedInventory.OnInventoryChanged -= UpdateUI;
        }
    }

    public void CloseWindow()
    {
        // Вместо уничтожения, мы просто выключаем окно.
        // Менеджер окон будет решать, когда его нужно уничтожить.
        gameObject.SetActive(false);
    }
}