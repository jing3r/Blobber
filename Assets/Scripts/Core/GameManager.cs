using UnityEngine;

/// <summary>
/// Главный управляющий класс игры (Singleton).
/// Отвечает за глобальные состояния, такие как Game Over.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // Стандартная реализация Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // TODO: Раскомментировать, если GameManager должен сохраняться между сценами
        // DontDestroyOnLoad(gameObject);

        // Подписываемся на статическое событие, чтобы отреагировать на уничтожение партии
        PartyManager.OnPartyWipe += HandlePartyWipe;
    }

    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта, чтобы избежать утечек памяти
        PartyManager.OnPartyWipe -= HandlePartyWipe;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Обрабатывает событие полного уничтожения партии.
    /// </summary>
    private void HandlePartyWipe()
    {
        Debug.LogError("GAME OVER! The party has been wiped out.");
        
        // Останавливаем время в игре, чтобы прекратить все процессы
        Time.timeScale = 0f;

        // Освобождаем курсор для взаимодействия с UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // TODO: Показать UI экрана "Game Over" с опциями загрузки или выхода в главное меню.
        // var gameOverUI = FindObjectOfType<GameOverUI>();
        // gameOverUI?.Show();
    }
}