using UnityEngine;
using TMPro;

/// <summary>
/// Управляет окном инвентаря, объединяя сетку предметов, панель экипировки и информацию о персонаже.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI weightInfoText;
    [SerializeField] private InventoryGridUI inventoryGridUI;
    [SerializeField] private EquipmentPanelUI equipmentPanelUI;

    private Inventory linkedInventory;
    private CharacterStats linkedStats;

    /// <summary>
    /// Инициализирует окно инвентаря, связывая его с логикой инвентаря и экипировки сущности.
    /// </summary>
    public void Initialize(Inventory inventoryToLink, string overrideName = null)
    {
        linkedInventory = inventoryToLink;
        linkedStats = inventoryToLink.GetComponent<CharacterStats>();
        var linkedEquipment = inventoryToLink.GetComponent<CharacterEquipment>();

        linkedInventory.OnInventoryChanged += UpdateWeightText;
        if (linkedEquipment != null)
        {
            linkedEquipment.OnEquipmentChanged += (slot, item) => UpdateWeightText();
        }
        inventoryGridUI.Initialize(inventoryToLink);

        bool hasEquipment = linkedEquipment != null;
        equipmentPanelUI.gameObject.SetActive(hasEquipment);
        if (hasEquipment)
        {
            equipmentPanelUI.Initialize(linkedEquipment);
        }

        characterNameText.text = overrideName ?? linkedInventory.gameObject.name;

        UpdateWeightText();
        gameObject.SetActive(true);
    }
    /// <summary>
    /// Закрывает это окно инвентаря. Вызывается по нажатию на UI кнопку.
    /// </summary>
        public void CloseWindow()
    {
        // Вместо уничтожения, мы просим менеджера закрыть окно, связанное с нашим инвентарем
        FindObjectOfType<InventoryUIManager>()?.CloseWindowFor(linkedInventory);
    }
    private void OnDestroy()
    {
        if (linkedInventory != null)
        {
            linkedInventory.OnInventoryChanged -= UpdateWeightText;
        }
    }

    private void UpdateWeightText()
    {
        if (linkedInventory == null) return;

        if (linkedStats != null)
        {
            weightInfoText.text = $"{linkedStats.TotalCarriedWeight:F1} / {linkedStats.CalculatedMaxCarryWeight:F1} kg";
        }
        else
        {
            weightInfoText.text = $"{linkedInventory.CurrentWeight:F1} / {linkedInventory.MaxWeightCapacity:F1} kg";
        }
    }
}