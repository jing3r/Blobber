using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;

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

    [Header("Rest Settings")]
    public float enemyCheckRadiusForRest = 15f; // Радиус проверки на врагов перед отдыхом
    public LayerMask characterLayerMaskForRest;    // Слой, на котором находятся враги (если не используем Perception)

    private bool isCursorFree = false;
    public bool IsCursorFree => isCursorFree;

    private Transform cameraTransform;
    private PartyManager partyManager;
    private FeedbackManager feedbackManager;
    private TargetingSystem targetingSystem;

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
        targetingSystem = GetComponent<TargetingSystem>();
        if (targetingSystem == null) Debug.LogError("PlayerGlobalActions: TargetingSystem не найден!", this);

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

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed || targetingSystem == null) return;
        
        RaycastHit hit;
        if (targetingSystem.TryGetTarget(actionDistance, interactionLayerMask, out hit))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                feedbackManager?.StopCurrentFeedback();
                string feedbackMessage = interactable.Interact();
                feedbackManager?.ShowFeedbackMessage(feedbackMessage);
            }
            else { feedbackManager?.ShowFeedbackMessage("There is nothing to interact with."); } // Перевод
        }
        else { feedbackManager?.ShowFeedbackMessage("There is nothing here."); } // Перевод
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
            targetStats.TakeDamage(damageToDeal, transform);
            feedbackMessage = $"{attacker.gameObject.name} попадает по {targetStats.gameObject.name} ({damageToDeal} урона).";
        }
        else
        {
            feedbackMessage = $"{attacker.gameObject.name} промахивается по {targetStats.gameObject.name}.";
        }
        feedbackManager?.ShowFeedbackMessage(feedbackMessage);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || targetingSystem == null) return;
        
        RaycastHit hit;
        if (targetingSystem.TryGetTarget(actionDistance, combatLayerMask, out hit))
        {
            CharacterStats targetStats = hit.collider.GetComponent<CharacterStats>();
            if (targetStats != null && !partyManager.partyMembers.Contains(targetStats))
            {
                AIController npcController = targetStats.GetComponent<AIController>();
                HandlePlayerAttackOnNPC(targetStats, npcController);
            }
            else { feedbackManager?.ShowFeedbackMessage("The target is immune to attack."); } // Перевод
        }
        else { feedbackManager?.ShowFeedbackMessage("Attack: Target not found."); } // Перевод
    }

    public void OnPrimaryAction(InputAction.CallbackContext context) // ЛКМ
    {
        if (!context.performed || targetingSystem == null) return;
        
        RaycastHit hit;
        bool actionTaken = false;

        // Сначала пытаемся атаковать
        if (targetingSystem.TryGetTarget(actionDistance, combatLayerMask, out hit))
        {
            CharacterStats targetStats = hit.collider.GetComponent<CharacterStats>();
            if (targetStats != null && !partyManager.partyMembers.Contains(targetStats))
            {
                if (!targetStats.IsDead)
                {
                    AIController npcController = targetStats.GetComponent<AIController>();
                    HandlePlayerAttackOnNPC(targetStats, npcController);
                    actionTaken = true;
                }
            }
        }

        // Если не атаковали, пытаемся взаимодействовать
        if (!actionTaken && targetingSystem.TryGetTarget(actionDistance, interactionLayerMask, out hit)) 
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
        
        if (!actionTaken) { feedbackManager?.ShowFeedbackMessage("There is nothing to do here."); } // Перевод
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
        bool enemiesNearby = false;
        // Ищем ВСЕХ персонажей в радиусе на указанном слое
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, enemyCheckRadiusForRest, characterLayerMaskForRest);
        
        // Debug.Log($"AttemptToRest: Found {nearbyColliders.Length} colliders on character layer.");

        foreach (Collider col in nearbyColliders)
        {
            // Исключаем самих членов партии (или весь объект Player, если коллайдер на нем)
            // Проверяем, что коллайдер не принадлежит объекту, на котором висит этот скрипт, или его дочерним объектам.
            if (col.transform.IsChildOf(transform) || col.transform == transform)
            {
                // Debug.Log($"AttemptToRest: Skipping self/party member: {col.gameObject.name}");
                continue;
            }

            AIController nearbyAI = col.GetComponent<AIController>();
            if (nearbyAI != null) // Если это NPC с AIController
            {
                // Debug.Log($"AttemptToRest: Checking NPC: {col.gameObject.name}, Alignment: {nearbyAI.currentAlignment}, IsDead: {nearbyAI.MyStats?.IsDead}");
                if (nearbyAI.currentAlignment == AIController.Alignment.Hostile && nearbyAI.MyStats != null && !nearbyAI.MyStats.IsDead)
                {
                    enemiesNearby = true;
                    // Debug.Log($"AttemptToRest: Hostile enemy {col.gameObject.name} found nearby!");
                    break; 
                }
            }
            // Если у объекта нет AIController, он не считается враждебным NPC для целей отдыха
            // (это могут быть мирные NPC без AIController или другие интерактивные объекты на слое "Characters")
        }

        if (enemiesNearby)
        {
            feedbackManager?.ShowFeedbackMessage("Нельзя отдыхать, враги поблизости!");
            return;
        }

        if (partyManager != null)
        {
            foreach (CharacterStats member in partyManager.partyMembers)
            {
                if (member != null) // Проверяем, что слот не пуст
                {
                    if (!member.IsDead) 
                    {
                        member.Heal(member.maxHealth); 
                    }
                    // Мертвые пока не воскрешаются от обычного отдыха

                    CharacterAbilities abilities = member.GetComponent<CharacterAbilities>();
                    if (abilities != null)
                    {
                        abilities.RestoreAllAbilityCharges();
                    }
                }
            }
            feedbackManager?.ShowFeedbackMessage("Отряд отдохнул и восстановил силы.");
            // Debug.Log("PlayerGlobalActions: Party has rested. Health and ability charges restored.");
        }
    }

    private void ApplyCursorState()
    {
        Cursor.lockState = isCursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isCursorFree;
    }

    public string GetCurrentHoverInfo()
    {
        if (targetingSystem == null) return "";

        RaycastHit hitInfo;
        LayerMask combinedMask = interactionLayerMask | combatLayerMask;

        if (targetingSystem.TryGetTarget(actionDistance, combinedMask, out hitInfo))
        {
            CharacterStats character = hitInfo.collider.GetComponent<CharacterStats>();
            Interactable interactable = hitInfo.collider.GetComponent<Interactable>();

            if (character != null && (partyManager == null || !partyManager.partyMembers.Contains(character)))
            {
                if (!character.IsDead)
                {
                    AIController npcController = character.GetComponent<AIController>();
                    string alignmentText = "";
                    if (npcController != null)
                    {
                        switch (npcController.currentAlignment)
                        {
                            case AIController.Alignment.Friendly: alignmentText = " (Friendly)"; break;
                            case AIController.Alignment.Neutral: alignmentText = " (Neutral)"; break;
                            case AIController.Alignment.Hostile: alignmentText = " (Hostile)"; break;
                        }
                    }

                    string hoverText = $"{character.gameObject.name}{alignmentText}\nHP: {character.currentHealth}/{character.maxHealth}";

                    CharacterStats potentialAttacker = GetAttackingPartyMember();
                    if (potentialAttacker != null)
                    {
                        int hitChance = basePlayerHitChance;
                        hitChance += potentialAttacker.AgilityHitBonusPercent;
                        hitChance -= character.AgilityEvasionBonusPercent;
                        hitChance = Mathf.Clamp(hitChance, minPlayerHitChance, maxPlayerHitChance);
                        hoverText += $" (Hit Chance: {hitChance}%)";
                    }
                    return hoverText;
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
}