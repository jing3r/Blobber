using UnityEngine;
using System.Linq;

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
            controller.Movement.StopMovement(false); 
        }
        controller.Movement.FaceTarget(controller.CurrentThreat); // Разворачиваемся к цели при входе в атаку
    }

public void UpdateState(AIController controller)
{
    Transform threat = controller.CurrentThreat;
    if (threat == null) 
    {
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // Проверка на смерть цели (остается)
    // ... (код проверки смерти, как в твоей последней версии AIStateChasing) ...
    // Если умерла: controller.ClearCurrentThreat(); controller.ChangeState(AIController.AIState.Idle); return;

    if (!controller.Movement.IsOnNavMesh())
    {
        if (controller.Movement.enabled) controller.Movement.StopMovement(false);
        return; 
    }

    controller.Movement.FaceTarget(threat);
    float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);

    // Если цель вышла из радиуса атаки ИЛИ (важно!) потерян LOS для самой атаки -> преследовать
    // AIPerception.HasLineOfSightToTarget() проверяет возможность "дотянуться" атакой.
    if (distanceToThreat > controller.attackStateSwitchRadius + 0.2f || !controller.Perception.HasLineOfSightToTarget(threat))
    {
        controller.ChangeState(AIController.AIState.Chasing);
        return;
    }
    
    if (controller.Movement.IsMoving) 
    {
        controller.Movement.StopMovement(false);
    }

    if (controller.Combat.IsReadyToAttack)
    {
        if (!controller.Combat.PerformAttack(threat))
        {
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