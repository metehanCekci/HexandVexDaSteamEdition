using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > Setup Hotbar UI
/// Sahnede HotbarUI hierarchy'sini oluşturur.
/// Inspector'dan her şey düzenlenebilir — pozisyon, boyut, renk, slot sayısı vs.
/// </summary>
public class HotbarSetupTool : EditorWindow
{
    private const int MAX_SLOTS = 5;
    private const int DEFAULT_VISIBLE = 3;
    private const float SLOT_SIZE = 60f;
    private const float SLOT_SPACING = 6f;

    [MenuItem("Tools/Setup Hotbar UI")]
    public static void Setup()
    {
        // 1. Eski HotbarUI varsa kaldır
        foreach (var old in Object.FindObjectsByType<HotbarUI>(FindObjectsSortMode.None))
        {
            Debug.Log($"Eski HotbarUI kaldırılıyor: {old.gameObject.name}");
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 2. Font yükle
        TMP_FontAsset font = UIStyle.LoadFont();

        // ─── ROOT: HotbarUI ───
        GameObject rootGO = new GameObject("HotbarUI");
        HotbarUI hotbar = rootGO.AddComponent<HotbarUI>();
        Undo.RegisterCreatedObjectUndo(rootGO, "Create HotbarUI");

        // ─── CANVAS ───
        GameObject canvasGO = new GameObject("HotbarCanvas");
        canvasGO.transform.SetParent(rootGO.transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        hotbar.hotbarCanvas = canvas;

        // ─── PANEL (bottom-center) ───
        GameObject panelGO = new GameObject("HotbarPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 12f);

        float totalWidth = DEFAULT_VISIBLE * SLOT_SIZE + (DEFAULT_VISIBLE - 1) * SLOT_SPACING + 20f;
        panelRT.sizeDelta = new Vector2(totalWidth, SLOT_SIZE + 30f);

        // Panel background
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        panelBG.raycastTarget = false;

        hotbar.hotbarPanel = panelRT;
        hotbar.panelBackground = panelBG;

        // ─── SLOTS (5 adet — ilk 3 aktif, son 2 gizli) ───
        hotbar.slots.Clear();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            HotbarSlotUI slotUI = CreateSlot(panelGO.transform, i, font);
            hotbar.slots.Add(slotUI);

            // Varsayılan: ilk 3 aktif, geri kalan gizli
            slotUI.gameObject.SetActive(i < DEFAULT_VISIBLE);
        }

        // ─── CONFIG DEFAULTS ───
        hotbar.maxVisibleSlots = DEFAULT_VISIBLE;
        hotbar.slotSize = SLOT_SIZE;
        hotbar.slotSpacing = SLOT_SPACING;

        // ─── SELECTION ───
        Selection.activeGameObject = rootGO;
        EditorUtility.SetDirty(rootGO);

        Debug.Log("HotbarUI sahnede oluşturuldu!\n" +
                  "• Inspector'dan pozisyon, boyut, renk ayarlayabilirsin\n" +
                  "• Slot'ları hierarchy'de seçip düzenleyebilirsin\n" +
                  "• 5 slot hazır — ilk 3 aktif, OrganPouch ile açılır\n" +
                  "• Runtime'da item'lar otomatik dolar");
    }

    private static HotbarSlotUI CreateSlot(Transform parent, int index, TMP_FontAsset font)
    {
        // ─── Slot Root ───
        GameObject slotGO = new GameObject($"HotbarSlot_{index}", typeof(RectTransform));
        slotGO.transform.SetParent(parent, false);

        RectTransform slotRT = slotGO.GetComponent<RectTransform>();
        slotRT.anchorMin = new Vector2(0f, 0.5f);
        slotRT.anchorMax = new Vector2(0f, 0.5f);
        slotRT.pivot = new Vector2(0f, 0.5f);
        slotRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        slotRT.anchoredPosition = new Vector2(10f + index * (SLOT_SIZE + SLOT_SPACING), 5f);

        // Background
        Image slotBG = slotGO.AddComponent<Image>();
        slotBG.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        slotBG.raycastTarget = true;

        // Button
        Button btn = slotGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.4f, 0.4f, 0.2f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.2f, 1f);
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        btn.colors = cb;

        // Tooltip
        HotbarSlotTooltip tooltip = slotGO.AddComponent<HotbarSlotTooltip>();

        // HotbarSlotUI component (tüm referansları tutar)
        HotbarSlotUI slotUI = slotGO.AddComponent<HotbarSlotUI>();
        slotUI.background = slotBG;
        slotUI.button = btn;
        slotUI.tooltip = tooltip;

        // ─── Icon ───
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(slotGO.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;
        slotUI.iconImage = iconImg;

        // ─── KeyLabel ───
        GameObject keyGO = new GameObject("KeyLabel", typeof(RectTransform));
        keyGO.transform.SetParent(slotGO.transform, false);
        RectTransform keyRT = keyGO.GetComponent<RectTransform>();
        keyRT.anchorMin = new Vector2(0f, 1f);
        keyRT.anchorMax = new Vector2(0f, 1f);
        keyRT.pivot = new Vector2(0f, 1f);
        keyRT.anchoredPosition = new Vector2(3f, -2f);
        keyRT.sizeDelta = new Vector2(20f, 18f);
        TMP_Text keyText = keyGO.AddComponent<TextMeshProUGUI>();
        keyText.text = (index + 1).ToString();
        keyText.fontSize = 11;
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        keyText.raycastTarget = false;
        if (font != null) keyText.font = font;
        slotUI.keyLabel = keyText;

        return slotUI;
    }
}
