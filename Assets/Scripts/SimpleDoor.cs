using UnityEngine;

public class SimpleDoor : Interactable
{
    [Tooltip("Сообщение после успешного взаимодействия (открытия).")]
    public string interactionFeedback = "Дверь открыта";
    [Tooltip("Сообщение, если дверь уже открыта.")]
    public string alreadyOpenFeedback = "Дверь уже открыта";

    private bool isOpen = false;

    public override string Interact()
    {
        if (!isOpen)
        {
            // Здесь может быть логика анимации, звука и т.д.
            // gameObject.SetActive(false); // Если дверь должна исчезнуть
            // transform.Rotate(0, 90, 0); // Если дверь должна повернуться
            isOpen = true;
            return interactionFeedback;
        }
        else
        {
            return alreadyOpenFeedback;
        }
    }
}