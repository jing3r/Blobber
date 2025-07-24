using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PartyManager : MonoBehaviour, ISaveable
{
    [Header("Interaction Settings")]
    [Tooltip("Максимальная дистанция для взаимодействия с контейнерами (Loot All, Drag&Drop).")]
    public float maxLootDistance = 5f;
    public List<CharacterStats> partyMembers = new List<CharacterStats>();
    
    private int activeMemberIndex = -1; 
    public CharacterStats ActiveMember => (activeMemberIndex >= 0 && activeMemberIndex < partyMembers.Count) ? partyMembers[activeMemberIndex] : null;

    public event System.Action<CharacterStats, CharacterStats> OnActiveMemberChanged;

    public static event System.Action OnPartyCompositionChanged;
    public static event System.Action OnPartyWipe;
    
    void Awake()
    {
        FindPartyMembersAndSubscribe();
    }

    void Start()
    {
        // Небольшая задержка, чтобы все системы успели инициализироваться
        Invoke(nameof(SelectFirstReadyMember), 0.1f);
    }
    
    private void SelectFirstReadyMember()
    {
        int firstReadyIndex = partyMembers.FindIndex(m => m != null && !m.IsDead && m.GetComponent<CharacterActionController>().CurrentState == CharacterActionController.ActionState.Ready);
        if (firstReadyIndex != -1)
        {
            SetActiveMember(firstReadyIndex);
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void FindPartyMembersAndSubscribe()
    {
        UnsubscribeFromEvents();
        partyMembers.Clear();
        foreach (Transform child in transform)
        {
            var memberStats = child.GetComponent<CharacterStats>();
            if (memberStats != null)
            {
                partyMembers.Add(memberStats);
                memberStats.onDied += HandlePartyMemberDeath;

                var actionController = child.GetComponent<CharacterActionController>();
                if (actionController != null)
                {
                    actionController.OnActionStarted += HandleActionStarted;
                    // --- НОВАЯ ПОДПИСКА ---
                    actionController.OnStateChanged += HandleCharacterStateChange;
                }
            }
        }
        OnPartyCompositionChanged?.Invoke();
    }
    
    private void UnsubscribeFromEvents()
    {
        foreach (var member in partyMembers)
        {
            if (member == null) continue;
            member.onDied -= HandlePartyMemberDeath;
            var actionController = member.GetComponent<CharacterActionController>();
            if (actionController != null)
            {
                actionController.OnActionStarted -= HandleActionStarted;
                actionController.OnStateChanged -= HandleCharacterStateChange;
            }
        }
    }

    // --- НОВЫЙ ОБРАБОТЧИК ---
    private void HandleCharacterStateChange(CharacterActionController.ActionState newState)
    {
        // Если какой-то персонаж стал готов, А АКТИВНОГО У НАС НЕТ,
        // то этот персонаж немедленно становится активным.
        if (newState == CharacterActionController.ActionState.Ready && ActiveMember == null)
        {
            // Находим индекс персонажа, который отправил событие
            var readyMember = partyMembers.FirstOrDefault(m => m.GetComponent<CharacterActionController>().CurrentState == CharacterActionController.ActionState.Ready);
            if(readyMember != null)
            {
                SetActiveMember(partyMembers.IndexOf(readyMember));
            }
        }
    }

    private void HandlePartyMemberDeath()
    {
        if (ActiveMember != null && ActiveMember.IsDead)
        {
            CycleToNextReadyMember();
        }
        CheckForPartyWipe();
    }
    
    private void CheckForPartyWipe()
    {
        if (partyMembers.Count > 0 && partyMembers.All(m => m == null || m.IsDead))
        {
            OnPartyWipe?.Invoke();
        }
    }

    public void SetActiveMember(int index)
    {
        if (index < 0 || index >= partyMembers.Count || partyMembers[index] == null || partyMembers[index].IsDead)
        {
            // Если пытаемся выбрать невалидного, сбрасываем активного
            if(activeMemberIndex != -1)
            {
                CharacterStats oldMember = ActiveMember;
                activeMemberIndex = -1;
                OnActiveMemberChanged?.Invoke(oldMember, null);
            }
            return;
        }
        
        if (index != activeMemberIndex)
        {
            CharacterStats oldMember = ActiveMember;
            activeMemberIndex = index;
            CharacterStats newMember = ActiveMember;
            OnActiveMemberChanged?.Invoke(oldMember, newMember);
        }
    }

    public void CycleToNextReadyMember()
    {
        int count = partyMembers.Count;
        if (count == 0) return;
        
        // Если активного нет, начинаем поиск с самого начала
        int startIndex = (activeMemberIndex == -1) ? count - 1 : activeMemberIndex;
        int currentIndex = (startIndex + 1) % count;

        for (int i = 0; i < count; i++)
        {
            var controller = partyMembers[currentIndex]?.GetComponent<CharacterActionController>();
            if (controller != null && controller.CurrentState == CharacterActionController.ActionState.Ready && !partyMembers[currentIndex].IsDead)
            {
                SetActiveMember(currentIndex);
                return;
            }
            currentIndex = (currentIndex + 1) % count;
        }
        
        // Если никого не нашли, сбрасываем активного
        SetActiveMember(-1);
    }

    private void HandleActionStarted()
    {
        CycleToNextReadyMember();
    }
    
    public CharacterStats GetRandomLivingMember()
    {
        var livingMembers = partyMembers.Where(member => member != null && !member.IsDead).ToList();
        return livingMembers.Count > 0 ? livingMembers[Random.Range(0, livingMembers.Count)] : null;
    }

public void LootAllFromSources(List<Inventory> sourceInventories)
{
    if (sourceInventories == null || sourceInventories.Count == 0) return;

    bool anyItemTakenAtAll = false;
    bool anyContainerWasTooFar = false;
    bool wereAnyItemsToLoot = false; // Новый флаг: были ли вообще предметы?

    // 1. Создаем упорядоченный список получателей
    List<CharacterStats> lootReceivers = new List<CharacterStats>();
    if (ActiveMember != null && !ActiveMember.IsDead)
    {
        lootReceivers.Add(ActiveMember);
    }
    foreach (var member in partyMembers)
    {
        if (member != null && !member.IsDead && member != ActiveMember)
        {
            lootReceivers.Add(member);
        }
    }
    if (lootReceivers.Count == 0)
    {
        FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No one in the party can carry items.");
        return;
    }

    // 2. Итерируемся по каждому исходному контейнеру
    foreach (var sourceInventory in sourceInventories)
    {
        if (sourceInventory == null) continue;
        
        List<InventoryItem> itemsToLoot = new List<InventoryItem>(sourceInventory.items);
        
        // Если в этом контейнере нет предметов, просто пропускаем его
        if (itemsToLoot.Count == 0) continue;

        // Если предметы есть, значит, потенциально было что забрать
        wereAnyItemsToLoot = true; 

        // --- ПРОВЕРКА ДИСТАНЦИИ ---
        float distance = Vector3.Distance(this.transform.position, sourceInventory.transform.position);
        if (distance > maxLootDistance)
        {
            anyContainerWasTooFar = true;
            continue; // Пропускаем этот непустой, но далекий контейнер
        }

        // 3. Распределяем предметы
        foreach (var itemToLoot in itemsToLoot)
        {
            foreach (var receiver in lootReceivers)
            {
                var receiverInventory = receiver.GetComponent<Inventory>();
                if (receiverInventory != null && receiverInventory.AddItem(itemToLoot.itemData, itemToLoot.quantity))
                {
                    sourceInventory.RemoveItem(itemToLoot);
                    anyItemTakenAtAll = true;
                    goto nextItem;
                }
            }
            nextItem:;
        }
    }

    // 4. Финальный, более точный фидбек
    var feedbackManager = FindObjectOfType<FeedbackManager>();
    if (feedbackManager == null) return;
    
    if (anyItemTakenAtAll)
    {
        //feedbackManager.ShowFeedbackMessage("Items have been transferred."); // закомментировано, чтобы не спамить при успехе
    }
    else if (wereAnyItemsToLoot && anyContainerWasTooFar)
    {
        // Сообщение "слишком далеко" показываем, только если БЫЛИ предметы, но мы до них не дотянулись
        feedbackManager.ShowFeedbackMessage("The container is too far away.");
    }
    else if (wereAnyItemsToLoot)
    {
        // Если предметы были, до всех дотянулись, но ничего не взяли -> нет места
        feedbackManager.ShowFeedbackMessage("Could not take any items (no space or capacity).");
    }
    // Если wereAnyItemsToLoot == false, значит, все контейнеры были пусты.
    // В этом случае мы ничего не выводим, так как никакого действия по сути не произошло.
}

    #region SaveSystem

    [System.Serializable]
    private class PartyManagerStateData
    {
        public PlayerSaveData Player;
        public List<PartyMemberSaveData> Party;
    }

    public object CaptureState()
    {
        var playerData = new PlayerSaveData();
        playerData.position[0] = transform.position.x;
        playerData.position[1] = transform.position.y;
        playerData.position[2] = transform.position.z;
        playerData.rotation[0] = transform.rotation.x;
        playerData.rotation[1] = transform.rotation.y;
        playerData.rotation[2] = transform.rotation.z;
        playerData.rotation[3] = transform.rotation.w;

        var partyData = new List<PartyMemberSaveData>();
        foreach (var member in partyMembers)
        {
            if (member == null) continue;
            
            // --- ИЗМЕНЕНИЕ: Собираем данные напрямую ---
            
            // Собираем данные инвентаря
            var inventory = member.GetComponent<Inventory>();
            var inventoryState = new List<InventorySlotSaveData>();
            if (inventory != null)
            {
                foreach (var itemSlot in inventory.items)
                {
                    if (itemSlot?.itemData == null) continue;
                    inventoryState.Add(new InventorySlotSaveData
                    {
                        itemDataName = itemSlot.itemData.name,
                        quantity = itemSlot.quantity
                    });
                }
            }

            // Собираем данные способностей
            var abilities = member.GetComponent<CharacterAbilities>();
            var abilitiesState = new List<AbilitySaveData>();
            if (abilities != null)
            {
                foreach (var slot in abilities.LearnedAbilities)
                {
                    if (slot?.abilityData == null) continue;
                    abilitiesState.Add(new AbilitySaveData
                    {
                        abilityDataName = slot.abilityData.name,
                        currentCharges = slot.currentCharges
                    });
                }
            }

            var memberData = new PartyMemberSaveData
            {
                memberName = member.gameObject.name,
                currentHealth = member.currentHealth,
                level = member.level,
                experience = member.experience,
                experienceToNextLevel = member.experienceToNextLevel,
                baseBody = member.baseBody,
                baseMind = member.baseMind,
                baseSpirit = member.baseSpirit,
                baseAgility = member.baseAgility,
                baseProficiency = member.baseProficiency,
                // Присваиваем собранные данные
                inventoryItems = inventoryState,
                abilitiesData = abilitiesState
            };
            partyData.Add(memberData);
        }
        
        var partyManagerState = new PartyManagerStateData
        {
            Player = playerData,
            Party = partyData
        };

        return partyManagerState;
    }

public void RestoreState(object state)
{
    if (state is PartyManagerStateData saveData)
    {
        // 1. Восстанавливаем позицию и поворот главного объекта (партии)
        Vector3 position = new Vector3(saveData.Player.position[0], saveData.Player.position[1], saveData.Player.position[2]);
        Quaternion rotation = new Quaternion(saveData.Player.rotation[0], saveData.Player.rotation[1], saveData.Player.rotation[2], saveData.Player.rotation[3]);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        if (cc != null) cc.enabled = true;

        // 2. Восстанавливаем состояние каждого члена партии
        foreach (var memberSave in saveData.Party)
        {
            // Находим соответствующего члена партии в сцене по имени
            var memberStats = partyMembers.FirstOrDefault(m => m != null && m.gameObject.name == memberSave.memberName);
                if (memberStats != null)
                {
                    // Восстанавливаем базовые атрибуты, уровень и опыт
                    memberStats.baseBody = memberSave.baseBody;
                    memberStats.baseMind = memberSave.baseMind;
                    memberStats.baseSpirit = memberSave.baseSpirit;
                    memberStats.baseAgility = memberSave.baseAgility;
                    memberStats.baseProficiency = memberSave.baseProficiency;
                    memberStats.level = memberSave.level;
                    memberStats.experience = memberSave.experience;
                    memberStats.experienceToNextLevel = memberSave.experienceToNextLevel;
                    memberStats.currentHealth = memberSave.currentHealth;

                    // Восстанавливаем инвентарь
                    var inventory = memberStats.GetComponent<Inventory>();
                    if (inventory != null)
                    {
                        inventory.items.Clear();
                        foreach (var savedSlot in memberSave.inventoryItems)
                        {
                            ItemData itemData = Resources.Load<ItemData>($"Items/{savedSlot.itemDataName}");
                            if (itemData != null)
                            {
                                // Используем AddItem, чтобы он сам нашел место.
                                // Для восстановления точной позиции потребуется более сложная логика.
                                inventory.AddItem(itemData, savedSlot.quantity);
                            }
                        }
                    }

                    // Восстанавливаем способности
                    var abilities = memberStats.GetComponent<CharacterAbilities>();
                    if (abilities != null)
                    {
                        foreach (var savedAbility in memberSave.abilitiesData)
                        {
                            var slot = abilities.LearnedAbilities.FirstOrDefault(s => s.abilityData != null && s.abilityData.name == savedAbility.abilityDataName);
                            if (slot != null)
                            {
                                slot.currentCharges = savedAbility.currentCharges;
                            }
                        }
                    }

                    // После установки всех данных, вызываем Refresh, чтобы пересчитать производные статы и обновить UI
                    memberStats.RefreshStatsAfterLoad();

                    // Дополнительно вызываем события для UI, которые не обновляются через RefreshStatsAfterLoad
                    abilities?.TriggerAbilitiesChanged();
                    memberStats.GetComponent<Inventory>()?.TriggerInventoryChanged();
            }
        }
        
        // В конце принудительно обновляем весь UI партии
        FindObjectOfType<PartyUIManager>()?.RefreshAllPartyMemberUIs();
    }
}

    #endregion
}