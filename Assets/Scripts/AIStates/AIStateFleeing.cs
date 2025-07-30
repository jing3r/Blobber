using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Состояние бегства. AI пытается убежать от своей цели (CurrentThreat).
/// </summary>
public class AIStateFleeing : IAIState
{
    // Кэшируем SO, чтобы не загружать его каждый раз.
    private StatusEffectData fearedStatusData; 

    public AIController.AIState GetStateType() => AIController.AIState.Fleeing;
    
    public void EnterState(AIController controller)
    {
        controller.Movement.SetStateSpeedMultiplier(1.2f);        
        // TODO: Заменить Resources.Load на систему управления ассетами (Asset Registry/Addressables).
        if (fearedStatusData == null)
        {
            fearedStatusData = Resources.Load<StatusEffectData>("StatusEffects/Feared");
            if (fearedStatusData == null)
            {
                Debug.LogError("[AIStateFleeing] Could not load 'Feared' StatusEffectData. Fleeing from status effect will not work.");
            }
        }
    }

    public void ExitState(AIController controller)
    {
        controller.Movement.SetStateSpeedMultiplier(1.0f);      
        controller.Movement.StopMovement();
        
        // После бегства всегда поворачиваемся лицом к бывшей угрозе.
        if (controller.CurrentThreat != null)
        {
            controller.Movement.FaceTarget(controller.CurrentThreat);
        }
    }

    public void UpdateState(AIController controller)
    {
        var threat = controller.CurrentThreat;
        if (threat == null)
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        if (ShouldStopFleeing(controller, threat))
        {
            controller.ChangeState(AIController.AIState.Idle);
            return;
        }

        if (FindFleeDestination(controller, threat, out Vector3 destination))
        {
            // Проверяем дистанцию до текущей цели, чтобы не спамить SetDestination
            if (Vector3.Distance(controller.Movement.Destination, destination) > 1.0f)
            {
                controller.Movement.MoveTo(destination);
            }
        }
        else
        {
            controller.ChangeState(AIController.AIState.Idle);
        }
    }

    private bool ShouldStopFleeing(AIController controller, Transform threat)
    {
        var statusEffects = controller.MyStats.GetComponent<CharacterStatusEffects>();
        bool isUnderFearedStatus = statusEffects != null && fearedStatusData != null && statusEffects.IsStatusActive(fearedStatusData);

        // Если мы под эффектом страха, бежим, пока он не закончится.
        if (isUnderFearedStatus)
        {
            return false;
        }
        
        // Если бежим по другой причине (например, мало HP), проверяем дистанцию.
        float distanceToThreat = Vector3.Distance(controller.transform.position, threat.position);
        return distanceToThreat > controller.FleeDistance;
    }

    private bool FindFleeDestination(AIController controller, Transform threat, out Vector3 result)
    {
        Vector3 directionFromThreat = (controller.transform.position - threat.position).normalized;
        Vector3 desiredDestination = controller.transform.position + directionFromThreat * 5f; // Пытаемся отбежать еще на 5м

        if (NavMesh.SamplePosition(desiredDestination, out var navHit, 10f, NavMesh.AllAreas))
        {
            result = navHit.position;
            return true;
        }
        
        // Если прямой путь заблокирован, ищем любую точку подальше
        for (int i = 0; i < 5; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * 10f;
            randomDir += controller.transform.position;
            if (NavMesh.SamplePosition(randomDir, out navHit, 10f, NavMesh.AllAreas))
            {
                // Убеждаемся, что случайная точка дальше от угрозы, чем текущая позиция
                if(Vector3.Distance(navHit.position, threat.position) > Vector3.Distance(controller.transform.position, threat.position))
                {
                    result = navHit.position;
                    return true;
                }
            }
        }
        
        result = controller.transform.position;
        return false; // Не удалось найти точку для бегства
    }
}