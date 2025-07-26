using UnityEngine;
using TMPro;
using System.Collections;
using System.Linq;

/// <summary>
/// Управляет отображением текстовой информации для игрока в едином UI-элементе.
/// Показывает как временный фидбек (результаты действий), так и постоянный (информация о цели под курсором).
/// </summary>
public class FeedbackManager : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Настройки")]
    [SerializeField] [Tooltip("Как долго показывать временное сообщение в секундах.")]
    private float feedbackDuration = 2.0f;

    private TargetingSystem targetingSystem;
    private PartyManager partyManager;

    private Coroutine feedbackCoroutine;

    private void Awake()
    {
        if (promptText == null)
        {
            Debug.LogError($"[{nameof(FeedbackManager)}] Prompt Text не назначен.", this);
            enabled = false;
            return;
        }
        promptText.text = string.Empty;

        targetingSystem = FindObjectOfType<TargetingSystem>();
        partyManager = FindObjectOfType<PartyManager>();
    }

    private void Update()
    {
        // Если в данный момент не отображается временное сообщение,
        // обновляем текст информацией о том, на что наведен курсор.
        if (feedbackCoroutine == null)
        {
            promptText.text = GetCurrentHoverInfo();
        }
    }

    /// <summary>
    /// Показывает временное сообщение, которое исчезнет через feedbackDuration.
    /// </summary>
    public void ShowFeedbackMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || !enabled) return;

        // Если уже есть активный фидбек, прерываем его
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }
        
        promptText.text = message;
        feedbackCoroutine = StartCoroutine(ClearFeedbackAfterDelay());
    }

    private IEnumerator ClearFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        feedbackCoroutine = null;
        // После исчезновения фидбека, текст немедленно обновится информацией о наведении в следующем Update.
    }

    private string GetCurrentHoverInfo()
    {
        if (targetingSystem == null || partyManager == null) return string.Empty;
        
        const float checkDistance = 15f;
        
        // Используем маску, которая включает все, с чем можно взаимодействовать или что можно атаковать
        LayerMask checkLayerMask = LayerMask.GetMask("Characters", "Interactable");

        if (targetingSystem.TryGetTarget(checkDistance, checkLayerMask, out var hitInfo))
        {
            if (hitInfo.collider.TryGetComponent<CharacterStats>(out var character))
            {
                // Игнорируем членов своей партии
                if (!partyManager.PartyMembers.ToList().Contains(character))
                {
                    return GetCharacterHoverInfo(character);
                }
            }

            if (hitInfo.collider.TryGetComponent<Interactable>(out var interactable))
            {
                return interactable.InteractionPrompt;
            }
        }

        return string.Empty;
    }
    
    private string GetCharacterHoverInfo(CharacterStats target)
    {
        if (target.IsDead)
        {
            // Мертвый персонаж также может быть Interactable (LootableCorpse)
            var interactable = target.GetComponent<Interactable>();
            return interactable != null ? interactable.InteractionPrompt : $"{target.name} (Defeated)";
        }
        
        var npcController = target.GetComponent<AIController>();
        string alignmentText = npcController != null ? $" ({npcController.CurrentAlignment})" : "";
        string info = $"{target.name}{alignmentText} (HP: {target.currentHealth}/{target.maxHealth})";
        
        var activePartyMember = partyManager.ActiveMember;
        if (activePartyMember != null && !activePartyMember.IsDead)
        {
            // TODO: Вынести расчет шанса попадания в CombatHelper, чтобы избежать дублирования логики
            int hitChance = 70 + activePartyMember.AgilityHitBonusPercent - target.AgilityEvasionBonusPercent;
            hitChance = Mathf.Clamp(hitChance, 10, 95);
            info += $" (Hit: {hitChance}%)";
        }
        
        return info;
    }
}