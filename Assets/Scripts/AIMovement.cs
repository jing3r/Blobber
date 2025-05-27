using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform followTarget;
    private bool isFollowingTarget = false;

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
    }

    void Update()
    {
        if (isFollowingTarget && followTarget != null && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.SetDestination(followTarget.position);
        }
    }

    public bool MoveTo(Vector3 destination)
    {
        isFollowingTarget = false; // Прекращаем следование, если было
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            return agent.SetDestination(destination);
        }
        return false;
    }

    public void Follow(Transform target)
    {
        if (target != null)
        {
            followTarget = target;
            isFollowingTarget = true;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position); // Первоначальная установка цели
            }
        }
    }

    public void StopMovement(bool cancelPath = true)
    {
        isFollowingTarget = false;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if (cancelPath && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
    
    public void ResumeMovement()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    public void SetSpeed(float speed)
    {
        if (agent.isOnNavMesh)
        {
            agent.speed = speed;
        }
    }

    public float GetSpeed()
    {
        return agent.isOnNavMesh ? agent.speed : 0f;
    }

    public bool IsOnNavMesh()
    {
        return agent.isOnNavMesh;
    }

    public bool IsStopped()
    {
        return agent.isStopped;
    }
    
    public void EnableAgent()
    {
        if (!agent.enabled) agent.enabled = true;
    }

    public void DisableAgent()
    {
        if (agent.enabled) agent.enabled = false;
    }

    public void ResetAndStopAgent()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if(agent.hasPath) agent.ResetPath();
        }
        isFollowingTarget = false;
    }
}