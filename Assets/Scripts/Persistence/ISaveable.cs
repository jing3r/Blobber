/// <summary>
/// Интерфейс для объектов, состояние которых можно сохранять и загружать.
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Собирает текущее состояние объекта в сериализуемый формат.
    /// </summary>
    /// <returns>Объект, содержащий данные для сохранения.</returns>
    object CaptureState();

    /// <summary>
    /// Восстанавливает состояние объекта из данных.
    /// </summary>
    /// <param name="state">Объект с данными, полученный из файла сохранения.</param>
    void RestoreState(object state);
}