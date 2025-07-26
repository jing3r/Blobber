using System.Collections.Generic;
using System;

/// <summary>
/// Корневой объект, содержащий все данные для одного сохранения игры.
/// </summary>
[Serializable]
public class GameSaveData
{
    public List<string> SaveableEntityIds;
    public List<object> SaveableEntityStates;

    public GameSaveData()
    {
        SaveableEntityIds = new List<string>();
        SaveableEntityStates = new List<object>();
    }
}

/// <summary>
/// Данные о позиции и повороте главного объекта игрока.
/// </summary>
[Serializable]
public class PlayerSaveData
{
    public float[] Position;
    public float[] Rotation;

    public PlayerSaveData()
    {
        Position = new float[3];
        Rotation = new float[4];
    }
}

/// <summary>
/// Данные о состоянии одного члена партии.
/// </summary>
[Serializable]
public class PartyMemberSaveData
{
    public string MemberName;
    public int CurrentHealth;
    public int Level;
    public int Experience;
    public int ExperienceToNextLevel;
    
    public int BaseBody;
    public int BaseMind;
    public int BaseSpirit;
    public int BaseAgility;
    public int BaseProficiency;

    public List<InventorySlotSaveData> InventoryItems;
    public List<AbilitySaveData> AbilitiesData;
}

/// <summary>
/// Данные о слоте инвентаря.
/// </summary>
[Serializable]
public class InventorySlotSaveData
{
    public string ItemDataName;
    public int Quantity;
}

/// <summary>
/// Данные о состоянии одной способности.
/// </summary>
[Serializable]
public class AbilitySaveData
{
    public string AbilityDataName;
    public int CurrentCharges;
}