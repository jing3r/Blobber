using UnityEngine;
using TMPro;

/// <summary>
/// Отображает текущую версию приложения в текстовом поле.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class VersionDisplayUI : MonoBehaviour
{
    private void Awake()
    {
        var textComponent = GetComponent<TextMeshProUGUI>();
        textComponent.text = $"v{Application.version}";
    }
}