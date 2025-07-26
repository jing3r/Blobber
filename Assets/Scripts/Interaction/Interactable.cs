using UnityEngine;

/// <summary>
/// Базовый класс для всех объектов, с которыми игрок может взаимодействовать.
/// </summary>
public class Interactable : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Текст, отображаемый при наведении на объект (например, 'Открыть дверь', 'Обыскать тело').")]
    private string interactionPrompt = "Interact";
    public string InteractionPrompt => interactionPrompt;

    /// <summary>
    /// Вызывается при взаимодействии с объектом. Логика реализуется в дочерних классах.
    /// </summary>
    /// <returns>Строка-результат для отображения в UI (может быть null).</returns>
    public virtual string Interact()
    {
        return null;
    }
}