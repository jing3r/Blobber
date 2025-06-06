using UnityEngine;
// using UnityEngine.SceneManagement; // Раскомментировать, если будет использоваться перезагрузка сцены

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // Раскомментировать, если GameManager должен сохраняться между сценами

        PartyManager.OnPartyWipe += HandleGameOver;
    }

    private void HandleGameOver()
    {
        Debug.LogError("GAME OVER! (Обработано GameManager)");
        Time.timeScale = 0f; // Останавливаем время в игре

        // Разблокируем курсор для возможного UI меню Game Over
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Дальнейшие действия: показать UI, предложить загрузку/выход
        // Debug.Log("Предложить игроку загрузить последнее сохранение или выйти в главное меню.");
        // if (FindObjectOfType<GameOverUI>() != null) FindObjectOfType<GameOverUI>().Show();
    }

    void OnDestroy()
    {
        PartyManager.OnPartyWipe -= HandleGameOver;
        if (Instance == this)
        {
            Instance = null;
        }
    }
}