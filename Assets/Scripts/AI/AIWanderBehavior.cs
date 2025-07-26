using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Реализует логику блуждания (Wandering) для AI.
/// Находит случайные точки в заданном радиусе и управляет движением к ним.
/// </summary>
[RequireComponent(typeof(AIMovement))]
public class AIWanderBehavior : MonoBehaviour
{
    [Header("Параметры блуждания")]
    [SerializeField] private float wanderRadius = 5f;
    [SerializeField] private float minWanderWaitTime = 2f;
    [SerializeField] private float maxWanderWaitTime = 5f;

    private AIMovement movement;
    private float nextWanderTime;
    private bool isWanderingToDestination;

    public bool IsWandering => isWanderingToDestination;
    public bool IsReadyForNewWanderPoint => Time.time >= nextWanderTime;

    private void Awake()
    {
        movement = GetComponent<AIMovement>();
    }

    /// <summary>
    /// Инициализирует таймер для первого блуждания. Вызывается из AIController.
    /// </summary>
    public void InitializeWanderTimer()
    {
        ResetWanderTimer();
        isWanderingToDestination = false;
    }

    /// <summary>
    /// Пытается найти новую точку для блуждания и начать движение к ней.
    /// </summary>
    /// <returns>True, если удалось найти валидную точку и начать движение.</returns>
    public bool TrySetNewWanderDestination()
    {
        if (FindRandomNavMeshPoint(transform.position, wanderRadius, out Vector3 destination))
        {
            movement.MoveTo(destination);
            isWanderingToDestination = true;
            return true;
        }
        
        // Если не удалось найти точку, сбрасываем таймер и попробуем позже
        ResetWanderTimer();
        isWanderingToDestination = false;
        return false;
    }

    /// <summary>
    /// Вызывается из состояния AIStateWandering для проверки, достигнута ли цель.
    /// </summary>
    public void UpdateWanderState()
    {
        if (movement.HasReachedDestination)
        {
            StopWandering();
        }
    }

    /// <summary>
    /// Прекращает текущее движение и сбрасывает таймер для следующего блуждания.
    /// </summary>
    public void StopWandering()
    {
        isWanderingToDestination = false;
        movement.StopMovement();
        ResetWanderTimer();
    }

    private void ResetWanderTimer()
    {
        nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
    }

    /// <summary>
    /// Вспомогательный метод для поиска случайной доступной точки на NavMesh.
    /// </summary>
    private bool FindRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += origin;
        
        if (NavMesh.SamplePosition(randomDirection, out var navHit, radius, NavMesh.AllAreas))
        {
            result = navHit.position;
            return true;
        }
        
        result = origin;
        return false;
    }
}