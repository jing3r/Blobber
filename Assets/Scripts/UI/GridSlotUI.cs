using UnityEngine;

/// <summary>
/// Простой компонент-маркер для ячейки в сетке инвентаря.
/// Хранит свои координаты в сетке.
/// </summary>
public class GridSlotUI : MonoBehaviour
{
    public int X { get; private set; }
    public int Y { get; private set; }

    public void SetCoordinates(int x, int y)
    {
        X = x;
        Y = y;
    }
}