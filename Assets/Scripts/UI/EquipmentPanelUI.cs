using UnityEngine;

/// <summary>
/// Управляет группой слотов экипировки (EquipmentSlotUI).
/// Отвечает за их инициализацию.
/// </summary>
public class EquipmentPanelUI : MonoBehaviour
{
    /// <summary>
    /// Инициализирует все дочерние слоты экипировки.
    /// </summary>
    public void Initialize(CharacterEquipment characterEquipment)
    {
        // Ищем слоты, включая неактивные, чтобы сделать код более надежным
        var equipmentSlots = GetComponentsInChildren<EquipmentSlotUI>(true); 
        
        foreach (var slot in equipmentSlots)
        {
            slot.Initialize(characterEquipment);
        }
    }
}