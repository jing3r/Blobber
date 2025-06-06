using UnityEngine;

public static class CombatHelper
{
    public static bool ResolveAttributeContest(CharacterStats attacker, CharacterStats defender, AbilityData ability)
    {
        if (attacker == null || defender == null || ability == null || !ability.usesContest)
        {
            return true; // Если нет данных для состязания, считаем его успешным (или не нужным)
        }

        int attackerScore = 0;
        int defenderScore = 0;

        // Атрибуты атакующего
        if(ability.attackerAttribute1 != AssociatedAttribute.None) attackerScore += attacker.GetAttributeValue(ability.attackerAttribute1) * 3; // 15% -> вес 3
        if(ability.attackerAttribute2 != AssociatedAttribute.None) attackerScore += attacker.GetAttributeValue(ability.attackerAttribute2) * 2; // 10% -> вес 2
        if(ability.attackerAttribute3 != AssociatedAttribute.None) attackerScore += attacker.GetAttributeValue(ability.attackerAttribute3) * 1; // 5%  -> вес 1

        // Атрибуты защищающегося
        if(ability.defenderAttribute1 != AssociatedAttribute.None) defenderScore += defender.GetAttributeValue(ability.defenderAttribute1) * 3;
        if(ability.defenderAttribute2 != AssociatedAttribute.None) defenderScore += defender.GetAttributeValue(ability.defenderAttribute2) * 2;
        if(ability.defenderAttribute3 != AssociatedAttribute.None) defenderScore += defender.GetAttributeValue(ability.defenderAttribute3) * 1;

        // Базовый шанс 50% + разница в очках (каждое очко разницы дает 5% к шансу)
        // Это упрощенная модель, где каждый "вес" атрибута примерно соответствует 5%
        // Например, если attackerScore = 10, defenderScore = 8, разница = 2. 50% + 2*5% = 60%
        int scoreDifference = attackerScore - defenderScore;
        int successChance = 50 + (scoreDifference * 5); // Каждое "очко" разницы дает 5%

        // Ограничиваем шанс (например, 5% - 95%)
        successChance = Mathf.Clamp(successChance, 5, 95); 
        
        bool success = Random.Range(1, 101) <= successChance;
        // Debug.Log($"Contest: {attacker.name} ({attackerScore}) vs {defender.name} ({defenderScore}). Diff: {scoreDifference}. Chance: {successChance}%. Result: {success}");
        return success;
    }
}