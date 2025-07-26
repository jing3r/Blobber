using UnityEngine;
using TMPro;

/// <summary>
/// Управляет визуальным представлением одной всплывающей подсказки.
/// </summary>
public class TooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Vector2 offset = new Vector2(10, -10); // Смещение от курсора

    private void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        Hide();
    }

    private void LateUpdate()
    {
        FollowCursor();
    }

    /// <summary>
    /// Заполняет подсказку данными и отображает ее.
    /// </summary>
    public void Show(ItemData itemData)
    {
        if (itemData == null) return;
        
        nameText.text = itemData.ItemName;
        statsText.text = $"Weigth: {itemData.Weight} kg | Size: {itemData.GridWidth}x{itemData.GridHeight}";
        descriptionText.text = itemData.Description;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Скрывает подсказку.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    
    private void FollowCursor()
    {
        if (!gameObject.activeSelf) return;

        Vector2 mousePosition = Input.mousePosition;
        Vector2 pivot = new Vector2(mousePosition.x < Screen.width / 2f ? 0 : 1, 
                                    mousePosition.y < Screen.height / 2f ? 0 : 1);
        rectTransform.pivot = pivot;
        
        transform.position = mousePosition + new Vector2(offset.x * (pivot.x == 0 ? 1 : -1), offset.y * (pivot.y == 0 ? 1 : -1));
    }
}