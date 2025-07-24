using UnityEngine;
using System.Linq;

public class AIStateChasing : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Chasing;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Chasing state towards {controller.CurrentThreat?.name}.");
        if (controller.CurrentThreat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        controller.Movement.StoppingDistance = controller.attackStateSwitchRadius * 0.9f;
    }

public void UpdateState(AIController controller)
{
    Transform threat = controller.CurrentThreat;

    // 1. Проверка, есть ли вообще угроза
    if (threat == null) 
    {
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // 2. Проверка, не умерла ли цель
    bool threatIsInvalid = false;
    if (threat.CompareTag("Player"))
    {
        if (controller.PartyManagerRef != null && !controller.PartyManagerRef.partyMembers.Any(m => m != null && !m.IsDead))
            threatIsInvalid = true;
    }
    else
    {
        CharacterStats threatStats = threat.GetComponent<CharacterStats>();
        if (threatStats != null) { if (threatStats.IsDead) threatIsInvalid = true; }
        else { threatIsInvalid = true; } // Не игрок и нет статов
    }

    if (threatIsInvalid)
    {
        controller.ClearCurrentThreat(); 
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // 3. Проверка, не на NavMesh ли мы
    if (!controller.Movement.IsOnNavMesh()) 
    {
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // 4. Проверка на выход за disengageRadius (ПОТЕРЯ ЦЕЛИ)
    float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);
    if (distanceToThreat > controller.Perception.disengageRadius)
    {
        controller.ClearCurrentThreat();
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // 5. Преследование цели
    controller.Movement.Follow(threat);
    // controller.Movement.StoppingDistance = controller.attackStateSwitchRadius * 0.9f; // Устанавливается в EnterState

    // 6. Переход в атаку: достаточно близко.
    // LOS для перехода в атаку все еще важен, чтобы не пытаться атаковать через стену.
    // AIPerception.HasLineOfSightToTarget() должен быть чистой проверкой луча.
    if (distanceToThreat <= controller.attackStateSwitchRadius && controller.Perception.HasLineOfSightToTarget(threat)) 
    {
        controller.ChangeState(AIController.AIState.Attacking);
    }
    // Если потерян LOS, но цель еще в disengageRadius, продолжаем следовать (Follow).
    // "Памяти" у нас сейчас нет. Если Perception перестанет давать эту цель как
    // GetPrimaryHostileThreatAggro (из-за потери LOS), то в Idle/Wandering
    // при следующей проверке CurrentThreat не установится, и AI вернется в Idle из Chasing.
    // Или, если AIController.Update в начале все же сбросит CurrentThreat на null,
    // то мы выйдем по первой проверке `if (threat == null)`.
}

    public void ExitState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} exiting Chasing state.");
    }
}