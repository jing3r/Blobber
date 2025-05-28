// Файл: AIStateWandering.cs
using UnityEngine;

public class AIStateWandering : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Wandering;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Wandering state.");
        if (controller.Movement.IsOnNavMesh() && controller.Movement.IsStopped())
        {
            // Движение к wanderDestination должно было быть установлено до входа в это состояние
            // или будет установлено в первой итерации UpdateState, если нужно.
            // controller.Movement.ResumeMovement(); // AIMovement.MoveTo сам снимет isStopped
        }
    }

    public void UpdateState(AIController controller)
    {
        // Приоритет 1: Стать враждебным и преследовать
        if (controller.currentAlignment == AIController.Alignment.Hostile && controller.CurrentThreat != null)
        {
            controller.ChangeState(AIController.AIState.Chasing);
            return;
        }

        // Проверка, что мы все еще можем двигаться и есть цель блуждания
        if (!controller.Movement.IsOnNavMesh() || !controller.IsWanderingToActiveDestination())
        {
            controller.ChangeState(AIController.AIState.Idle); // Если что-то пошло не так с блужданием
            return;
        }
        
        // Двигаемся к точке блуждания (AIMovement.MoveTo должно быть вызвано AIController при переходе или здесь)
        // AIController должен передать wanderDestination в AIMovement.MoveTo()
        // и этот метод только проверяет достижение
        controller.Movement.MoveTo(controller.GetCurrentWanderDestination());


        if (controller.Movement.HasReachedDestination)
        {
            controller.ChangeState(AIController.AIState.Idle);
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Wandering state.");
        // Остановить текущее движение блуждания, если оно не завершено
        if (controller.Movement.IsOnNavMesh())
        {
            // controller.Movement.StopMovement(); // AIController сделает это при смене состояния, если нужно
        }
        controller.ResetWanderingState(); // Сбрасываем флаг и таймер
    }
}