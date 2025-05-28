// Файл: AIStateAttacking.cs
using UnityEngine;

public class AIStateAttacking : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Attacking;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Attacking state against {controller.CurrentThreat?.name}.");
        if (controller.CurrentThreat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        if (controller.Movement.IsOnNavMesh() && controller.Movement.IsMoving)
        {
            controller.Movement.StopMovement(false); // Останавливаемся для атаки, не сбрасывая путь
        }
    }

    public void UpdateState(AIController controller)
    {
        Transform threat = controller.CurrentThreat;
        if (threat == null || !controller.Movement.IsOnNavMesh()) // Если цель исчезла или мы не на NavMesh
        {
            // Если нет угрозы, пытаемся перейти в Idle. 
            // Если не на NavMesh, но агент активен, это проблема, которую AIController должен обработать.
            // Пока просто переходим в Idle.
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        
        // Если агент все еще активен, но не на NavMesh, останавливаем его, чтобы избежать ошибок
        if(!controller.Movement.IsOnNavMesh() && controller.Movement.enabled) {
            controller.Movement.StopMovement(false);
            // Можно добавить логику возвращения на NavMesh или переход в специфическое состояние
            return; // Не можем атаковать, если не на NavMesh
        }


        controller.FaceTarget(threat);
        float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);

        if (distanceToThreat > controller.attackStateSwitchRadius + 0.2f) 
        {
            controller.ChangeState(AIController.AIState.Chasing);
            return;
        }
        
        // Убедимся, что стоим на месте
        if (controller.Movement.IsMoving) 
        {
            controller.Movement.StopMovement(false);
        }

        if (controller.Combat.IsReadyToAttack)
        {
            if (!controller.Combat.PerformAttack(threat))
            {
                // Атака не удалась (цель мертва/невалидна), сбрасываем угрозу
                controller.ClearCurrentThreat(); 
                controller.ChangeState(AIController.AIState.Idle);
            }
        }
    }

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Attacking state.");
    }
}