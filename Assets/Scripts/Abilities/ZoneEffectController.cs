using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Для LINQ

public class ZoneEffectController : MonoBehaviour
{
    private CharacterStats _zoneCaster;
    private AbilityData _sourceAbilityData; // Ссылка на AbilityData, которая создала эту зону
    private float _zoneDuration;
    private float _timeAlive;
    private float _effectTickRate = 0.5f; // Интервал тиков, фиксированный для зоны
    
    private StatusEffectData _slowEffectDataSO; // Кэшируем SO для статуса "Slowed"
    private float _damagePerTickInZone; // Урон за тик, берется из DamageEffectData в AbilityData

    private float _nextEffectTickTime;

    private List<CharacterStats> _targetsCurrentlyInZone = new List<CharacterStats>();

    // Инициализация зоны. Теперь принимает AbilityData, чтобы получить все параметры эффектов.
    public void Initialize(CharacterStats caster, float duration, AbilityData sourceAbility)
    {
        _zoneCaster = caster;
        _zoneDuration = duration;
        _sourceAbilityData = sourceAbility; 
        _timeAlive = 0f;

        // --- Получаем параметры эффектов из sourceAbilityData ---
        // 1. Урон
        var damageEffect = sourceAbility.effectsToApply.OfType<DamageEffectData>().FirstOrDefault();
        if (damageEffect != null)
        {
            _damagePerTickInZone = damageEffect.baseDamageAmount + (caster != null ? Mathf.FloorToInt(caster.GetAttributeValue(damageEffect.scalingAttribute) * damageEffect.scaleFactor) : 0);
            _damagePerTickInZone = Mathf.Max(1, _damagePerTickInZone); 
        }
        else
        {
            _damagePerTickInZone = 0;
        }

        // 2. Статус замедления
        var applySlowEffect = sourceAbility.effectsToApply.OfType<ApplyStatusEffectData>().FirstOrDefault(e => e.statusEffectToApply != null && e.statusEffectToApply.statusID == "Slowed");
        if (applySlowEffect != null)
        {
            _slowEffectDataSO = applySlowEffect.statusEffectToApply;
        }
        else
        {
            // Debug.LogWarning($"Zone '{gameObject.name}': Could not find 'Slowed' ApplyStatusEffectData in sourceAbilityData. Zone will not apply slow.");
        }
        // --------------------------------------------------------

        _nextEffectTickTime = Time.time; 
        Destroy(gameObject, _zoneDuration + 0.1f);
    }

    void Update()
    {
        _timeAlive += Time.deltaTime;
    }
    
    void OnTriggerEnter(Collider other)
    {
        CharacterStats targetStats = other.GetComponent<CharacterStats>();
        if (CanAffectTarget(targetStats))
        {
            if (!_targetsCurrentlyInZone.Contains(targetStats))
            {
                _targetsCurrentlyInZone.Add(targetStats);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (Time.time < _nextEffectTickTime) return; 
        _nextEffectTickTime = Time.time + _effectTickRate; 

        CharacterStats targetStats = other.GetComponent<CharacterStats>();
        if (targetStats == null) return;

        bool canAffect = CanAffectTarget(targetStats);
        bool isInList = _targetsCurrentlyInZone.Contains(targetStats);

        if (!canAffect || !isInList) 
        {
            return;
        }

        // 1. Наносим урон (если настроен)
        if (_damagePerTickInZone > 0) 
        {
            targetStats.TakeDamage(Mathf.CeilToInt(_damagePerTickInZone), _zoneCaster?.transform);
        }

        // 2. Пытаемся наложить/обновить статус замедления
        CharacterStatusEffects targetStatusEffects = targetStats.GetComponent<CharacterStatusEffects>();
        if (targetStatusEffects != null && _slowEffectDataSO != null)
        {
            bool contestSucceeded = true; 
            if (_sourceAbilityData != null && _sourceAbilityData.usesContest && _zoneCaster != null)
            {
                contestSucceeded = CombatHelper.ResolveAttributeContest(_zoneCaster, targetStats, _sourceAbilityData);
            }

            if (contestSucceeded)
            {
                targetStatusEffects.ApplyStatus(_slowEffectDataSO, _zoneCaster); 
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        CharacterStats targetStats = other.GetComponent<CharacterStats>();
        if (targetStats != null && _targetsCurrentlyInZone.Contains(targetStats))
        {
            CharacterStatusEffects targetStatusEffects = targetStats.GetComponent<CharacterStatusEffects>();
            targetStatusEffects?.RemoveStatusByID(_slowEffectDataSO?.statusID); 
            _targetsCurrentlyInZone.Remove(targetStats);
        }
    }

    private bool CanAffectTarget(CharacterStats targetStats)
    {
        if (targetStats == null || targetStats == _zoneCaster || targetStats.IsDead)
            return false;

        if (_sourceAbilityData == null) return false;

        AIController targetAI = targetStats.GetComponent<AIController>();
        
        // Кастер - игрок
        if (_zoneCaster != null && _zoneCaster.GetComponent<AIController>() == null)
        {
            if (targetAI != null) // Цель - NPC
            {
                if (targetAI.currentAlignment == AIController.Alignment.Hostile && _sourceAbilityData.affectsEnemiesInAoe) return true;
                if (targetAI.currentAlignment == AIController.Alignment.Neutral && _sourceAbilityData.affectsNeutralsInAoe) return true;
            }
            return false; 
        }
        else // Кастер - NPC
        {
            if (targetAI == null && _sourceAbilityData.affectsEnemiesInAoe) return true; 
        }
        return false;
    }
}