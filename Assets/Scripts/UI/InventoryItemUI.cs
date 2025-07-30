using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Управляет визуальным представлением одного предмета в сетке инвентаря.
/// Обрабатывает весь ввод, связанный с предметом (клик, перетаскивание, наведение).
/// </summary>
public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                                            IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Ссылки")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;

    // Кэшированные компоненты и ссылки
    private InventoryItem linkedItem;
    private InventoryGridUI parentGrid;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Transform originalParent;
    private bool isEquipped = false;
    private bool isMouseOver;
    private Dictionary<KeyCode, int> numpadKeymap;

    #region Initialization
    private void Awake()
    {
        // Инициализируем карту клавиш для быстрой передачи предметов
        numpadKeymap = new Dictionary<KeyCode, int>
        {
            {KeyCode.Keypad1, 0}, {KeyCode.Keypad2, 1}, {KeyCode.Keypad3, 2},
            {KeyCode.Keypad4, 3}, {KeyCode.Keypad5, 4}, {KeyCode.Keypad6, 5}
        };
    }

    /// <summary>
    /// Инициализирует UI-элемент данными о предмете и родительской сеткой.
    /// </summary>
    public void Initialize(InventoryItem item, InventoryGridUI grid)
    {
        linkedItem = item;
        parentGrid = grid;
        linkedItem.SetOwner(grid.GetLinkedInventory());

        // Кэшируем компоненты для производительности
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        rootCanvas = GetComponentInParent<Canvas>();

        UpdateVisuals();
    }
    /// <summary>
    /// Специальный метод инициализации для предметов, отображаемых в слотах экипировки.
    /// </summary>
    public void InitializeAsEquipped(InventoryItem item)
    {
        isEquipped = true;
        linkedItem = item;
        parentGrid = null;

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        rootCanvas = GetComponentInParent<Canvas>();

        itemIcon.sprite = item.ItemData.Icon;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        quantityText.gameObject.SetActive(false);
    }

    public InventoryItem GetLinkedItem() => linkedItem;
    #endregion

    #region Update & Visuals
    private void Update()
    {
        // Обрабатываем ввод только когда курсор находится над этим элементом
        if (isMouseOver)
        {
            HandleTooltipInput();
            HandleDirectTransferInput();
        }
    }

    private void UpdateVisuals()
    {
        if (linkedItem?.ItemData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        var itemData = linkedItem.ItemData;
        itemIcon.sprite = itemData.Icon;

        // Настраиваем размер UI-элемента в соответствии с размером предмета в сетке
        var gridLayout = parentGrid.GetGridLayoutGroup();
        if (gridLayout != null)
        {
            float width = itemData.GridWidth * gridLayout.cellSize.x + (itemData.GridWidth - 1) * gridLayout.spacing.x;
            float height = itemData.GridHeight * gridLayout.cellSize.y + (itemData.GridHeight - 1) * gridLayout.spacing.y;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        // Отображаем количество, только если предмет стакается
        bool showQuantity = linkedItem.Quantity > 1;
        quantityText.gameObject.SetActive(showQuantity);
        if (showQuantity)
        {
            quantityText.text = linkedItem.Quantity.ToString();
        }
    }
    #endregion

    #region Pointer Handlers (Interfaces)
    public void OnPointerEnter(PointerEventData eventData)
    {
        isMouseOver = true;
        parentGrid?.OnPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isMouseOver = false;
        TooltipManager.Instance?.HideTooltip();
        parentGrid?.OnPointerExit(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (eventData.clickCount == 2)
        {
            HandleDoubleClick();
            return;
        }
        if (isEquipped) return;

        bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (altPressed)
        {
            HandleSplitStack(ctrlPressed);
            return;
        }

        if (shiftPressed)
        {
            HandleFastTransfer();
            return;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            eventData.pointerDrag = null;
            return;
        }
        originalParent = transform.parent;
        transform.SetParent(rootCanvas.transform);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(originalParent);

        if (isEquipped)
        {
            canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(false);
        }
        else
        {
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = parentGrid.GetPositionForItem(linkedItem);
            canvasGroup.blocksRaycasts = true;
        }
        HandleDrop(eventData);
    }
    #endregion

    #region Input Logic Implementation
    private void HandleTooltipInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            TooltipManager.Instance?.ShowTooltip(linkedItem.ItemData);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            TooltipManager.Instance?.HideTooltip();
        }
    }

    private void HandleDirectTransferInput()
    {
        foreach (var pair in numpadKeymap)
        {
            if (Input.GetKeyDown(pair.Key))
            {
                FindObjectOfType<InventoryUIManager>()?.HandleDirectItemTransfer(linkedItem, pair.Value);
                break;
            }
        }
    }

    private void HandleDoubleClick()
    {
        var ownerInventory = GetOwnerInventoryFromContext();
        if (ownerInventory == null) return;

        if (ownerInventory.TryGetComponent<CharacterEquipment>(out var equipment))
        {
            if (isEquipped)
            {
                // Если предмет уже экипирован, снимаем его
                var slotType = GetSlotTypeFromItem(linkedItem, equipment);
                if (slotType.HasValue)
                {
                    equipment.Unequip(slotType.Value);
                }
            }
            else
            {
                // Если предмет в инвентаре, экипируем его
                equipment.AutoEquip(linkedItem, ownerInventory);
            }
        }
    }


    private void HandleSplitStack(bool isCtrlPressed)
    {
        var ownerInventory = parentGrid.GetLinkedInventory();
        if (isCtrlPressed)
        {
            ownerInventory.SplitStack(linkedItem, 1); // Ctrl+Alt+Click - отделить 1
        }
        else
        {
            ownerInventory.SplitStack(linkedItem); // Alt+Click - разделить пополам
        }
    }

    private void HandleFastTransfer()
    {
        FindObjectOfType<InventoryUIManager>()?.HandleFastItemTransfer(linkedItem);
    }

    private void HandleDrop(PointerEventData eventData)
    {
        if (isEquipped)
        {
            var equipment = linkedItem.GetOwnerInventory()?.GetComponent<CharacterEquipment>();
            if (equipment == null) return;

            var targetGridUI = FindComponentInRaycast<InventoryGridUI>(eventData);
            if (targetGridUI != null)
            {
                var gridLayout = targetGridUI.GetGridLayoutGroup();
                if (gridLayout == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetGridUI.transform as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint);

                int x = Mathf.FloorToInt(localPoint.x / (gridLayout.cellSize.x + gridLayout.spacing.x));
                int y = Mathf.FloorToInt(-localPoint.y / (gridLayout.cellSize.y + gridLayout.spacing.y));

                var slotType = GetSlotTypeFromItem(linkedItem, equipment);
                if (slotType.HasValue)
                {
                    equipment.Unequip(slotType.Value, x, y);
                }
            }
        }
        else
        {
            var sourceInventory = linkedItem.GetOwnerInventory();
            if (sourceInventory == null) return;

            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);

            // Приоритет 1: raycast на другой предмет (для стака)
            foreach (var result in raycastResults)
            {
                var targetItemUI = result.gameObject.GetComponentInParent<InventoryItemUI>();
                if (targetItemUI != null && targetItemUI != this)
                {
                    sourceInventory.HandleDropOntoItem(linkedItem, targetItemUI.GetLinkedItem());
                    return;
                }
            }

            // Приоритет 2: raycast на сетку инвентаря
            foreach (var result in raycastResults)
            {
                var targetGridUI = result.gameObject.GetComponentInParent<InventoryGridUI>();
                if (targetGridUI != null)
                {
                    var gridLayout = targetGridUI.GetGridLayoutGroup();
                    // Проверяем, что gridLayout не null, чтобы избежать ошибок
                    if (gridLayout == null) continue;

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(targetGridUI.transform as RectTransform, eventData.position, eventData.pressEventCamera, out var localPoint);

                    int x = Mathf.FloorToInt(localPoint.x / (gridLayout.cellSize.x + gridLayout.spacing.x));
                    int y = Mathf.FloorToInt(-localPoint.y / (gridLayout.cellSize.y + gridLayout.spacing.y));

                    sourceInventory.HandleDropOntoGrid(linkedItem, targetGridUI.GetLinkedInventory(), x, y);
                    return;
                }
            }
        }
    }
    private EquipmentSlotType? GetSlotTypeFromItem(InventoryItem item, CharacterEquipment equipment)
    {
        // Проходим по всем возможным единичным флагам
        foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
        {
            // Пропускаем None и комбинированные флаги
            if (slot == EquipmentSlotType.None || slot == EquipmentSlotType.Hands) continue;

            if (equipment.GetItemInSlot(slot) == item)
            {
                return slot;
            }
        }
        return null;
    }
    private T FindComponentInRaycast<T>(PointerEventData eventData) where T : class
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var result in results)
        {
            var component = result.gameObject.GetComponentInParent<T>();
            if (component != null) return component;
        }
        return null; 
    }

    private Inventory GetOwnerInventoryFromContext()
    {
        if (isEquipped)
        {
            return linkedItem.GetOwnerInventory();
        }
        else
        {

            return parentGrid.GetLinkedInventory();
        }
    }
    #endregion
}