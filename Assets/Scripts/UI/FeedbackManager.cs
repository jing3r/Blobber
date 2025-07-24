
using UnityEngine;
using TMPro;
using System.Collections;

public class FeedbackManager : MonoBehaviour
{
    [Header("Ссылки UI")]
    [Tooltip("Текстовое поле для отображения подсказок и фидбека.")]
    public TextMeshProUGUI promptText;

    [Header("Настройки Фидбека")]
    [Tooltip("Как долго показывать сообщение о результате действия (в секундах).")]
    public float feedbackDuration = 2.0f;

    private Coroutine feedbackCoroutine;
    
    // --- ИЗМЕНЕНИЕ: Ссылка на InputManager вместо PlayerGlobalActions ---
    private InputManager inputManager;
    // --- НОВОЕ: Ссылка на TargetingSystem для получения информации о цели ---
    private TargetingSystem targetingSystem;
    private PartyManager partyManager;

    void Awake()
    {
        if (promptText == null)
        {
            Debug.LogError("FeedbackManager: TextMeshProUGUI (promptText) не назначен!", this);
            enabled = false;
            return;
        }
        promptText.text = "";

        // Находим InputManager на объекте игрока
        inputManager = FindObjectOfType<InputManager>();
        targetingSystem = FindObjectOfType<TargetingSystem>();
        partyManager = FindObjectOfType<PartyManager>();

        if (inputManager == null)
        {
            Debug.LogWarning("FeedbackManager: Не удалось найти InputManager. Обновление информации о наведении не будет работать.", this);
        }
    }

    void Update()
    {
        // Если сейчас не показывается временный фидбек, обновляем информацию о наведении
        if (feedbackCoroutine == null)
        {
            string hoverInfo = GetCurrentHoverInfo();
            if (promptText.text != hoverInfo)
            {
                promptText.text = hoverInfo;
            }
        }
    }

    /// <summary>
    /// Показывает временное сообщение в UI.
    /// </summary>
    public void ShowFeedbackMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || promptText == null) return;

        StopCurrentFeedback();
        promptText.text = message;
        feedbackCoroutine = StartCoroutine(ClearFeedbackAfterDelay());
    }

    private IEnumerator ClearFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        feedbackCoroutine = null;
    }

    public void StopCurrentFeedback()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }
    }

    // --- НОВЫЙ МЕТОД: Логика отображения информации о цели, перенесенная сюда ---
    private string GetCurrentHoverInfo()
    {
        if (targetingSystem == null || partyManager == null) return "";
        
        // Используем настройки из InputManager или PlayerGlobalActions, нужна общая точка
        // Пока возьмем дефолтные значения
        float checkDistance = 15f; 
        LayerMask checkLayerMask = ~0; // Все слои

        if (targetingSystem.TryGetTarget(checkDistance, checkLayerMask, out RaycastHit hitInfo))
        {
            CharacterStats character = hitInfo.collider.GetComponent<CharacterStats>();
            Interactable interactable = hitInfo.collider.GetComponent<Interactable>();

            if (character != null && !partyManager.partyMembers.Contains(character))
            {
                if (!character.IsDead)
                {
                    AIController npcCtrl = character.GetComponent<AIController>();
                    string alignmentText = npcCtrl != null ? $" ({npcCtrl.currentAlignment})" : "";
                    string previewText = $"{character.gameObject.name}{alignmentText} (HP: {character.currentHealth}/{character.maxHealth})";
                    
                    CharacterStats attacker = partyManager.ActiveMember;
                    if (attacker != null && !attacker.IsDead)
                    {
                        int hitChance = Mathf.Clamp(70 + attacker.AgilityHitBonusPercent - character.AgilityEvasionBonusPercent, 10, 95);
                        previewText += $" (Hit: {hitChance}%)";
                    }
                    return previewText;
                }
                else
                {
                    return interactable?.interactionPrompt ?? $"{character.gameObject.name} (Defeated)";
                }
            }
            
            if (interactable != null)
            {
                return interactable.interactionPrompt;
            }
        }

        return "";
    }
}