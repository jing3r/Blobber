using UnityEngine;

[RequireComponent(typeof(CharacterStats))]
public class AICombat : MonoBehaviour
{
    [Header("Combat Parameters")]
    [Tooltip("Effective range for initiating attacks. Should be less than or equal to AIController's attackRadius for state switching.")]
    public float effectiveAttackRange = 2f; 
    [Tooltip("If > 0, overrides damage from CharacterStats.")]
    public int overrideAttackDamage = 0;
    [Tooltip("If > 0, overrides attack cooldown from CharacterStats.")]
    public float overrideAttackCooldown = 0f;

    [Header("Chance To Hit Configuration")]
    [Range(0, 100)] public int baseHitChance = 50;
    [Range(0, 100)] public int minHitChance = 5;
    [Range(0, 100)] public int maxHitChance = 95;

    private CharacterStats myStats;
    private FeedbackManager feedbackManager; // For combat log messages
    private PartyManager partyManager;       // To target specific party members if player is the threat

    private float nextAttackTimeInternal = 0f;
    public bool IsReadyToAttack => Time.time >= nextAttackTimeInternal;
private CharacterStatusEffects _statusEffects; 
    void Awake()
    {
    myStats = GetComponent<CharacterStats>();
    if (myStats == null)
    {
        Debug.LogError($"AICombat ({gameObject.name}): CharacterStats not found! Combat will not function.", this);
        enabled = false;
        return;
    }

    // ----- ИНИЦИАЛИЗАЦИЯ -----
    _statusEffects = GetComponent<CharacterStatusEffects>();
    if (_statusEffects == null)
    {
        // Debug.LogWarning($"{gameObject.name}: CharacterStatusEffects not found on AICombat's object.");
    }

        // Attempt to find FeedbackManager and PartyManager on the Player object
        // This assumes a single player setup or a central player object
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            feedbackManager = playerObject.GetComponentInChildren<FeedbackManager>();
            partyManager = playerObject.GetComponent<PartyManager>();
        }
        if (feedbackManager == null)
        {
            Debug.LogWarning($"AICombat ({gameObject.name}): FeedbackManager not found on Player. Combat messages will not be shown.", this);
        }
        if (partyManager == null && playerObject != null) // Only warn if player object exists but no PartyManager
        {
            // This might be fine if the AI is only intended to fight other AIs
            // Debug.LogWarning($"AICombat ({gameObject.name}): PartyManager not found on Player. Targeting player party members might fail.", this);
        }
    }
    
    void Start()
    {
        // Initial attack delay can be set here or when an attack state is first entered
        ResetAttackTimerWithRandomOffset();
    }

    public float GetCurrentAttackDamageValue()
    {
        return overrideAttackDamage > 0 ? overrideAttackDamage : myStats.CalculatedDamage;
    }

    public float GetCurrentAttackCooldownValue()
    {
        return overrideAttackCooldown > 0f ? overrideAttackCooldown : myStats.CalculatedAttackCooldown;
    }

    public void ResetAttackTimer()
    {
        nextAttackTimeInternal = Time.time + GetCurrentAttackCooldownValue();
    }
    
    public void ResetAttackTimerWithRandomOffset(float maxOffsetFactor = 0.3f)
    {
        float cooldown = GetCurrentAttackCooldownValue();
        nextAttackTimeInternal = Time.time + cooldown + Random.Range(0, cooldown * maxOffsetFactor);
    }


    /// <summary>
    /// Attempts to perform an attack on the given target.
    /// </summary>
    /// <param name="overallTarget">The general Transform of the target (e.g., Player object or another NPC object).</param>
    /// <param name="specificTargetStats">Optional: If a specific CharacterStats (like a party member) is already determined.</param>
    /// <returns>True if an attack attempt was made (hit or miss), false if target was invalid or dead.</returns>
    public bool PerformAttack(Transform overallTarget, CharacterStats specificTargetStats = null)
    {
    if (_statusEffects != null && _statusEffects.IsStatusActive("Stunned")) // Убедись, что ID "Stunned" совпадает
    {
        // Debug.Log(gameObject.name + " is Stunned! Cannot attack.");
        return false; // Не можем атаковать, если оглушены
    }

    if (overallTarget == null || myStats == null || myStats.IsDead) return false;
        if (overallTarget == null || myStats == null || myStats.IsDead) return false;

        CharacterStats finalTargetStats = specificTargetStats;
        string finalTargetName = overallTarget.name;

        // If a specific target isn't provided, try to determine it
        if (finalTargetStats == null)
        {
            // Is the overall target the player's party?
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player"); // Cache this if called frequently
            if (playerObject != null && overallTarget == playerObject.transform)
            {
                if (partyManager != null)
                {
                    finalTargetStats = partyManager.GetRandomLivingMember();
                    if (finalTargetStats != null) finalTargetName = finalTargetStats.gameObject.name;
                }
                else // No PartyManager, cannot target player members specifically
                {
                    Debug.LogWarning($"AICombat ({gameObject.name}): Trying to attack player party, but PartyManager is missing or not found.", this);
                    return false; 
                }
            }
            else // Assume overallTarget is another NPC
            {
                finalTargetStats = overallTarget.GetComponent<CharacterStats>();
                if (finalTargetStats != null) finalTargetName = finalTargetStats.gameObject.name;
            }
        }
        else // Specific target was provided
        {
            finalTargetName = finalTargetStats.gameObject.name;
        }


        if (finalTargetStats != null && !finalTargetStats.IsDead)
        {
            // Check distance (optional, AIController usually handles this before calling PerformAttack)
            // float distanceToTarget = Vector3.Distance(transform.position, finalTargetStats.transform.position);
            // if (distanceToTarget > effectiveAttackRange) {
            //     // Debug.Log($"{gameObject.name} target {finalTargetName} is out of effective attack range.");
            //     return false; // Target too far
            // }

            int hitChance = baseHitChance;
            hitChance += myStats.AgilityHitBonusPercent;
            hitChance -= finalTargetStats.AgilityEvasionBonusPercent;
            hitChance = Mathf.Clamp(hitChance, minHitChance, maxHitChance);

            string feedbackMsg;
            if (Random.Range(0, 100) < hitChance)
            {
                int damageToDeal = Mathf.RoundToInt(GetCurrentAttackDamageValue());
                finalTargetStats.TakeDamage(damageToDeal);
                feedbackMsg = $"{myStats.gameObject.name} hits {finalTargetName} ({damageToDeal} damage).";
            }
            else
            {
                feedbackMsg = $"{myStats.gameObject.name} misses {finalTargetName}.";
            }
            
            feedbackManager?.ShowFeedbackMessage(feedbackMsg);
            ResetAttackTimer(); // Reset cooldown after an attack attempt
            return true; // Attack attempt was made
        }
        
        // Target is null, dead, or otherwise invalid
        // Debug.LogWarning($"{myStats.gameObject.name} failed to attack: Target {finalTargetName} is invalid or dead.");
        return false;
    }
}