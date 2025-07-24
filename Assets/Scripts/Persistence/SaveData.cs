// SaveData.cs

using System.Collections.Generic;

[System.Serializable]
public class GameSaveData
{
    public List<string> saveableEntityIds;
    public List<object> saveableEntityStates;

    public GameSaveData()
    {
        saveableEntityIds = new List<string>();
        saveableEntityStates = new List<object>();
    }
}

[System.Serializable]
public class PlayerSaveData
{
    public float[] position;
    public float[] rotation;

    public PlayerSaveData()
    {
        position = new float[3];
        rotation = new float[4];
    }
}

[System.Serializable]
public class PartyMemberSaveData
{
    public string memberName;
    public int currentHealth;
    public List<InventorySlotSaveData> inventoryItems;
    public int level;
    public int experience;
    public int experienceToNextLevel;
    
    public int baseBody;
    public int baseMind;
    public int baseSpirit;
    public int baseAgility;
    public int baseProficiency;

    public List<AbilitySaveData> abilitiesData;

    public PartyMemberSaveData()
    {
        inventoryItems = new List<InventorySlotSaveData>();
        abilitiesData = new List<AbilitySaveData>();
        level = 1;
    }
}

[System.Serializable]
public class InventorySlotSaveData
{
    public string itemDataName;
    public int quantity;
}

[System.Serializable]
public class AbilitySaveData
{
    public string abilityDataName;
    public int currentCharges;
}