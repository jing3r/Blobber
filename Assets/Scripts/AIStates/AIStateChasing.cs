using UnityEngine;
using System.Linq;

/// <summary>
/// Состояние преследования. AI движется к своей цели (CurrentThreat).
/// </summary>
public class AIStateChasing : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Chasing;

    public void EnterState(AIController controller)
    {
        controller.Movement.SetStateSpeedMultiplier(1.0f);         
        if (controller.CurrentThreat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
        }
    }

    public void ExitState(AIController controller)
    {
        controller.Movement.SetStateSpeedMultiplier(1.0f);      
    }

    public void UpdateState(AIController controller)
    {
        var threat = controller.CurrentThreat;
        if (threat == null || IsThreatInvalid(threat, controller))
        {
            controller.ClearCurrentThreat();
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);
        
        if (distanceToThreat > controller.Perception.DisengageRadius)
        {
            controller.ClearCurrentThreat();
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        if (distanceToThreat <= controller.AttackStateSwitchRadius && controller.Perception.HasLineOfSightToTarget(threat))
        {
            controller.ChangeState(AIController.AIState.Attacking);
            return;
        }

        controller.Movement.Follow(threat);
    }

    private bool IsThreatInvalid(Transform threat, AIController controller)
    {
        if (threat.CompareTag("Player"))
        {
            // Угроза (партия) невалидна, если в ней не осталось живых членов.
            return controller.PartyManagerRef != null && !controller.PartyManagerRef.PartyMembers.Any(m => m != null && !m.IsDead);
        }
        
        // Угроза (NPC) невалидна, если у нее нет статов или она мертва.
        var threatStats = threat.GetComponent<CharacterStats>();
        return threatStats == null || threatStats.IsDead;
    }
}