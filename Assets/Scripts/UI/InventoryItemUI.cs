using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, 
                                            IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Ссылки")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;

    private InventoryItem linkedItem;
        public InventoryItem GetLinkedItem()
    {
        return linkedItem;
    }
    private InventoryGridUI parentGrid;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Transform originalParent;
 private bool isMouseOver = false;
    // --- НАЧАЛО ИЗМЕНЕНИЯ ---
    private Dictionary<KeyCode, int> numpadKeymap;

    void Awake()
    {
        // Инициализируем карту клавиш один раз
        numpadKeymap = new Dictionary<KeyCode, int>
        {
            {KeyCode.Keypad1, 0}, {KeyCode.Keypad2, 1}, {KeyCode.Keypad3, 2},
            {KeyCode.Keypad4, 3}, {KeyCode.Keypad5, 4}, {KeyCode.Keypad6, 5}
        };
    }
    // --- КОНЕЦ ИЗМЕНЕНИЯ ---

    void Update()
    {
        if (isMouseOver)
        {
            // Логика для ПКМ
            if (Input.GetMouseButtonDown(1)) { TooltipManager.Instance.ShowTooltip(linkedItem.itemData); }
            else if (Input.GetMouseButtonUp(1)) { TooltipManager.Instance.HideTooltip(); }

            // --- НАЧАЛО ИЗМЕНЕНИЯ ---
            // Логика для Num-клавиш
            foreach (var pair in numpadKeymap)
            {
                if (Input.GetKeyDown(pair.Key))
                {
                    var invUIManager = FindObjectOfType<InventoryUIManager>();
                    if (invUIManager != null)
                    {
                        invUIManager.HandleDirectItemTransfer(linkedItem, pair.Value);
                    }
                    break; // Прерываем цикл, чтобы не обрабатывать несколько нажатий за кадр
                }
            }
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
        }
    }
     
    public void Initialize(InventoryItem item, InventoryGridUI grid)
    {
        linkedItem = item;
        parentGrid = grid;
linkedItem.SetOwner(grid.GetLinkedInventory());
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();

        UpdateVisuals();
    }

private void UpdateVisuals()
{
    if (linkedItem?.itemData == null)
    {
        gameObject.SetActive(false);
        return;
    }

    itemIcon.sprite = linkedItem.itemData.icon;
    
    var gridLayout = parentGrid.GetGridLayoutGroup();
    if (gridLayout != null)
    {
        float width = linkedItem.itemData.gridWidth * gridLayout.cellSize.x + (linkedItem.itemData.gridWidth - 1) * gridLayout.spacing.x;
        float height = linkedItem.itemData.gridHeight * gridLayout.cellSize.y + (linkedItem.itemData.gridHeight - 1) * gridLayout.spacing.y;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
    
    // --- ВОТ КЛЮЧЕВОЙ БЛОК ---
    // Показываем/скрываем текст количества
    if (linkedItem.quantity > 1)
    {
        quantityText.gameObject.SetActive(true);
        quantityText.text = linkedItem.quantity.ToString();
    }
    else
    {
        quantityText.gameObject.SetActive(false);
    }
}
        public void OnPointerEnter(PointerEventData eventData)
    {
        isMouseOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isMouseOver = false;
        // Также скрываем тултип, если мышь ушла с предмета, даже если кнопка еще зажата
        TooltipManager.Instance.HideTooltip();
    }
    #region Drag & Drop Handlers

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Нельзя перетаскивать левой кнопкой, если это не она
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Запоминаем исходного родителя (itemsContainer)
        originalParent = transform.parent;
        
        // Временно делаем предмет дочерним элементом самого канваса, чтобы он рисовался поверх всего
        transform.SetParent(rootCanvas.transform);
        transform.SetAsLastSibling(); // Гарантирует, что он будет самым верхним элементом UI

        // Делаем объект "прозрачным" для рейкастов, чтобы можно было определить, над какой ячейкой мы его отпустили
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        
        // Перемещаем объект вслед за курсором. `rectTransform.position` работает для всех режимов канваса.
        rectTransform.position = eventData.position;
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        // Возвращаем UI элемент в его исходный контейнер на время обработки,
        // чтобы избежать визуальных глитчей. Redraw() его потом пересоздаст.
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = parentGrid.GetPositionForItem(linkedItem);
        canvasGroup.blocksRaycasts = true;

        // 1. Ищем, бросили ли мы предмет НА ДРУГОЙ ПРЕДМЕТ
        InventoryItemUI targetItemUI = null;
        foreach (var result in eventData.hovered)
        {
            targetItemUI = result.GetComponentInParent<InventoryItemUI>();
            // Убедимся, что это не мы сами
            if (targetItemUI != null && targetItemUI != this)
            {
                break;
            }
            targetItemUI = null; // Если это мы сами, сбрасываем
        }

        // Если нашли целевой предмет, вызываем логику объединения
        if (targetItemUI != null)
        {
            Inventory sourceInventory = linkedItem.GetOwnerInventory();
            if(sourceInventory != null)
            {
                sourceInventory.HandleDropOntoItem(linkedItem, targetItemUI.linkedItem);
            }
            return; // Завершаем обработку
        }

        // 2. Если не бросили на предмет, ищем, бросили ли мы НА СЕТКУ
        InventoryGridUI targetGrid = null;
        foreach (var result in eventData.hovered)
        {
            targetGrid = result.GetComponentInParent<InventoryGridUI>();
            if (targetGrid != null) break;
        }

        // Если нашли целевую сетку, вызываем логику перемещения
        if (targetGrid != null)
        {
            // Вычисляем координаты ячейки под курсором
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetGrid.transform as RectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPoint);

            var gridLayout = targetGrid.GetGridLayoutGroup();
            int x = Mathf.FloorToInt(localPoint.x / (gridLayout.cellSize.x + gridLayout.spacing.x));
            int y = Mathf.FloorToInt(-localPoint.y / (gridLayout.cellSize.y + gridLayout.spacing.y));

            Inventory sourceInventory = linkedItem.GetOwnerInventory();
            Inventory targetInventory = targetGrid.GetLinkedInventory();

            if(sourceInventory != null && targetInventory != null)
            {
                sourceInventory.HandleDropOntoGrid(linkedItem, targetInventory, x, y);
            }
        }
        // Если не бросили ни на предмет, ни на сетку, ничего не делаем.
        // Предмет визуально уже вернулся на место.
    }
public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // --- НАЧАЛО ИЗМЕНЕНИЯ ---
        if (eventData.clickCount == 2)
        {
            var equipment = parentGrid.GetLinkedInventory().GetComponent<CharacterEquipment>();
            if (equipment != null)
            {
                // --- НАЧАЛО ИЗМЕНЕНИЯ ---
                // Теперь передаем и исходный инвентарь
                equipment.AutoEquip(linkedItem, parentGrid.GetLinkedInventory());
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
                return;
            }
        }
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---

        bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (altPressed)
        {
            if (ctrlPressed)
            {
                parentGrid.GetLinkedInventory().SplitStack(linkedItem, 1);
            }
            else
            {
                parentGrid.GetLinkedInventory().SplitStack(linkedItem);
            }
            return;
        }

        if (shiftPressed)
        {
            var invUIManager = FindObjectOfType<InventoryUIManager>();
            if (invUIManager != null)
            {
                invUIManager.HandleFastItemTransfer(linkedItem);
            }
        }
    }
    #endregion
}