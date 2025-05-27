using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PartyManager : MonoBehaviour
{
    public List<CharacterStats> partyMembers = new List<CharacterStats>();

    // Статические события для глобального оповещения
    public static event System.Action OnPartyCompositionChanged; // Если состав партии меняется динамически
    public static event System.Action OnPartyWipe;               // Когда вся партия погибает

    void Awake()
    {
        FindPartyMembersAndSubscribe();
    }

    private void FindPartyMembersAndSubscribe()
    {
        partyMembers.Clear();

        // Собираем CharacterStats из прямых дочерних объектов
        foreach (Transform child in transform)
        {
            CharacterStats memberStats = child.GetComponent<CharacterStats>();
            if (memberStats != null) // && child.gameObject.activeSelf) // Можно добавить проверку на активность
            {
                partyMembers.Add(memberStats);
                memberStats.onDied += CheckForPartyWipe; // Подписываемся на смерть
                // Debug.Log($"PartyManager: Найден и подписан член партии: {child.name}"); // Оставим для отладки, если нужно
            }
        }

        if (partyMembers.Count == 0)
        {
            Debug.LogError("PartyManager: Не найдено ни одного члена партии в дочерних объектах! Игра может работать некорректно.", this);
        }
        // Пример предупреждения, если размер партии не ожидаемый (можно убрать, если размер динамический)
        // else if (partyMembers.Count != 4)
        // {
        //     Debug.LogWarning($"PartyManager: Найдено {partyMembers.Count} членов партии (ожидалось 4).", this);
        // }

        OnPartyCompositionChanged?.Invoke(); // Оповещаем об изменении состава
    }

    private void CheckForPartyWipe()
    {
        // Проверяем, все ли члены партии (которые были добавлены) мертвы
        if (partyMembers.Count > 0 && partyMembers.All(member => member == null || member.IsDead))
        {
            Debug.LogWarning("PartyManager: ВСЯ ПАРТИЯ ПОГИБЛА! GAME OVER.");
            OnPartyWipe?.Invoke();
        }
    }

    public CharacterStats GetRandomLivingMember()
    {
        if (partyMembers.Count == 0) return null;

        List<CharacterStats> livingMembers = partyMembers.Where(member => member != null && !member.IsDead).ToList();
        if (livingMembers.Count > 0)
        {
            return livingMembers[Random.Range(0, livingMembers.Count)];
        }
        return null; // Нет живых членов партии
    }

    void OnDestroy()
    {
        // Отписываемся от событий смерти всех членов партии
        foreach (var member in partyMembers)
        {
            if (member != null)
            {
                member.onDied -= CheckForPartyWipe;
            }
        }
    }
}