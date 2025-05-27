using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("BillboardUI: Main Camera not found! Script will be disabled.", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // Поворачиваем объект так, чтобы он всегда был обращен к камере
            transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                             mainCameraTransform.rotation * Vector3.up);
        }
    }
}