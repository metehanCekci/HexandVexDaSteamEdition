using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > Setup Active Perk Bar
/// Sahnede ActivePerkBar canvas'ini olusturur.
/// Inspector'dan duzenlenebilir — pozisyon, boyut, renk vs. hep gorunur.
/// </summary>
public class ActivePerkBarSetup : EditorWindow
{
    [MenuItem("Tools/Setup Active Perk Bar")]
    public static void Setup()
    {
        // 1. Eski PerkListUI'lari kaldir
        foreach (var old in Object.FindObjectsByType<PerkListUI>(FindObjectsSortMode.None))
        {
            Debug.Log($"Eski PerkListUI kaldiriliyor: {old.gameObject.name}");
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 2. Eski ActivePerkBar varsa kaldir
        foreach (var old in Object.FindObjectsByType<ActivePerkBar>(FindObjectsSortMode.None))
        {
            Transform root = old.transform;
            Debug.Log($"Eski ActivePerkBar kaldiriliyor: {root.name}");
            Undo.DestroyObjectImmediate(root.gameObject);
        }

        // 3. Font yukle
        TMP_FontAsset font = UIStyle.LoadFont();

        // ─── ROOT: ActivePerkBar ───
        GameObject rootGO = new GameObject("ActivePerkBar");
        ActivePerkBar bar = rootGO.AddComponent<ActivePerkBar>();
        Undo.RegisterCreatedObjectUndo(rootGO, "Create ActivePerkBar");

        // ─── CANVAS ───
        GameObject canvasGO = new GameObject("PerkBarCanvas");
        canvasGO.transform.SetParent(rootGO.transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        bar.barCanvas = canvas;

        // ─── ICON CONTAINER ───
        GameObject containerGO = new GameObject("IconContainer", typeof(RectTransform));
        containerGO.transform.SetParent(canvasGO.transform, false);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.anchoredPosition = new Vector2(0f, -10f);
        containerRT.sizeDelta = new Vector2(600f, 56f);

        HorizontalLayoutGroup hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter csf = containerGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bar.container = containerRT;

        // ─── TOOLTIP ───
        bar.BuildTooltip(canvasGO.transform);
        if (font != null && bar.tooltipText != null)
            bar.tooltipText.font = font;

        // ─── INV BUTONU ───
        bar.BuildInventoryButton(canvasGO.transform);
        // Font'u INV butonuna da uygula
        if (font != null)
        {
            var btnTMP = bar.inventoryButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnTMP != null) btnTMP.font = font;
        }

        // ─── SELECTION ───
        Selection.activeGameObject = rootGO;
        EditorUtility.SetDirty(rootGO);

        Debug.Log("✅ ActivePerkBar sahnede oluşturuldu!\n" +
                  "• Inspector'dan pozisyon, boyut, renk ayarlayabilirsin\n" +
                  "• Runtime'da ikonlar otomatik dolar\n" +
                  "• INV butonu sağ üstte — PerkInventoryUI'ı açar");
    }
}
