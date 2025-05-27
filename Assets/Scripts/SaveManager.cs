using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string saveFileName = "savegame.json";
    private string saveFilePath;

    private PlayerMovement playerMovement;
    private PartyManager partyManager;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        saveFilePath = Path.Combine(Application.persistentDataPath, saveFileName);

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            partyManager = playerObject.GetComponent<PartyManager>();
        }
        else
        {
            Debug.LogError("SaveManager: Не найден объект Player! Функциональность сохранения/загрузки может быть нарушена.", this);
        }
    }

    public void SaveGame()
    {
        if (playerMovement == null || partyManager == null)
        {
            Debug.LogError("SaveManager: Отсутствуют ссылки на PlayerMovement или PartyManager. Сохранение отменено.");
            return;
        }

        GameSaveData saveData = new GameSaveData();

        saveData.playerData.position = playerMovement.transform.position;
        saveData.playerData.rotation = playerMovement.transform.rotation;

        foreach (CharacterStats memberStats in partyManager.partyMembers)
        {
            if (memberStats == null) continue;

            PartyMemberSaveData memberSave = new PartyMemberSaveData
            {
                memberName = memberStats.gameObject.name,
                currentHealth = memberStats.currentHealth,
                maxHealth = memberStats.maxHealth,
                level = memberStats.level,
                experience = memberStats.experience,
                experienceToNextLevel = memberStats.experienceToNextLevel,
                baseBody = memberStats.baseBody,
                baseMind = memberStats.baseMind,
                baseSpirit = memberStats.baseSpirit,
                baseAgility = memberStats.baseAgility,
                baseProficiency = memberStats.baseProficiency,
                inventoryItems = new List<InventorySlotSaveData>() // Инициализируем список
            };

            Inventory memberInventory = memberStats.GetComponent<Inventory>();
            if (memberInventory != null)
            {
                foreach (InventoryItem invItem in memberInventory.items)
                {
                    if (invItem.itemData != null)
                    {
                        memberSave.inventoryItems.Add(new InventorySlotSaveData
                        {
                            itemDataName = invItem.itemData.name,
                            quantity = invItem.quantity
                        });
                    }
                }
            }
            saveData.partyMembersData.Add(memberSave);
        }

        try
        {
            string json = JsonUtility.ToJson(saveData, true); // true для форматирования (удобно для отладки)
            File.WriteAllText(saveFilePath, json);
            // Сообщение об успехе теперь выводится в PlayerGlobalActions
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: Ошибка при сохранении игры: {e.Message}");
        }
    }

    public void LoadGame()
    {
        if (!File.Exists(saveFilePath))
        {
            Debug.LogWarning($"SaveManager: Файл сохранения не найден по пути: {saveFilePath}");
            return;
        }
        if (playerMovement == null || partyManager == null)
        {
            Debug.LogError("SaveManager: Отсутствуют ссылки на PlayerMovement или PartyManager. Загрузка отменена.");
            return;
        }

        try
        {
            string json = File.ReadAllText(saveFilePath);
            GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(json);

            CharacterController cc = playerMovement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerMovement.transform.position = loadedData.playerData.position;
            playerMovement.transform.rotation = loadedData.playerData.rotation;
            if (cc != null) cc.enabled = true;

            foreach (PartyMemberSaveData memberSaveData in loadedData.partyMembersData)
            {
                CharacterStats memberStats = partyManager.partyMembers.FirstOrDefault(
                    m => m != null && m.gameObject.name == memberSaveData.memberName);

                if (memberStats != null)
                {
                    memberStats.baseBody = memberSaveData.baseBody;
                    memberStats.baseMind = memberSaveData.baseMind;
                    memberStats.baseSpirit = memberSaveData.baseSpirit;
                    memberStats.baseAgility = memberSaveData.baseAgility;
                    memberStats.baseProficiency = memberSaveData.baseProficiency;

                    memberStats.level = memberSaveData.level;
                    memberStats.experience = memberSaveData.experience;
                    memberStats.experienceToNextLevel = memberSaveData.experienceToNextLevel;
                    memberStats.currentHealth = memberSaveData.currentHealth;
                    // maxHealth будет пересчитан в RefreshStatsAfterLoad

                    memberStats.RefreshStatsAfterLoad();

                    Inventory memberInventory = memberStats.GetComponent<Inventory>();
                    if (memberInventory != null)
                    {
                        memberInventory.items.Clear();
                        foreach (InventorySlotSaveData slotSaveData in memberSaveData.inventoryItems)
                        {
                            // Путь "Items/" должен соответствовать вашей структуре папок в Resources
                            ItemData itemData = Resources.Load<ItemData>($"Items/{slotSaveData.itemDataName}");
                            if (itemData != null)
                            {
                                memberInventory.AddItem(itemData, slotSaveData.quantity);
                            }
                            else
                            {
                                Debug.LogWarning($"SaveManager: Не удалось загрузить ItemData с именем '{slotSaveData.itemDataName}'. Убедитесь, что он находится в папке Resources/Items.");
                            }
                        }
                    }
                }
                else
                {
                     Debug.LogWarning($"SaveManager: Не найден член партии с именем '{memberSaveData.memberName}' при загрузке.");
                }
            }
            // Сообщение об успехе теперь выводится в PlayerGlobalActions

            PartyUIManager partyUI = FindObjectOfType<PartyUIManager>();
            if (partyUI != null) { partyUI.RefreshAllPartyMemberUIs(); }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: Ошибка при загрузке игры: {e.Message}");
        }
    }
}