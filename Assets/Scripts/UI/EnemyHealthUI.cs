using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет отображением полосы здоровья для врага (или любого NPC).
/// </summary>
public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private CharacterStats enemyStats;

    private void Awake()
    {
        // Автоматический поиск CharacterStats в родительском объекте, если не указан вручную
        if (enemyStats == null)
        {
            enemyStats = GetComponentInParent<CharacterStats>();
        }

        if (!ValidatePrerequisites())
        {
            gameObject.SetActive(false);
            return;
        }

        enemyStats.onHealthChanged.AddListener(UpdateHealthDisplay);
        enemyStats.onDied += HandleDeath;

        UpdateHealthDisplay(enemyStats.currentHealth, enemyStats.maxHealth);
    }

    private void OnDestroy()
    {
        if (enemyStats != null)
        {
            enemyStats.onHealthChanged.RemoveListener(UpdateHealthDisplay);
            enemyStats.onDied -= HandleDeath;
        }
    }

    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
        {
            healthSlider.value = 0;
            return;
        }
        healthSlider.value = (float)currentHealth / maxHealth;
    }

    private void HandleDeath()
    {
        gameObject.SetActive(false);
    }
    
    private bool ValidatePrerequisites()
    {
        if (healthSlider == null)
        {
            Debug.LogError($"[{nameof(EnemyHealthUI)}] Health Slider not assigned on '{gameObject.name}'.", this);
            return false;
        }
        if (enemyStats == null)
        {
            Debug.LogError($"[{nameof(EnemyHealthUI)}] CharacterStats not found on '{gameObject.name}' or its parents.", this);
            return false;
        }
        return true;
    }
}