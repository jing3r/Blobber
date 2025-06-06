using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class AIStateFleeing : IAIState
{
    public AIController.AIState GetStateType() => AIController.AIState.Fleeing;

    public void EnterState(AIController controller)
    {
        // Debug.Log($"{controller.gameObject.name} entering Fleeing state from {controller.CurrentThreat?.name}.");
        if (controller.CurrentThreat == null)
        {
            // Если нет угрозы, от которой нужно бежать, возвращаемся в Idle
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
        // Движение начнется в UpdateState
    }

public void UpdateState(AIController controller)
{
    Transform threat = controller.CurrentThreat; 
    if (threat == null || !controller.Movement.IsOnNavMesh())
    {
        controller.ChangeState(AIController.AIState.Idle);
        return;
    }

    // ----- НОВОЕ: Проверка на окончание статуса "Страх" -----
    CharacterStatusEffects statusEffects = controller.MyStats.GetComponent<CharacterStatusEffects>();
    if (statusEffects != null && !statusEffects.IsStatusActive("Feared")) // Убедись, что ID "Feared" совпадает с твоим SO
    {
        // Debug.Log($"{controller.gameObject.name} is no longer Feared (status ended). Switching to Idle.");
        controller.ChangeState(AIController.AIState.Idle); // Если страх прошел, перестаем убегать
        return;
    }
    // ----------------------------------------------------

    // Проверка, не умерла ли угроза, от которой убегаем (этот блок уже должен быть у тебя)
    CharacterStats threatStats = null;
    bool targetIsPlayerParty = threat.CompareTag("Player"); // Предполагая, что у объекта игрока есть тег "Player"
    if (targetIsPlayerParty)
    {
            if (controller.PartyManagerRef != null && !controller.PartyManagerRef.partyMembers.Any(m => m != null && !m.IsDead))
            { 
            controller.ClearCurrentThreat(); 
            controller.ChangeState(AIController.AIState.Idle); return; 
            }
    }
    else
    {
        threatStats = threat.GetComponent<CharacterStats>();
        if (threatStats != null && threatStats.IsDead)
        { 
            controller.ClearCurrentThreat(); 
            controller.ChangeState(AIController.AIState.Idle); return; 
        }
        else if (threatStats == null && !targetIsPlayerParty) 
        {
            controller.ClearCurrentThreat();
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
    }

    float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);
    if (distanceToThreat > controller.fleeDistance) 
    {
        // Debug.Log($"{controller.gameObject.name} (Fleeing): Fled far enough from {threat.name}. Switching to Idle.");
        controller.ChangeState(AIController.AIState.Idle); 
        return;
    }

    // Логика движения ОТ угрозы (этот блок уже должен быть у тебя)
    Vector3 directionFromThreat = (controller.transform.position - threat.position).normalized;
    if (directionFromThreat == Vector3.zero) 
    {
        directionFromThreat = (Random.insideUnitSphere).normalized;
        if (directionFromThreat == Vector3.zero) directionFromThreat = Vector3.forward; 
    }
    
    Vector3 fleeDestinationPoint = controller.transform.position + directionFromThreat * 5f; 

    NavMeshHit hit; 
    if (NavMesh.SamplePosition(fleeDestinationPoint, out hit, 10f, NavMesh.AllAreas))
    {
        controller.Movement.MoveTo(hit.position);
    }
    else 
    {
        Vector3 randomDir = Random.insideUnitSphere * 7f; 
        randomDir.y = 0; 
        randomDir += controller.transform.position;
        if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas)) 
        { 
            controller.Movement.MoveTo(hit.position); 
        }
        else 
        { 
            controller.Movement.StopMovement(); 
            controller.ChangeState(AIController.AIState.Idle); 
        }
    }
}

public void ExitState(AIController controller)
{
    // Debug.Log($"{controller.gameObject.name} exiting Fleeing state.");
    if (controller.Movement.IsOnNavMesh())
    {
        controller.Movement.StopMovement(); 
    }

    // ----- НОВОЕ: Поворот к угрозе при выходе из состояния бегства -----
    if (controller.CurrentThreat != null) 
    {
        // Debug.Log($"{controller.gameObject.name} exiting Fleeing, was fleeing from {controller.CurrentThreat.name}. Now facing threat.");
        controller.Movement.FaceTarget(controller.CurrentThreat);
    }
}
}