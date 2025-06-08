
using UnityEngine;
using UnityEngine.InputSystem;

public class TargetingSystem : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private PlayerGlobalActions playerGlobalActions;
    [SerializeField] private Transform cameraTransform;

    void Awake()
    {
        if (playerGlobalActions == null) playerGlobalActions = GetComponent<PlayerGlobalActions>();
        if (cameraTransform == null) cameraTransform = GetComponentInChildren<Camera>()?.transform;
        
        if (playerGlobalActions == null || cameraTransform == null)
        {
            Debug.LogError("TargetingSystem: Необходимые ссылки не найдены! Система не будет работать.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Основной метод для получения цели-трансформа (существа, интерактивные объекты).
    /// </summary>
    public bool TryGetTarget(float maxDistance, LayerMask layerMask, out RaycastHit hitInfo)
    {
        return PerformRaycast(out hitInfo, maxDistance, layerMask);
    }
    
    /// <summary>
    /// Специализированный метод для получения точки на земле (для AoE).
    /// </summary>
    public bool TryGetGroundPoint(float maxDistance, LayerMask groundDetectionLayers, out Vector3 point)
    {
        point = Vector3.zero;
        RaycastHit hit;

        // Сначала пускаем луч на максимальную дистанцию, чтобы определить начальную точку
        // Используем общую маску, чтобы луч останавливался на препятствиях
        if (!PerformRaycast(out hit, maxDistance, groundDetectionLayers))
        {
            // Если луч ни во что не попал, берем точку на максимальной дальности
            hit.point = GetRayFromScreen().GetPoint(maxDistance);
        }
        
        // Теперь "приземляем" эту точку
        if (Physics.Raycast(hit.point + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 25f, groundDetectionLayers))
        {
            point = groundHit.point;
            return true;
        }

        Debug.LogWarning("TargetingSystem: Could not find a valid ground position.");
        return false;
    }
    
    /// <summary>
    /// Возвращает луч в зависимости от состояния курсора.
    /// </summary>
    public Ray GetRayFromScreen()
    {
        Camera mainCamera = cameraTransform.GetComponent<Camera>();
        if (playerGlobalActions.IsCursorFree)
        {
            return mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        }
    }
    
    /// <summary>
    /// Централизованная логика рейкаста.
    /// </summary>
    private bool PerformRaycast(out RaycastHit hitInfo, float maxDistance, LayerMask layerMask)
    {
        Ray ray = GetRayFromScreen();
        return Physics.Raycast(ray, out hitInfo, maxDistance, layerMask);
    }
}