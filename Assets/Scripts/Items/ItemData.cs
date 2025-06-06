using UnityEngine;

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
}