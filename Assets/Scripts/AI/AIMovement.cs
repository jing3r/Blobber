using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Обеспечивает навигацию AI с использованием NavMeshAgent.
/// Отвечает за движение к точке, следование за целью и повороты.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterStats))]
public class AIMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private CharacterStats myStats;
    private float stateSpeedMultiplier = 1.0f; 
    
    private Transform followTarget;
    private bool isFollowingTarget;
    
    public bool IsMoving => agent.velocity.sqrMagnitude > 0.01f && agent.hasPath && !agent.isStopped;
    public bool HasReachedDestination => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);
    public Vector3 Destination => agent.destination;
    public float StoppingDistance
    {
        get => agent.stoppingDistance;
        set => agent.stoppingDistance = value;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        myStats = GetComponent<CharacterStats>(); 
    }

    private void Start() 
    {
        UpdateAgentSpeedFromStats();
        
        // Подписка для автоматического обновления скорости при изменении статов.
        myStats.onAttributesChanged += UpdateAgentSpeedFromStats;
    }
    
    private void OnDestroy() 
    {
        if (myStats != null)
        {
            myStats.onAttributesChanged -= UpdateAgentSpeedFromStats;
        }
    }

    private void Update()
    {
        // Обновляем позицию назначения, только если AI находится в режиме следования
        // и позиция цели изменилась (небольшая оптимизация).
        if (isFollowingTarget && followTarget != null && agent.isOnNavMesh && !agent.isStopped)
        {
            if (agent.destination != followTarget.position)
            {
                agent.SetDestination(followTarget.position);
            }
        }
    }

    /// <summary>
    /// Отправляет AI к указанной точке в мире.
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        isFollowingTarget = false;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    /// <summary>
    /// Заставляет AI постоянно следовать за указанной целью.
    /// </summary>
    public void Follow(Transform target)
    {
        if (target != null)
        {
            followTarget = target;
            isFollowingTarget = true;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }
        else
        {
            StopMovement();
        }
    }

    /// <summary>
    /// Останавливает любое текущее движение AI.
    /// </summary>
    public void StopMovement(bool cancelPath = true)
    {
        isFollowingTarget = false;
        followTarget = null;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if (cancelPath && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
    
    /// <summary>
    /// Мгновенно поворачивает AI лицом к цели.
    /// </summary>
    public void FaceTarget(Transform target) 
    {
        if (target == null) return;
        
        Vector3 direction = (target.position - transform.position);
        direction.y = 0; // Поворот только в горизонтальной плоскости

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    /// <summary>
    /// Полностью сбрасывает состояние агента. Используется при смерти AI.
    /// </summary>
    public void ResetAndStopAgent()
    {
        StopMovement();
    }
    
    public void EnableAgent() { if (agent != null && !agent.enabled) agent.enabled = true; }
    public void DisableAgent() { if (agent != null && agent.enabled) agent.enabled = false; }

    /// <summary>
    /// Устанавливает множитель скорости для текущего состояния AI (например, 0.7 для блуждания, 1.2 для бегства).
    /// </summary>
    public void SetStateSpeedMultiplier(float multiplier)
    {
        stateSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        UpdateAgentSpeedFromStats();
    }

    /// <summary>
    /// Синхронизирует скорость NavMeshAgent со значением из CharacterStats.
    /// </summary>
    private void UpdateAgentSpeedFromStats()
    {
            float targetSpeed = myStats.CurrentMovementSpeed * stateSpeedMultiplier;
            if (agent.speed != targetSpeed)
            {
                agent.speed = targetSpeed;
            }
    }
}