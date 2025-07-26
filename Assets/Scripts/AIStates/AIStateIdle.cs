using UnityEngine;

/// <summary>
/// Состояние бездействия. AI ожидает, ищет угрозы или решает начать блуждать.
/// </summary>
public class AIStateIdle : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Idle;

    public void EnterState(AIController controller)
    {
        controller.Movement.StopMovement();
        controller.ClearCurrentThreat();
    }

    public void ExitState(AIController controller) { }

    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Обнаружение угрозы.
        // AIPerception предоставляет готовый результат, нужно только на него отреагировать.
        var perceivedThreat = controller.Perception.PrimaryHostileThreat;
        if (perceivedThreat != null)
        {
            // Проверяем, нужно ли убегать или атаковать
            if (controller.FleesOnSightOfPlayer && controller.Perception.IsPlayerSpottedForFleeing())
            {
                controller.ForceFlee(perceivedThreat);
                return;
            }
            
            if (controller.CurrentAlignment == AIController.Alignment.Hostile)
            {
                controller.BecomeHostileTowards(perceivedThreat);
            }
            return;
        }

        // Приоритет 2: Переход к блужданию, если пришло время.
        if (controller.CanWander && controller.WanderBehavior.IsReadyForNewWanderPoint)
        {
            if (controller.WanderBehavior.TrySetNewWanderDestination())
            {
                controller.ChangeState(AIController.AIState.Wandering);
            }
        }
    }
}