using UnityEngine;
using UnityEngine.InputSystem; // Для новой системы ввода
using System.Collections.Generic; // Для List<AbilityData>
using System.Linq; // Для FirstOrDefault

public class AbilityCastingSystem : MonoBehaviour
{
    public static AbilityCastingSystem Instance { get; private set; }

    [Header("Ссылки")]
    [Tooltip("Ссылка на PlayerGlobalActions для проверки режима курсора.")]
    public PlayerGlobalActions playerGlobalActions;
    [Tooltip("Ссылка на PartyManager для получения информации о кастере.")]
    public PartyManager partyManager;
    [Tooltip("Ссылка на FeedbackManager для сообщений.")]
    public FeedbackManager feedbackManager; // Опционально, но полезно

    [Header("Layer Masks for Targeting")]
    public LayerMask interactableLayerMask; // Назначь сюда слой Interactable
    public LayerMask characterLayerMask;    // Назначь сюда слой Characters
    public LayerMask defaultRaycastLayers;  // Все остальное, если нужно
    [Header("Targeting Layers")]
    public LayerMask groundLayerMask = 1; // Слой по умолчанию (Everything), нужно настроить на слой земли
        public LayerMask generalObstacleLayerMask;

    // Ссылки на CharacterAbilities активного персонажа (или первого в партии)
    // Это нужно, чтобы знать, какие способности есть у кастера
    private CharacterAbilities currentCasterAbilities;
    private CharacterStats currentCasterStats;

    // Для хранения CharacterStats члена партии, над UI которого наведен курсор
    private CharacterStats hoveredPartyMemberTarget;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerGlobalActions == null) Debug.LogError("AbilityCastingSystem: PlayerGlobalActions не назначен!", this);
        if (partyManager == null) Debug.LogError("AbilityCastingSystem: PartyManager не назначен!", this);
        // FeedbackManager опционален
    }

    void Start()
    {
        // Предполагаем, что кастер - это первый живой член партии.
        // Эту логику можно будет усложнить (например, выбор активного персонажа).
        UpdateCurrentCaster();
    }
    
    void Update()
    {
        // Можно обновлять кастера, если состав партии может меняться или активный персонаж
        // UpdateCurrentCaster(); 
        // Но пока это не требуется каждый кадр, делаем в Start и при смене партии (если будет такой эвент)
    }

    private void UpdateCurrentCaster()
    {
        if (partyManager != null && partyManager.partyMembers.Count > 0)
        {
            // Ищем первого живого члена партии
            currentCasterStats = partyManager.partyMembers.FirstOrDefault(m => m != null && !m.IsDead);
            if (currentCasterStats != null)
            {
                currentCasterAbilities = currentCasterStats.GetComponent<CharacterAbilities>();
                if (currentCasterAbilities == null)
                {
                    // Debug.LogError($"AbilityCastingSystem: У {currentCasterStats.gameObject.name} отсутствует CharacterAbilities!", this);
                }
            }
            else
            {
                currentCasterAbilities = null; // Нет живых кастеров
            }
        }
    }


    // --- Публичные методы для UIPartyMemberTargetDetector ---
    public void SetHoveredPartyMember(CharacterStats memberStats)
    {
        hoveredPartyMemberTarget = memberStats;
    }

    public void ClearHoveredPartyMemberIfCurrent(CharacterStats memberStats)
    {
        if (hoveredPartyMemberTarget == memberStats)
        {
            hoveredPartyMemberTarget = null;
        }
    }

    // --- Методы для обработки ввода способностей (от PlayerInput компонента) ---
    // Эти методы будут вызываться из компонента PlayerInput на объекте игрока,
    // когда соответствующие действия (Ability1, Ability2, ..., Ability0) срабатывают.

    public void OnAbility1(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(0); }
    public void OnAbility2(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(1); }
    public void OnAbility3(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(2); }
    public void OnAbility4(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(3); }
    public void OnAbility5(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(4); }
    public void OnAbility6(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(5); }
    public void OnAbility7(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(6); }
    public void OnAbility8(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(7); }
    public void OnAbility9(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(8); }
    public void OnAbility0(InputAction.CallbackContext context) { if (context.performed) TryCastAbility(9); }


private void TryCastAbility(int abilityIndex)
{
    UpdateCurrentCaster();

    if (currentCasterAbilities == null || currentCasterStats == null || currentCasterStats.IsDead)
    {
        this.feedbackManager?.ShowFeedbackMessage("Cannot use ability: No valid caster.");
        return;
    }

    AbilitySlot abilitySlot = currentCasterAbilities.GetAbilitySlotByIndex(abilityIndex);
    if (abilitySlot == null || abilitySlot.abilityData == null)
    {
        return; 
    }

    AbilityData ability = abilitySlot.abilityData;

    if (!currentCasterAbilities.CanUseAbility(abilitySlot))
    {
        string reason = abilitySlot.currentCharges <= 0 ? "No charges left" : "Ability on cooldown";
        this.feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: {reason}.");
        return;
    }

    Transform primaryTargetTransform = null;
    CharacterStats primaryTargetStats = null;
    Vector3 castPoint = Vector3.zero; 

    Ray ray = GetRayFromScreen(); 
    float abilityRange = ability.range > 0 ? ability.range : (playerGlobalActions != null ? playerGlobalActions.actionDistance : 100f); // 100f как очень большая дефолтная дальность, если не указана

    if (ability.targetType == TargetType.Self || ability.targetType == TargetType.AreaAroundCaster)
    {
        primaryTargetTransform = currentCasterStats.transform;
        primaryTargetStats = currentCasterStats;
        castPoint = currentCasterStats.transform.position;
    }
else if (ability.targetType == TargetType.Point_GroundTargeted)
{
    Debug.Log($"--- Casting {ability.abilityName} (Point_GroundTargeted) ---");
    Debug.Log($"Ability Range: {abilityRange}");

    RaycastHit hit;
    // generalObstacleLayerMask должна быть настроена в инспекторе. Убедись, что она НЕ включает слой игрока, ЕСЛИ ты хочешь, чтобы луч проходил СКВОЗЬ других персонажей/объекты,
    // чтобы всегда пытаться достать до земли на abilityRange. Если generalObstacleLayerMask включает стены и т.д., это нормально.
    // Стандартное поведение: луч, пущенный изнутри коллайдера, игнорирует этот коллайдер.
    // Но если между камерой и точкой на земле есть другой NPC, луч может попасть в него.
    // Чтобы луч всегда шел до земли (или до abilityRange), generalObstacleLayerMask должна быть пустой или содержать только непроходимые препятствия.
    // Либо, если мы хотим, чтобы зона ставилась на первое попавшееся препятствие:
    
    // Вариант 1: Луч останавливается на первом препятствии (кроме игрока, т.к. луч изнутри)
    // LayerMask firstHitMask = generalObstacleLayerMask; // Используем общую маску препятствий

    // Вариант 2: Луч пытается пройти СКВОЗЬ все (кроме игрока) до земли на abilityRange
    // Для этого маска должна быть либо очень специфичной (только слой "Ground"), либо почти пустой.
    // Но если мы хотим, чтобы зона ставилась НА объект, если он ближе чем земля, то Вариант 1.
    // Давай пока оставим generalObstacleLayerMask.

    Debug.Log($"Raycasting for initial point with mask: {LayerMask.LayerToName(0)}... (actually using generalObstacleLayerMask: {generalObstacleLayerMask.value})");
    if (Physics.Raycast(ray, out hit, abilityRange, generalObstacleLayerMask)) 
    {
        castPoint = hit.point; 
        Debug.Log($"Initial Raycast HIT object: {hit.collider.name} at distance: {hit.distance}. CastPoint set to: {castPoint}");
    }
    else 
    {
        castPoint = ray.GetPoint(abilityRange); 
        Debug.Log($"Initial Raycast MISSED or went full range. CastPoint set to max range: {castPoint}");
    }

    // "Приземляем" castPoint
    // groundLayerMask должна быть настроена в инспекторе.
    Debug.Log($"Attempting to ground castPoint: {castPoint} using groundLayerMask: {groundLayerMask.value}");
    RaycastHit groundHit;
    // Стреляем лучом вниз от точки (с небольшим смещением вверх, чтобы не быть внутри земли)
    // Увеличим дальность падения луча на всякий случай и высоту старта луча.
    if (Physics.Raycast(castPoint + Vector3.up * 5f, Vector3.down, out groundHit, 25f, groundLayerMask)) // Было: castPoint + Vector3.up * 0.5f, Vector3.down, out groundHit, 20f
    {
        castPoint = groundHit.point;
        Debug.Log($"Grounding successful. Final CastPoint: {castPoint}");
    }
    else
    {
        Debug.LogWarning($"{ability.abilityName}: Could not find a valid ground position near {castPoint} (initial point after range check). Zone not created.");
        this.feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: Could not find a valid ground position.");
        return; 
    }
        // primaryTargetTransform и primaryTargetStats остаются null
    }
    else // Single_Creature, Single_Interactable
    {
        // Проверка UI ховера
        if (playerGlobalActions != null && playerGlobalActions.IsCursorFree && hoveredPartyMemberTarget != null)
        {
            if (ability.targetType == TargetType.Single_Creature) // Можно бафать/лечить своих
            {
                primaryTargetTransform = hoveredPartyMemberTarget.transform;
            }
            // Если Single_Interactable, UI ховер на члене партии не должен работать для этой цели
        }
        
        // Если цель не выбрана через UI, делаем Raycast
        if (primaryTargetTransform == null) 
        {
            RaycastHit hit;
            LayerMask specificTargetMask = GetLayerMaskForAbility(ability); // Эта маска должна содержать ТОЛЬКО валидные слои для цели способности
                                                                        // Например, для Single_Creature - слой "Characters"
                                                                        // Для Телекинеза - слои "Characters" и "InteractableItems"
            if (Physics.Raycast(ray, out hit, abilityRange, specificTargetMask))
            {
                primaryTargetTransform = hit.transform;
            }
        }

        // Валидация цели для Телекинеза
        if (ability.abilityName == "Telekinesis") // TODO: Заменить на флаг IsTelekinesisAbility
        {
            bool isValidTelekinesisTarget = false;
            if (primaryTargetTransform != null)
            {
                Interactable interactableComponent = primaryTargetTransform.GetComponent<Interactable>();
                if (interactableComponent != null)
                {
                    CharacterStats targetStatsForTele = primaryTargetTransform.GetComponent<CharacterStats>();
                    if (targetStatsForTele != null) 
                    {
                        if (targetStatsForTele.IsDead || targetStatsForTele.GetComponent<LootableCorpse>() != null) 
                        {
                            isValidTelekinesisTarget = true;
                        }
                    }
                    else 
                    {
                        isValidTelekinesisTarget = true;
                    }
                }
            }
            if (!isValidTelekinesisTarget)
            {
                this.feedbackManager?.ShowFeedbackMessage("Telekinesis: Invalid target.");
                return; 
            }
        }
        
        // Установка castPoint и primaryTargetStats для Single_Target способностей
        if (primaryTargetTransform != null)
        {
            primaryTargetStats = primaryTargetTransform.GetComponent<CharacterStats>();
            castPoint = primaryTargetTransform.position; 
        }
    }

    // Финальные проверки валидности цели
    if ((ability.targetType == TargetType.Single_Creature || ability.targetType == TargetType.Single_Interactable) && primaryTargetTransform == null)
    {
        this.feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: Target not found.");
        return;
    }
    
    if (ability.targetType == TargetType.Single_Creature && primaryTargetStats == null)
    {
        // Исключение для Телекинеза на труп (LootableCorpse), который может быть Single_Creature по типу цели, но не иметь "живых" CharacterStats
        bool isTelekinesisOnCorpseWithoutStats = (ability.abilityName == "Telekinesis" && 
                                                 primaryTargetTransform != null && 
                                                 primaryTargetTransform.GetComponent<LootableCorpse>() != null &&
                                                 primaryTargetStats == null); // Если у трупа почему-то нет CharacterStats, но есть LootableCorpse

        if (!isTelekinesisOnCorpseWithoutStats)
        {
            this.feedbackManager?.ShowFeedbackMessage($"{ability.abilityName}: Target is not a creature.");
            return;
        }
    }
    
    // Вызов каста
    bool castInitiated = currentCasterAbilities.TryUseAbility(
        abilitySlot, 
        currentCasterStats, 
        this.feedbackManager, 
        primaryTargetTransform, 
        castPoint, 
        ability 
    );
    
    if (castInitiated)
    {
        // Общий фидбек, если нужен и не был дан ранее
        // (например, для способностей без состязания и без специфичного фидбека от эффектов)
        if (!ability.usesContest && ability.abilityName != "Telekinesis" && ability.targetType != TargetType.Self && ability.targetType != TargetType.AreaAroundCaster)
        {
            // Можно добавить фидбек, что способность X была использована на Y, если Y - это primaryTargetTransform.name
            // if(primaryTargetTransform != null) this.feedbackManager?.ShowFeedbackMessage($"{currentCasterStats.gameObject.name} uses {ability.abilityName} on {primaryTargetTransform.name}.");
            // else this.feedbackManager?.ShowFeedbackMessage($"{currentCasterStats.gameObject.name} uses {ability.abilityName}.");
        }
    }
}

    private LayerMask GetLayerMaskForAbility(AbilityData ability)
    {
        // Возвращаем маску в зависимости от того, на какие типы объектов может целиться способность
        // Это может быть слой "Characters", слой "Interactable", или их комбинация.
        // Если способность может целиться во что угодно (кроме игрока), то generalObstacleLayerMask.
        
        if (ability.targetType == TargetType.Single_Interactable)
        {
            if (ability.abilityName == "Telekinesis") // TODO: Заменить на флаг
            {
                // Телекинез на Interactable и Characters (трупы)
                // Предположим, у тебя есть слои "InteractableItems" и "Characters"
                return LayerMask.GetMask("InteractableItems", "Characters"); // Настрой свои слои
            }
            else
            {
                return LayerMask.GetMask("InteractableItems"); // Только интерактивные предметы
            }
        }
        else if (ability.targetType == TargetType.Single_Creature)
        {
            return LayerMask.GetMask("Characters"); // Только персонажи
        }
        
        // Для Point_GroundTargeted мы используем groundLayerMask для "приземления",
        // а для основного луча - маску, которая блокирует все, КРОМЕ игрока.
        // Эту маску можно назвать, например, "WorldGeometryAndCharactersAndInteractables"
        // или просто использовать generalObstacleLayerMask, если она включает все нужное.
        // Пока оставим так, в TryCastAbility логика будет конкретнее.

        return generalObstacleLayerMask; // По умолчанию для других случаев или если не указано
    }
        private Ray GetRayFromScreen()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("AbilityCastingSystem: Main Camera not found for Raycast!");
            return new Ray(transform.position, transform.forward); // Фоллбэк, но это плохо
        }
        if (playerGlobalActions != null && playerGlobalActions.IsCursorFree)
        {
            return mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        }
    }
}