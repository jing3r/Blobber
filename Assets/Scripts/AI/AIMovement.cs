using UnityEngine;
using UnityEngine.AI; // Для NavMeshAgent

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterStats))] // Убедимся, что CharacterStats есть
public class AIMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform followTarget;
    private bool isFollowingTarget = false;
    private CharacterStats myStats; // Ссылка на CharacterStats этого NPC

    // Свойства для чтения состояния агента
    public bool IsMoving => agent.velocity.sqrMagnitude > 0.01f && agent.hasPath && !agent.isStopped;
    public bool HasReachedDestination => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);
    public float StoppingDistance 
    {
        get => agent.stoppingDistance;
        set => agent.stoppingDistance = value;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        myStats = GetComponent<CharacterStats>(); 

        if (myStats == null)
        {
            Debug.LogError($"AIMovement на {gameObject.name} не может найти CharacterStats! AI движение будет отключено.", this);
            enabled = false; 
            return;
        }
        if (agent == null)
        {
             Debug.LogError($"AIMovement на {gameObject.name} не может найти NavMeshAgent! AI движение будет отключено.", this);
            enabled = false;
            return;
        }
    }

    void Start() 
    {
        // Устанавливаем начальную скорость NavMeshAgent из CharacterStats
        // CharacterStats.Awake -> RecalculateAllStats -> CurrentMovementSpeed уже должен быть рассчитан
        if (agent.isOnNavMesh) // Проверяем, что агент на NavMesh перед установкой скорости
        {
            agent.speed = myStats.CurrentMovementSpeed;
        }
        else
        {
            // Debug.LogWarning($"AIMovement Start: {gameObject.name} is not on NavMesh. Speed not set yet.");
            // Можно попробовать установить скорость позже, если агент появится на NavMesh,
            // или убедиться, что NPC спаунятся на NavMesh.
        }

        // Подписываемся на изменение атрибутов, чтобы обновлять скорость агента,
        // если CurrentMovementSpeed в CharacterStats изменится (например, от статусов или смены Agility)
        myStats.onAttributesChanged += UpdateAgentSpeedFromStats;
    }
    
    void OnDestroy() 
    {
        // Отписываемся от события, чтобы избежать ошибок
        if (myStats != null)
        {
            myStats.onAttributesChanged -= UpdateAgentSpeedFromStats;
        }
    }

    /// <summary>
    /// Обновляет скорость NavMeshAgent на основе CurrentMovementSpeed из CharacterStats.
    /// Вызывается событием onAttributesChanged.
    /// </summary>
    private void UpdateAgentSpeedFromStats()
    {
        if (agent.isOnNavMesh && myStats != null) // Дополнительная проверка на myStats
        {
            if (agent.speed != myStats.CurrentMovementSpeed) // Обновляем только если значение изменилось
            {
                agent.speed = myStats.CurrentMovementSpeed;
                // Debug.Log($"{gameObject.name} NavMeshAgent speed updated to: {agent.speed}");
            }
        }
    }

    void Update()
    {
        // Основная логика движения при следовании за целью
        if (isFollowingTarget && followTarget != null && agent.isOnNavMesh && !agent.isStopped)
        {
            // Проверяем, действительно ли нужно обновлять цель каждый кадр,
            // NavMeshAgent сам будет следовать к установленной цели.
            // Обновление может быть полезно, если цель очень быстро меняет направление.
            // Но для оптимизации можно обновлять реже, если followTarget не слишком быстро движется.
            // Пока оставим обновление каждый кадр для точности.
            if (agent.destination != followTarget.position) // Оптимизация: устанавливаем только если изменилась
            {
                agent.SetDestination(followTarget.position);
            }
        }
    }

    /// <summary>
    /// Отправляет AI к указанной точке.
    /// </summary>
    /// <returns>True, если путь успешно установлен.</returns>
    public bool MoveTo(Vector3 destination)
    {
        isFollowingTarget = false; // Прекращаем следование, если было
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false; // Убедимся, что агент может двигаться
            return agent.SetDestination(destination);
        }
        // Debug.LogWarning($"{gameObject.name} (AIMovement.MoveTo): Not on NavMesh. Cannot set destination.");
        return false;
    }

    /// <summary>
    /// Заставляет AI следовать за указанной целью.
    /// </summary>
    public void Follow(Transform target)
    {
        if (target != null)
        {
            followTarget = target;
            isFollowingTarget = true;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false; // Убедимся, что агент может двигаться
                agent.SetDestination(target.position); // Устанавливаем первую цель
            }
            // else Debug.LogWarning($"{gameObject.name} (AIMovement.Follow): Not on NavMesh. Cannot start following {target.name}.");
        }
        else
        {
            // Если цель null, прекращаем следование и останавливаемся
            StopMovement(true);
            isFollowingTarget = false;
            followTarget = null;
        }
    }

    /// <summary>
    /// Останавливает текущее движение AI.
    /// </summary>
    /// <param name="cancelPath">Если true, текущий путь будет сброшен.</param>
    public void StopMovement(bool cancelPath = true)
    {
        isFollowingTarget = false; // В любом случае прекращаем следование
        if (agent.isOnNavMesh)
        {
            if (!agent.isStopped) agent.isStopped = true;
            if (cancelPath && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
    
    /// <summary>
    /// Возобновляет движение по текущему пути, если он был.
    /// </summary>
    public void ResumeMovement()
    {
        if (agent.isOnNavMesh && agent.hasPath) // Возобновляем только если есть путь
        {
            agent.isStopped = false;
        }
    }

    // Метод SetSpeed(float speed) можно оставить, если нужна возможность внешне
    // ПЕРЕОПРЕДЕЛИТЬ скорость агента, игнорируя CharacterStats.
    // Но если скорость всегда должна браться из CharacterStats, то он не нужен,
    // так как UpdateAgentSpeedFromStats будет делать это автоматически.
    // Если он остается, он должен быть использован с осторожностью.
    // Пока оставим его, на случай если AI состояниям понадобится временно изменить скорость
    // (например, для рывка AI или замедленного движения при поиске).
    public void SetSpeed(float speed)
    {
        if (agent.isOnNavMesh)
        {
            agent.speed = speed;
        }
    }

    /// <summary>
    /// Возвращает текущую установленную скорость NavMeshAgent.
    /// </summary>
    public float GetSpeed()
    {
        return agent.isOnNavMesh ? agent.speed : 0f;
    }

    public bool IsOnNavMesh() => agent.isOnNavMesh;
    public bool IsStopped() => agent.isStopped;
    
    public void EnableAgent() { if (!agent.enabled) agent.enabled = true; }
    public void DisableAgent() { if (agent.enabled) agent.enabled = false; }

    public void ResetAndStopAgent()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if(agent.hasPath) agent.ResetPath();
        }
        isFollowingTarget = false;
        followTarget = null; // Сбрасываем цель следования
    }

    /// <summary>
    /// Мгновенно поворачивает AI лицом к цели.
    /// </summary>
    public void FaceTarget(Transform target) 
    {
        if (target == null) return;
        Vector3 direction = (target.position - transform.position).normalized;
        if (direction != Vector3.zero) // Проверка, чтобы избежать ошибки с LookRotation(0,0,0)
        {
            // Поворачиваем только по оси Y
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = lookRotation; 
        }
    }
}