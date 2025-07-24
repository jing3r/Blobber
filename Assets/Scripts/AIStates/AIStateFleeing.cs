using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class AIStateFleeing : IAIState
{
    private StatusEffectData fearedStatusData; // Будем искать его один раз

    public AIController.AIState GetStateType() => AIController.AIState.Fleeing;
    public void EnterState(AIController controller)
    {
        // Находим и кэшируем SO страха при первом входе в состояние
        if (fearedStatusData == null)
        {
            // Это все еще загрузка из Resources, но она происходит один раз за жизнь AI.
            // В будущем это можно заменить на реестр статусов.
            fearedStatusData = Resources.Load<StatusEffectData>("StatusEffects/Feared"); // УКАЖИ ПРАВИЛЬНЫЙ ПУТЬ!
            if (fearedStatusData == null)
            {
                Debug.LogError("AIStateFleeing: Could not load 'Feared' StatusEffectData from Resources/StatusEffects/. Fleeing state might not work correctly with status effects.");
            }
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

    // Проверка, не умерла ли угроза, от которой убегаем
    bool threatIsInvalid = false;
    if (threat.CompareTag("Player")) // Убедись, что у объекта игрока есть тег "Player"
    {
        if (controller.PartyManagerRef != null && !controller.PartyManagerRef.partyMembers.Any(m => m != null && !m.IsDead))
        {
            threatIsInvalid = true; 
        }
    }
    else
    {
        CharacterStats threatStats = threat.GetComponent<CharacterStats>();
        if (threatStats != null)
        {
            if (threatStats.IsDead) threatIsInvalid = true;
        }
        else // Не игрок и нет CharacterStats
        {
            threatIsInvalid = true;
        }
    }

    if (threatIsInvalid)
    {
        // Debug.Log($"{controller.gameObject.name} (Fleeing): Threat {threat.name} is invalid (dead/no stats). Clearing and switching to Idle.");
        controller.ClearCurrentThreat(); 
        controller.ChangeState(AIController.AIState.Idle); 
        return;
    }
        CharacterStatusEffects statusEffects = controller.MyStats.GetComponent<CharacterStatusEffects>();
        
        // ОБНОВЛЕННЫЙ ВЫЗОВ
        bool isUnderFearedStatus = statusEffects != null && statusEffects.IsStatusActive(fearedStatusData);

        bool stopFleeing = false;
        if (isUnderFearedStatus)
        {
            // Логика остается той же, но теперь мы знаем, что статус закончился, если IsStatusActive(fearedStatusData) вернет false.
            // Поскольку наш IsStatusActive теперь всегда актуален, нам не нужно ничего больше.
        }
        else // Если не под статусом, убегаем по "естественным" причинам
        {
            float distanceToThreat = Vector3.Distance(controller.transform.position, controller.CurrentThreat.position);
            if (distanceToThreat > controller.fleeDistance)
            {
                stopFleeing = true;
            }
        }
        
        if (stopFleeing)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }
    
    // Если все еще должны убегать, продолжаем логику движения.
        Vector3 directionFromThreat = (controller.transform.position - threat.position).normalized;
    if (directionFromThreat == Vector3.zero) 
    {
        directionFromThreat = (Random.insideUnitSphere).normalized;
        if (directionFromThreat == Vector3.zero) directionFromThreat = Vector3.forward; 
    }
    
    Vector3 fleeDestinationPoint = controller.transform.position + directionFromThreat * 5f; // Пытаемся отбежать еще на 5м 

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
            // Debug.LogWarning($"{controller.gameObject.name} (Fleeing): Cornered or cannot find flee path. Stopping and switching to Idle.");
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