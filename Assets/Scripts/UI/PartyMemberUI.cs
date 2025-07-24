using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PartyMemberUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI experienceText;
    public TextMeshProUGUI attributesText;
    public Image highlightImage; // Для подсветки активного
    public Image stateIndicatorImage; // Для индикации Ready/Recovery

    [Header("Настройки")]
    public Color readyColor = Color.green;
    public Color recoveryColor = Color.gray;

    private CharacterStats linkedStats;
    private CharacterActionController linkedActionController;
    private Color originalHighlightColor;

    void Awake()
    {
        if (highlightImage != null)
        {
            originalHighlightColor = highlightImage.color;
        }
    }
    
    void OnDestroy()
    {
        if (linkedStats != null)
        {
            // Отписываемся от всех событий
            linkedStats.onHealthChanged.RemoveListener(UpdateHealthUI);
            linkedStats.onExperienceChanged -= UpdateExperienceUI;
            linkedStats.onLevelUp -= UpdateLevelUI;
            linkedStats.onAttributesChanged -= UpdateAttributesUI;
        }
        if (linkedActionController != null)
        {
            linkedActionController.OnStateChanged -= UpdateActionStateUI;
        }
    }

    public void Setup(CharacterStats statsToLink)
    {
        linkedStats = statsToLink;
        if (linkedStats == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        linkedActionController = linkedStats.GetComponent<CharacterActionController>();

        // Подписка на события
        linkedStats.onHealthChanged.AddListener(UpdateHealthUI);
        linkedStats.onExperienceChanged += UpdateExperienceUI;
        linkedStats.onLevelUp += UpdateLevelUI;
        linkedStats.onAttributesChanged += UpdateAttributesUI;
        
        if (linkedActionController != null)
        {
            linkedActionController.OnStateChanged += UpdateActionStateUI;
        }

        UpdateAllUI();
    }
    
    public CharacterStats GetLinkedStats() { return linkedStats; }

    public void SetHighlight(bool isHighlighted, Color highlightColor)
    {
        if (highlightImage == null) return;
        highlightImage.color = isHighlighted ? highlightColor : originalHighlightColor;
    }
    
    // --- НОВЫЙ МЕТОД ---
    private void UpdateActionStateUI(CharacterActionController.ActionState newState)
    {
        if (stateIndicatorImage == null) return;
        
        stateIndicatorImage.color = (newState == CharacterActionController.ActionState.Ready) ? readyColor : recoveryColor;
    }

    private void UpdateAllUI()
    {
        if (linkedStats == null) return;
        UpdateHealthUI(linkedStats.currentHealth, linkedStats.maxHealth);
        UpdateLevelUI(linkedStats.level);
        UpdateAttributesUI();
        if (linkedActionController != null)
        {
            UpdateActionStateUI(linkedActionController.CurrentState);
        }
    }

    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (healthText == null || linkedStats == null) return;
        healthText.text = $"{linkedStats.gameObject.name}\nHP: {currentHealth} / {maxHealth}";
    }

    private void UpdateExperienceUI(int currentExperience, int experienceToNextLevel)
    {
        if (experienceText == null || linkedStats == null) return;
        experienceText.text = $"Lvl: {linkedStats.level} (XP: {currentExperience} / {experienceToNextLevel})";
    }

    private void UpdateLevelUI(int newLevel) // newLevel передается, но в основном используется для триггера
    {
        if (linkedStats == null) return;
        // Обновляем текст опыта, так как он содержит информацию об уровне
        UpdateExperienceUI(linkedStats.experience, linkedStats.experienceToNextLevel);
        // Атрибуты также могли измениться (например, maxHealth из-за Body),
        // или просто для консистентности при левелапе
        UpdateAttributesUI();
    }

    private void UpdateAttributesUI()
    {
        if (attributesText == null || linkedStats == null) return;

        string attrSummary = "Attributes:\n";
        attrSummary += $"Body: {linkedStats.CurrentBody} (Base: {linkedStats.baseBody})\n";
        attrSummary += $"Mind: {linkedStats.CurrentMind} (Base: {linkedStats.baseMind})\n";
        attrSummary += $"Spirit: {linkedStats.CurrentSpirit} (Base: {linkedStats.baseSpirit})\n";
        attrSummary += $"Agility: {linkedStats.CurrentAgility} (Base: {linkedStats.baseAgility})\n";
        attrSummary += $"Prof.: {linkedStats.CurrentProficiency} (Base: {linkedStats.baseProficiency})";
        attributesText.text = attrSummary;
    }
}