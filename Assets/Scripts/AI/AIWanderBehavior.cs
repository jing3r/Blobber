using UnityEngine;
using UnityEngine.AI; // Для NavMeshHit

[RequireComponent(typeof(AIMovement))] // AIWanderBehavior будет использовать AIMovement для движения
public class AIWanderBehavior : MonoBehaviour
{
    [Header("Wander Parameters")]
    public float wanderRadius = 5f;
    public float minWanderWaitTime = 2f;
    public float maxWanderWaitTime = 5f;

    private AIMovement movement;
    private float nextWanderTimeInternal = 0f;
    private Vector3 currentWanderDestinationInternal;
    private bool isWanderingToActiveDestinationInternal = false;

    // Публичные свойства для чтения из AIController/состояний
    public float GetNextWanderTime => nextWanderTimeInternal;
    public bool IsWanderingToActiveDestination => isWanderingToActiveDestinationInternal;
    public Vector3 GetCurrentWanderDestination => currentWanderDestinationInternal;
    
    void Awake()
    {
        movement = GetComponent<AIMovement>();
        if (movement == null)
        {
            Debug.LogError($"AIWanderBehavior ({gameObject.name}): AIMovement component not found! Wander behavior will not function.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Инициализирует таймер для первого блуждания. Вызывается из AIController.Start.
    /// </summary>
    public void InitializeWanderTimer()
    {
        nextWanderTimeInternal = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
        isWanderingToActiveDestinationInternal = false; // Начинаем не двигаясь
    }

    /// <summary>
    /// Пытается найти новую точку для блуждания и установить её в AIMovement.
    /// </summary>
    /// <param name="ownerPosition">Позиция владельца AI (transform.position из AIController).</param>
    /// <returns>True, если удалось найти валидную точку и начать движение к ней.</returns>
    public bool TrySetNewWanderDestination(Vector3 ownerPosition)
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection.y = 0; // Для блуждания по плоскости (если не хотим прыгать на разные уровни)
        randomDirection += ownerPosition;
        NavMeshHit navHit; 

        if (NavMesh.SamplePosition(randomDirection, out navHit, wanderRadius, NavMesh.AllAreas))
        {
            currentWanderDestinationInternal = navHit.position;
            isWanderingToActiveDestinationInternal = true;
            movement.MoveTo(navHit.position); // Запускаем движение
            return true;
        }
        else 
        {
            // Если не удалось найти точку в радиусе, попробуем очень близко к текущей позиции
            Vector3 fallbackRandomDir = Random.insideUnitSphere * 2f;
            fallbackRandomDir.y = 0;
            fallbackRandomDir += ownerPosition;

            if (NavMesh.SamplePosition(fallbackRandomDir, out navHit, 2f, NavMesh.AllAreas))
            {
                currentWanderDestinationInternal = navHit.position;
                isWanderingToActiveDestinationInternal = true;
                movement.MoveTo(navHit.position); // Запускаем движение
                return true;
            }
        }
        isWanderingToActiveDestinationInternal = false; // Точку не нашли
        return false;
    }

    /// <summary>
    /// Обновляет логику блуждания (вызывается из AIStateWandering.UpdateState).
    /// </summary>
    /// <returns>True, если блуждание продолжается (еще не достигли цели).</returns>
    public bool UpdateWander()
    {
        if (!movement.IsOnNavMesh() || !isWanderingToActiveDestinationInternal)
        {
            return false; // Не можем блуждать или нет активной цели
        }

        // movement.MoveTo() уже был вызван в TrySetNewWanderDestination.
        // Здесь мы просто проверяем, достигнута ли цель.
        return !movement.HasReachedDestination; // Возвращаем true, если НЕ достигли цели
    }

    /// <summary>
    /// Сбрасывает текущее состояние блуждания и таймер.
    /// </summary>
    public void StopWandering()
    {
        isWanderingToActiveDestinationInternal = false;
        movement.StopMovement(); // Останавливаем движение, если оно было
        nextWanderTimeInternal = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime); // Готовим таймер к следующему блужданию
    }

    /// <summary>
    /// Только сбрасывает таймер, не сбрасывая текущее движение.
    /// </summary>
    public void ResetWanderTimer()
    {
        nextWanderTimeInternal = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);
    }
}