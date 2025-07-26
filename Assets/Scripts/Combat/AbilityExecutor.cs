using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Статический класс, отвечающий за исполнение логики способностей (поиск целей, применение эффектов).
/// </summary>
public static class AbilityExecutor
{
    /// <summary>
    /// Исполняет способность, применяя все ее эффекты к соответствующим целям.
    /// </summary>
    public static void Execute(CharacterStats caster, AbilityData ability, CharacterStats primaryTarget, 
                               Transform interactableTarget, Vector3 groundPoint, FeedbackManager feedbackManager)
    {
        if (caster == null || ability == null)
        {
            Debug.LogError("[AbilityExecutor] Caster or AbilityData is null.");
            return;
        }

        var executionContext = new AbilityExecutionContext(caster, ability, primaryTarget, interactableTarget, groundPoint);
        var areaTargets = executionContext.AreaTargets;

        string lastFeedback = null;
        foreach (var effect in ability.EffectsToApply)
        {
            string feedback = effect.ApplyEffect(
                executionContext.Caster, 
                executionContext.Ability, 
                executionContext.PrimaryTarget, 
                executionContext.InteractableTarget, 
                executionContext.GroundPoint,
                ref areaTargets
            );

            if (!string.IsNullOrEmpty(feedback))
            {
                lastFeedback = feedback;
            }
        }
        
        ProvideExecutionFeedback(lastFeedback, ability, feedbackManager);
    }

    /// <summary>
    /// Внутренняя структура для удобной передачи контекста исполнения способности.
    /// </summary>
    private class AbilityExecutionContext
    {
        public CharacterStats Caster { get; }
        public AbilityData Ability { get; }
        public CharacterStats PrimaryTarget { get; }
        public Transform InteractableTarget { get; }
        public Vector3 GroundPoint { get; }
        public List<CharacterStats> AreaTargets { get; }

        public AbilityExecutionContext(CharacterStats caster, AbilityData ability, CharacterStats primaryTarget, Transform interactableTarget, Vector3 groundPoint)
        {
            Caster = caster;
            Ability = ability;
            PrimaryTarget = primaryTarget;
            InteractableTarget = interactableTarget;
            GroundPoint = groundPoint;
            
            // Логика поиска целей по области выполняется один раз при создании контекста
            AreaTargets = FindTargetsInArea();
        }

        private List<CharacterStats> FindTargetsInArea()
        {
            var targets = new List<CharacterStats>();
            if (Ability.TargetType != TargetType.AreaAroundCaster) return targets;

            Vector3 aoeCenter = Caster.transform.position;
            
            // Фильтруем и добавляем самого кастера
            if (Ability.AffectsSelfInAoe && !Caster.IsDead) targets.Add(Caster);
            
            // Фильтруем и добавляем членов партии
            if (Ability.AffectsPartyMembersInAoe)
            {
                var partyManager = Caster.GetComponentInParent<PartyManager>();
                if (partyManager != null)
                {
                    var partyTargets = partyManager.PartyMembers
                        .Where(m => m != null && m != Caster && !m.IsDead && Vector3.Distance(aoeCenter, m.transform.position) <= Ability.AreaOfEffectRadius);
                    targets.AddRange(partyTargets);
                }
            }
            
            // Фильтруем и добавляем NPC
            var colliders = Physics.OverlapSphere(aoeCenter, Ability.AreaOfEffectRadius, LayerMask.GetMask("Characters"));
            foreach (var col in colliders)
            {
                var targetStats = col.GetComponent<CharacterStats>();
                if (ShouldAffectNpc(targetStats, targets))
                {
                    targets.Add(targetStats);
                }
            }

            return targets.Distinct().ToList();
        }

        private bool ShouldAffectNpc(CharacterStats npcStats, List<CharacterStats> currentTargets)
        {
            if (npcStats == null || npcStats.IsDead || Caster == npcStats || currentTargets.Contains(npcStats))
            {
                return false;
            }

            var npcController = npcStats.GetComponent<AIController>();
            if (npcController == null) return false;

            switch (npcController.CurrentAlignment)
            {
                case AIController.Alignment.Hostile: return Ability.AffectsEnemiesInAoe;
                case AIController.Alignment.Neutral: return Ability.AffectsNeutralsInAoe;
                case AIController.Alignment.Friendly: return Ability.AffectsAlliesInAoe;
                default: return false;
            }
        }
    }
    
    private static void ProvideExecutionFeedback(string lastFeedback, AbilityData ability, FeedbackManager feedbackManager)
    {
        // Если ни один из эффектов не вернул фидбек (например, AoE не нашел целей)
        if (lastFeedback == null && (ability.TargetType == TargetType.AreaAroundCaster || ability.TargetType == TargetType.Point_GroundTargeted))
        {
            lastFeedback = $"{ability.AbilityName} did not affect any targets.";
        }

        if (!string.IsNullOrEmpty(lastFeedback))
        {
            feedbackManager?.ShowFeedbackMessage(lastFeedback);
        }
    }
}