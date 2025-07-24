using UnityEngine;

public class AIStateIdle : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Idle;

    public void EnterState(AIController controller)
    {
        if (controller.Movement.IsOnNavMesh() && !controller.Movement.IsStopped())
        {
            controller.Movement.StopMovement();
        }
        controller.WanderBehavior.StopWandering(); // Останавливаем блуждание, если оно было
        controller.ClearCurrentThreat(); // В Idle у нас нет активной угрозы
    }


    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Проверка на необходимость бегства при виде игрока (для не-враждебных)
        if (controller.fleesOnSightOfPlayer &&
            controller.currentAlignment != AIController.Alignment.Hostile &&
            controller.Perception.PlayerTarget != null && // PlayerTarget из Perception уже учитывает все для "замечания"
            controller.Perception.IsPlayerSpottedForFleeing()) // Проверяет радиус бегства
        {
            controller.ForceFlee(controller.Perception.PlayerTarget);
            return;
        }

        // Приоритет 2: Проверка на новую враждебную угрозу (если AI враждебен)
        if (controller.currentAlignment == AIController.Alignment.Hostile)
        {
            // Состояние САМО опрашивает Perception
            Transform potentialThreat = controller.Perception.GetPrimaryHostileThreatAggro();
            if (potentialThreat != null)
            {
                controller.SetCurrentThreat(potentialThreat); // Состояние устанавливает угрозу
                controller.ChangeState(AIController.AIState.Chasing); // Состояние инициирует переход
                return;
            }
        }

        // Приоритет 3: Переход к блужданию
        // Используем controller.canWander, которое теперь в AIController
        if (controller.canWander && Time.time >= controller.WanderBehavior.GetNextWanderTime && controller.Movement.IsOnNavMesh())
        {
            if (controller.WanderBehavior.TrySetNewWanderDestination(controller.transform.position))
            {
                controller.ChangeState(AIController.AIState.Wandering);
            }
            else
            {
                controller.WanderBehavior.ResetWanderTimer();
            }
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Idle state.");
    }
}