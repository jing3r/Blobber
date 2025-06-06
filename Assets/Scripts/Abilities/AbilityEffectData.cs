// Все приведёныные ниже классы - в общем файле: AbilityEffectData.cs 
using UnityEngine;
using System; // Для Serializable
using UnityEngine.AI; // Для NavMesh.SamplePosition
using System.Collections;
using System.Collections.Generic; 

public enum AssociatedAttribute { Body, Agility, Spirit, Mind, Proficiency, None } // Proficiency добавлен
public enum TargetType { Self, Single_Creature, Single_Interactable, AreaAroundCaster, Point_GroundTargeted /*, Point_AreaEffect - пока убрали */ }
public enum AIStateForEffect { Idle, Wandering, Chasing, Attacking, Fleeing, Dead } // Для ChangeAIStateEffectData
public enum AlignmentForEffect { Friendly, Neutral, Hostile } // Для ChangeAlignmentEffectData


[Serializable]
public abstract class AbilityEffectData
{
    public virtual string GetDescription() => "Базовый эффект";
    // Метод для применения эффекта, будет переопределен в наследниках
    // casterStats: кто кастует
    // primaryTargetStats: основная одиночная цель-существо (может быть null)
    // primaryTargetTransform: основная одиночная цель-трансформ (для Interactable или если нет CharacterStats)
    // allTargetsInArea: список всех CharacterStats в зоне действия (для AoE)
    // successfulContest: результат состязания атрибутов (если оно было)
    public abstract void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea, bool successfulContest);
}

[Serializable]
public class HealEffectData : AbilityEffectData
{
    public int baseHealAmount = 10;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Mind;
    [Tooltip("Коэффициент для атрибута, например, 2 означает +2 ХП за каждое очко атрибута")]
    public float scaleFactor = 2f;
    [Tooltip("Если true и TargetType способности - AreaAroundCaster, эффект применится ко всем целям в allTargetsInArea. Иначе - только к primaryTargetStats.")]
    public bool applyToAllInAreaIfAoE = true;


    public override string GetDescription() => $"Лечит на {baseHealAmount} + ({scaleFactor} * {scalingAttribute}) HP.";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> allTargetsInArea, bool successfulContest)
    {
        if (!successfulContest && casterStats.GetComponent<AIController>() == null) return; // Состязание провалено для эффекта, если это не способность NPC (NPC пока игнорируют состязания для себя)

        int healAmount = baseHealAmount;
        if (scalingAttribute != AssociatedAttribute.None)
        {
            healAmount += Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
        }

        if (applyToAllInAreaIfAoE && allTargetsInArea != null && allTargetsInArea.Count > 0)
        {
            foreach (var target in allTargetsInArea)
            {
                if (target != null && !target.IsDead)
                {
                    target.Heal(healAmount);
                    // Debug.Log($"{casterStats.gameObject.name} heals {target.gameObject.name} for {healAmount} HP (AoE).");
                }
            }
        }
        else if (primaryTargetStats != null && !primaryTargetStats.IsDead)
        {
            primaryTargetStats.Heal(healAmount);
            // Debug.Log($"{casterStats.gameObject.name} heals {primaryTargetStats.gameObject.name} for {healAmount} HP.");
        }
    }
}

[Serializable]
public class DamageEffectData : AbilityEffectData // Для прямого урона, если понадобится отдельно от статусов
{
    public int baseDamageAmount = 5;
    public AssociatedAttribute scalingAttribute = AssociatedAttribute.Body;
    public float scaleFactor = 1f;
    [Tooltip("Если true и TargetType способности - AreaAroundCaster, эффект применится ко всем целям в allTargetsInArea. Иначе - только к primaryTargetStats.")]
    public bool applyToAllInAreaIfAoE = true;
    // public DamageType damageType; // В будущем

    public override string GetDescription() => $"Наносит {baseDamageAmount} + ({scaleFactor} * {scalingAttribute}) урона.";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> allTargetsInArea, bool successfulContest)
    {
        if (!successfulContest && casterStats.GetComponent<AIController>() == null) return;

        int damageAmount = baseDamageAmount;
        if (scalingAttribute != AssociatedAttribute.None)
        {
            damageAmount += Mathf.FloorToInt(casterStats.GetAttributeValue(scalingAttribute) * scaleFactor);
        }
        damageAmount = Mathf.Max(1, damageAmount); // Минимальный урон 1

        if (applyToAllInAreaIfAoE && allTargetsInArea != null && allTargetsInArea.Count > 0)
        {
            foreach (var target in allTargetsInArea)
            {
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(damageAmount, casterStats.transform);
                }
            }
        }
        else if (primaryTargetStats != null && !primaryTargetStats.IsDead)
        {
            primaryTargetStats.TakeDamage(damageAmount, casterStats.transform);
        }
    }
}


[Serializable]
public class ApplyStatusEffectData : AbilityEffectData
{
    [Tooltip("ScriptableObject с данными о статус-эффекте")]
    public StatusEffectData statusEffectToApply; // Ссылка на SO
    [Tooltip("Если true и TargetType способности - AreaAroundCaster, эффект применится ко всем целям в allTargetsInArea. Иначе - только к primaryTargetStats.")]
    public bool applyToAllInAreaIfAoE = true;

    public override string GetDescription() => statusEffectToApply != null ? $"Накладывает статус: {statusEffectToApply.statusName}" : "Накладывает (не указан)";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> allTargetsInArea, bool successfulContest)
    {
        if (statusEffectToApply == null)
        {
            Debug.LogWarning("ApplyStatusEffectData: StatusEffectToApply не назначен.");
            return;
        }
        
        // Состязание проверяется перед вызовом ApplyEffect для всей способности.
        // Если оно провалено, этот метод не должен был быть вызван для эффектов, зависящих от состязания.
        // Однако, если какой-то статус должен накладываться независимо от состязания (маловероятно), нужна доп. логика.
        // Пока считаем, что если состязание есть и оно провалено, то до ApplyEffect дело не доходит.
        // НО! Мы решили, что состязание влияет на ВСЕ эффекты способности.
        // Значит, если successfulContest == false, мы ничего не делаем здесь, если это способность игрока.
        // Для NPC, пока они не участвуют в состязаниях как кастеры, они всегда "успешны".
        
        if (!successfulContest && casterStats.GetComponent<AIController>() == null) // Если кастер - игрок и состязание провалено
        {
            // Debug.Log($"ApplyStatusEffect ({statusEffectToApply.statusName}): Состязание провалено, эффект не применен.");
            return;
        }


        if (applyToAllInAreaIfAoE && allTargetsInArea != null && allTargetsInArea.Count > 0)
        {
            foreach (var target in allTargetsInArea)
            {
                if (target != null) // Статус можно наложить и на мертвого, если так задумано (например, для последующего эффекта)
                {
                    CharacterStatusEffects statusHandler = target.GetComponent<CharacterStatusEffects>();
                    if (statusHandler != null)
                    {
                        statusHandler.ApplyStatus(statusEffectToApply, casterStats);
                    }
                }
            }
        }
        else if (primaryTargetStats != null)
        {
            CharacterStatusEffects statusHandler = primaryTargetStats.GetComponent<CharacterStatusEffects>();
            if (statusHandler != null)
            {
                statusHandler.ApplyStatus(statusEffectToApply, casterStats);
            }
        }
    }
}

[Serializable]
public class ChangeAIStateEffectData : AbilityEffectData
{
    public AIStateForEffect targetState = AIStateForEffect.Fleeing;

    public override string GetDescription() => $"Меняет состояние AI на: {targetState}";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> ___, bool successfulContest)
    {
        if (!successfulContest && casterStats.GetComponent<AIController>() == null) return;

        if (primaryTargetStats != null)
        {
            AIController targetAI = primaryTargetStats.GetComponent<AIController>();
            if (targetAI != null)
            {
                // Преобразуем наш enum в AIController.AIState
                if (Enum.TryParse(targetState.ToString(), out AIController.AIState aiState))
                {
                    if (targetState == AIStateForEffect.Fleeing)
                    {
                        targetAI.ForceFlee(casterStats.transform); // Убегать от кастера
                    }
                    else
                    {
                        targetAI.ChangeState(aiState);
                    }
                }
                else { Debug.LogWarning($"ChangeAIStateEffectData: Не удалось преобразовать {targetState} в AIController.AIState"); }
            }
        }
    }
}

[Serializable]
public class ChangeAlignmentEffectData : AbilityEffectData
{
    public AlignmentForEffect targetAlignment = AlignmentForEffect.Neutral;

    public override string GetDescription() => $"Меняет отношение AI на: {targetAlignment}";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> ___, bool successfulContest)
    {
        if (!successfulContest && casterStats.GetComponent<AIController>() == null) return;

        if (primaryTargetStats != null)
        {
            AIController targetAI = primaryTargetStats.GetComponent<AIController>();
            if (targetAI != null)
            {
                 if (Enum.TryParse(targetAlignment.ToString(), out AIController.Alignment alignment))
                 {
                    if (targetAlignment == AlignmentForEffect.Neutral && targetAI.currentAlignment == AIController.Alignment.Hostile)
                    {
                        targetAI.currentAlignment = AIController.Alignment.Neutral;
                        targetAI.ClearCurrentThreat(); 
                        targetAI.ChangeState(AIController.AIState.Idle); 
                        // УБРАН ВЫЗОВ FeedbackManager.Instance. Сообщение будет дано уровнем выше.
                        // Debug.Log($"{targetAI.gameObject.name} теперь нейтрален к {casterStats.gameObject.name}."); // Для отладки можно оставить Debug.Log
                    }
                 }
                 else { Debug.LogWarning($"ChangeAlignmentEffectData: Не удалось преобразовать {targetAlignment} в AIController.Alignment"); }
            }
        }
    }
}

[Serializable]
public class InteractEffectData : AbilityEffectData // Для Телекинеза
{
    public override string GetDescription() => "Взаимодействует с объектом на расстоянии.";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats _, Transform primaryTargetTransform, Vector3 __, ref List<CharacterStats> ___, bool successfulContest)
    {
        if (primaryTargetTransform != null)
        {
            Interactable interactable = primaryTargetTransform.GetComponent<Interactable>();
            if (interactable != null)
            {
                string feedback = interactable.Interact();
                // УБРАН ВЫЗОВ FeedbackManager.Instance.
                // PlayerGlobalActions уже должен давать фидбек от Interactable.Interact(), если он возвращает строку.
                // Если нужен дополнительный фидбек о том, что способность "Телекинез" сработала,
                // его должен дать AbilityCastingSystem или AbilityExecutor.
                if (!string.IsNullOrEmpty(feedback))
                {
                    // Debug.Log($"Телекинез на {interactable.name}: {feedback}");
                }
            }
        }
    }
}


public enum DisplacementDirectionType { AwayFromCaster, TowardsCaster, CasterForward, SpecificVector }

[Serializable]
public class DisplacementEffectData : AbilityEffectData
{
    [Tooltip("Базовая дистанция перемещения.")]
    public float baseDisplacementDistance = 3.0f;
    [Tooltip("Атрибут кастера, влияющий на дистанцию.")]
    public AssociatedAttribute distanceScalingAttribute = AssociatedAttribute.Body;
    [Tooltip("Сколько метров добавляется за каждое очко атрибута.")]
    public float distancePerAttributePoint = 0.5f;
    public DisplacementDirectionType directionType = DisplacementDirectionType.AwayFromCaster;
    [Tooltip("Если true, эффект применяется к кастеру, а не к primaryTargetStats.")]
    public bool targetIsCaster = false;
    [Tooltip("Общая длительность перемещения в секундах.")]
    public float displacementDuration = 0.3f; // Например, толчок длится 0.3 секунды
    // movementSteps убираем, будем двигать каждый FixedUpdate


    public override string GetDescription() => $"Перемещает цель на дистанцию, зависящую от Тела, за {displacementDuration} сек.";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform _, Vector3 __, ref List<CharacterStats> ___, bool successfulContest)
    {
        if (!targetIsCaster && !successfulContest && casterStats.GetComponent<AIController>() == null) return;

        CharacterStats actualTargetStats = targetIsCaster ? casterStats : primaryTargetStats;
        if (actualTargetStats == null) return;

        // Корутину должен запустить MonoBehaviour. actualTargetStats - это MonoBehaviour.
        actualTargetStats.StartCoroutine(DisplaceTargetCoroutine(casterStats, actualTargetStats));
    }

    private IEnumerator DisplaceTargetCoroutine(CharacterStats casterStats, CharacterStats targetToDisplace)
    {
        CharacterController targetController = null;
        if (targetToDisplace != null)
        {
            targetController = targetToDisplace.GetComponent<CharacterController>();
            if (targetController == null && targetToDisplace.GetComponent<AIController>() == null) // Член партии
            {
                PartyManager partyMgr = targetToDisplace.GetComponentInParent<PartyManager>();
                if (partyMgr != null) targetController = partyMgr.GetComponent<CharacterController>();
            }
        }

        if (targetController == null || !targetController.enabled)
        {
            Debug.LogWarning($"DisplacementCoroutine: CharacterController не найден или отключен для {targetToDisplace?.name}.");
            yield break; // Выходим из корутины
        }

        float finalDisplacementDistance = baseDisplacementDistance;
        if (distanceScalingAttribute != AssociatedAttribute.None && casterStats != null)
        {
            finalDisplacementDistance += casterStats.GetAttributeValue(distanceScalingAttribute) * distancePerAttributePoint;
        }
        finalDisplacementDistance = Mathf.Max(0.1f, finalDisplacementDistance);

        Vector3 moveDirection = Vector3.zero;
        Transform casterTransform = casterStats.transform;
        Transform targetForDirCalc = targetIsCaster ? casterTransform : targetToDisplace.transform;

        switch (directionType)
        {
            case DisplacementDirectionType.AwayFromCaster:
                if (targetIsCaster) { Debug.LogWarning("DisplacementEffect: AwayFromCaster нелогично для targetIsCaster."); yield break; }
                moveDirection = (casterTransform.position == targetForDirCalc.position) ?
                                targetForDirCalc.forward :
                                (targetForDirCalc.position - casterTransform.position).normalized;
                break;
            case DisplacementDirectionType.TowardsCaster:
                if (targetIsCaster) { Debug.LogWarning("DisplacementEffect: TowardsCaster нелогично для targetIsCaster."); yield break; }
                moveDirection = (casterTransform.position == targetForDirCalc.position) ?
                                -targetForDirCalc.forward :
                                (casterTransform.position - targetForDirCalc.position).normalized;
                break;
            case DisplacementDirectionType.CasterForward:
                Transform lookTransform = casterTransform;
                PlayerMovement playerMovement = casterStats.GetComponentInParent<PlayerMovement>();
                if (playerMovement != null)
                {
                    Camera playerCam = playerMovement.GetComponentInChildren<Camera>();
                    if (playerCam != null) lookTransform = playerCam.transform;
                }
                moveDirection = lookTransform.forward;
                break;
        }
        moveDirection.y = 0;
        moveDirection = moveDirection.normalized;

        if (moveDirection == Vector3.zero) yield break;

        float elapsedTime = 0f;
        // Скорость перемещения = общая дистанция / общее время
        float speed = finalDisplacementDistance / Mathf.Max(0.01f, displacementDuration); // Защита от деления на ноль

        // Блокируем NavMeshAgent у AI, если это он, чтобы не конфликтовал с принудительным движением
        AIMovement aiMovement = targetToDisplace.GetComponent<AIMovement>();
        UnityEngine.AI.NavMeshAgent agent = (aiMovement != null) ? aiMovement.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
        bool agentWasEnabled = false;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true; // Останавливаем агента
            // agent.enabled = false; // Отключаем NavMeshAgent на время толчка - более радикально
            // Вместо отключения можно попробовать agent.Move(), но он может конфликтовать с CharacterController.Move()
            // Либо использовать NavMeshAgent.Warp() для мгновенного перемещения по NavMesh, но это не даст физики столкновений.
            // Самый безопасный способ, если есть CharacterController - двигать его, а NavMeshAgent пусть будет isStopped.
            // Но для корректной работы NavMeshAgent после толчка, его нужно будет "синхронизировать" с новой позицией.
            // Пока что просто isStopped = true.
            agentWasEnabled = true;
        }


        // Debug.Log($"Starting displacement for {targetToDisplace.name}: Dist={finalDisplacementDistance}, Dir={moveDirection}, Dur={displacementDuration}, Speed={speed}");

        while (elapsedTime < displacementDuration)
        {
            // Рассчитываем смещение для текущего кадра (FixedUpdate для физики)
            Vector3 step = moveDirection * speed * Time.fixedDeltaTime;
            targetController.Move(step);
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate(); // Ждем следующего FixedUpdate для физически корректного движения
        }

        // После завершения толчка, если это был AI, нужно обновить позицию NavMeshAgent, если он был активен
        if (agentWasEnabled && agent != null) // && !agent.enabled) // Если мы отключали agent.enabled
        {
            // agent.enabled = true; // Включаем обратно
            // Если агент был просто isStopped, то после толчка он может оказаться не на NavMesh.
            // Попытаемся его "телепортировать" на ближайшую точку NavMesh.
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false; // Разрешаем агенту снова двигаться
            }
            else
            {
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(targetController.transform.position, out navHit, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position); // Телепортируем на NavMesh
                    agent.isStopped = false;
                    // Debug.Log($"{targetToDisplace.name} warped to NavMesh after push and resumed.");
                }
                else
                {
                    Debug.LogWarning($"{targetToDisplace.name} could not be placed on NavMesh after push.");
                    // Возможно, AI нужно перевести в состояние Idle или заставить искать путь заново
                    // agent.isStopped = true; // Оставляем остановленным, если не на NavMesh
                }
            }
        }
        // Debug.Log($"{targetToDisplace.name} displacement finished.");
    }
}

[Serializable]
public class CreateZoneEffectData : AbilityEffectData
{
    [Tooltip("Префаб объекта-зоны, который будет создан.")]
    public GameObject zonePrefab;

    [Tooltip("Базовая длительность существования зоны в секундах.")]
    public float baseZoneDuration = 10f;

    [Tooltip("Атрибут кастера, влияющий на длительность зоны.")]
    public AssociatedAttribute durationScalingAttribute = AssociatedAttribute.Mind;

    [Tooltip("Сколько секунд добавляется к длительности за каждое очко атрибута.")]
    public float durationPerAttributePoint = 1.0f; // Изменено с 3.0 на 1.0 для Quagmire (Mind * 1.0 * 3 = Mind * 3)
                                                   // Или можно было бы в AbilityData Quagmire поставить durationPerAttributePoint = 3

    [Header("Zone Placement Options")]
    [Tooltip("Максимальная высота над землей, на которой может быть создана зона. Если точка выше, зона не создастся.")]
    public float maxPlacementHeightAboveGround = 1.5f;
    [Tooltip("Слой(и), считающийся 'землей' для размещения зоны.")]
    public LayerMask groundPlacementMask; // Настрой в инспекторе AbilityData -> Quagmire -> CreateZoneEffectData


    public override string GetDescription() => $"Creates a zone '{zonePrefab?.name}'.";

    public override void ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats _, Transform __, Vector3 castPointReceived, ref List<CharacterStats> ___, bool ____)
    {

        if (zonePrefab == null)
        {
            Debug.LogWarning("CreateZoneEffectData: ZonePrefab is not assigned.");
            return;
        }
        if (casterStats == null)
        {
            Debug.LogWarning("CreateZoneEffectData: CasterStats is null.");
            return;
        }
        if (sourceAbility == null)
        {
            Debug.LogWarning("CreateZoneEffectData: SourceAbility is null (needed for radius).");
            return;
        }

        // Рассчитываем финальную длительность зоны
        float finalDuration = baseZoneDuration;
        if (durationScalingAttribute != AssociatedAttribute.None)
        {
            finalDuration += casterStats.GetAttributeValue(durationScalingAttribute) * durationPerAttributePoint;
        }
        finalDuration = Mathf.Max(1.0f, finalDuration); // Минимальная длительность зоны 1 секунда

        // --- "Приземление" зоны ---
        // castPoint - это точка, куда указал игрок (либо на объекте, либо на макс. дальности луча)
        Vector3 finalSpawnPosition = castPointReceived;
        RaycastHit groundHit;

        // Пытаемся найти землю прямо под castPoint или немного ниже
        if (Physics.Raycast(castPointReceived + Vector3.up * 0.5f, Vector3.down, out groundHit, maxPlacementHeightAboveGround + 0.5f, groundPlacementMask))
        {
            finalSpawnPosition = groundHit.point;
            // Debug.Log($"Zone placed on ground at: {finalSpawnPosition}");
        }
        else
        {
            // Если не нашли землю точно под точкой, можно попробовать NavMesh.SamplePosition
            // или просто отменить создание зоны, если она должна быть строго на земле.
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(castPointReceived, out navHit, 2.0f, NavMesh.AllAreas))
            {
                finalSpawnPosition = navHit.position;
                // Debug.Log($"Zone snapped to NavMesh at: {finalSpawnPosition}");
            }
            else
            {
                Debug.LogWarning($"CreateZoneEffectData: Could not find a valid ground position near {castPointReceived} for zone '{zonePrefab.name}'. Zone not created.");
                // Если зона обязательно должна быть на земле/NavMesh, отменяем создание
                return;
            }
        }
        // --------------------------

        GameObject zoneInstance = GameObject.Instantiate(zonePrefab, finalSpawnPosition, Quaternion.identity);

        // Настройка радиуса зоны (масштаба или коллайдера)
        // Предположим, что у префаба зоны есть SphereCollider для триггера,
        // и его радиус в префабе настроен на 0.5 (чтобы диаметр был 1).
        // Тогда, чтобы получить нужный радиус из AbilityData, мы должны умножить scale на (areaOfEffectRadius / 0.5)
        SphereCollider zoneTrigger = zoneInstance.GetComponent<SphereCollider>();
        if (zoneTrigger != null)
        {
            zoneTrigger.radius = sourceAbility.areaOfEffectRadius;
            // Если визуал зоны (например, простой цилиндр/сфера) также должен масштабироваться,
            // и он в префабе имеет размер, соответствующий диаметру 1 (радиусу 0.5), то:
            // float scaleMultiplier = sourceAbility.areaOfEffectRadius / 0.5f;
            // zoneInstance.transform.localScale = new Vector3(scaleMultiplier, zoneInstance.transform.localScale.y, scaleMultiplier);
            // Для простого цилиндра, который лежит на земле, Y scale обычно не меняют или делают маленьким.
            // Пока просто меняем радиус коллайдера. Визуал должен быть настроен в префабе или управляться отдельно.
        }
        else
        {
            // Если нет SphereCollider, можно попытаться масштабировать весь объект,
            // но это менее точно и зависит от исходного размера префаба.
            // zoneInstance.transform.localScale = Vector3.one * sourceAbility.areaOfEffectRadius * 2f; // Примерно, если префаб 1x1x1
            Debug.LogWarning($"CreateZoneEffectData: Zone prefab '{zonePrefab.name}' does not have a SphereCollider. Radius not set dynamically.");
        }

        // Инициализация контроллера зоны
        ZoneEffectController zoneController = zoneInstance.GetComponent<ZoneEffectController>();
        if (zoneController != null)
        {
            zoneController.Initialize(casterStats, finalDuration, sourceAbility);
        }
        else
        {
            // Если у префаба зоны нет своего контроллера, он просто будет существовать и исчезнет
            // (но не будет иметь эффектов без контроллера)
            Debug.LogWarning($"CreateZoneEffectData: Zone prefab '{zonePrefab.name}' is missing ZoneEffectController script.");
            GameObject.Destroy(zoneInstance, finalDuration); // Все равно уничтожаем по таймеру
        }
    }
}


// Конец файла AbilityEffectData.cs 