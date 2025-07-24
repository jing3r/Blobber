using UnityEngine;
using System;

// Атрибут DisallowMultipleComponent запрещает вешать этот скрипт на один объект дважды.
[DisallowMultipleComponent]
// ExecuteInEditMode позволяет коду в Awake() и Reset() работать прямо в редакторе Unity.
[ExecuteInEditMode]
public class SaveableEntity : MonoBehaviour
{
    // [SerializeField] заставляет Unity сохранять это поле, даже если оно приватное.
    [SerializeField] private string uniqueId = "";

    // Публичное свойство только для чтения, чтобы другие скрипты могли получить ID.
    public string UniqueId => uniqueId;

    private void Awake()
    {
        // Этот код сработает только в редакторе, а не в собранной игре.
        // Он нужен на случай, если вы скопируете объект (Ctrl+D),
        // т.к. при копировании Reset() не вызывается, а Awake() - да.
        if (Application.isEditor && !Application.isPlaying)
        {
            // Если ID пуст (новый объект) или если мы обнаружили дубликат (скопированный объект),
            // то генерируем новый ID.
            // Проверка на дубликат - это продвинутая тема, пока ограничимся проверкой на пустоту.
            if (string.IsNullOrEmpty(uniqueId))
            {
                GenerateId();
            }
        }
    }

    // Этот метод вызывается Unity автоматически, когда вы впервые добавляете компонент к объекту
    // или нажимаете "Reset" в контекстном меню компонента.
    private void Reset()
    {
        GenerateId();
    }
    
    // Добавляет опцию "Generate New ID" в меню компонента (три точки в инспекторе).
    // Полезно, если вы все-таки умудрились создать дубликат и хотите исправить это вручную.
    [ContextMenu("Generate New ID")]
    private void GenerateId()
    {
        // Генерируем новый глобально уникальный идентификатор и преобразуем его в строку.
        uniqueId = Guid.NewGuid().ToString();
    }
}