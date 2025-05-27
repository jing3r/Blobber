using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameSaveData
{
    public PlayerSaveData playerData;
    public List<PartyMemberSaveData> partyMembersData;

    public GameSaveData()
    {
        playerData = new PlayerSaveData();
        partyMembersData = new List<PartyMemberSaveData>();
    }
}

[System.Serializable]
public class PlayerSaveData
{
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public class PartyMemberSaveData
{
    public string memberName;
    public int currentHealth;
    public int maxHealth;
    public List<InventorySlotSaveData> inventoryItems;
    public int level;
    public int experience;
    public int experienceToNextLevel;
    public int baseBody;
    public int baseMind;
    public int baseSpirit;
    public int baseAgility;
    public int baseProficiency;

    public PartyMemberSaveData()
    {
        inventoryItems = new List<InventorySlotSaveData>();
        level = 1; // Начальное значение по умолчанию
    }
}

[System.Serializable]
public class InventorySlotSaveData
{
    public string itemDataName;
    public int quantity;
}