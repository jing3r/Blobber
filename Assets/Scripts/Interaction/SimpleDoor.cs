using UnityEngine;

/// <summary>
/// Управляет состоянием простой двери, с которой можно взаимодействовать.
/// </summary>
public class SimpleDoor : Interactable, ISaveable
{
    [Header("Настройки фидбека")]
    [SerializeField] private string interactionFeedback = "The door opens.";
    [SerializeField] private string alreadyOpenFeedback = "The door is already open.";
    
    // [SerializeField] private Animator doorAnimator; // Для будущей анимации
    [SerializeField] private bool isOpen = false;

    public override string Interact()
    {
        if (isOpen)
        {
            return alreadyOpenFeedback;
        }
        
        isOpen = true;
        // TODO: Запустить анимацию открытия двери
        // doorAnimator?.SetTrigger("Open");
        
        return interactionFeedback;
    }
    
    #region Save System Implementation
    [System.Serializable]
    private struct DoorSaveData
    {
        public bool IsOpen;
    }

    public object CaptureState()
    {
        return new DoorSaveData { IsOpen = this.isOpen };
    }

    public void RestoreState(object state)
    {
        if (state is DoorSaveData saveData)
        {
            this.isOpen = saveData.IsOpen;
            // TODO: Принудительно установить визуальное состояние двери (открыта/закрыта)
        }
    }
    #endregion
}