using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class PlayerGlobalActions : MonoBehaviour
{
    [Header("Общие Настройки")]
    public float actionDistance = 3f;
    public LayerMask interactionLayerMask;
    public LayerMask combatLayerMask;

    [Header("Боевые Настройки Игрока")]
    [Range(0, 100)] public int basePlayerHitChance = 70;
    [Range(0, 100)] public int minPlayerHitChance = 10;
    [Range(0, 100)] public int maxPlayerHitChance = 95;

    private bool isCursorFree = false;
    public bool IsCursorFree => isCursorFree;

    private Transform cameraTransform;
    private PartyManager partyManager;
    private FeedbackManager feedbackManager;

    void Awake()
    {
        cameraTransform = GetComponentInChildren<Camera>()?.transform;
        if (cameraTransform == null) { Debug.LogError("PlayerGlobalActions: Камера не найдена! Скрипт будет отключен.", this); enabled = false; return; }

        partyManager = GetComponent<PartyManager>();
        if (partyManager == null) { Debug.LogError("PlayerGlobalActions: PartyManager не найден! Скрипт будет отключен.", this); enabled = false; return; }

        feedbackManager = GetComponent<FeedbackManager>();
        if (feedbackManager == null) {
            feedbackManager = GetComponentInChildren<FeedbackManager>();
             if (feedbackManager == null) Debug.LogWarning("PlayerGlobalActions: FeedbackManager не найден. Подсказки и фидбек не будут работать.", this);
        }

        if (interactionLayerMask == 0) interactionLayerMask = Physics.DefaultRaycastLayers;
        if (combatLayerMask == 0) combatLayerMask = Physics.DefaultRaycastLayers;

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

    public void OnInteract(InputAction.CallbackContext context) // Клавиша E
    {
        if (!context.performed) return;
        RaycastHit hit;
        if (PerformRaycast(out hit, actionDistance, interactionLayerMask))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                feedbackManager?.StopCurrentFeedback();
                string feedbackMessage = interactable.Interact();
                feedbackManager?.ShowFeedbackMessage(feedbackMessage);
            }
            else { feedbackManager?.ShowFeedbackMessage("Не с чем взаимодействовать."); }
        }
        else { feedbackManager?.ShowFeedbackMessage("Здесь нечего делать."); }
    }
    
    private CharacterStats GetAttackingPartyMember()
    {
        if (partyManager == null || partyManager.partyMembers.Count == 0) return null;
        return partyManager.partyMembers.FirstOrDefault(member => member != null && !member.IsDead);
    }

    private void HandlePlayerAttackOnNPC(CharacterStats targetStats, AIController npcController)
    {
        CharacterStats attacker = GetAttackingPartyMember();
        if (attacker == null) { feedbackManager?.ShowFeedbackMessage("Некому атаковать!"); return; }
        if (targetStats.IsDead) { feedbackManager?.ShowFeedbackMessage($"{targetStats.gameObject.name} уже повержен."); return; }

        // Если NPC не враждебен и может стать враждебным, делаем его враждебным
        if (npcController != null && npcController.currentAlignment != AIController.Alignment.Hostile)
        {
            if (npcController.canBecomeHostileOnAttack)
            {
                npcController.BecomeHostileTowards(transform, true); // 'transform' здесь это объект Игрока (партии)
            }
            // Если canBecomeHostileOnAttack == false, то NPC не станет враждебным,
            // но мы все равно можем его атаковать (например, квестовое убийство "плохого" дружественного NPC).
            // Или можно добавить проверку и запретить атаку, если это требуется.
        }
        
        int hitChance = basePlayerHitChance;
        hitChance += attacker.AgilityHitBonusPercent;
        hitChance -= targetStats.AgilityEvasionBonusPercent;
        hitChance = Mathf.Clamp(hitChance, minPlayerHitChance, maxPlayerHitChance);

        string feedbackMessage;
        if (Random.Range(0, 100) < hitChance)
        {
            int damageToDeal = attacker.CalculatedDamage;
            targetStats.TakeDamage(damageToDeal);
            feedbackMessage = $"{attacker.gameObject.name} попадает по {targetStats.gameObject.name} ({damageToDeal} урона).";
        }
        else
        {
            feedbackMessage = $"{attacker.gameObject.name} промахивается по {targetStats.gameObject.name}.";
        }
        feedbackManager?.ShowFeedbackMessage(feedbackMessage);
    }

    public void OnAttack(InputAction.CallbackContext context) // Клавиша F (Форсированная АТАКА)
    {
        if (!context.performed) return;
        RaycastHit hit;
        if (PerformRaycast(out hit, actionDistance, combatLayerMask))
        {
            CharacterStats targetStats = hit.collider.GetComponent<CharacterStats>();
            if (targetStats != null)
            {
                if (partyManager != null && !partyManager.partyMembers.Contains(targetStats)) // Не союзник
                {
                    AIController npcController = targetStats.GetComponent<AIController>();
                    HandlePlayerAttackOnNPC(targetStats, npcController);
                }
                else { feedbackManager?.ShowFeedbackMessage("Нельзя атаковать своих."); }
            }
            else { feedbackManager?.ShowFeedbackMessage("Цель невосприимчива к атаке."); }
        }
        else { feedbackManager?.ShowFeedbackMessage("Атака: Цель не найдена."); }
    }

    public void OnPrimaryAction(InputAction.CallbackContext context) // ЛКМ
    {
        if (!context.performed) return;
        RaycastHit hit;
        bool actionTaken = false;

        if (PerformRaycast(out hit, actionDistance, combatLayerMask))
        {
            CharacterStats targetStats = hit.collider.GetComponent<CharacterStats>();
            if (targetStats != null && (partyManager == null || !partyManager.partyMembers.Contains(targetStats))) // Не союзник
            {
                if (!targetStats.IsDead) // Атакуем только живых
                {
                    AIController npcController = targetStats.GetComponent<AIController>();
                    HandlePlayerAttackOnNPC(targetStats, npcController);
                    actionTaken = true;
                }
                // Если мертв, то не делаем actionTaken = true, чтобы дать шанс Interactable
            }
        }

        if (!actionTaken && PerformRaycast(out hit, actionDistance, interactionLayerMask)) 
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                feedbackManager?.StopCurrentFeedback();
                string feedbackMessage = interactable.Interact();
                feedbackManager?.ShowFeedbackMessage(feedbackMessage);
                actionTaken = true;
            }
        }
        
        if (!actionTaken) { feedbackManager?.ShowFeedbackMessage("Здесь нечего делать."); }
    }

    private bool PerformRaycast(out RaycastHit hitInfo, float maxDistance, LayerMask layerMask)
    {
        Ray ray;
        if (isCursorFree)
        {
            Camera camComponent = cameraTransform.GetComponent<Camera>();
            if (camComponent == null) { hitInfo = default; return false; }
            ray = camComponent.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            ray = new Ray(cameraTransform.position, cameraTransform.forward);
        }
        return Physics.Raycast(ray, out hitInfo, maxDistance, layerMask);
    }

    private void ApplyCursorState()
    {
        Cursor.lockState = isCursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isCursorFree;
    }

    public string GetCurrentHoverInfo()
    {
        RaycastHit characterHitInfo, interactableHitInfo;
        bool hitCharacter = PerformRaycast(out characterHitInfo, actionDistance, combatLayerMask);
        bool hitInteractable = PerformRaycast(out interactableHitInfo, actionDistance, interactionLayerMask);

        CharacterStats character = hitCharacter ? characterHitInfo.collider.GetComponent<CharacterStats>() : null;
        Interactable interactable = hitInteractable ? interactableHitInfo.collider.GetComponent<Interactable>() : null;
        AIController npcCtrl = character ? character.GetComponent<AIController>() : null;

        string promptToShow = "";

        if (character != null && (partyManager == null || !partyManager.partyMembers.Contains(character))) // Not a party member
        {
            if (!character.IsDead)
            {
                string alignmentText = "";
                if (npcCtrl != null)
                {
                    switch (npcCtrl.currentAlignment)
                    {
                        case AIController.Alignment.Friendly: alignmentText = " (Дружелюбен)"; break;
                        case AIController.Alignment.Neutral:  alignmentText = " (Нейтрален)"; break;
                        case AIController.Alignment.Hostile:  alignmentText = " (Враждебен)"; break;
                    }
                }
                promptToShow = $"{character.gameObject.name}{alignmentText} (HP: {character.currentHealth}/{character.maxHealth})";
                
                CharacterStats potentialAttacker = GetAttackingPartyMember();
                if (potentialAttacker != null)
                {
                    int hitChancePreview = basePlayerHitChance;
                    hitChancePreview += potentialAttacker.AgilityHitBonusPercent;
                    hitChancePreview -= character.AgilityEvasionBonusPercent;
                    hitChancePreview = Mathf.Clamp(hitChancePreview, minPlayerHitChance, maxPlayerHitChance);
                    promptToShow += $" (Шанс: {hitChancePreview}%)";
                }
            }
            else 
            {
                Interactable corpseInteractable = characterHitInfo.collider.GetComponent<LootableCorpse>();
                promptToShow = corpseInteractable != null ? corpseInteractable.interactionPrompt : $"{character.gameObject.name} (Повержен)";
            }
        }
        else if (interactable != null)
        {
            bool isSameTargetAsCharacter = hitCharacter && characterHitInfo.collider == interactableHitInfo.collider;
            if (!isSameTargetAsCharacter || string.IsNullOrEmpty(promptToShow))
            {
                 promptToShow = interactable.interactionPrompt;
            }
        }
        return promptToShow;
    }

    public void OnSaveGame(InputAction.CallbackContext context)
    {
        if (context.performed && SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
            feedbackManager?.ShowFeedbackMessage("Игра сохранена!");
        }
    }

    public void OnLoadGame(InputAction.CallbackContext context)
    {
        if (context.performed && SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame();
            feedbackManager?.ShowFeedbackMessage("Игра загружена!");
        }
    }

    public void OnOpenInventory(InputAction.CallbackContext context) { /* ... */ }
    public void OnOpenMap(InputAction.CallbackContext context) { /* ... */ }
    public void OnOpenMenu(InputAction.CallbackContext context) { /* ... */ }
    public void OnRest(InputAction.CallbackContext context) { /* ... */ }
}