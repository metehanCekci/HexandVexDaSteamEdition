using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Sahneye ornek bir Volume Slider olusturur.
/// Mask + BarPattern yapisini gosterir — buna bakarak kendi slider'ini ayarlayabilirsin.
/// Tools > Create Sample Volume Slider
/// </summary>
public static class VolumeSliderSetup
{
    [MenuItem("Tools/Create Sample Volume Slider")]
    public static void CreateSampleSlider()
    {
        // Find or create a Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Sahnede Canvas bulunamadi! Önce bir Canvas olustur.");
            return;
        }

        // Try to load the fill (bar) sprite
        Sprite barSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MAIN MENU AND UI/fill.png");
        if (barSprite == null)
            Debug.LogWarning("fill.png sprite bulunamadi. BarMask'a manuel olarak sprite ata.");

        // Try to load the slider handle sprite
        Sprite handleSprite = null;
        // Check for sliderhead as aseprite-imported sprite
        var allSprites = AssetDatabase.FindAssets("sliderhead t:Sprite");
        if (allSprites.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(allSprites[0]);
            handleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── Root: Slider GameObject ──────────────────────────────────────────
        GameObject sliderGO = new GameObject("SampleVolumeSlider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(canvas.transform, false);

        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRT.pivot = new Vector2(0.5f, 0.5f);
        sliderRT.sizeDelta = new Vector2(400, 40);
        sliderRT.anchoredPosition = Vector2.zero;

        Slider slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        // ── Background (optional dark bg) ────────────────────────────────────
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);

        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        Stretch(bgRT);

        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

        // ── Fill Area ────────────────────────────────────────────────────────
        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);

        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        Stretch(fillAreaRT);
        // Slight padding so bars don't touch edges
        fillAreaRT.offsetMin = new Vector2(5, 4);
        fillAreaRT.offsetMax = new Vector2(-5, -4);

        // ── BarMask (Image with bar sprite + Mask) ───────────────────────────
        GameObject barMaskGO = new GameObject("BarMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        barMaskGO.transform.SetParent(fillAreaGO.transform, false);

        RectTransform barMaskRT = barMaskGO.GetComponent<RectTransform>();
        Stretch(barMaskRT);

        Image barMaskImg = barMaskGO.GetComponent<Image>();
        if (barSprite != null)
        {
            barMaskImg.sprite = barSprite;
            barMaskImg.type = Image.Type.Simple;
            barMaskImg.preserveAspect = false;
        }
        barMaskImg.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Unfilled bars = dark gray

        Mask mask = barMaskGO.GetComponent<Mask>();
        mask.showMaskGraphic = true; // Show unfilled bars as gray

        // ── Fill (renkli bar sprite, sabit boyut, clipped by BarMask) ─────────
        // Fill Area'nin ici boyutunu hesapla (padding cikarilmis)
        float fillAreaW = 400f - 10f; // sliderWidth - left/right padding
        float fillAreaH = 40f - 8f;   // sliderHeight - top/bottom padding

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barMaskGO.transform, false);

        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        // Sabit boyut — sol alta sabitle, slider anchor'i hareket ettirse de sprite uzamaz
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 0f);
        fillRT.pivot = new Vector2(0f, 0f);
        fillRT.sizeDelta = new Vector2(fillAreaW, fillAreaH);
        fillRT.anchoredPosition = Vector2.zero;

        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.sprite = barSprite; // Ayni cubuk sprite — renkli olanla degistir
        fillImg.type = Image.Type.Simple;
        fillImg.color = Color.white;

        // ── Handle Slide Area ────────────────────────────────────────────────
        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);

        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        Stretch(handleAreaRT);

        // ── Handle ───────────────────────────────────────────────────────────
        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);

        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 0f);
        handleRT.anchorMax = new Vector2(0f, 1f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(20, 0);
        handleRT.anchoredPosition = Vector2.zero;

        Image handleImg = handleGO.GetComponent<Image>();
        if (handleSprite != null)
        {
            handleImg.sprite = handleSprite;
            handleImg.color = Color.white;
        }
        else
        {
            handleImg.color = Color.white;
        }

        // ── Wire Slider references ───────────────────────────────────────────
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;

        // Select it
        Selection.activeGameObject = sliderGO;
        Undo.RegisterCreatedObjectUndo(sliderGO, "Create Sample Volume Slider");

        Debug.Log("Sample Volume Slider olusturuldu! Hierarchy'de 'SampleVolumeSlider' objesine bak.\n" +
                  "BarMask'in sprite'ini kontrol et — fill.png cubuk deseni olmali.");
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
