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
    public float aggroRadiusPlayer = 10f; // Радиус агрессии конкретно на игрока

    [Header("Flee on Sight Parameters")]
    public float fleeOnSightRadiusPlayer = 12f; // Радиус для бегства при виде игрока

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
        PlayerTarget = null;
        PrimaryHostileThreat = null;
        // VisibleTargets.Clear();

        // Обнаружение игрока
        if (playerPartyTransformInternal != null)
        {
            if (IsTargetInRadius(playerPartyTransformInternal, sightRadius) && IsTargetInVisionCone(playerPartyTransformInternal) && HasLineOfSightToTarget(playerPartyTransformInternal))
            {
                PlayerTarget = playerPartyTransformInternal;
            }
        }

        // Обнаружение основной враждебной угрозы (пока фокусируемся на игроке, если он видим и AI враждебен)
        // В будущем здесь будет более сложная логика с фракциями и другими NPC
        if (aiController.currentAlignment == AIController.Alignment.Hostile && PlayerTarget != null)
        {
             float distanceToPlayer = Vector3.Distance(transform.position, PlayerTarget.position);
             if (distanceToPlayer <= aggroRadiusPlayer) // Используем aggroRadiusPlayer для определения угрозы
             {
                PrimaryHostileThreat = PlayerTarget;
             }
        }
        // Если AI не враждебен, но игрок слишком близко в aggroRadius, он может стать угрозой, если AI станет враждебным
        else if (PlayerTarget != null && Vector3.Distance(transform.position, PlayerTarget.position) <= aggroRadiusPlayer)
        {
            // Не устанавливаем PrimaryHostileThreat, но AIController может использовать PlayerTarget
            // для решения о смене currentAlignment на Hostile.
        }


        // Пример для поиска других целей (можно расширить)
        // Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, sightRadius, targetLayers);
        // foreach (Collider targetCollider in targetsInViewRadius)
        // {
        //     if (targetCollider.transform == transform || targetCollider.transform == playerPartyTransformInternal) continue; // Skip self and player if already handled

        //     if (IsTargetInVisionCone(targetCollider.transform) && HasLineOfSightToTarget(targetCollider.transform))
        //     {
        //         VisibleTargets.Add(targetCollider.transform);
        //         // Логика определения PrimaryHostileThreat среди NPC
        //     }
        // }
    }

    public bool IsPlayerVisibleAndInAggroRadius()
    {
        return PlayerTarget != null && Vector3.Distance(transform.position, PlayerTarget.position) <= aggroRadiusPlayer;
    }
    
    public bool IsPlayerVisibleAndInFleeRadius()
    {
         return PlayerTarget != null && Vector3.Distance(transform.position, PlayerTarget.position) <= fleeOnSightRadiusPlayer;
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

    public bool HasLineOfSightToTarget(Transform target)
    {
        if (target == null) return false;
        
        Vector3 rayStartPoint = transform.position + Vector3.up * 0.5f; 
        Vector3 targetPoint = target.position + Vector3.up * 0.5f; 

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null) targetPoint = target.position + cc.center;
        else {
            Collider col = target.GetComponent<Collider>();
            if (col != null) targetPoint = col.bounds.center;
        }
        
        // Проверка, что точки не совпадают, чтобы избежать ошибки Linecast
        if (rayStartPoint == targetPoint) return true; // Если точки совпадают, считаем, что есть прямая видимость

        RaycastHit hit;
        if (Physics.Linecast(rayStartPoint, targetPoint, out hit, obstacleLayers))
        {
            return hit.transform == target || IsChildOrSame(hit.transform, target);
        }
        return true; 
    }

    private bool IsChildOrSame(Transform hitTransform, Transform targetTransform)
    {
        if (hitTransform == targetTransform) return true;
        return hitTransform.IsChildOf(targetTransform);
    }
}