using UnityEngine;

public class Interactable : MonoBehaviour
{
    [Tooltip("Текст, отображаемый при наведении на объект.")]
    public string interactionPrompt = "Interact";

    /// <summary>
    /// Вызывается при взаимодействии с объектом.
    /// </summary>
    /// <returns>Сообщение о результате взаимодействия для отображения игроку, или null/empty.</returns>
    public virtual string Interact()
    {
        return null;
    }
}