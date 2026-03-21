using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Profil seçim popup'ı — CollectionUI gibi açılır panel.
/// Max 3 profil kartı gösterir: isim, collection %, aksiyon butonları.
/// Kartlar runtime'da oluşturulur (AddListener persist sorunu yok).
/// Ana menüde "Profiles" butonuna bağlanır.
/// </summary>
public class ProfileUI : MonoBehaviour
{
    public static ProfileUI instance;

    [Header("Panel")]
    public GameObject panelRoot;
    public CanvasGroup panelCanvasGroup;

    [Header("Font")]
    public TMP_FontAsset profileFont;

    [Header("Ses")]
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip clickClip;

    // Runtime
    private ProfileCardUI[] cards;
    private bool isOpen = false;
    private bool isBuilt = false;

    // Rename state
    private int renamingProfileId = -1;

    void Awake()
    {
        instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (renamingProfileId >= 0)
                FinishRename(renamingProfileId);
            else
                Close();
        }

        if (renamingProfileId >= 0 && Input.GetKeyDown(KeyCode.Return))
            FinishRename(renamingProfileId);
    }

    // ═══════════════════════════════════════════
    // OPEN / CLOSE
    // ═══════════════════════════════════════════

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        panelRoot.SetActive(true);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;

        if (!isBuilt) BuildUI();
        RefreshAllCards();

        PlaySound(openClip);
        StartCoroutine(OpenAnimation());
    }

    public void Close()
    {
        if (!isOpen) return;

        if (renamingProfileId >= 0)
            FinishRename(renamingProfileId);

        isOpen = false;
        PlaySound(closeClip);
        StartCoroutine(CloseAnimation());
    }

    public void Toggle()
    {
        if (isOpen) Close(); else Open();
    }

    private IEnumerator OpenAnimation()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        panelRoot.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - t) * (1f - t);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = ease;
            panelRoot.transform.localScale = Vector3.LerpUnclamped(
                new Vector3(0.85f, 0.85f, 1f), Vector3.one, ease);
            yield return null;
        }

        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        panelRoot.transform.localScale = Vector3.one;
    }

    private IEnumerator CloseAnimation()
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        panelRoot.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // REFRESH
    // ═══════════════════════════════════════════

    private void RefreshAllCards()
    {
        if (ProfileManager.instance == null || cards == null) return;

        for (int i = 0; i < ProfileManager.MAX_PROFILES; i++)
        {
            if (cards[i] == null) continue;

            bool exists = ProfileManager.instance.ProfileExists(i);
            bool isActive = (i == ProfileManager.instance.ActiveProfileId);
            string name = ProfileManager.instance.GetProfileName(i);
            float pct = ProfileManager.instance.GetCollectionPercent(i);

            cards[i].Refresh(exists, isActive, name, pct);
        }
    }

    // ═══════════════════════════════════════════
    // CARD ACTIONS
    // ═══════════════════════════════════════════

    public void OnSelectProfile(int id)
    {
        if (ProfileManager.instance == null) return;
        PlaySound(clickClip);

        if (!ProfileManager.instance.ProfileExists(id))
            ProfileManager.instance.CreateProfile(id, $"Profile {id + 1}");

        ProfileManager.instance.SwitchProfile(id);
        RefreshAllCards();
    }

    public void OnResetProfile(int id)
    {
        if (ProfileManager.instance == null) return;
        PlaySound(clickClip);
        ProfileManager.instance.ResetProfile(id);
        RefreshAllCards();
    }

    public void OnUnlockAll(int id)
    {
        if (ProfileManager.instance == null) return;
        PlaySound(clickClip);
        ProfileManager.instance.UnlockAllForProfile(id);
        RefreshAllCards();
    }

    public void OnDeleteProfile(int id)
    {
        if (ProfileManager.instance == null) return;
        PlaySound(clickClip);
        ProfileManager.instance.DeleteProfile(id);
        RefreshAllCards();
    }

    public void OnStartRename(int id)
    {
        if (cards == null || cards[id] == null) return;
        PlaySound(clickClip);

        renamingProfileId = id;
        string currentName = ProfileManager.instance != null
            ? ProfileManager.instance.GetProfileName(id) : "";
        cards[id].ShowRenameInput(currentName);
    }

    public void FinishRename(int id)
    {
        if (id < 0 || id >= ProfileManager.MAX_PROFILES) return;
        if (cards == null || cards[id] == null) return;

        string newName = cards[id].GetRenameText();
        if (!string.IsNullOrEmpty(newName) && ProfileManager.instance != null)
            ProfileManager.instance.SetProfileName(id, newName);

        cards[id].HideRenameInput();
        renamingProfileId = -1;
        RefreshAllCards();
    }

    // ═══════════════════════════════════════════
    // RUNTIME BUILD (çağrılır ilk Open'da)
    // ═══════════════════════════════════════════

    private void BuildUI()
    {
        if (panelRoot == null) return;
        isBuilt = true;
        cards = new ProfileCardUI[ProfileManager.MAX_PROFILES];

        // Header
        GameObject headerGO = MakeElement("Header", panelRoot.transform);
        RectTransform hrt = headerGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0, -10);
        hrt.sizeDelta = new Vector2(0, 60);

        TMP_Text title = MakeText(headerGO.transform, "PROFILES", 32, TextAlignmentOptions.Center);
        title.color = Color.white;
        Stretch(title.GetComponent<RectTransform>());

        // Close button
        GameObject closeBtnGO = MakeElement("CloseButton", panelRoot.transform);
        RectTransform cbrt = closeBtnGO.GetComponent<RectTransform>();
        cbrt.anchorMin = new Vector2(1, 1);
        cbrt.anchorMax = new Vector2(1, 1);
        cbrt.pivot = new Vector2(1, 1);
        cbrt.anchoredPosition = new Vector2(-15, -15);
        cbrt.sizeDelta = new Vector2(40, 40);

        Image cbImg = closeBtnGO.AddComponent<Image>();
        cbImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        cbImg.raycastTarget = true;

        Button closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = cbImg;
        closeBtn.onClick.AddListener(Close);

        TMP_Text xText = MakeText(closeBtnGO.transform, "X", 22, TextAlignmentOptions.Center);
        xText.color = Color.white;
        xText.raycastTarget = false;
        Stretch(xText.GetComponent<RectTransform>());

        // Cards
        float cardWidth = 280f;
        float cardHeight = 320f;
        float spacing = 30f;
        float totalWidth = ProfileManager.MAX_PROFILES * cardWidth
            + (ProfileManager.MAX_PROFILES - 1) * spacing;
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < ProfileManager.MAX_PROFILES; i++)
        {
            GameObject cardGO = MakeElement($"ProfileCard_{i}", panelRoot.transform);
            RectTransform crt = cardGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), -10f);
            crt.sizeDelta = new Vector2(cardWidth, cardHeight);

            Image cardBg = cardGO.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            cardBg.raycastTarget = true;

            ProfileCardUI card = cardGO.AddComponent<ProfileCardUI>();
            card.profileId = i;
            card.Build(this, profileFont);
            cards[i] = card;
        }

        ApplyFontToAll();
    }

    private void ApplyFontToAll()
    {
        if (profileFont == null || panelRoot == null) return;
        var texts = panelRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
            t.font = profileFont;
    }

    // ═══════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(clip);
    }

    public static GameObject MakeElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static TMP_Text MakeText(Transform parent, string text, float fontSize, TextAlignmentOptions align)
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
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

/// <summary>
/// Tek profil kartı UI bileşeni — runtime'da oluşturulur.
/// </summary>
public class ProfileCardUI : MonoBehaviour
{
    public int profileId;

    public TMP_Text nameText;
    public TMP_Text percentText;
    public TMP_Text statusText;
    public TMP_InputField renameInput;
    public Button selectBtn;
    public Button resetBtn;
    public Button unlockBtn;
    public Button renameBtn;
    public Button deleteBtn;

    private GameObject renamePanel;
    private ProfileUI owner;

    public void Build(ProfileUI owner, TMP_FontAsset font)
    {
        this.owner = owner;

        // Profile number label
        TMP_Text numText = ProfileUI.MakeText(transform, $"#{profileId + 1}", 18,
            TextAlignmentOptions.TopLeft);
        numText.color = new Color(0.6f, 0.6f, 0.7f);
        RectTransform nrt = numText.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 1);
        nrt.anchorMax = new Vector2(1, 1);
        nrt.pivot = new Vector2(0, 1);
        nrt.anchoredPosition = new Vector2(12, -10);
        nrt.sizeDelta = new Vector2(-24, 25);

        // Profile name
        nameText = ProfileUI.MakeText(transform, "Empty Slot", 24, TextAlignmentOptions.Center);
        nameText.color = Color.white;
        RectTransform nmrt = nameText.GetComponent<RectTransform>();
        nmrt.anchorMin = new Vector2(0, 1);
        nmrt.anchorMax = new Vector2(1, 1);
        nmrt.pivot = new Vector2(0.5f, 1);
        nmrt.anchoredPosition = new Vector2(0, -40);
        nmrt.sizeDelta = new Vector2(-20, 35);

        // Status text (ACTIVE)
        statusText = ProfileUI.MakeText(transform, "", 16, TextAlignmentOptions.Center);
        statusText.color = new Color(0.3f, 1f, 0.4f);
        RectTransform srt = statusText.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -78);
        srt.sizeDelta = new Vector2(-20, 22);

        // Collection percent
        percentText = ProfileUI.MakeText(transform, "0%", 20, TextAlignmentOptions.Center);
        percentText.color = new Color(1f, 0.85f, 0.3f);
        RectTransform prt = percentText.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 1);
        prt.anchorMax = new Vector2(1, 1);
        prt.pivot = new Vector2(0.5f, 1);
        prt.anchoredPosition = new Vector2(0, -104);
        prt.sizeDelta = new Vector2(-20, 28);

        // Buttons
        float btnY = -150f;
        float btnH = 32f;
        float btnGap = 6f;
        int id = profileId; // capture for lambda

        selectBtn = MakeButton("Select", new Color(0.2f, 0.5f, 0.9f), btnY);
        selectBtn.onClick.AddListener(() => owner.OnSelectProfile(id));
        btnY -= btnH + btnGap;

        renameBtn = MakeButton("Rename", new Color(0.4f, 0.4f, 0.55f), btnY);
        renameBtn.onClick.AddListener(() => owner.OnStartRename(id));
        btnY -= btnH + btnGap;

        resetBtn = MakeButton("Reset", new Color(0.7f, 0.35f, 0.1f), btnY);
        resetBtn.onClick.AddListener(() => owner.OnResetProfile(id));
        btnY -= btnH + btnGap;

        unlockBtn = MakeButton("Unlock All", new Color(0.15f, 0.65f, 0.3f), btnY);
        unlockBtn.onClick.AddListener(() => owner.OnUnlockAll(id));
        btnY -= btnH + btnGap;

        deleteBtn = MakeButton("Delete", new Color(0.75f, 0.15f, 0.15f), btnY);
        deleteBtn.onClick.AddListener(() => owner.OnDeleteProfile(id));

        // Rename input (hidden)
        BuildRenamePanel();

        if (font != null)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
                t.font = font;
        }
    }

    private Button MakeButton(string label, Color bgColor, float yPos)
    {
        GameObject btnGO = ProfileUI.MakeElement($"Btn_{label}", transform);
        RectTransform brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.1f, 1);
        brt.anchorMax = new Vector2(0.9f, 1);
        brt.pivot = new Vector2(0.5f, 1);
        brt.anchoredPosition = new Vector2(0, yPos);
        brt.sizeDelta = new Vector2(0, 32);

        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor = bgColor * 0.8f;
        btn.colors = cb;

        TMP_Text txt = ProfileUI.MakeText(btnGO.transform, label, 16, TextAlignmentOptions.Center);
        txt.color = Color.white;
        txt.raycastTarget = false;
        RectTransform trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return btn;
    }

    private void BuildRenamePanel()
    {
        renamePanel = ProfileUI.MakeElement("RenamePanel", transform);
        RectTransform rrt = renamePanel.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 1);
        rrt.anchorMax = new Vector2(1, 1);
        rrt.pivot = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, -40);
        rrt.sizeDelta = new Vector2(-16, 35);

        Image bg = renamePanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 1f);

        // Input field
        GameObject inputGO = ProfileUI.MakeElement("InputField", renamePanel.transform);
        RectTransform irt = inputGO.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(5, 2);
        irt.offsetMax = new Vector2(-5, -2);

        // Text area
        GameObject textArea = ProfileUI.MakeElement("Text Area", inputGO.transform);
        RectTransform tart = textArea.GetComponent<RectTransform>();
        tart.anchorMin = Vector2.zero;
        tart.anchorMax = Vector2.one;
        tart.offsetMin = Vector2.zero;
        tart.offsetMax = Vector2.zero;

        // Input text
        GameObject inputTextGO = new GameObject("Text", typeof(RectTransform));
        inputTextGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 20;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.Center;
        RectTransform itrt = inputTextGO.GetComponent<RectTransform>();
        itrt.anchorMin = Vector2.zero;
        itrt.anchorMax = Vector2.one;
        itrt.offsetMin = Vector2.zero;
        itrt.offsetMax = Vector2.zero;

        // Placeholder
        GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI placeholder = placeholderGO.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Enter name...";
        placeholder.fontSize = 20;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        placeholder.alignment = TextAlignmentOptions.Center;
        RectTransform phrt = placeholderGO.GetComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero;
        phrt.anchorMax = Vector2.one;
        phrt.offsetMin = Vector2.zero;
        phrt.offsetMax = Vector2.zero;

        renameInput = inputGO.AddComponent<TMP_InputField>();
        renameInput.textViewport = tart;
        renameInput.textComponent = inputText;
        renameInput.placeholder = placeholder;
        renameInput.characterLimit = 16;
        renameInput.contentType = TMP_InputField.ContentType.Standard;
        int id = profileId;
        renameInput.onSubmit.AddListener((_) => owner.FinishRename(id));

        renamePanel.SetActive(false);
    }

    public void Refresh(bool exists, bool isActive, string profileName, float collectionPercent)
    {
        if (exists)
        {
            nameText.text = string.IsNullOrEmpty(profileName) ? $"Profile {profileId + 1}" : profileName;
            nameText.gameObject.SetActive(true);
            percentText.text = $"Collection: {collectionPercent:F1}%";
            percentText.gameObject.SetActive(true);
            statusText.text = isActive ? "ACTIVE" : "";
            statusText.gameObject.SetActive(true);

            selectBtn.gameObject.SetActive(!isActive);
            renameBtn.gameObject.SetActive(true);
            resetBtn.gameObject.SetActive(true);
            unlockBtn.gameObject.SetActive(isActive);
            deleteBtn.gameObject.SetActive(!isActive);

            var btnText = selectBtn.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "Select";

            Image bg = GetComponent<Image>();
            if (bg != null)
                bg.color = isActive
                    ? new Color(0.15f, 0.18f, 0.28f, 0.95f)
                    : new Color(0.12f, 0.12f, 0.18f, 0.95f);
        }
        else
        {
            nameText.text = "Empty Slot";
            nameText.gameObject.SetActive(true);
            percentText.text = "";
            percentText.gameObject.SetActive(false);
            statusText.text = "";
            statusText.gameObject.SetActive(false);

            selectBtn.gameObject.SetActive(true);
            var btnText = selectBtn.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "Create";

            renameBtn.gameObject.SetActive(false);
            resetBtn.gameObject.SetActive(false);
            unlockBtn.gameObject.SetActive(false);
            deleteBtn.gameObject.SetActive(false);

            Image bg = GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0.08f, 0.08f, 0.12f, 0.8f);
        }
    }

    public void ShowRenameInput(string currentName)
    {
        renamePanel.SetActive(true);
        nameText.gameObject.SetActive(false);
        renameInput.text = currentName;
        renameInput.Select();
        renameInput.ActivateInputField();
    }

    public void HideRenameInput()
    {
        renamePanel.SetActive(false);
        nameText.gameObject.SetActive(true);
    }

    public string GetRenameText()
    {
        return renameInput != null ? renameInput.text.Trim() : "";
    }
}
