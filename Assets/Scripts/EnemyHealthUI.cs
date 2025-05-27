using UnityEngine;
using UnityEngine.UI; // Для Slider

public class EnemyHealthUI : MonoBehaviour
{
    [Tooltip("Слайдер для отображения здоровья.")]
    public Slider healthSlider;

    [Tooltip("Ссылка на CharacterStats врага (обычно на родительском объекте). Будет найдена автоматически, если не назначена.")]
    public CharacterStats enemyStats;

    void Awake()
    {
        if (enemyStats == null)
        {
            enemyStats = GetComponentInParent<CharacterStats>();
        }

        if (enemyStats == null)
        {
            Debug.LogError($"EnemyHealthUI ({gameObject.name}): CharacterStats not found! UI will be disabled.", this);
            gameObject.SetActive(false);
            return;
        }

        if (healthSlider == null)
        {
            Debug.LogError($"EnemyHealthUI ({gameObject.name}): Health Slider not assigned! UI will be disabled.", this);
            gameObject.SetActive(false);
            return;
        }

        enemyStats.onHealthChanged.AddListener(UpdateHealthDisplay);
        enemyStats.onDied += HandleDeath;

        UpdateHealthDisplay(enemyStats.currentHealth, enemyStats.maxHealth); // Инициализация
    }

    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (healthSlider == null) return; // Дополнительная проверка
        if (maxHealth <= 0) // Избегаем деления на ноль и некорректных значений
        {
            healthSlider.value = 0;
            return;
        }

        healthSlider.value = (float)currentHealth / maxHealth;
    }

    private void HandleDeath()
    {
        gameObject.SetActive(false); // Скрываем весь UI элемент при смерти
    }

    void OnDestroy()
    {
        if (enemyStats != null)
        {
            enemyStats.onHealthChanged.RemoveListener(UpdateHealthDisplay);
            enemyStats.onDied -= HandleDeath;
        }
    }
}