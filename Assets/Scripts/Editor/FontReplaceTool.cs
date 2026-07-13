using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Tüm sahneler ve prefablardaki TMP_Text fontlarını kalıcı olarak Alagard'a çevirir.
/// HexAndVex > Replace All Fonts (Alagard) menüsünden erişilir.
/// </summary>
public class FontReplaceTool : EditorWindow
{
    [MenuItem("HexAndVex/Replace All Fonts (Alagard)")]
    public static void OpenWindow()
    {
        var win = GetWindow<FontReplaceTool>("Font Replace");
        win.minSize = new Vector2(380, 220);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Alagard Font Degistirme Araci", EditorStyles.boldLabel);
        GUILayout.Space(5);
        GUILayout.Label(
            "Tum sahneler ve prefablardaki TMP_Text fontlarini\n" +
            "kalici olarak Alagard'a cevirir.",
            EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);

        if (GUILayout.Button("Sadece Acik Sahneyi Degistir", GUILayout.Height(35)))
            ReplaceInCurrentScene();

        GUILayout.Space(5);

        if (GUILayout.Button("Tum Prefablari Degistir", GUILayout.Height(35)))
            ReplaceInAllPrefabs();

        GUILayout.Space(5);

        if (GUILayout.Button("Tum Sahneleri Degistir", GUILayout.Height(35)))
            ReplaceInAllScenes();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Not: Degisiklikler kalicidir (Ctrl+Z ile geri alinabilir).\n" +
            "Yeni eklenen UI'lar icin tekrar calistirilabilir.\n" +
            "Runtime'da FontOverrider zaten tum fontlari yakalar.",
            MessageType.Info);
    }

    private static TMP_FontAsset GetAlagardFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/alagard SDF.asset");
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/alagard SDF.asset");
        if (font == null)
        {
            Debug.LogError("Alagard SDF font asset bulunamadi!");
            return null;
        }
        return font;
    }

    private static void ReplaceInCurrentScene()
    {
        TMP_FontAsset font = GetAlagardFont();
        if (font == null) return;

        int count = 0;
        foreach (var txt in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (txt.font != font)
            {
                Undo.RecordObject(txt, "Replace Font");
                txt.font = font;
                EditorUtility.SetDirty(txt);
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Acik sahnede {count} TMP_Text fontu Alagard'a cevirildi.");
    }

    private static void ReplaceInAllPrefabs()
    {
        TMP_FontAsset font = GetAlagardFont();
        if (font == null) return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int totalCount = 0;
        int prefabCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            bool changed = false;

            foreach (var txt in texts)
            {
                if (txt.font != font)
                {
                    txt.font = font;
                    changed = true;
                    totalCount++;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                prefabCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{prefabCount} prefab'da toplam {totalCount} TMP_Text fontu Alagard'a cevirildi.");
    }

    private static void ReplaceInAllScenes()
    {
        TMP_FontAsset font = GetAlagardFont();
        if (font == null) return;

        // Mevcut sahneyi kaydet
        EditorSceneManager.SaveOpenScenes();
        string currentScenePath = EditorSceneManager.GetActiveScene().path;

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int totalCount = 0;
        int sceneCount = 0;

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int count = 0;
            foreach (var txt in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (txt.font != font)
                {
                    txt.font = font;
                    EditorUtility.SetDirty(txt);
                    count++;
                }
            }

            if (count > 0)
            {
                EditorSceneManager.SaveScene(scene);
                sceneCount++;
                totalCount += count;
            }
        }

        // Orijinal sahneyi geri ac
        if (!string.IsNullOrEmpty(currentScenePath))
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);

        Debug.Log($"{sceneCount} sahnede toplam {totalCount} TMP_Text fontu Alagard'a cevirildi.");
    }
}
