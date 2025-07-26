using UnityEngine;

/// <summary>
/// Состояние атаки. AI стоит на месте и атакует свою цель, пока она в радиусе и видимости.
/// </summary>
public class AIStateAttacking : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Attacking;

    public void EnterState(AIController controller)
    {
        if (controller.CurrentThreat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        
        controller.Movement.StopMovement(false); // Останавливаемся, но не сбрасываем путь
        controller.Movement.FaceTarget(controller.CurrentThreat);
    }

    public void ExitState(AIController controller) { }

    public void UpdateState(AIController controller)
    {
        var threat = controller.CurrentThreat;
        if (threat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        controller.Movement.FaceTarget(threat);
        
        float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);

        if (distanceToThreat > controller.AttackStateSwitchRadius || !controller.Perception.HasLineOfSightToTarget(threat))
        {
            controller.ChangeState(AIController.AIState.Chasing);
            return;
        }

        if (controller.Combat.IsReadyToAttack)
        {
            controller.Combat.PerformAttack(threat);
        }
    }
}