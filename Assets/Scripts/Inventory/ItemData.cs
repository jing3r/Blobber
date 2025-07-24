using UnityEngine;
// --- НАЧАЛО ИЗМЕНЕНИЯ ---
[System.Flags]
public enum EquipmentSlotType
{
    None = 0,
    Head = 1 << 0,      // 1
    Chest = 1 << 1,     // 2
    Legs = 1 << 2,      // 4
    Feet = 1 << 3,      // 8
    Armor = 1 << 4,     // 16
    LeftHand = 1 << 5,  // 32
    RightHand = 1 << 6, // 64

    // Комбинированный флаг для удобства
    Hands = LeftHand | RightHand // 96
}
// --- КОНЕЦ ИЗМЕНЕНИЯ ---


[CreateAssetMenu(fileName = "New Item", menuName = "RPG/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Общие свойства")]
    public string itemName = "New Item";
    [TextArea(3, 5)]
    public string description = "Item description.";
    public Sprite icon; // Для будущего UI

    [Header("Свойства инвентаря")]
    public float weight = 1.0f;
    public int gridWidth = 1;
    public int gridHeight = 1;
    public int maxStackSize = 1;

    [Header("Свойства экипировки")]
    public EquipmentSlotType validSlots = EquipmentSlotType.None;
    
}