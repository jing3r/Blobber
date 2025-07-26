using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Предоставляет централизованные методы для определения цели в мире (Raycasting).
/// Учитывает состояние курсора (свободный/заблокированный).
/// </summary>
[RequireComponent(typeof(PlayerGlobalActions))]
public class TargetingSystem : MonoBehaviour
{
    // Кэшированные ссылки
    private PlayerGlobalActions playerGlobalActions;
    private Camera mainCamera;

    private void Awake()
    {
        playerGlobalActions = GetComponent<PlayerGlobalActions>();
        mainCamera = GetComponentInChildren<Camera>();
        
        if (mainCamera == null)
        {
            Debug.LogError($"[{nameof(TargetingSystem)}] Main camera not found as a child of '{gameObject.name}'. Disabling component.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Пытается найти объект в мире по лучу от камеры.
    /// </summary>
    /// <returns>True, если объект на указанном слое был найден.</returns>
    public bool TryGetTarget(float maxDistance, LayerMask layerMask, out RaycastHit hitInfo)
    {
        Ray ray = GetRayFromScreenCenterOrCursor();
        return Physics.Raycast(ray, out hitInfo, maxDistance, layerMask);
    }
    
    /// <summary>
    /// Пытается найти точку на земле, на которую смотрит игрок.
    /// Используется для способностей, нацеливаемых на землю.
    /// </summary>
    public bool TryGetGroundPoint(float maxDistance, LayerMask groundLayer, out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = GetRayFromScreenCenterOrCursor();

        // Если луч попадает напрямую в землю
        if (Physics.Raycast(ray, out var hit, maxDistance, groundLayer))
        {
            point = hit.point;
            return true;
        }

        // Если луч уходит "в небо" или в стену, ищем ближайшую точку на земле под ним
        Vector3 pointAtMaxDistance = ray.GetPoint(maxDistance);
        if (Physics.Raycast(pointAtMaxDistance + Vector3.up * 5f, Vector3.down, out var groundHit, 25f, groundLayer))
        {
            point = groundHit.point;
            return true;
        }

        return false;
    }
    
    private Ray GetRayFromScreenCenterOrCursor()
    {
        if (playerGlobalActions.IsCursorFree)
        {
            // Если курсор свободен, пускаем луч из его позиции
            return mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            // Если курсор заблокирован, пускаем луч из центра экрана
            return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        }
    }
}