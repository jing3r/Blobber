using UnityEngine;

/// <summary>
/// Отвечает за "органы чувств" AI: зрение, слух (в будущем) и обнаружение угроз.
/// Предоставляет "сырые" данные о восприятии для AIController.
/// </summary>
[RequireComponent(typeof(AIController))]
public class AIPerception : MonoBehaviour
{
    [Header("Параметры зрения")]
    [SerializeField] [Range(0, 360)] private float visionConeAngle = 180f;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Параметры агрессии")]
    [SerializeField] private float engageRadius = 10f;
    public float EngageRadius => engageRadius;
    
    [SerializeField] private float disengageRadius = 15f;
    public float DisengageRadius => disengageRadius;

    [Header("Параметры бегства")]
    [SerializeField] private float fleeOnSightRadius = 12f;
    public float FleeOnSightRadius => fleeOnSightRadius;
    
    private Transform playerPartyTransform;

    public Transform PrimaryHostileThreat { get; private set; }

    // Для оптимизации восприятие обновляется с заданной периодичностью, а не каждый кадр.
    [SerializeField] private float perceptionUpdateInterval = 0.2f;
    private float nextPerceptionUpdateTime;

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerPartyTransform = playerObject.transform;
        }
        
        // Рандомизирует первый вызов, чтобы AI не "думали" все одновременно
        nextPerceptionUpdateTime = Time.time + Random.Range(0, perceptionUpdateInterval);
    }

    private void Update()
    {
        if (Time.time >= nextPerceptionUpdateTime)
        {
            UpdatePerception();
            nextPerceptionUpdateTime = Time.time + perceptionUpdateInterval;
        }
    }

    /// <summary>
    /// Проверяет, находится ли игрок в зоне видимости и в радиусе для начала бегства.
    /// </summary>
    public bool IsPlayerSpottedForFleeing()
    {
        return PrimaryHostileThreat != null && Vector3.Distance(transform.position, PrimaryHostileThreat.position) <= fleeOnSightRadius;
    }

    /// <summary>
    /// Проверяет, есть ли прямая видимость до цели.
    /// </summary>
    public bool HasLineOfSightToTarget(Transform target)
    {
        if (target == null) return false;

        // Поднимаем точки, чтобы луч не ушел под землю.
        Vector3 startPoint = transform.position + Vector3.up * 0.5f;
        Vector3 endPoint = target.position + Vector3.up * 0.5f;
        
        var targetCollider = target.GetComponent<Collider>();
        if(targetCollider != null)
        {
            endPoint = targetCollider.bounds.center;
        }

        if (Physics.Linecast(startPoint, endPoint, out var hit, obstacleLayers))
        {
            // Если луч попал в дочерний объект цели, это все равно считается прямой видимостью.
            return hit.transform.IsChildOf(target) || hit.transform == target;
        }
        
        return true;
    }

    /// <summary>
    /// Основная логика обновления восприятия.
    /// </summary>
    private void UpdatePerception()
    {
        // TODO: Расширить для оценки угроз от других NPC, а не только от игрока.
        PrimaryHostileThreat = null; 
        
        if (playerPartyTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerPartyTransform.position);
        
        // Оптимизация: если игрок слишком далеко, нет смысла в дальнейших проверках.
        if (distanceToPlayer > disengageRadius) return;
        
        if (IsTargetInVisionCone(playerPartyTransform) && HasLineOfSightToTarget(playerPartyTransform))
        {
            PrimaryHostileThreat = playerPartyTransform;
        }
    }

    private bool IsTargetInVisionCone(Transform target)
    {
        if (visionConeAngle >= 360f) return true; 

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        if (directionToTarget == Vector3.zero) return true;
        
        return Vector3.Angle(transform.forward, directionToTarget) < visionConeAngle / 2;
    }
}