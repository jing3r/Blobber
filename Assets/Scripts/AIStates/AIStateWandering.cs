using UnityEngine;

/// <summary>
/// Состояние блуждания. AI движется к случайно выбранной точке.
/// </summary>
public class AIStateWandering : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Wandering;

    public void EnterState(AIController controller)
    {
        // Если по какой-то причине мы вошли в это состояние, не имея цели,
        // немедленно пытаемся ее найти или вернуться в Idle.
        if (!controller.WanderBehavior.IsWandering)
        {
            if (!controller.WanderBehavior.TrySetNewWanderDestination())
            {
                controller.ChangeState(AIController.AIState.Idle);
            }
        }
    }
    
    public void ExitState(AIController controller)
    {
        // Важно остановить блуждание при выходе из состояния, чтобы сбросить таймер.
        controller.WanderBehavior.StopWandering();
    }

    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Обнаружение угрозы (аналогично Idle).
        var perceivedThreat = controller.Perception.PrimaryHostileThreat;
        if (perceivedThreat != null)
        {
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

        // Приоритет 2: Продолжение движения к цели.
        controller.WanderBehavior.UpdateWanderState();
        if (!controller.WanderBehavior.IsWandering)
        {
            // Цель достигнута, возвращаемся в Idle.
            controller.ChangeState(AIController.AIState.Idle);
        }
    }
}