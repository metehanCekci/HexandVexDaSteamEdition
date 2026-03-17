using UnityEngine;
using UnityEditor;

public class PersistentHUDSetupTool
{
    private const string CONFIG_PATH = "Assets/ScriptableObjects/PersistentHUDConfig.asset";

    [MenuItem("Tools/Persistent HUD/Select Config")]
    static void SelectConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<PersistentHUDConfig>(CONFIG_PATH);
        if (config == null)
        {
            // Auto-create if missing
            CreateConfig();
            config = AssetDatabase.LoadAssetAtPath<PersistentHUDConfig>(CONFIG_PATH);
        }
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    [MenuItem("Tools/Persistent HUD/Create Config")]
    static void CreateConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PersistentHUDConfig>(CONFIG_PATH);
        if (existing != null)
        {
            Debug.Log("PersistentHUDConfig already exists at " + CONFIG_PATH);
            Selection.activeObject = existing;
            return;
        }

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        var config = ScriptableObject.CreateInstance<PersistentHUDConfig>();
        AssetDatabase.CreateAsset(config, CONFIG_PATH);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        Debug.Log("Created PersistentHUDConfig at " + CONFIG_PATH);
    }

    [MenuItem("Tools/Persistent HUD/Apply Config (Runtime)")]
    static void ApplyRuntime()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Only works in Play Mode.");
            return;
        }
        if (PersistentHUD.instance != null)
        {
            PersistentHUD.instance.RebuildFromConfig();
            Debug.Log("PersistentHUD rebuilt from config.");
        }
        else
        {
            Debug.LogWarning("PersistentHUD instance not found.");
        }
    }
}
