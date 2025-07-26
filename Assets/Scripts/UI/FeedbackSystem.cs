using UnityEngine;

/// <summary>
/// Структура для передачи "сырых" данных о результате выполнения действия (атаки или эффекта).
/// Используется классом FeedbackGenerator для создания текстовых сообщений.
/// </summary>
public struct EffectResult
{
    public bool WasApplied;
    public int TargetsHit;
    public int TargetsAffected;
    public float TotalValue; // Суммарный урон, лечение и т.д.
    public bool IsSingleTarget;
    public string EffectType; // Например, "dazed", "healed"
    public string TargetName;
}

/// <summary>
/// Статический класс-помощник для генерации текстовых сообщений для игрока.
/// </summary>
public static class FeedbackGenerator
{
    /// <summary>
    /// Генерирует сообщение для базовой атаки.
    /// </summary>
    public static string GenerateAttackFeedback(EffectResult result, string casterName)
    {
        if (!result.WasApplied || !result.IsSingleTarget) return null;

        if (result.TargetsAffected > 0)
        {
            return $"{casterName} hits {result.TargetName} ({Mathf.CeilToInt(result.TotalValue)} damage).";
        }
        else
        {
            return $"{casterName} misses {result.TargetName}.";
        }
    }

    /// <summary>
    /// Генерирует сообщение о результате применения способности.
    /// </summary>
    public static string GenerateAbilityFeedback(AbilityData ability, EffectResult result)
    {
        if (!result.WasApplied) return null;

        if (result.IsSingleTarget)
        {
            return GenerateSingleTargetAbilityFeedback(result);
        }
        else
        {
            return GenerateAreaAbilityFeedback(ability, result);
        }
    }

    private static string GenerateSingleTargetAbilityFeedback(EffectResult result)
    {
        string targetName = string.IsNullOrEmpty(result.TargetName) ? "Target" : result.TargetName;

        if (result.TargetsAffected > 0) // Успех
        {
            if (result.TotalValue > 0)
            {
                return $"{targetName} is {result.EffectType} for {Mathf.CeilToInt(result.TotalValue)}.";
            }
            return $"{targetName} is now {result.EffectType}.";
        }
        else // Провал
        {
            return $"{targetName} resisted the {result.EffectType} effect!";
        }
    }

    private static string GenerateAreaAbilityFeedback(AbilityData ability, EffectResult result)
    {
        if (result.TargetsHit == 0)
        {
            return $"{ability.AbilityName} did not hit any targets.";
        }

        string targetWord = Pluralize(result.TargetsHit, "target", "targets");

        // Для урона или лечения
        if (result.TotalValue > 0)
        {
            int avgValue = Mathf.CeilToInt(result.TotalValue / result.TargetsHit);
            return $"{ability.AbilityName} hits {result.TargetsHit} {targetWord}, dealing an average of {avgValue} damage.";
        }
        // Для статус-эффектов
        else
        {
            if (result.TargetsAffected == 0)
            {
                return $"{ability.AbilityName} affected no one out of {result.TargetsHit} {targetWord}.";
            }
            string affectedWord = Pluralize(result.TargetsAffected, "is", "are");
            return $"{result.TargetsAffected} of {result.TargetsHit} {targetWord} {affectedWord} now {result.EffectType}.";
        }
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}