using UnityEngine;
using System.Collections.Generic; // Понадобится, если будем возвращать списки целей

public class AIPerception : MonoBehaviour
{
    [Header("Sight Parameters")]
    public float sightRadius = 15f;
    public float visionConeAngle = 180f; // Угол обзора в градусах (полный угол)
    public LayerMask targetLayers;    // Слои, на которых могут быть цели (игрок, другие NPC)
    public LayerMask obstacleLayers;  // Слои, которые блокируют зрение

    [Header("Aggro Parameters")]
    public float engageRadius = 10f;
    public float disengageRadius = 15f;

    [Header("Flee on Sight Parameters")]
    public float fleeOnSightRadius = 12f; // Радиус для бегства при виде игрока

    // Обнаруженные сущности (могут обновляться не каждый кадр для оптимизации)
    public Transform PlayerTarget { get; private set; }
    public Transform PrimaryHostileThreat { get; private set; } // Наиболее приоритетная враждебная цель
    // public List<Transform> VisibleTargets { get; private set; } // Для будущего расширения

    private Transform playerPartyTransformInternal;
    private AIController aiController; // Ссылка на основной AIController для доступа к currentAlignment

    // Для оптимизации можно обновлять восприятие не каждый кадр
    [SerializeField] private float perceptionUpdateInterval = 0.2f; // e.g., 5 times per second
    private float nextPerceptionUpdateTime;

    void Awake()
    {
        aiController = GetComponentInParent<AIController>(); // Предполагаем, что Perception - дочерний или на том же объекте
        if (aiController == null)
        {
            Debug.LogError($"AIPerception ({gameObject.name}): AIController not found! Perception will not function.", this);
            enabled = false;
        }
        // VisibleTargets = new List<Transform>();
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerPartyTransformInternal = playerObject.transform;
        }
        nextPerceptionUpdateTime = Time.time + Random.Range(0, perceptionUpdateInterval); // Рандомизация первого апдейта
    }

    void Update()
    {
        if (Time.time >= nextPerceptionUpdateTime)
        {
            UpdatePerception();
            nextPerceptionUpdateTime = Time.time + perceptionUpdateInterval;
        }
    }

private void UpdatePerception()
{
    // Сначала сбрасываем цели этого кадра
    PlayerTarget = null; 
    // PrimaryHostileThreat сбрасывать здесь не нужно, AIController сам решит, когда его "забыть"
    // или он будет перезаписан, если найдется новая приоритетная цель.
    // Мы можем иметь временную переменную для "увиденной в этом кадре" приоритетной цели.
    Transform currentlyPerceivedPrimaryThreat = null;


    // 1. Обнаружение игрока (с учетом конуса, LOS и sightRadius)
    if (playerPartyTransformInternal != null)
    {
        if (IsTargetInRadius(playerPartyTransformInternal, sightRadius) && 
            IsTargetInVisionCone(playerPartyTransformInternal) && // Для первоначального "замечания"
            HasLineOfSightToTarget(playerPartyTransformInternal, true)) // true - для строгого LOS без учета "памяти"
        {
            PlayerTarget = playerPartyTransformInternal; // Игрок замечен в этом кадре
        }
    }

    // 2. Определение основной враждебной угрозы (то, что AI видит ПРЯМО СЕЙЧАС как самую опасную цель)
    // Это может быть игрок или другой NPC. Пока фокусируемся на игроке.
    if (PlayerTarget != null) // Если игрок вообще замечен
    {
        // Если AI враждебен ИЛИ игрок в engageRadius (даже если AI еще не враждебен, он может сагриться)
        // Игрок становится кандидатом в PrimaryHostileThreat, если он в engageRadius.
        if (IsTargetInRadius(PlayerTarget, engageRadius)) // Используем engageRadius для определения агрессии
        {
             // Если AI уже враждебен к игроку, или просто видит игрока в engageRadius,
             // то игрок - это текущая воспринимаемая угроза.
             currentlyPerceivedPrimaryThreat = PlayerTarget;
        }
    }
    // TODO: Добавить логику для обнаружения других NPC как PrimaryHostileThreat на основе фракций и т.д.

    // Обновляем публичное свойство PrimaryHostileThreat
    // Это то, что AIController будет использовать для принятия решений об агрессии
    PrimaryHostileThreat = currentlyPerceivedPrimaryThreat; 

    // VisibleTargets.Clear();
    // ... (логика для других целей, если нужна) ...
}


    public bool IsPlayerVisibleAndInEngageRadius()
    {
        return PlayerTarget != null && Vector3.Distance(transform.position, PlayerTarget.position) <= engageRadius;
    }
    
    public bool IsPlayerVisibleAndInFleeRadius()
    {
         return PlayerTarget != null && Vector3.Distance(transform.position, PlayerTarget.position) <= fleeOnSightRadius;
    }

    public bool IsTargetInRadius(Transform target, float radius)
    {
        if (target == null) return false;
        return Vector3.Distance(transform.position, target.position) <= radius;
    }

    private bool IsTargetInVisionCone(Transform target)
    {
        if (target == null) return false;
        if (visionConeAngle >= 360f) return true; 

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        if (directionToTarget == Vector3.zero) return true; // Если цель в той же точке, считаем ее видимой в конусе
        return Vector3.Angle(transform.forward, directionToTarget) < visionConeAngle / 2;
    }
    public Transform GetPrimaryHostileThreatAggro()
{
    // Этот метод должен возвращать цель, на которую AI должен сагриться,
    // если она в engageRadius и есть LOS.
    // PrimaryHostileThreat уже должен быть установлен корректно в UpdatePerception.
    if (PrimaryHostileThreat != null && 
        IsTargetInRadius(PrimaryHostileThreat, engageRadius) && 
        HasLineOfSightToTarget(PrimaryHostileThreat, true)) // Снова проверяем LOS на момент запроса
    {
        return PrimaryHostileThreat;
    }
    return null;
}
// Новый метод для AIController (для бегства при виде)
    public bool IsPlayerSpottedForFleeing()
    {
        // PlayerTarget устанавливается в UpdatePerception с учетом конуса, LOS и sightRadius.
        // Здесь мы просто проверяем, что PlayerTarget установлен и находится в fleeOnSightRadius.
        return PlayerTarget != null && IsTargetInRadius(PlayerTarget, fleeOnSightRadius);
    }


// HasLineOfSightToTarget МОЖЕТ БЫТЬ ПЕРЕГРУЖЕН для разных нужд
public bool HasLineOfSightToTarget(Transform target, bool strictLOSCheck = false)
{
    if (target == null) return false;
    
    // Если не строгая проверка, и у AI есть "память" или он уже сфокусирован,
    // то можно вернуть true, даже если LOS был потерян на мгновение.
    // Но для AIPerception, который предоставляет "сырые" данные, лучше всегда делать строгую проверку.
    // AIController будет решать, как использовать эту информацию с учетом памяти.

    Vector3 rayStartPoint = transform.position + Vector3.up * 0.5f; 
    Vector3 targetPoint = target.position + Vector3.up * 0.5f; 

    CharacterController cc = target.GetComponent<CharacterController>();
    Collider col = target.GetComponent<Collider>(); // Получаем коллайдер один раз

    if (cc != null) targetPoint = target.position + cc.center;
    else if (col != null) targetPoint = col.bounds.center;
    
    if (rayStartPoint == targetPoint) return true;

    RaycastHit hit;
    // Debug.DrawLine(rayStartPoint, targetPoint, Color.magenta, 0.1f); // Для отладки LOS
    if (Physics.Linecast(rayStartPoint, targetPoint, out hit, obstacleLayers))
    {
        // Проверяем, попали ли мы в саму цель или ее дочерний объект (например, часть модели)
        // или в другой коллайдер, принадлежащий тому же корневому объекту цели.
        if (hit.transform == target || hit.transform.IsChildOf(target)) return true;
        
        // Если у цели несколько коллайдеров, но мы попали не в тот, что на основном transform,
        // а в другой коллайдер того же объекта, это тоже считается LOS.
        if (col != null && hit.collider == col) return true; 
        
        // Более сложная проверка, если у цели сложная иерархия с коллайдерами
        // CharacterStats targetRootStats = target.GetComponentInParent<CharacterStats>(); // Или другая корневая сущность
        // CharacterStats hitRootStats = hit.transform.GetComponentInParent<CharacterStats>();
        // if (targetRootStats != null && hitRootStats == targetRootStats) return true;

        // Debug.Log($"LOS to {target.name} blocked by {hit.collider.name}");
        return false; // LOS заблокирован чем-то другим
    }
    return true; // Препятствий нет
}

    private bool IsChildOrSame(Transform hitTransform, Transform targetTransform)
    {
        if (hitTransform == targetTransform) return true;
        return hitTransform.IsChildOf(targetTransform);
    }
}