using UnityEngine;
using TMPro;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private RectTransform rectTransform;

    void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        // Изначально тултип скрыт
        gameObject.SetActive(false);
    }

void Update()
{
    // Тултип следует за курсором
    Vector2 newPosition = Input.mousePosition;

    // Добавляем небольшое смещение, чтобы курсор был в углу, а не по центру
    newPosition.x += rectTransform.sizeDelta.x / 2;
    newPosition.y -= rectTransform.sizeDelta.y / 2;

    transform.position = newPosition;
}
    
    public void Show(ItemData itemData)
    {
        if (itemData == null) return;
        
        nameText.text = itemData.itemName;
        statsText.text = $"Weight: {itemData.weight} kg | Size: {itemData.gridWidth}x{itemData.gridHeight}";
        descriptionText.text = itemData.description;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}