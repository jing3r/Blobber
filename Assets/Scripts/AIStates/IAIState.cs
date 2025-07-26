/// <summary>
/// Интерфейс для всех состояний в State Machine AI.
/// </summary>
public interface IAIState
{
    void EnterState(AIController controller);
    void UpdateState(AIController controller);
    void ExitState(AIController controller);
    AIController.AIState GetStateType();
}