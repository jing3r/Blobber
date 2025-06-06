using UnityEngine;
using TMPro;

public class PartyMemberUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Текстовое поле для отображения имени и здоровья.")]
    public TextMeshProUGUI healthText;
    [Tooltip("Текстовое поле для отображения уровня и опыта.")]
    public TextMeshProUGUI experienceText;
    [Tooltip("Текстовое поле для отображения атрибутов.")]
    public TextMeshProUGUI attributesText;

    private CharacterStats linkedStats;

    public void Setup(CharacterStats statsToLink)
    {
        linkedStats = statsToLink;
        if (linkedStats == null)
        {
            Debug.LogError($"PartyMemberUI ({gameObject.name}): Attempted to setup with null CharacterStats! Disabling UI slot.", this);
            gameObject.SetActive(false);
            return;
        }

        // Подписка на события
        linkedStats.onHealthChanged.AddListener(UpdateHealthUI);
        linkedStats.onExperienceChanged += UpdateExperienceUI;
        linkedStats.onLevelUp += UpdateLevelUI;
        linkedStats.onAttributesChanged += UpdateAttributesUI;

        // Первоначальное обновление всех UI элементов
        UpdateAllUI();

        // Опционально: gameObject.name = $"UI Slot for {linkedStats.gameObject.name}";
    }

    private void UpdateAllUI()
    {
        if (linkedStats == null) return; // Если статы отсоединились, ничего не обновляем

        UpdateHealthUI(linkedStats.currentHealth, linkedStats.maxHealth);
        // UpdateLevelUI вызывает UpdateExperienceUI, а также UpdateAttributesUI
        UpdateLevelUI(linkedStats.level); // Это обновит и опыт, и атрибуты через цепочку вызовов
        // UpdateAttributesUI(); // Этот вызов уже будет сделан из UpdateLevelUI
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

        string attrSummary = "Атрибуты:\n";
        attrSummary += $"Тело: {linkedStats.CurrentBody} (Баз: {linkedStats.baseBody})\n";
        attrSummary += $"Разум: {linkedStats.CurrentMind} (Баз: {linkedStats.baseMind})\n";
        attrSummary += $"Дух: {linkedStats.CurrentSpirit} (Баз: {linkedStats.baseSpirit})\n";
        attrSummary += $"Ловкость: {linkedStats.CurrentAgility} (Баз: {linkedStats.baseAgility})\n";
        attrSummary += $"Проф.: {linkedStats.CurrentProficiency} (Баз: {linkedStats.baseProficiency})";
        attributesText.text = attrSummary;
    }

    void OnDestroy()
    {
        if (linkedStats != null)
        {
            linkedStats.onHealthChanged.RemoveListener(UpdateHealthUI);
            linkedStats.onExperienceChanged -= UpdateExperienceUI;
            linkedStats.onLevelUp -= UpdateLevelUI;
            linkedStats.onAttributesChanged -= UpdateAttributesUI;
        }
    }
}