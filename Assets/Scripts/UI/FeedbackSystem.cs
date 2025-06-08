// FeedbackSystem.cs

using UnityEngine;

/// <summary>
/// Структура для передачи результатов работы одного эффекта способности.
/// </summary>
public struct EffectResult
{
    public bool WasApplied;             // Сработал ли эффект вообще
    public int TargetsHit;              // Скольких целей коснулся эффект
    public int TargetsAffected;         // На скольких целях эффект сработал успешно (после состязания)
    
    public float TotalValue;            // Суммарное значение (урона, лечения)
    public bool IsSingleTarget;         // Это был эффект на одну цель?
    
    public bool UseSimpleCasterFeedback; // <-- НОВОЕ ПОЛЕ

    public string EffectType;           // Глагол/описание эффекта для фидбека ("healed", "dazed", "damaged")
    public string TargetName;           // Имя одиночной цели
    
    // Статический конструктор для "пустого" результата
    public static EffectResult None => new EffectResult { WasApplied = false };
}

/// <summary>
/// Статический класс для генерации текстовых сообщений для игрока.
/// </summary>
public static class FeedbackGenerator
{
    /// <summary>
    /// Возвращает правильную форму слова (единственное или множественное число).
    /// </summary>
    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    /// <summary>
    /// Генерирует основное сообщение о результате применения способности на основе EffectResult.
    /// </summary>
    public static string GenerateFeedback(AbilityData ability, EffectResult result)
    {
        if (!result.WasApplied) return null; // Нечего показывать

        // --- Обработка для ОДИНОЧНОЙ цели ---
        if (result.IsSingleTarget)
        {
            if (string.IsNullOrEmpty(result.TargetName)) result.TargetName = "Target"; // Запасной вариант

            if (result.TargetsAffected == 1) // Успех
            {
                // Если есть TotalValue (урон/хил), используем его
                if (result.TotalValue > 0)
                {
                    return $"{result.TargetName} is {result.EffectType} for {Mathf.CeilToInt(result.TotalValue)}.";
                }
                // Если TotalValue нет (чистый статус), используем глагол эффекта
                else
                {
                    return $"{result.TargetName} is now {result.EffectType}.";
                }
            }
            else // Провал состязания
            {
                return $"{result.TargetName} resisted the {result.EffectType} effect!";
            }
        }
        // --- Обработка для AOE ---
        else
        {
            if (result.TargetsHit == 0)
            {
                return $"{ability.abilityName} did not hit any targets.";
            }

            string targetWord = Pluralize(result.TargetsHit, "target", "targets");

            // Для урона или лечения
            if (result.TotalValue > 0)
            {
                int avgValue = Mathf.CeilToInt(result.TotalValue / result.TargetsHit);
                // Пример: "Quagmire hits 3 targets, dealing an average of 5 damage."
                return $"{ability.abilityName} hits {result.TargetsHit} {targetWord}, dealing an average of {avgValue} damage.";
            }
            // Для статус-эффектов
            else
            {
                if (result.TargetsAffected == 0)
                {
                    // Пример: "Flashbang affected no one out of 3 targets."
                    return $"{ability.abilityName} affected no one out of {result.TargetsHit} {targetWord}.";
                }
                string affectedWord = Pluralize(result.TargetsAffected, "is", "are");
                // Пример: "2 of 3 targets are now dazed."
                return $"{result.TargetsAffected} of {result.TargetsHit} {targetWord} {affectedWord} now {result.EffectType}.";
            }
        }
    }

    public static string TelekinesisInteract(string message)
    {
        return string.IsNullOrEmpty(message) ? "Telekinesis: Nothing happened." : message;
    }
    public static string TargetNotFound(string abilityName)
{
    return $"{abilityName}: Target not found.";
}

// Этот может понадобиться в будущем
public static string InvalidTarget(string abilityName)
{
    return $"{abilityName}: Invalid target.";
}
}