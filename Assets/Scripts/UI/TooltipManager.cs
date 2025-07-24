using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private GameObject tooltipPrefab;
    private TooltipUI currentTooltip;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void ShowTooltip(ItemData itemData)
    {
        if (currentTooltip == null)
        {
            var tooltipGO = Instantiate(tooltipPrefab, transform); // Создаем внутри менеджера
            currentTooltip = tooltipGO.GetComponent<TooltipUI>();
        }
        currentTooltip.Show(itemData);
    }

    public void HideTooltip()
    {
        if (currentTooltip != null)
        {
            currentTooltip.Hide();
        }
    }
}