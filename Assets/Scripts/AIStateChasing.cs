using UnityEngine;

public class AIStateChasing : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Chasing;

    public void EnterState(AIController controller)
    {
        if (controller.CurrentThreat == null )
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        controller.Movement.StoppingDistance = controller.attackStateSwitchRadius * 0.9f;
    }

public void UpdateState(AIController controller)
{
    Transform threat = controller.CurrentThreat;

    if (threat == null) 
    {
        // Debug.Log($"{controller.gameObject.name} (Chasing): Threat is null. Switching to Idle.");
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    CharacterStats threatStats = threat.GetComponent<CharacterStats>();
    if (threatStats != null && threatStats.IsDead)
    {
        // Debug.Log($"{controller.gameObject.name} (Chasing): Threat {threat.name} is dead. Clearing and switching to Idle.");
        controller.ClearCurrentThreat(); 
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    if (!controller.Movement.IsOnNavMesh()) 
    {
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // Если AIController сбросил CurrentThreat (из-за disengageRadius или смерти цели),
    // а мы все еще здесь, это ошибка логики, но на всякий случай:
    // if (controller.CurrentThreat == null) { controller.ChangeState(AIController.AIState.Idle); return; }


    // Преследуем цель
    controller.Movement.Follow(threat);
    controller.Movement.StoppingDistance = controller.attackStateSwitchRadius * 0.9f; 

    float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);

    // Переход в атаку, ЕСЛИ ЕСТЬ ПРЯМАЯ ВИДИМОСТЬ (LOS) и мы достаточно близко.
    // Угол обзора здесь НЕ используется для уже активной угрозы.
    if (distanceToThreat <= controller.attackStateSwitchRadius && controller.Perception.HasLineOfSightToTarget(threat)) 
    {
        // Debug.Log($"{controller.gameObject.name} (Chasing): Close to {threat.name} and has LOS. Switching to Attacking.");
        controller.ChangeState(AIController.AIState.Attacking);
    }
    // Если потеряли LOS, но цель еще не вышла за disengageRadius (это проверяется в AIController.Update),
    // то продолжаем двигаться к последней точке, куда вел AIMovement.Follow().
    // Если AIController сбросит CurrentThreat из-за долгой потери LOS (если мы такую логику добавим), то мы перейдем в Idle.
}

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Chasing state.");
        // Не обязательно останавливать движение здесь, так как следующее состояние (Attacking или Idle)
        // должно само решить, что делать с движением.
    }
}