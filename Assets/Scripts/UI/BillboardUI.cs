using UnityEngine;

/// <summary>
/// Поворачивает этот GameObject лицом к основной камере сцены.
/// Используется для UI-элементов в мировом пространстве (например, полоски здоровья над врагами).
/// </summary>
public class BillboardUI : MonoBehaviour
{
    private Transform cameraTransform;

    private void Awake()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError($"[{nameof(BillboardUI)}] - Main Camera not found on object '{gameObject.name}'. Disabling component.", this);
            enabled = false;
        }
    }
    
    private void LateUpdate()
    {
        // Поворачиваем Z-ось нашего объекта в ту же сторону, что и Z-ось камеры.
        transform.forward = cameraTransform.forward;
    }
}