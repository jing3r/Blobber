using UnityEngine;

public class AIStateWandering : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Wandering;
    
    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Wandering state.");
        // AIWanderBehavior.TrySetNewWanderDestination уже вызывает movement.MoveTo()
        // Так что здесь не нужно вызывать movement.ResumeMovement()
        // Если AI вошел сюда, но у него нет активной точки блуждания, пусть AIWanderBehavior это обрабатывает
        if (!controller.WanderBehavior.IsWanderingToActiveDestination)
        {
            // Если нет активной цели, попробуем найти ее. Если не получится, вернемся в Idle.
            if (!controller.WanderBehavior.TrySetNewWanderDestination(controller.transform.position))
            {
                controller.ChangeState(AIController.AIState.Idle);
                return;
            }
        }
    }

    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Проверка на необходимость бегства при виде игрока (для не-враждебных)
        if (controller.fleesOnSightOfPlayer &&
            controller.currentAlignment != AIController.Alignment.Hostile &&
            controller.Perception.PlayerTarget != null)
        {
            if (controller.Perception.IsPlayerSpottedForFleeing())
            {
                controller.ForceFlee(controller.Perception.PlayerTarget);
                return;
            }
        }

        // Приоритет 2: Проверка на новую враждебную угрозу (если AI враждебен)
        if (controller.currentAlignment == AIController.Alignment.Hostile)
        {
            Transform potentialThreat = controller.Perception.GetPrimaryHostileThreatAggro();
            if (potentialThreat != null)
            {
                controller.SetCurrentThreat(potentialThreat);
                controller.ChangeState(AIController.AIState.Chasing);
                return;
            }
        }

        // Логика блуждания: просто обновляем AIWanderBehavior
        if (!controller.Movement.IsOnNavMesh() || !controller.WanderBehavior.IsWanderingToActiveDestination)
        {
            // Если не на NavMesh или нет активной точки блуждания, возвращаемся в Idle
            controller.ChangeState(AIController.AIState.Idle); 
            return;
        }
        
        if (controller.Movement.HasReachedDestination) // Проверяем достижение цели через AIMovement
        {
            controller.ChangeState(AIController.AIState.Idle); // Достигли точки, переходим в Idle
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Wandering state.");
        controller.WanderBehavior.StopWandering(); // Останавливаем блуждание и сбрасываем таймер
    }
}