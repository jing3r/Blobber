// Файл: IAIState.cs
using UnityEngine;

public interface IAIState
{
    void EnterState(AIController controller);
    void UpdateState(AIController controller);
    void ExitState(AIController controller);
    AIController.AIState GetStateType(); // Чтобы AIController знал, какой тип состояния активен
}