// Файл: AIStateFleeing.cs
using UnityEngine;
using UnityEngine.AI; // Для NavMeshHit

public class AIStateFleeing : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Fleeing;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Fleeing state from {controller.CurrentThreat?.name}.");
        if (controller.CurrentThreat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        if (controller.Movement.IsOnNavMesh() && controller.Movement.IsStopped())
        {
            // controller.Movement.ResumeMovement(); // AIMovement.MoveTo сам снимет isStopped
        }
    }

    public void UpdateState(AIController controller)
    {
        Transform threat = controller.CurrentThreat; 
        if (threat == null || !controller.Movement.IsOnNavMesh())
        {
            // Debug.Log($"{controller.gameObject.name} (Fleeing): No threat to flee from or not on NavMesh. Switching to Idle.");
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        CharacterStats threatStats = threat.GetComponent<CharacterStats>();
        if (threatStats != null && threatStats.IsDead)
        {
            // Debug.Log($"{controller.gameObject.name} (Fleeing): Threat {threat.name} is dead. Clearing and switching to Idle.");
            controller.ClearCurrentThreatAndSearch(); 
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);
        if (distanceToThreat > controller.fleeDistance) 
        {
            // Debug.Log($"{controller.gameObject.name} (Fleeing): Fled far enough from {threat.name} (dist: {distanceToThreat} > {controller.fleeDistance}). Switching to Idle.");
            // Не сбрасываем CurrentThreat здесь, чтобы AI "помнил" от кого убежал и не сагрился сразу,
            // если currentAlignment не изменился. AIController решит, когда сбросить угрозу.
            controller.ChangeState(AIController.AIState.Idle); 
            return;
        }

        // Логика движения ОТ угрозы
        Vector3 directionFromThreat = (controller.transform.position - threat.position).normalized;
        if (directionFromThreat == Vector3.zero) // Если AI и угроза в одной точке
        {
            directionFromThreat = (Random.insideUnitSphere).normalized; // Случайное направление
            if (directionFromThreat == Vector3.zero) directionFromThreat = Vector3.forward; // На всякий случай
        }
        
        Vector3 fleeDestinationPoint = controller.transform.position + directionFromThreat * 5f; // Пытаемся отбежать на 5м

        NavMeshHit hit; 
        if (NavMesh.SamplePosition(fleeDestinationPoint, out hit, 10f, NavMesh.AllAreas))
        {
            controller.Movement.MoveTo(hit.position);
        }
        else 
        {
            // Попытка найти случайную точку, если идеальное направление заблокировано
            Vector3 randomDir = Random.insideUnitSphere * 7f; // Чуть больший радиус для случайного поиска
            randomDir.y = 0; // Двигаемся по плоскости
            randomDir += controller.transform.position;
            if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas)) 
            { 
                controller.Movement.MoveTo(hit.position); 
            }
            else // Загнан в угол или не может найти путь
            { 
                // Debug.LogWarning($"{controller.gameObject.name} (Fleeing): Cornered or cannot find flee path. Switching to Idle.");
                controller.Movement.StopMovement(); // Останавливаемся
                controller.ChangeState(AIController.AIState.Idle); 
            }
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Fleeing state.");
        if (controller.Movement.IsOnNavMesh())
        {
            controller.Movement.StopMovement(); // Останавливаемся, когда выходим из состояния бегства
        }
    }
}