using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class InteractEffectData : AbilityEffectData
{
    public override string ApplyEffect(CharacterStats casterStats, AbilityData sourceAbility, CharacterStats primaryTargetStats, Transform primaryTargetTransform, Vector3 castPoint, ref List<CharacterStats> allTargetsInArea)
    {
        if (primaryTargetTransform != null)
        {
            Interactable interactable = primaryTargetTransform.GetComponent<Interactable>();
            if (interactable != null)
            {
                return interactable.Interact();
            }
        }
        return "Telekinesis: Nothing to interact with.";
    }
}