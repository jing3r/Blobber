using UnityEngine;

public class EquipmentPanelUI : MonoBehaviour
{
    public void Initialize(CharacterEquipment characterEquipment)
    {
        // Ищем слоты прямо здесь, включая неактивные.
        // Это делает код устойчивым к тому, включены ли слоты в префабе по умолчанию.
        var equipmentSlots = GetComponentsInChildren<EquipmentSlotUI>(true); 
        
        // Передаем каждому слоту ссылку на компонент экипировки персонажа
        foreach (var slot in equipmentSlots)
        {
            slot.Initialize(characterEquipment);
        }
    }
}