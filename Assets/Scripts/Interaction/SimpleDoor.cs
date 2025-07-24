// SimpleDoor.cs

using UnityEngine;

public class SimpleDoor : Interactable, ISaveable
{
    [Tooltip("Сообщение после успешного взаимодействия (открытия).")]
    public string interactionFeedback = "The door is opened";
    [Tooltip("Сообщение, если дверь уже открыта.")]
    public string alreadyOpenFeedback = "The door is already open";

    private bool isOpen = false;
    
    public override string Interact()
    {
        if (!isOpen)
        {
            isOpen = true;
            // Здесь в будущем будет логика анимации, звука и т.д.
            // Никаких отключений коллайдеров или других "физических" изменений здесь.
            return interactionFeedback;
        }
        else
        {
            return alreadyOpenFeedback;
        }
    }

    #region SaveSystem
    
    [System.Serializable]
    private struct DoorSaveData
    {
        public bool isOpenState;
    }

    public object CaptureState()
    {
        return new DoorSaveData
        {
            isOpenState = this.isOpen
        };
    }

    public void RestoreState(object state)
    {
        if (state is DoorSaveData saveData)
        {
            this.isOpen = saveData.isOpenState;

            // Если нужно визуально отобразить открытое состояние после загрузки,
            // код для этого должен быть здесь. Например:
            // if (this.isOpen) {
            //     GetComponent<MeshRenderer>().material.color = Color.green; // Пример
            // }
        }
    }

    #endregion
}