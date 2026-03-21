using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Node minimap'i pause menüsüne otomatik kurar.
/// HexAndVex > Setup Node Minimap menüsünden erişilir.
/// PauseManager'ın pauseMenuUI paneline dropdown olarak eklenir.
/// </summary>
public class NodeMinimapSetupTool : EditorWindow
{
    [MenuItem("HexAndVex/Setup Node Minimap")]
    public static void OpenWindow()
    {
        var win = GetWindow<NodeMinimapSetupTool>("Node Minimap Setup");
        win.minSize = new Vector2(350, 200);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Node Minimap Kurulum Araci", EditorStyles.boldLabel);
        GUILayout.Space(5);
        GUILayout.Label(
            "Pause menusune collapsible node haritasi ekler.\n" +
            "Gecilen, mevcut ve gelecek node'lari gosterir.",
            EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);

        if (GUILayout.Button("Sahneye Kur", GUILayout.Height(40)))
            Setup();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Kurulum:\n" +
            "1. PauseManager'in pauseMenuUI panelini bulur\n" +
            "2. Icine 'NODE MAP' toggle butonu + collapsible panel ekler\n" +
            "3. NodeMinimapUI component'ini olusturur",
            MessageType.Info);
    }

    private void Setup()
    {
        // Zaten varsa uyar
        if (Object.FindFirstObjectByType<NodeMinimapUI>() != null)
        {
            if (!EditorUtility.DisplayDialog("Node Minimap Zaten Var",
                "Sahnede zaten bir NodeMinimapUI bulundu. Yeniden olusturmak istiyor musun?",
                "Evet", "Iptal"))
                return;

            var old = Object.FindFirstObjectByType<NodeMinimapUI>();
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        TMP_FontAsset font = UIStyle.LoadFont();

        // PauseManager'dan pauseMenuUI'yi bul
        PauseManager pm = Object.FindFirstObjectByType<PauseManager>();
        Transform parentPanel = null;
        if (pm != null && pm.pauseMenuUI != null)
            parentPanel = pm.pauseMenuUI.transform;

        if (parentPanel == null)
        {
            Debug.LogWarning("PauseManager veya pauseMenuUI bulunamadi! " +
                "Bagimsiz canvas olusturuluyor.");
            // Fallback: kendi canvas'ini olustur
            var canvas = CreateFallbackCanvas();
            parentPanel = canvas.transform;
        }

        // ── NodeMinimap root ──────────────────────────────
        GameObject rootGO = new GameObject("NodeMinimap", typeof(RectTransform));
        rootGO.transform.SetParent(parentPanel, false);
        Undo.RegisterCreatedObjectUndo(rootGO, "Create Node Minimap");

        RectTransform rootRT = rootGO.GetComponent<RectTransform>();
        // Sag alt koseye yerles
        rootRT.anchorMin = new Vector2(1, 0);
        rootRT.anchorMax = new Vector2(1, 0);
        rootRT.pivot = new Vector2(1, 0);
        rootRT.anchoredPosition = new Vector2(-20, 20);
        rootRT.sizeDelta = new Vector2(260, 400);

        // ── Toggle Button ────────────────────────────────
        GameObject toggleGO = new GameObject("ToggleButton", typeof(RectTransform));
        toggleGO.transform.SetParent(rootGO.transform, false);
        RectTransform trt = toggleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(0, 32);

        Image toggleBg = toggleGO.AddComponent<Image>();
        toggleBg.color = new Color(0.08f, 0.08f, 0.15f, 0.9f);

        Button toggleBtn = toggleGO.AddComponent<Button>();
        toggleBtn.targetGraphic = toggleBg;
        ColorBlock cb = toggleBtn.colors;
        cb.highlightedColor = new Color(0.12f, 0.12f, 0.22f, 0.95f);
        cb.pressedColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        toggleBtn.colors = cb;

        // Toggle text
        GameObject toggleTxtGO = new GameObject("Text", typeof(RectTransform));
        toggleTxtGO.transform.SetParent(toggleGO.transform, false);
        RectTransform ttrt = toggleTxtGO.GetComponent<RectTransform>();
        ttrt.anchorMin = Vector2.zero;
        ttrt.anchorMax = Vector2.one;
        ttrt.offsetMin = new Vector2(10, 0);
        ttrt.offsetMax = new Vector2(-10, 0);

        TextMeshProUGUI toggleTxt = toggleTxtGO.AddComponent<TextMeshProUGUI>();
        toggleTxt.text = "NODE MAP  >";
        toggleTxt.fontSize = 16;
        toggleTxt.color = Color.white;
        toggleTxt.alignment = TextAlignmentOptions.Left;
        if (font != null) toggleTxt.font = font;

        // ── Content Panel (collapsible, fixed size — Rebuild ayarlar) ──
        GameObject contentGO = new GameObject("ContentPanel", typeof(RectTransform));
        contentGO.transform.SetParent(rootGO.transform, false);
        RectTransform crt = contentGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.anchoredPosition = new Vector2(0, -34);
        crt.sizeDelta = new Vector2(0, 350);

        Image contentBg = contentGO.AddComponent<Image>();
        contentBg.color = new Color(0.03f, 0.03f, 0.08f, 0.85f);
        contentBg.raycastTarget = true;

        Outline contentOutline = contentGO.AddComponent<Outline>();
        contentOutline.effectColor = new Color(0.15f, 0.2f, 0.35f, 0.5f);
        contentOutline.effectDistance = new Vector2(1, -1);

        contentGO.SetActive(false);

        // ── NodeMinimapUI component ──────────────────────
        NodeMinimapUI minimap = rootGO.AddComponent<NodeMinimapUI>();
        minimap.contentPanel = contentGO;
        minimap.toggleButton = toggleBtn;
        minimap.toggleButtonText = toggleTxt;
        minimap.minimapFont = font;

        // Toggle button onClick — runtime'da Awake'te baglanir (AddListener persist etmez)

        Debug.Log("Node Minimap pause menusune eklendi!");
        Selection.activeGameObject = rootGO;
    }

    private GameObject CreateFallbackCanvas()
    {
        GameObject canvasGO = new GameObject("NodeMinimapCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Minimap Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }
}
