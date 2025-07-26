using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/// <summary>
/// Отображает информацию об одном члене партии (здоровье, статы, состояние).
/// </summary>
public class PartyMemberUI : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI experienceText;
    [SerializeField] private TextMeshProUGUI attributesText;
    [SerializeField] private Image highlightImage;
    [SerializeField] private Image stateIndicatorImage;

    [Header("Цвета состояний")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color recoveryColor = Color.gray;

    private CharacterStats linkedStats;
    private CharacterActionController linkedActionController;
    private Color originalHighlightColor;
    private StringBuilder stringBuilder = new StringBuilder(128);

    private void Awake()
    {
        if (highlightImage != null)
        {
            originalHighlightColor = highlightImage.color;
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Настраивает UI-элемент для отображения данных конкретного персонажа.
    /// </summary>
    public void Setup(CharacterStats statsToLink)
    {
        UnsubscribeFromEvents(); // Отписываемся от старых событий на случай переиспользования
        
        linkedStats = statsToLink;
        if (linkedStats == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        linkedActionController = linkedStats.GetComponent<CharacterActionController>();
        SubscribeToEvents();
        UpdateAllUI();
    }
    
    /// <summary>
    /// Возвращает связанный с этим UI CharacterStats.
    /// </summary>
    public CharacterStats GetLinkedStats() => linkedStats;

    /// <summary>
    /// Включает или выключает подсветку активного персонажа.
    /// </summary>
    public void SetHighlight(bool isHighlighted)
    {
        if (highlightImage == null) return;
        highlightImage.enabled = isHighlighted;
    }
    
    private void SubscribeToEvents()
    {
        if (linkedStats != null)
        {
            linkedStats.onHealthChanged.AddListener(UpdateHealthUI);
            linkedStats.onExperienceChanged += UpdateExperienceUI;
            linkedStats.onAttributesChanged += UpdateAttributesUI;
        }
        if (linkedActionController != null)
        {
            linkedActionController.OnStateChanged += UpdateActionStateUI;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (linkedStats != null)
        {
            linkedStats.onHealthChanged.RemoveListener(UpdateHealthUI);
            linkedStats.onExperienceChanged -= UpdateExperienceUI;
            linkedStats.onAttributesChanged -= UpdateAttributesUI;
        }
        if (linkedActionController != null)
        {
            linkedActionController.OnStateChanged -= UpdateActionStateUI;
        }
    }
    
    private void UpdateAllUI()
    {
        if (linkedStats == null) return;
        UpdateHealthUI(linkedStats.currentHealth, linkedStats.maxHealth);
        UpdateExperienceUI(linkedStats.Experience, linkedStats.ExperienceToNextLevel);
        UpdateAttributesUI();
        if (linkedActionController != null)
        {
            UpdateActionStateUI(linkedActionController.CurrentState);
        }
    }

    private void UpdateActionStateUI(CharacterActionController.ActionState newState)
    {
        if (stateIndicatorImage == null) return;
        stateIndicatorImage.color = (newState == CharacterActionController.ActionState.Ready) ? readyColor : recoveryColor;
    }
    
    private void UpdateHealthUI(int current, int max)
    {
        if (healthText != null) healthText.text = $"{linkedStats.name}\nHP: {current} / {max}";
    }

    private void UpdateExperienceUI(int current, int max)
    {
        if (experienceText != null) experienceText.text = $"Lvl: {linkedStats.Level} (XP: {current} / {max})";
    }
    
    // Использование StringBuilder для оптимизации и предотвращения создания "мусорных" строк
    private void UpdateAttributesUI()
    {
        if (attributesText == null) return;

        stringBuilder.Clear();
        stringBuilder.AppendLine("Attributes:");
        stringBuilder.AppendLine($"Body: {linkedStats.CurrentBody} ({linkedStats.BaseBody})");
        stringBuilder.AppendLine($"Mind: {linkedStats.CurrentMind} ({linkedStats.BaseMind})");
        stringBuilder.AppendLine($"Spirit: {linkedStats.CurrentSpirit} ({linkedStats.BaseSpirit})");
        stringBuilder.AppendLine($"Agility: {linkedStats.CurrentAgility} ({linkedStats.BaseAgility})");
        stringBuilder.Append($"Prof.: {linkedStats.CurrentProficiency} ({linkedStats.BaseProficiency})");

        attributesText.text = stringBuilder.ToString();
    }
}