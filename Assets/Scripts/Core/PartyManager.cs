using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Управляет составом и состоянием партии игрока.
/// Отвечает за смену активного персонажа, обработку событий партии и взаимодействие с миром от лица всей группы.
/// </summary>
public class PartyManager : MonoBehaviour, ISaveable
{
    [Header("Настройки взаимодействия")]
    [SerializeField]
    [Tooltip("Максимальная дистанция для взаимодействия с контейнерами (Loot All, Drag&Drop).")]
    private float maxLootDistance = 5f;
    public float MaxLootDistance => maxLootDistance;

    [Header("Состав партии")]
    [SerializeField]
    private List<CharacterStats> partyMembers = new List<CharacterStats>();
    public IReadOnlyList<CharacterStats> PartyMembers => partyMembers.AsReadOnly();

    private int activeMemberIndex = -1;
    public CharacterStats ActiveMember => (activeMemberIndex >= 0 && activeMemberIndex < PartyMembers.Count) ? PartyMembers[activeMemberIndex] : null;

    #region Events
    public event System.Action<CharacterStats, CharacterStats> OnActiveMemberChanged;
    public static event System.Action OnPartyCompositionChanged;
    public static event System.Action OnPartyWipe;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        FindAndSubscribePartyMembers();
    }

    private void Start()
    {
        // Небольшая задержка, чтобы все другие системы успели инициализироваться перед выбором персонажа
        Invoke(nameof(SelectFirstReadyMember), 0.1f);
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllEvents();
    }
    #endregion

    #region Party Member Management
    /// <summary>
    /// Устанавливает персонажа с указанным индексом как активного.
    /// </summary>
    public void SetActiveMember(int index)
    {
        if (index == activeMemberIndex) return;

        if (index < 0 || index >= PartyMembers.Count || PartyMembers[index] == null || PartyMembers[index].IsDead)
        {
            // Если пытаемся выбрать невалидного или сбросить выбор (-1), снимаем выделение
            if (activeMemberIndex != -1)
            {
                var oldMember = ActiveMember;
                activeMemberIndex = -1;
                OnActiveMemberChanged?.Invoke(oldMember, null);
            }
            return;
        }

        var previousMember = ActiveMember;
        activeMemberIndex = index;
        OnActiveMemberChanged?.Invoke(previousMember, ActiveMember);
    }

    /// <summary>
    /// Переключает активного персонажа на следующего готового к действию.
    /// </summary>
    public void CycleToNextReadyMember()
    {
        if (PartyMembers.Count == 0) return;

        int startIndex = (activeMemberIndex == -1) ? PartyMembers.Count - 1 : activeMemberIndex;
        for (int i = 0; i < PartyMembers.Count; i++)
        {
            int currentIndex = (startIndex + 1 + i) % PartyMembers.Count;
            var member = PartyMembers[currentIndex];
            var actionController = member?.GetComponent<CharacterActionController>();

            if (actionController != null && actionController.CurrentState == CharacterActionController.ActionState.Ready && !member.IsDead)
            {
                SetActiveMember(currentIndex);
                return;
            }
        }
        
        SetActiveMember(-1);
    }
    
    /// <summary>
    /// Возвращает случайного живого члена партии. Используется AI для выбора цели.
    /// </summary>
    public CharacterStats GetRandomLivingMember()
    {
        var livingMembers = PartyMembers.Where(member => member != null && !member.IsDead).ToList();
        return livingMembers.Count > 0 ? livingMembers[Random.Range(0, livingMembers.Count)] : null;
    }

    private void SelectFirstReadyMember()
    {
        int firstReadyIndex = partyMembers.FindIndex(m => m != null && !m.IsDead && m.GetComponent<CharacterActionController>().CurrentState == CharacterActionController.ActionState.Ready);
        if (firstReadyIndex != -1)
        {
            SetActiveMember(firstReadyIndex);
        }
    }
    #endregion

    #region Event Handlers
    private void HandleCharacterActionStarted()
    {
        CycleToNextReadyMember();
    }

    private void HandleCharacterStateChange(CharacterActionController.ActionState newState)
    {
        // Если какой-то персонаж стал готов, а активного в данный момент нет,
        // этот персонаж немедленно становится активным.
        if (newState == CharacterActionController.ActionState.Ready && ActiveMember == null)
        {
            SelectFirstReadyMember();
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
        if (PartyMembers.Count > 0 && PartyMembers.All(m => m == null || m.IsDead))
        {
            OnPartyWipe?.Invoke();
        }
    }
    #endregion

    #region Party Actions
    /// <summary>
    /// Собирает все предметы из списка инвентарей-источников.
    /// </summary>
    public void LootAllFromSources(List<Inventory> sourceInventories)
    {
        if (sourceInventories == null || sourceInventories.Count == 0) return;

        var lootReceivers = GetLootReceivers();
        if (lootReceivers.Count == 0)
        {
            FindObjectOfType<FeedbackManager>()?.ShowFeedbackMessage("No one in the party can carry items.");
            return;
        }

        bool anyItemTaken = false;
        bool anyContainerTooFar = false;
        bool anyItemsWereAvailable = false;

        foreach (var sourceInventory in sourceInventories)
        {
            if (sourceInventory == null || sourceInventory.Items.Count == 0) continue;
            
            anyItemsWereAvailable = true;
            if (Vector3.Distance(transform.position, sourceInventory.transform.position) > maxLootDistance)
            {
                anyContainerTooFar = true;
                continue;
            }

            // Создаем копию списка, так как будем изменять его в цикле
            List<InventoryItem> itemsToLoot = new List<InventoryItem>(sourceInventory.Items);
            foreach (var itemToLoot in itemsToLoot)
            {
                if (TryDistributeItemToParty(itemToLoot, lootReceivers))
                {
                    sourceInventory.RemoveItem(itemToLoot);
                    anyItemTaken = true;
                }
            }
        }
        
        ProvideLootFeedback(anyItemTaken, anyItemsWereAvailable, anyContainerTooFar);
    }

    private List<CharacterStats> GetLootReceivers()
    {
        var receivers = new List<CharacterStats>();
        if (ActiveMember != null && !ActiveMember.IsDead)
        {
            receivers.Add(ActiveMember);
        }
        foreach (var member in PartyMembers)
        {
            if (member != null && !member.IsDead && !receivers.Contains(member))
            {
                receivers.Add(member);
            }
        }
        return receivers;
    }
    
    private bool TryDistributeItemToParty(InventoryItem item, List<CharacterStats> receivers)
    {
        foreach (var receiver in receivers)
        {
            var receiverInventory = receiver.GetComponent<Inventory>();
            if (receiverInventory != null && receiverInventory.AddItem(item.ItemData, item.Quantity))
            {
                return true;
            }
        }
        return false;
    }

    private void ProvideLootFeedback(bool anyItemTaken, bool anyItemsAvailable, bool anyContainerTooFar)
    {
        var feedbackManager = FindObjectOfType<FeedbackManager>();
        if(feedbackManager == null) return;

        if (anyItemTaken) return; // Не спамим при успехе

        if (anyItemsAvailable && anyContainerTooFar)
        {
            feedbackManager.ShowFeedbackMessage("A container is too far away.");
        }
        else if (anyItemsAvailable)
        {
            feedbackManager.ShowFeedbackMessage("Could not take any items (no space or capacity).");
        }
    }
    #endregion

    #region Initialization & Subscriptions
    private void FindAndSubscribePartyMembers()
    {
        UnsubscribeFromAllEvents(); // Гарантируем, что старых подписок не осталось
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
                    actionController.OnActionStarted += HandleCharacterActionStarted;
                    actionController.OnStateChanged += HandleCharacterStateChange;
                }
            }
        }
        OnPartyCompositionChanged?.Invoke();
    }

    private void UnsubscribeFromAllEvents()
    {
        foreach (var member in PartyMembers)
        {
            if (member == null) continue;
            member.onDied -= HandlePartyMemberDeath;
            var actionController = member.GetComponent<CharacterActionController>();
            if (actionController != null)
            {
                actionController.OnActionStarted -= HandleCharacterActionStarted;
                actionController.OnStateChanged -= HandleCharacterStateChange;
            }
        }
    }
    #endregion

    #region Save System Implementation
    [System.Serializable]
    private class PartyManagerSaveData
    {
        public PlayerSaveData Player;
        public List<PartyMemberSaveData> Party;
    }

    public object CaptureState()
    {
        var partyData = PartyMembers
            .Where(m => m != null)
            .Select(CapturePartyMemberState)
            .ToList();

        return new PartyManagerSaveData
        {
            Player = CapturePlayerState(),
            Party = partyData
        };
    }

    public void RestoreState(object state)
    {
        if (state is PartyManagerSaveData saveData)
        {
            RestorePlayerState(saveData.Player);

            foreach (var memberSaveData in saveData.Party)
            {
                var memberStats = partyMembers.FirstOrDefault(m => m != null && m.gameObject.name == memberSaveData.MemberName);
                if (memberStats != null)
                {
                    RestorePartyMemberState(memberStats, memberSaveData);
                }
            }

            FindObjectOfType<PartyUIManager>()?.RefreshAllPartyMemberUIs();
        }
    }

    private PlayerSaveData CapturePlayerState()
    {
        return new PlayerSaveData
        {
            Position = new[] { transform.position.x, transform.position.y, transform.position.z },
            Rotation = new[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w }
        };
    }

    private void RestorePlayerState(PlayerSaveData data)
    {
        Vector3 position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
        Quaternion rotation = new Quaternion(data.Rotation[0], data.Rotation[1], data.Rotation[2], data.Rotation[3]);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        if (cc != null) cc.enabled = true;
    }

    private PartyMemberSaveData CapturePartyMemberState(CharacterStats member)
    {
        var inventory = member.GetComponent<Inventory>();
        var inventoryItems = inventory?.Items
            .Where(itemSlot => itemSlot?.ItemData != null)
            .Select(itemSlot => new InventorySlotSaveData
            {
                ItemDataName = itemSlot.ItemData.name,
                Quantity = itemSlot.Quantity
            }).ToList() ?? new List<InventorySlotSaveData>();
        
        var abilities = member.GetComponent<CharacterAbilities>();
        var abilitiesData = abilities?.LearnedAbilities
            .Where(slot => slot?.AbilityData != null)
            .Select(slot => new AbilitySaveData
            {
                AbilityDataName = slot.AbilityData.name,
                CurrentCharges = slot.CurrentCharges
            }).ToList() ?? new List<AbilitySaveData>();

        return new PartyMemberSaveData
        {
            MemberName = member.gameObject.name,
            CurrentHealth = member.currentHealth,
            Level = member.Level,
            Experience = member.Experience,
            ExperienceToNextLevel = member.ExperienceToNextLevel,
            BaseBody = member.BaseBody,
            BaseMind = member.BaseMind,
            BaseSpirit = member.BaseSpirit,
            BaseAgility = member.BaseAgility,
            BaseProficiency = member.BaseProficiency,
            InventoryItems = inventoryItems,
            AbilitiesData = abilitiesData
        };
    }

    private void RestorePartyMemberState(CharacterStats member, PartyMemberSaveData data)
    {
        member.RestoreBaseStatsFromSave(data);
        
        var inventory = member.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.Clear();
            foreach (var savedSlot in data.InventoryItems)
            {
                ItemData itemData = Resources.Load<ItemData>($"Items/{savedSlot.ItemDataName}");
                if (itemData != null)
                {
                    inventory.AddItem(itemData, savedSlot.Quantity);
                }
            }
        }
        
        var abilities = member.GetComponent<CharacterAbilities>();
        if (abilities != null)
        {
            foreach (var savedAbility in data.AbilitiesData)
            {
                var slot = abilities.LearnedAbilities.FirstOrDefault(s => s.AbilityData != null && s.AbilityData.name == savedAbility.AbilityDataName);
                if (slot != null)
                {
                    slot.CurrentCharges = savedAbility.CurrentCharges;
                }
            }
        }

        member.RefreshStatsAfterLoad();
        abilities?.TriggerAbilitiesChanged();
        inventory?.TriggerInventoryChanged();
    }
    #endregion
}