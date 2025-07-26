using UnityEngine;
using System;

/// <summary>
/// Компонент, помечающий GameObject как сохраняемую сущность.
/// Автоматически генерирует и хранит уникальный идентификатор для этой сущности.
/// </summary>
[DisallowMultipleComponent]
[ExecuteInEditMode]
public class SaveableEntity : MonoBehaviour
{
    [SerializeField] private string uniqueId = "";
    public string UniqueId => uniqueId;

    private void Awake()
    {
        // В режиме редактора генерируем ID, если он пуст.
        // Это полезно при дублировании объектов, так как Reset() не вызывается.
        if (Application.isEditor && !Application.isPlaying)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                GenerateId();
            }
        }
    }

    // Вызывается при первом добавлении компонента в редакторе или через меню Reset.
    private void Reset()
    {
        GenerateId();
    }
    
    [ContextMenu("Generate New ID")]
    private void GenerateId()
    {
        uniqueId = Guid.NewGuid().ToString();
    }
}