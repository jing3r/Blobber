using UnityEngine;

public class AIStateIdle : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Idle;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Idle state.");
        if (controller.Movement.IsOnNavMesh() && !controller.Movement.IsStopped())
        {
            controller.Movement.StopMovement();
        }
        // Сбросить флаг блуждания, если он был установлен
        controller.ResetWanderingState();
    }

    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Стать враждебным и преследовать, если есть угроза
        if (controller.currentAlignment == AIController.Alignment.Hostile && controller.CurrentThreat != null)
        {
            // Проверка расстояния до угрозы должна быть здесь или в Perception, 
            // AIController должен решить о смене состояния на основе этой информации.
            // Для простоты, если есть угроза и мы враждебны, переходим в Chasing.
            // AIController.Update() должен был установить CurrentThreat, если он в радиусе агрессии.
            controller.ChangeState(AIController.AIState.Chasing);
            return;
        }

        // Приоритет 2: Блуждать, если разрешено и пришло время
        if (controller.canWander && Time.time >= controller.GetNextWanderTime() && controller.Movement.IsOnNavMesh())
        {
            if (controller.TrySetNewWanderDestination())
            {
                controller.ChangeState(AIController.AIState.Wandering);
            }
            else // Не удалось найти точку, сбрасываем таймер ожидания
            {
                controller.ResetWanderTimer();
            }
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Idle state.");
    }
}