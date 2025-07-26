using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// Обрабатывает глобальные действия игрока, не связанные с передвижением или боем,
/// такие как отдых, сохранение/загрузка, управление курсором.
/// </summary>
[RequireComponent(typeof(PartyManager))]
public class PlayerGlobalActions : MonoBehaviour
{
    [Header("Настройки отдыха")]
    [SerializeField] [Tooltip("Радиус, в котором не должно быть врагов для отдыха.")]
    private float enemyCheckRadiusForRest = 15f;
    [SerializeField] [Tooltip("Слой, на котором находятся персонажи (для проверки на врагов).")]
    private LayerMask characterLayerMaskForRest;

    public bool IsCursorFree { get; private set; }

    private PartyManager partyManager;
    private FeedbackManager feedbackManager;
    
    private void Awake()
    {
        partyManager = GetComponent<PartyManager>();
        feedbackManager = FindObjectOfType<FeedbackManager>();
        
        ApplyCursorState(false);
    }
    
    #region Input Handlers (Called by PlayerInput component)
    public void OnToggleCursor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ApplyCursorState(!IsCursorFree);
        }
    }
    
    public void OnRest(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            AttemptToRest();
        }
    }
    
    public void OnSaveGame(InputAction.CallbackContext context)
    {
        if (context.performed && SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
            feedbackManager?.ShowFeedbackMessage("Game Saved!");
        }
    }

    public void OnLoadGame(InputAction.CallbackContext context)
    {
        if (context.performed && SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame();
            feedbackManager?.ShowFeedbackMessage("Game Loaded!");
        }
    }
    #endregion

    private void ApplyCursorState(bool isFree)
    {
        IsCursorFree = isFree;
        Cursor.lockState = IsCursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = IsCursorFree;
    }

    private void AttemptToRest()
    {
        if (AreEnemiesNearby())
        {
            feedbackManager?.ShowFeedbackMessage("Cannot rest, enemies are nearby!");
            return;
        }

        foreach (var member in partyManager.PartyMembers)
        {
            if (member != null && !member.IsDead)
            {
                member.Heal(member.maxHealth); 
                member.GetComponent<CharacterAbilities>()?.RestoreAllAbilityCharges();
                member.GetComponent<CharacterStatusEffects>()?.ClearStatusEffectsOnRest();
            }
        }
        
        feedbackManager?.ShowFeedbackMessage("The party is well rested.");
    }
    
    private bool AreEnemiesNearby()
    {
        var nearbyColliders = Physics.OverlapSphere(transform.position, enemyCheckRadiusForRest, characterLayerMaskForRest);

        // Проверяем, есть ли поблизости живые враги.
        return nearbyColliders.Any(col =>
        {
            // Пропускаем коллайдеры, принадлежащие самой партии
            if (col.transform.IsChildOf(transform) || col.transform == transform) return false;

            var ai = col.GetComponent<AIController>();
            return ai != null && ai.CurrentAlignment == AIController.Alignment.Hostile && !ai.MyStats.IsDead;
        });
    }
}