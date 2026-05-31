using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapLauncher : MonoBehaviour
{
    [Header("Default map if no --map= is provided")]
    public string defaultMapScene = "Map6";

    [Header("Optional: allowed map scene names")]
    public string[] allowedScenes = new string[] { "Map1", "Map2", "Map3", "Map4", "Map5", "Map6" };

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        string chosen = ParseMapArg() ?? defaultMapScene;

        if (!IsAllowed(chosen))
        {
            Debug.LogWarning($"[MapLauncher] Unknown map '{chosen}'. Falling back to '{defaultMapScene}'.");
            chosen = defaultMapScene;
        }

        Debug.Log($"[MapLauncher] Loading map scene: {chosen}");
        SceneManager.LoadScene(chosen, LoadSceneMode.Single);
    }

    string ParseMapArg()
    {
        string[] args = Environment.GetCommandLineArgs();
        foreach (var a in args)
        {
            if (!a.StartsWith("--map=", StringComparison.OrdinalIgnoreCase)) continue;

            string value = a.Substring("--map=".Length).Trim();
            if (string.IsNullOrEmpty(value)) return null;

            // numeric shortcut: --map=3 -> Map3
            if (int.TryParse(value, out int n))
                return "Map" + n;

            return value;
        }
        return null;
    }

    bool IsAllowed(string sceneName)
    {
        for (int i = 0; i < allowedScenes.Length; i++)
            if (string.Equals(allowedScenes[i], sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
