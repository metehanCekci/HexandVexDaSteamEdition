using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using TMPro;

/// <summary>
/// Profil UI'ını MainMenu sahnesine otomatik kurar.
/// HexAndVex > Setup Profile UI menüsünden erişilir.
/// </summary>
public class ProfileSetupTool : EditorWindow
{
    [MenuItem("HexAndVex/Setup Profile UI")]
    public static void OpenWindow()
    {
        var win = GetWindow<ProfileSetupTool>("Profile Setup");
        win.minSize = new Vector2(350, 200);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Profil UI Kurulum Araci", EditorStyles.boldLabel);
        GUILayout.Space(5);
        GUILayout.Label(
            "MainMenu sahnesine profil popup canvas'ini,\n" +
            "ProfileManager'i ve PROFILES butonunu kurar.\n" +
            "Buton MainCanvas icine eklenir.",
            EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);

        if (GUILayout.Button("Sahneye Kur", GUILayout.Height(40)))
            SetupProfileUI();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Kurulum otomatik olarak:\n" +
            "1. ProfileCanvas + popup panel olusturur\n" +
            "2. ProfileManager objesi yoksa ekler\n" +
            "3. MainCanvas icine PROFILES butonu ekler\n" +
            "   (Collection butonunun altina yerlestirir)",
            MessageType.Info);
    }

    private void SetupProfileUI()
    {
        // Zaten varsa uyar
        if (Object.FindFirstObjectByType<ProfileUI>() != null)
        {
            if (!EditorUtility.DisplayDialog("Profile UI Zaten Var",
                "Sahnede zaten bir ProfileUI bulundu. Yeniden olusturmak istiyor musun?",
                "Evet, Yeniden Olustur", "Iptal"))
                return;

            var old = Object.FindFirstObjectByType<ProfileUI>();
            if (old != null) Undo.DestroyObjectImmediate(old.transform.root.gameObject);
        }

        TMP_FontAsset font = UIStyle.LoadFont();

        // ProfileManager yoksa olustur
        if (Object.FindFirstObjectByType<ProfileManager>() == null)
        {
            GameObject pmGO = new GameObject("ProfileManager");
            Undo.RegisterCreatedObjectUndo(pmGO, "Create ProfileManager");
            pmGO.AddComponent<ProfileManager>();
        }

        // ── 1. ProfileCanvas (popup panel icin) ─────────────
        GameObject canvasGO = new GameObject("ProfileCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Profile UI");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 2. Panel Root (popup — runtime'da kartlar olusturulacak) ──
        GameObject panelRoot = CreatePanel(canvasGO.transform, "PanelRoot",
            Vector2.zero, new Vector2(1100, 500));
        Image panelBg = panelRoot.GetComponent<Image>();
        panelBg.color = new Color(0f, 0.02f, 0.06f, 0.97f);

        Outline outline = panelRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.2f, 0.4f, 0.6f);
        outline.effectDistance = new Vector2(2, -2);

        CanvasGroup panelCG = panelRoot.AddComponent<CanvasGroup>();

        // ── 3. ProfileUI component ─────────────────────────
        ProfileUI profileUI = canvasGO.AddComponent<ProfileUI>();
        profileUI.panelRoot = panelRoot;
        profileUI.panelCanvasGroup = panelCG;
        profileUI.profileFont = font;

        // Panel baslangiçta kapali
        panelRoot.SetActive(false);

        // ── 4. PROFILES butonu — MainCanvas icine ──────────
        CreateProfileButton(profileUI, font);

        Debug.Log("Profile UI sahnede kuruldu!");
        Selection.activeGameObject = canvasGO;
    }

    private void CreateProfileButton(ProfileUI profileUI, TMP_FontAsset font)
    {
        // MainCanvas'i bul
        GameObject mainCanvas = GameObject.Find("MainCanvas");
        if (mainCanvas == null)
        {
            Debug.LogWarning("MainCanvas bulunamadi! PROFILES butonu olusturulamadi. " +
                "Sahnede 'MainCanvas' adinda bir Canvas olmali.");
            return;
        }

        // Zaten PROFILES butonu varsa ekleme
        var existingBtns = mainCanvas.GetComponentsInChildren<Button>(true);
        foreach (var b in existingBtns)
        {
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt != null && txt.text.Trim().ToUpper() == "PROFILES")
            {
                Debug.Log("PROFILES butonu zaten MainCanvas icinde mevcut.");
                return;
            }
        }

        // Collection butonunu bul (kopyalamak icin)
        Button collectionBtn = null;
        foreach (var b in existingBtns)
        {
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt != null && txt.text.Trim().ToUpper() == "COLLECTION")
            {
                collectionBtn = b;
                break;
            }
        }

        if (collectionBtn != null)
        {
            // Collection butonunu klonla
            Transform parent = collectionBtn.transform.parent;
            RectTransform collRT = collectionBtn.GetComponent<RectTransform>();

            GameObject btnGO = Object.Instantiate(collectionBtn.gameObject, parent);
            btnGO.name = "ProfilesButton";
            Undo.RegisterCreatedObjectUndo(btnGO, "Create Profiles Button");

            // Pozisyonu Collection butonunun altina kaydir
            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchoredPosition = collRT.anchoredPosition
                + new Vector2(0, -(collRT.sizeDelta.y + 10));

            // Metni degistir
            var btnText = btnGO.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "PROFILES";

            // Eski onClick'leri temizle, yenisini bagla
            Button btn = btnGO.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(
                btn.onClick, new UnityEngine.Events.UnityAction(profileUI.Open));

            Debug.Log("PROFILES butonu MainCanvas > Collection butonunun altina eklendi.");
        }
        else
        {
            // Collection butonu yok — MainCanvas icine sifirdan olustur
            GameObject btnGO = new GameObject("ProfilesButton", typeof(RectTransform));
            btnGO.transform.SetParent(mainCanvas.transform, false);
            Undo.RegisterCreatedObjectUndo(btnGO, "Create Profiles Button");

            RectTransform brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0);
            brt.anchorMax = new Vector2(0.5f, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 30);
            brt.sizeDelta = new Vector2(UIStyle.MainMenuBtnWidth, UIStyle.MainMenuBtnHeight);

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = UIStyle.BgDark;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.colors = UIStyle.ButtonColors();
            UnityEventTools.AddPersistentListener(
                btn.onClick, new UnityEngine.Events.UnityAction(profileUI.Open));

            UIStyle.AddOutline(btnGO);

            GameObject txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(btnGO.transform, false);
            RectTransform trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "PROFILES";
            tmp.fontSize = UIStyle.FontSizeNormal;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;

            Debug.Log("PROFILES butonu MainCanvas icine olusturuldu (Collection butonu bulunamadi).");
        }
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = Color.black;
        return go;
    }
}
