using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class PlayerGlobalActions : MonoBehaviour
{
    [Header("Настройки")]
    public float interactionDistance = 3f; // Оставляем здесь, так как это параметр мира
    public LayerMask interactionLayerMask;
    
    [Header("Настройки Отдыха")]
    public float enemyCheckRadiusForRest = 15f;
    public LayerMask characterLayerMaskForRest;

    private bool isCursorFree = false;
    public bool IsCursorFree => isCursorFree;

    // Ссылки на системы
    private PartyManager partyManager;
    private FeedbackManager feedbackManager;
    
    void Awake()
    {
        partyManager = GetComponent<PartyManager>();
        feedbackManager = FindObjectOfType<FeedbackManager>();
        
        ApplyCursorState();
    }
    
    public void OnToggleCursor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isCursorFree = !isCursorFree;
            ApplyCursorState();
        }
    }
    private void ApplyCursorState()
    {
        Cursor.lockState = isCursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isCursorFree;
    }

    public void OnRest(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            AttemptToRest();
        }
    }

    private void AttemptToRest()
    
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, enemyCheckRadiusForRest, characterLayerMaskForRest);
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.transform.IsChildOf(transform) || col.transform == transform) continue;

            var nearbyAI = col.GetComponent<AIController>();
            if (nearbyAI != null && nearbyAI.currentAlignment == AIController.Alignment.Hostile && !nearbyAI.MyStats.IsDead)
            {
                feedbackManager?.ShowFeedbackMessage("Cannot rest, enemies are nearby!");
                return;
            }
        }

        if (partyManager != null)
        {
            foreach (CharacterStats member in partyManager.partyMembers)
            {
                if (member != null && !member.IsDead)
                {
                    member.Heal(member.maxHealth); 
                    member.GetComponent<CharacterAbilities>()?.RestoreAllAbilityCharges();
                }
            }
            feedbackManager?.ShowFeedbackMessage("The party is well rested.");
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
}