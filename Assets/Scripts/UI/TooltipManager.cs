using UnityEngine;

/// <summary>
/// Singleton для управления отображением всплывающих подсказок (Tooltip).
/// </summary>
public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private GameObject tooltipPrefab;
    private TooltipUI currentTooltip;

    private void Awake()
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

    /// <summary>
    /// Показывает подсказку с информацией из ItemData.
    /// </summary>
    public void ShowTooltip(ItemData itemData)
    {
        if (currentTooltip == null)
        {
            var tooltipGO = Instantiate(tooltipPrefab, transform);
            currentTooltip = tooltipGO.GetComponent<TooltipUI>();
        }
        currentTooltip.Show(itemData);
    }

    /// <summary>
    /// Скрывает активную подсказку.
    /// </summary>
    public void HideTooltip()
    {
        currentTooltip?.Hide();
    }
}