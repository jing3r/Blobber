using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System;

/// <summary>
/// Управляет процессами сохранения и загрузки игры (Singleton).
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Настройки сохранения")]
    [SerializeField] private string saveFileName = "savegame.dat";
    
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
    }

    /// <summary>
    /// Сохраняет текущее состояние игры в файл.
    /// </summary>
    public void SaveGame()
    {
        var worldState = CaptureWorldState();
        WriteSaveFile(worldState);
        Debug.Log($"Game Saved to {SaveFilePath}");
    }

    /// <summary>
    /// Загружает состояние игры из файла.
    /// </summary>
    public void LoadGame()
    {
        var worldState = ReadSaveFile();
        if (worldState.Count > 0)
        {
            RestoreWorldState(worldState);
            Debug.Log("Game Loaded.");
        }
    }

    private Dictionary<string, object> CaptureWorldState()
    {
        var state = new Dictionary<string, object>();
        foreach (var saveable in FindObjectsOfType<SaveableEntity>(true))
        {
            var saveableComponent = saveable.GetComponent<ISaveable>();
            if (saveableComponent != null)
            {
                state[saveable.UniqueId] = saveableComponent.CaptureState();
            }
        }
        return state;
    }

    private void RestoreWorldState(Dictionary<string, object> state)
    {
        foreach (var saveable in FindObjectsOfType<SaveableEntity>(true))
        {
            if (state.TryGetValue(saveable.UniqueId, out object savedState))
            {
                var saveableComponent = saveable.GetComponent<ISaveable>();
                saveableComponent?.RestoreState(savedState);
            }
        }
    }

    private void WriteSaveFile(Dictionary<string, object> worldState)
    {
        var saveData = new GameSaveData
        {
            SaveableEntityIds = worldState.Keys.ToList(),
            SaveableEntityStates = worldState.Values.ToList()
        };

        try
        {
            using (var stream = File.Open(SaveFilePath, FileMode.Create))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, saveData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to write save file: {e.Message}");
        }
    }

    private Dictionary<string, object> ReadSaveFile()
    {
        string path = SaveFilePath;
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SaveManager] Save file not found.");
            return new Dictionary<string, object>();
        }

        try
        {
            using (var stream = File.Open(path, FileMode.Open))
            {
                var formatter = new BinaryFormatter();
                var saveData = (GameSaveData)formatter.Deserialize(stream);

                var worldState = new Dictionary<string, object>();
                for (int i = 0; i < saveData.SaveableEntityIds.Count; i++)
                {
                    worldState[saveData.SaveableEntityIds[i]] = saveData.SaveableEntityStates[i];
                }
                return worldState;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to read or deserialize save file: {e.Message}");
            return new Dictionary<string, object>();
        }
    }
}