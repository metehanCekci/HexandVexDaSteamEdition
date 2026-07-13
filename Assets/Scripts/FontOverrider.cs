using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Runtime'da tüm TMP_Text fontlarını Alagard'a çevirir.
/// Her sahne yüklendiğinde otomatik çalışır (RuntimeInitializeOnLoadMethod).
/// Sahnede inspector'dan atanmış veya kodla oluşturulmuş tüm textleri yakalar.
/// </summary>
public static class FontOverrider
{
    private static TMP_FontAsset cachedFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        OverrideAllFonts();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OverrideAllFonts();
    }

    public static void OverrideAllFonts()
    {
        if (cachedFont == null)
            cachedFont = Resources.Load<TMP_FontAsset>("alagard SDF");

        if (cachedFont == null) return;

        foreach (var txt in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (txt.font != cachedFont)
                txt.font = cachedFont;
        }
    }
}
