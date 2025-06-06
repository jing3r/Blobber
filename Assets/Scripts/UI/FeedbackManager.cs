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
    private PlayerGlobalActions playerActions; // Ссылка для получения информации о наведении

    void Awake()
    {
        if (promptText == null)
        {
            Debug.LogError("FeedbackManager: TextMeshProUGUI (promptText) не назначен!", this);
            enabled = false; // Отключаем, если нет основного UI элемента
            return;
        }
        promptText.text = ""; // Очищаем при старте

        // Находим PlayerGlobalActions для доступа к логике наведения
        // Предполагается, что они на одном объекте Player или PlayerGlobalActions легко доступен
        playerActions = GetComponent<PlayerGlobalActions>(); // Если они на одном объекте
        if (playerActions == null)
        {
             // Попробуем найти на родительском, если FeedbackManager будет дочерним
             playerActions = GetComponentInParent<PlayerGlobalActions>();
        }
        if (playerActions == null)
        {
            Debug.LogWarning("FeedbackManager: Не удалось найти PlayerGlobalActions. Обновление информации о наведении может не работать.", this);
        }
    }

    void Update()
    {
        // Если сейчас не показывается временный фидбек, обновляем информацию о наведении
        if (feedbackCoroutine == null && playerActions != null) // Проверяем playerActions
        {
            // Запрашиваем у PlayerGlobalActions, какой текст сейчас должен быть для наведения
            string hoverInfo = playerActions.GetCurrentHoverInfo();
            if (promptText.text != hoverInfo) // Обновляем только если текст изменился
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
        // После показа фидбека, UI в Update сам обновит информацию о наведении
        feedbackCoroutine = null;
        // Не нужно явно вызывать UpdateHoverInfo здесь, Update сам справится
    }

    /// <summary>
    /// Останавливает текущую корутину фидбека, если она активна.
    /// </summary>
    public void StopCurrentFeedback()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }
    }
}