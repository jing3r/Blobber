using UnityEngine;
using System; // Для атрибута Flags

/// <summary>
/// Типы слотов для экипировки. Использует Flags для возможности указать несколько слотов.
/// </summary>
[Flags]
public enum EquipmentSlotType
{
    None = 0,
    Head = 1 << 0,
    Chest = 1 << 1,
    Legs = 1 << 2,
    Feet = 1 << 3,
    Armor = 1 << 4,
    LeftHand = 1 << 5,
    RightHand = 1 << 6,
    Hands = LeftHand | RightHand
}

/// <summary>
/// ScriptableObject, содержащий все неизменяемые данные о предмете.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "RPG/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Основная информация")]
    [SerializeField] private string itemName = "New Item";
    [SerializeField] [TextArea(3, 5)] private string description = "Item description.";
    [SerializeField] private Sprite icon;

    [Header("Свойства инвентаря")]
    [SerializeField] private float weight = 1.0f;
    [SerializeField] [Range(1, 10)] private int gridWidth = 1;
    [SerializeField] [Range(1, 10)] private int gridHeight = 1;
    [SerializeField] [Min(1)] private int maxStackSize = 1;

    [Header("Свойства экипировки")]
    [SerializeField] private EquipmentSlotType validSlots = EquipmentSlotType.None;
    
    // Публичные свойства для доступа к данным только на чтение
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public float Weight => weight;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public int MaxStackSize => maxStackSize;
    public EquipmentSlotType ValidSlots => validSlots;
}