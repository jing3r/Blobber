using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string saveFileName = "savegame.dat";
    private string saveFilePath;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        saveFilePath = Path.Combine(Application.persistentDataPath, saveFileName);
    }
    
    public void SaveGame()
    {
        var saveData = new GameSaveData();
        
        // 1. Захватываем состояние всех ISaveable объектов в сцене
        var worldState = new Dictionary<string, object>();
        foreach (var saveable in FindObjectsOfType<SaveableEntity>(true))
        {
            ISaveable saveableComponent = saveable.GetComponent<ISaveable>();
            if (saveableComponent != null)
            {
                worldState[saveable.UniqueId] = saveableComponent.CaptureState();
            }
        }
        
        // Конвертируем словарь в списки для сериализации
        saveData.saveableEntityIds = worldState.Keys.ToList();
        saveData.saveableEntityStates = worldState.Values.ToList();

        // 2. Сохраняем файл
        SaveFile(saveData);
    }

    public void LoadGame()
    {
        var saveData = LoadFile();
        if (saveData == null) return;
        
        // 1. Конвертируем списки обратно в словарь для удобного доступа
        var worldState = new Dictionary<string, object>();
        for (int i = 0; i < saveData.saveableEntityIds.Count; i++)
        {
            worldState[saveData.saveableEntityIds[i]] = saveData.saveableEntityStates[i];
        }

        // 2. Восстанавливаем состояние всех ISaveable объектов в сцене
        foreach (var saveable in FindObjectsOfType<SaveableEntity>(true))
        {
            if (worldState.TryGetValue(saveable.UniqueId, out object savedState))
            {
                ISaveable saveableComponent = saveable.GetComponent<ISaveable>();
                if (saveableComponent != null)
                {
                    saveableComponent.RestoreState(savedState);
                }
            }
        }
    }

    private void SaveFile(GameSaveData saveData)
    {
        try
        {
            using (var stream = File.Open(saveFilePath, FileMode.Create))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, saveData);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: Ошибка при сохранении файла: {e.Message}");
        }
    }

    private GameSaveData LoadFile()
    {
        if (!File.Exists(saveFilePath))
        {
            Debug.LogWarning("SaveManager: Файл сохранения не найден.");
            return null;
        }

        try
        {
            using (var stream = File.Open(saveFilePath, FileMode.Open))
            {
                var formatter = new BinaryFormatter();
                return (GameSaveData)formatter.Deserialize(stream);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: Ошибка при загрузке файла: {e.Message}.");
            return null;
        }
    }
}