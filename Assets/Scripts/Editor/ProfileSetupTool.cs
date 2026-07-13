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

        // ── 3. PanelRoot içeriğini oluştur ───────────────────
        BuildPanelContent(panelRoot.transform, font);

        // ── 4. ProfileUI component ─────────────────────────
        ProfileUI profileUI = canvasGO.AddComponent<ProfileUI>();
        profileUI.panelRoot = panelRoot;
        profileUI.panelCanvasGroup = panelCG;
        profileUI.profileFont = font;

        // Panel baslangiçta kapali
        panelRoot.SetActive(false);

        // ── 5. PROFILES butonu — MainCanvas icine ──────────
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

        // Zaten PROFILES butonu varsa → onClick'i yeni ProfileUI'a bağla
        var existingBtns = mainCanvas.GetComponentsInChildren<Button>(true);
        foreach (var b in existingBtns)
        {
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt != null && txt.text.Trim().ToUpper() == "PROFILES")
            {
                b.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(
                    b.onClick, new UnityEngine.Events.UnityAction(profileUI.Open));
                Debug.Log("PROFILES butonu mevcut — onClick yeni ProfileUI'a bağlandı.");
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

    // ═══════════════════════════════════════════════
    // PANEL CONTENT — PanelRoot'un içini doldurur
    // ═══════════════════════════════════════════════

    private static void BuildPanelContent(Transform panelRoot, TMP_FontAsset font)
    {
        // Mevcut içerik varsa temizle
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(panelRoot.GetChild(i).gameObject);

        // ── Header ──
        GameObject headerGO = MakeElement("Header", panelRoot);
        RectTransform hrt = headerGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0, -10);
        hrt.sizeDelta = new Vector2(0, 60);

        TextMeshProUGUI titleTxt = MakeText(headerGO.transform, "PROFILES", 32,
            TextAlignmentOptions.Center, font);
        titleTxt.color = Color.white;
        Stretch(titleTxt.GetComponent<RectTransform>());

        // ── Close Button ──
        GameObject closeBtnGO = MakeElement("CloseButton", panelRoot);
        RectTransform cbrt = closeBtnGO.GetComponent<RectTransform>();
        cbrt.anchorMin = new Vector2(1, 1);
        cbrt.anchorMax = new Vector2(1, 1);
        cbrt.pivot = new Vector2(1, 1);
        cbrt.anchoredPosition = new Vector2(-15, -15);
        cbrt.sizeDelta = new Vector2(40, 40);

        Image cbImg = closeBtnGO.AddComponent<Image>();
        cbImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        closeBtnGO.AddComponent<Button>().targetGraphic = cbImg;

        TextMeshProUGUI xTxt = MakeText(closeBtnGO.transform, "X", 22,
            TextAlignmentOptions.Center, font);
        xTxt.color = Color.white;
        xTxt.raycastTarget = false;
        Stretch(xTxt.GetComponent<RectTransform>());

        // ── Profile Cards (3 adet) ──
        float cardWidth = 280f;
        float cardHeight = 320f;
        float spacing = 30f;
        int maxProfiles = 3; // ProfileManager.MAX_PROFILES
        float totalWidth = maxProfiles * cardWidth + (maxProfiles - 1) * spacing;
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < maxProfiles; i++)
        {
            GameObject cardGO = MakeElement($"ProfileCard_{i}", panelRoot);
            RectTransform crt = cardGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), -10f);
            crt.sizeDelta = new Vector2(cardWidth, cardHeight);

            Image cardBg = cardGO.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            BuildCardContent(cardGO.transform, i, font);
        }
    }

    private static void BuildCardContent(Transform card, int profileId, TMP_FontAsset font)
    {
        // NumText (#1, #2, #3)
        TextMeshProUGUI numTxt = MakeText(card, $"#{profileId + 1}", 18,
            TextAlignmentOptions.TopLeft, font);
        numTxt.color = new Color(0.6f, 0.6f, 0.7f);
        numTxt.gameObject.name = "NumText";
        RectTransform nrt = numTxt.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1);
        nrt.pivot = new Vector2(0, 1);
        nrt.anchoredPosition = new Vector2(12, -10);
        nrt.sizeDelta = new Vector2(-24, 25);

        // NameText
        TextMeshProUGUI nameTxt = MakeText(card, "Empty Slot", 24,
            TextAlignmentOptions.Center, font);
        nameTxt.color = Color.white;
        nameTxt.gameObject.name = "NameText";
        RectTransform nmrt = nameTxt.GetComponent<RectTransform>();
        nmrt.anchorMin = new Vector2(0, 1); nmrt.anchorMax = new Vector2(1, 1);
        nmrt.pivot = new Vector2(0.5f, 1);
        nmrt.anchoredPosition = new Vector2(0, -40);
        nmrt.sizeDelta = new Vector2(-20, 35);

        // StatusText (ACTIVE)
        TextMeshProUGUI statusTxt = MakeText(card, "", 16,
            TextAlignmentOptions.Center, font);
        statusTxt.color = new Color(0.3f, 1f, 0.4f);
        statusTxt.gameObject.name = "StatusText";
        RectTransform srt = statusTxt.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -78);
        srt.sizeDelta = new Vector2(-20, 22);

        // PercentText
        TextMeshProUGUI pctTxt = MakeText(card, "0%", 20,
            TextAlignmentOptions.Center, font);
        pctTxt.color = new Color(1f, 0.85f, 0.3f);
        pctTxt.gameObject.name = "PercentText";
        RectTransform prt = pctTxt.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 1); prt.anchorMax = new Vector2(1, 1);
        prt.pivot = new Vector2(0.5f, 1);
        prt.anchoredPosition = new Vector2(0, -104);
        prt.sizeDelta = new Vector2(-20, 28);

        // Buttons
        float btnY = -150f;
        float btnH = 32f;
        float btnGap = 6f;

        MakeCardButton(card, "Btn_Select", "Select", new Color(0.2f, 0.5f, 0.9f), btnY, font);
        btnY -= btnH + btnGap;
        MakeCardButton(card, "Btn_Rename", "Rename", new Color(0.4f, 0.4f, 0.55f), btnY, font);
        btnY -= btnH + btnGap;
        MakeCardButton(card, "Btn_Reset", "Reset", new Color(0.7f, 0.35f, 0.1f), btnY, font);
        btnY -= btnH + btnGap;
        MakeCardButton(card, "Btn_Unlock All", "Unlock All", new Color(0.15f, 0.65f, 0.3f), btnY, font);
        btnY -= btnH + btnGap;
        MakeCardButton(card, "Btn_Delete", "Delete", new Color(0.75f, 0.15f, 0.15f), btnY, font);

        // RenamePanel (hidden)
        BuildRenamePanel(card, font);
    }

    private static void MakeCardButton(Transform parent, string name, string label,
        Color bgColor, float yPos, TMP_FontAsset font)
    {
        GameObject btnGO = MakeElement(name, parent);
        RectTransform brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.1f, 1);
        brt.anchorMax = new Vector2(0.9f, 1);
        brt.pivot = new Vector2(0.5f, 1);
        brt.anchoredPosition = new Vector2(0, yPos);
        brt.sizeDelta = new Vector2(0, 32);

        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor = bgColor * 0.8f;
        btn.colors = cb;

        TextMeshProUGUI txt = MakeText(btnGO.transform, label, 16,
            TextAlignmentOptions.Center, font);
        txt.color = Color.white;
        txt.raycastTarget = false;
        Stretch(txt.GetComponent<RectTransform>());
    }

    private static void BuildRenamePanel(Transform card, TMP_FontAsset font)
    {
        GameObject renamePanel = MakeElement("RenamePanel", card);
        RectTransform rrt = renamePanel.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1);
        rrt.pivot = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, -40);
        rrt.sizeDelta = new Vector2(-16, 35);

        Image bg = renamePanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 1f);

        // Input field
        GameObject inputGO = MakeElement("InputField", renamePanel.transform);
        RectTransform irt = inputGO.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(5, 2); irt.offsetMax = new Vector2(-5, -2);

        // Text area
        GameObject textArea = MakeElement("Text Area", inputGO.transform);
        Stretch(textArea.GetComponent<RectTransform>());

        // Input text
        GameObject inputTextGO = MakeElement("Text", textArea.transform);
        Stretch(inputTextGO.GetComponent<RectTransform>());
        TextMeshProUGUI inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 20;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.Center;
        if (font != null) inputText.font = font;

        // Placeholder
        GameObject placeholderGO = MakeElement("Placeholder", textArea.transform);
        Stretch(placeholderGO.GetComponent<RectTransform>());
        TextMeshProUGUI placeholder = placeholderGO.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Enter name...";
        placeholder.fontSize = 20;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        placeholder.alignment = TextAlignmentOptions.Center;
        if (font != null) placeholder.font = font;

        // TMP_InputField component
        TMP_InputField input = inputGO.AddComponent<TMP_InputField>();
        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.characterLimit = 16;
        input.contentType = TMP_InputField.ContentType.Standard;

        renamePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private static GameObject MakeElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string text, float fontSize,
        TextAlignmentOptions align, TMP_FontAsset font)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
