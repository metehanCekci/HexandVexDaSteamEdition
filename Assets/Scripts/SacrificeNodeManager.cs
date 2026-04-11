using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class SacrificeNodeManager : MonoBehaviour
{
    public static SacrificeNodeManager instance;

    // ─── UI References ───
    [Header("UI References")]
    public GameObject panel;
    public CanvasGroup canvasGroup;
    public TMP_Text titleText;
    public TMP_Text statusText;
    public TMP_Text tubeCountText;

    [Header("Tube")]
    public RectTransform tubeArea;
    public Transform perkGridParent;
    public GameObject addButton;
    public Image tubeImage;

    [Header("Lever")]
    public RectTransform leverHandle;
    public Image leverHandleImage;
    public Button leverButton;
    public TMP_Text leverText;

    [Header("Reward Slots")]
    public SacrificeRewardSlot rareSlot;
    public SacrificeRewardSlot epicSlot;
    public SacrificeRewardSlot legendarySlot;

    [Header("Acid Pool")]
    public Image acidPoolImage;

    [Header("Leave")]
    public Button leaveButton;

    // ─── Runtime State ───
    private List<BasePerk> tubePerks = new List<BasePerk>();
    private List<GameObject> tubePerkIcons = new List<GameObject>();
    private bool isAnimating;

    // ─── Persistent Rewards (run-wide) ───
    private GameObject persistentRarePerk;
    private GameObject persistentEpicPerk;
    private GameObject persistentLegendaryPerk;
    private GameObject persistentSecretPerk;
    private bool poolGenerated;

    // ─── Popup ───
    private GameObject popupPanel;
    private Transform popupGrid;

    // ─── Font cache ───
    private TMP_FontAsset cachedFont;

    // ═══════════════════════════════════════════
    // SINGLETON & LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ═══════════════════════════════════════════
    // SHOW / HIDE
    // ═══════════════════════════════════════════

    public void Show()
    {
        if (panel == null) BuildFromCode();
        if (!poolGenerated) GeneratePool();

        // Return any leftover tube perks from previous visit
        ReturnAllTubePerks();

        panel.SetActive(true);
        Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);

        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Show();

        RefreshUI();
        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        ReturnAllTubePerks();
        if (popupPanel != null) popupPanel.SetActive(false);
        if (panel != null)
        {
            panel.SetActive(false);
            Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    // ═══════════════════════════════════════════
    // POOL GENERATION
    // ═══════════════════════════════════════════

    private void GeneratePool()
    {
        var lum = LevelUpManager.instance;
        if (lum == null) return;
        persistentRarePerk = PickRandom(lum.rarePerks);
        persistentEpicPerk = PickRandom(lum.epicPerks);
        persistentLegendaryPerk = PickRandom(lum.legendaryPerks);
        persistentSecretPerk = FindSecretPerk();
        poolGenerated = true;
    }

    private void RerollRewards()
    {
        var lum = LevelUpManager.instance;
        if (lum == null) return;
        persistentRarePerk = PickRandom(lum.rarePerks);
        persistentEpicPerk = PickRandom(lum.epicPerks);
        persistentLegendaryPerk = PickRandom(lum.legendaryPerks);
        persistentSecretPerk = FindSecretPerk();
    }

    private GameObject FindSecretPerk()
    {
        var orbs = Resources.FindObjectsOfTypeAll<SecretPerkOrb>();
        if (orbs.Length > 0 && orbs[0].secretPerkPool != null && orbs[0].secretPerkPool.Count > 0)
            return PickRandom(orbs[0].secretPerkPool);
        if (LevelUpManager.instance != null)
            return PickRandom(LevelUpManager.instance.legendaryPerks);
        return null;
    }

    private GameObject PickRandom(List<GameObject> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    public void ResetForNewRun()
    {
        poolGenerated = false;
        persistentRarePerk = null;
        persistentEpicPerk = null;
        persistentLegendaryPerk = null;
        persistentSecretPerk = null;
        ReturnAllTubePerks();
    }

    // ═══════════════════════════════════════════
    // TUBE MANAGEMENT
    // ═══════════════════════════════════════════

    public void AddPerkToTube(BasePerk perk)
    {
        if (isAnimating || perk == null) return;
        if (tubePerks.Contains(perk)) return;
        if (tubePerks.Count >= 10) return;

        var rm = RunManager.instance;
        if (rm.activePerks.Contains(perk))
        {
            perk.OnUnequip();
            rm.activePerks.Remove(perk);
        }
        else
        {
            rm.inventoryPerks.Remove(perk);
        }

        tubePerks.Add(perk);
        CreateTubeIcon(perk);

        rm.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
        RefreshUI();
    }

    public void RemovePerkFromTube(BasePerk perk)
    {
        if (isAnimating || perk == null) return;
        int index = tubePerks.IndexOf(perk);
        if (index < 0) return;

        tubePerks.RemoveAt(index);
        RunManager.instance.inventoryPerks.Add(perk);

        if (index < tubePerkIcons.Count)
        {
            Destroy(tubePerkIcons[index]);
            tubePerkIcons.RemoveAt(index);
        }

        RunManager.instance.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
        RefreshUI();
    }

    private void ReturnAllTubePerks()
    {
        if (RunManager.instance == null) return;
        for (int i = tubePerks.Count - 1; i >= 0; i--)
        {
            if (tubePerks[i] != null)
                RunManager.instance.inventoryPerks.Add(tubePerks[i]);
        }
        tubePerks.Clear();
        ClearTubeIcons();

        RunManager.instance.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
    }

    private void CreateTubeIcon(BasePerk perk)
    {
        if (perkGridParent == null) return;

        GameObject iconGO = new GameObject("TubePerk", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(perkGridParent, false);
        iconGO.transform.SetSiblingIndex(tubePerkIcons.Count);
        iconGO.layer = 5;

        Image img = iconGO.GetComponent<Image>();
        if (perk.icon != null) img.sprite = perk.icon;
        img.preserveAspect = true;

        // Rarity border (behind icon via SetAsFirstSibling)
        GameObject borderGO = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGO.transform.SetParent(iconGO.transform, false);
        borderGO.layer = 5;
        RectTransform brt = borderGO.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-3, -3);
        brt.offsetMax = new Vector2(3, 3);
        brt.SetAsFirstSibling();
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.color = SacrificeRewardSlot.GetRarityColor(perk.rarity);
        borderImg.raycastTarget = false;

        // Click to remove from tube
        Button btn = iconGO.AddComponent<Button>();
        BasePerk captured = perk;
        btn.onClick.AddListener(() => RemovePerkFromTube(captured));

        tubePerkIcons.Add(iconGO);
        StartCoroutine(DropAnimation(iconGO));
    }

    private void ClearTubeIcons()
    {
        foreach (var go in tubePerkIcons)
            if (go != null) Destroy(go);
        tubePerkIcons.Clear();
    }

    private IEnumerator DropAnimation(GameObject icon)
    {
        if (icon == null) yield break;
        RectTransform rt = icon.GetComponent<RectTransform>();
        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = target + new Vector2(0, 80);
        float t = 0f;
        while (t < 0.25f)
        {
            if (rt == null) yield break;
            t += Time.unscaledDeltaTime;
            float ease = 1f - (1f - Mathf.Clamp01(t / 0.25f)) * (1f - Mathf.Clamp01(t / 0.25f));
            rt.anchoredPosition = Vector2.Lerp(target + new Vector2(0, 80), target, ease);
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = target;
    }

    // ═══════════════════════════════════════════
    // LEVER LOGIC
    // ═══════════════════════════════════════════

    private bool CanPullLever()
    {
        int c = tubePerks.Count;
        return !isAnimating && (c == 1 || c == 2 || c == 4 || c == 6 || c == 10);
    }

    private void OnLeverClicked()
    {
        if (!CanPullLever()) return;
        StartCoroutine(LeverSequence());
    }

    private IEnumerator LeverSequence()
    {
        isAnimating = true;
        int count = tubePerks.Count;

        yield return StartCoroutine(AnimateLeverPull());
        yield return StartCoroutine(AcidDissolveAnimation());

        if (count == 1)
        {
            // ─── Reroll ───
            ConsumeTubePerks();
            RerollRewards();
            RefreshUI();
            if (statusText != null) statusText.text = "REWARDS REROLLED!";
            isAnimating = false;
            yield return new WaitForSecondsRealtime(1.2f);
            if (statusText != null) statusText.text = "DRAG PERKS INTO THE TUBE";
        }
        else
        {
            // ─── Grant reward ───
            GameObject rewardPrefab = count switch
            {
                2 => persistentRarePerk,
                4 => persistentEpicPerk,
                6 => persistentLegendaryPerk,
                10 => persistentSecretPerk,
                _ => null
            };

            ConsumeTubePerks();

            if (rewardPrefab != null)
            {
                BasePerk bp = rewardPrefab.GetComponent<BasePerk>();
                if (statusText != null)
                    statusText.text = $"ACQUIRED: {(bp != null ? bp.perkName.ToUpper() : "???")}";
                RunManager.instance.AddPerk(rewardPrefab);
            }

            yield return new WaitForSecondsRealtime(1.5f);
            isAnimating = false;

            Hide();
            if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.Hide();
            if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
        }
    }

    private void ConsumeTubePerks()
    {
        foreach (var perk in tubePerks)
            if (perk != null) Destroy(perk.gameObject);
        tubePerks.Clear();
        ClearTubeIcons();
    }

    // ═══════════════════════════════════════════
    // ANIMATIONS
    // ═══════════════════════════════════════════

    private IEnumerator AnimateLeverPull()
    {
        if (leverHandle == null) yield break;
        Vector2 startPos = leverHandle.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, -120);

        // Pull down
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            leverHandle.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.Clamp01(t / 0.3f));
            yield return null;
        }
        leverHandle.anchoredPosition = endPos;

        yield return new WaitForSecondsRealtime(0.15f);

        // Return up
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            leverHandle.anchoredPosition = Vector2.Lerp(endPos, startPos, Mathf.Clamp01(t / 0.2f));
            yield return null;
        }
        leverHandle.anchoredPosition = startPos;
    }

    private IEnumerator AcidDissolveAnimation()
    {
        // Gather tube icon transforms
        List<RectTransform> rts = new List<RectTransform>();
        List<CanvasGroup> cgs = new List<CanvasGroup>();
        foreach (var go in tubePerkIcons)
        {
            if (go == null) continue;
            rts.Add(go.GetComponent<RectTransform>());
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cgs.Add(cg);
        }

        Color acidOriginal = acidPoolImage != null ? acidPoolImage.color : Color.green;
        Color acidBright = new Color(0.2f, 1f, 0.2f, 0.85f);

        float duration = 0.8f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            // Slide icons down
            foreach (var rt in rts)
                if (rt != null)
                    rt.anchoredPosition += new Vector2(0, -200f * Time.unscaledDeltaTime);

            // Fade + shrink
            foreach (var cg in cgs)
            {
                if (cg == null) continue;
                cg.alpha = 1f - p;
                cg.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, p);
            }

            // Acid glow pulse
            if (acidPoolImage != null)
                acidPoolImage.color = Color.Lerp(acidOriginal, acidBright, Mathf.PingPong(t * 3f, 1f));

            yield return null;
        }

        if (acidPoolImage != null) acidPoolImage.color = acidOriginal;
    }

    // ═══════════════════════════════════════════
    // PERK SELECTION POPUP (for "+" button)
    // ═══════════════════════════════════════════

    private void ShowPerkPopup()
    {
        if (isAnimating) return;
        if (tubePerks.Count >= 6) return;

        if (popupPanel == null) BuildPopup();

        // Clear old entries
        foreach (Transform child in popupGrid)
            Destroy(child.gameObject);

        // Gather available perks (not already in tube)
        var rm = RunManager.instance;
        List<BasePerk> available = new List<BasePerk>();
        available.AddRange(rm.activePerks);
        available.AddRange(rm.inventoryPerks);
        available.RemoveAll(p => tubePerks.Contains(p));

        if (available.Count == 0)
        {
            HidePerkPopup();
            return;
        }

        foreach (var perk in available)
            CreatePopupEntry(perk);

        popupPanel.SetActive(true);
    }

    private void HidePerkPopup()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private void CreatePopupEntry(BasePerk perk)
    {
        GameObject go = new GameObject("PopupPerk", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(popupGrid, false);
        go.layer = 5;
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 50f;
        le.minHeight = 50f;

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        iconGO.layer = 5;
        RectTransform irt = iconGO.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0, 0.1f);
        irt.anchorMax = new Vector2(0, 0.9f);
        irt.anchoredPosition = new Vector2(28, 0);
        irt.sizeDelta = new Vector2(38, 0);
        Image iconImg = iconGO.GetComponent<Image>();
        if (perk.icon != null) iconImg.sprite = perk.icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Name text
        GameObject nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(go.transform, false);
        nameGO.layer = 5;
        TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text = perk.perkName;
        nameTxt.fontSize = 16;
        nameTxt.color = SacrificeRewardSlot.GetRarityColor(perk.rarity);
        nameTxt.alignment = TextAlignmentOptions.Left;
        nameTxt.raycastTarget = false;
        if (cachedFont != null) nameTxt.font = cachedFont;
        RectTransform nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero;
        nrt.anchorMax = Vector2.one;
        nrt.offsetMin = new Vector2(55, 5);
        nrt.offsetMax = new Vector2(-10, -5);

        // Button
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.22f, 0.22f, 0.35f, 1f);
        btn.colors = cb;
        BasePerk captured = perk;
        btn.onClick.AddListener(() =>
        {
            AddPerkToTube(captured);
            HidePerkPopup();
        });
    }

    // ═══════════════════════════════════════════
    // LEAVE
    // ═══════════════════════════════════════════

    private void OnLeaveClicked()
    {
        if (isAnimating) return;
        ReturnAllTubePerks();
        Hide();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.Hide();
        if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
    }

    // ═══════════════════════════════════════════
    // UI REFRESH
    // ═══════════════════════════════════════════

    private void RefreshUI()
    {
        int count = tubePerks.Count;

        // Tube count
        if (tubeCountText != null)
            tubeCountText.text = $"{count}/10";

        // "+" button (hide at 6+)
        if (addButton != null)
        {
            addButton.SetActive(count < 6);
            addButton.transform.SetAsLastSibling();
        }

        // Lever state
        bool canPull = CanPullLever();
        if (leverButton != null) leverButton.interactable = canPull;
        if (leverHandleImage != null)
            leverHandleImage.color = canPull ? new Color(0.9f, 0.95f, 1f) : new Color(0.25f, 0.25f, 0.25f);
        if (leverText != null)
        {
            leverText.text = count == 1 ? "REROLL" : (canPull ? "PULL" : "---");
            leverText.color = canPull ? Color.white : new Color(0.35f, 0.35f, 0.35f);
        }

        // Reward slots
        if (rareSlot != null)
        {
            rareSlot.Setup(PerkRarity.Rare, persistentRarePerk, 2);
            rareSlot.SetHighlighted(count == 2);
        }
        if (epicSlot != null)
        {
            epicSlot.Setup(PerkRarity.Epic, persistentEpicPerk, 4);
            epicSlot.SetHighlighted(count == 4);
        }
        if (legendarySlot != null)
        {
            legendarySlot.Setup(PerkRarity.Legendary, persistentLegendaryPerk, 6);
            legendarySlot.SetHighlighted(count == 6);
        }

        // Status text
        if (statusText != null)
        {
            if (count == 0) statusText.text = "DRAG PERKS INTO THE TUBE";
            else if (count == 1) statusText.text = "PULL LEVER TO REROLL REWARDS";
            else if (count == 2) statusText.text = "PULL LEVER FOR RARE PERK";
            else if (count == 3) statusText.text = "NEED 1 MORE FOR EPIC (4)";
            else if (count == 4) statusText.text = "PULL LEVER FOR EPIC PERK";
            else if (count == 5) statusText.text = "NEED 1 MORE FOR LEGENDARY (6)";
            else if (count == 6) statusText.text = "PULL LEVER FOR LEGENDARY PERK";
            else if (count >= 7 && count <= 9) statusText.text = $"NEED {10 - count} MORE FOR ???";
            else if (count == 10) statusText.text = "PULL LEVER FOR ??? PERK";
        }
    }

    // ═══════════════════════════════════════════
    // BUILD UI FROM CODE (runtime fallback)
    // ═══════════════════════════════════════════

    private void BuildFromCode()
    {
        cachedFont = Resources.Load<TMP_FontAsset>("alagard SDF");

        // ─── Canvas ───
        GameObject canvasGO = new GameObject("SacrificeCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // ─── Panel (full screen dark bg) ───
        panel = MakeUI("SacrificePanel", canvasGO.transform);
        Stretch(panel);
        Image panelBG = panel.AddComponent<Image>();
        panelBG.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        canvasGroup = panel.AddComponent<CanvasGroup>();

        // ─── Title ───
        titleText = MakeText("Title", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-50, -15), new Vector2(500, 55),
            "SACRIFICE MACHINE", 36, new Color(0.8f, 0.2f, 0.6f), TextAlignmentOptions.Center);

        // ─── Machine Body ───
        GameObject machineGO = MakeUI("MachineBody", panel.transform);
        RectTransform machineRT = machineGO.GetComponent<RectTransform>();
        SetAnchored(machineRT, new Vector2(0.5f, 0.5f), new Vector2(-50, 20), new Vector2(420, 520));
        Image machineBG = machineGO.AddComponent<Image>();
        machineBG.color = new Color(0.12f, 0.12f, 0.15f, 1f);

        // Machine header stripe
        GameObject headerGO = MakeUI("Header", machineGO.transform);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0, 35);
        Image headerBG = headerGO.AddComponent<Image>();
        headerBG.color = new Color(0.08f, 0.08f, 0.1f, 1f);

        // ─── Tube (glass area) ───
        GameObject tubeOuterGO = MakeUI("TubeOuter", machineGO.transform);
        RectTransform tubeOuterRT = tubeOuterGO.GetComponent<RectTransform>();
        tubeOuterRT.anchorMin = new Vector2(0.06f, 0.22f);
        tubeOuterRT.anchorMax = new Vector2(0.94f, 0.93f);
        tubeOuterRT.offsetMin = Vector2.zero;
        tubeOuterRT.offsetMax = Vector2.zero;
        Image tubeBorder = tubeOuterGO.AddComponent<Image>();
        tubeBorder.color = new Color(0.2f, 0.55f, 0.3f, 0.45f);

        GameObject tubeInnerGO = MakeUI("TubeInner", tubeOuterGO.transform);
        Stretch(tubeInnerGO);
        tubeInnerGO.GetComponent<RectTransform>().offsetMin = new Vector2(3, 3);
        tubeInnerGO.GetComponent<RectTransform>().offsetMax = new Vector2(-3, -3);
        tubeImage = tubeInnerGO.AddComponent<Image>();
        tubeImage.color = new Color(0.12f, 0.35f, 0.18f, 0.1f);
        tubeArea = tubeInnerGO.GetComponent<RectTransform>();

        // Drop zone component
        SacrificeTubeDropZone dropZone = tubeInnerGO.AddComponent<SacrificeTubeDropZone>();
        dropZone.highlightImage = tubeImage;

        // Perk grid inside tube
        GameObject gridGO = MakeUI("PerkGrid", tubeInnerGO.transform);
        Stretch(gridGO);
        gridGO.GetComponent<RectTransform>().offsetMin = new Vector2(10, 10);
        gridGO.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -30);
        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(55, 55);
        grid.spacing = new Vector2(10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        perkGridParent = gridGO.transform;

        // "+" button (child of grid — participates in layout)
        addButton = MakeUI("AddButton", gridGO.transform);
        Image addBG = addButton.AddComponent<Image>();
        addBG.color = new Color(0.18f, 0.55f, 0.25f, 0.6f);
        Button addBtn = addButton.AddComponent<Button>();
        ColorBlock addCB = addBtn.colors;
        addCB.highlightedColor = new Color(0.25f, 0.75f, 0.35f, 0.85f);
        addBtn.colors = addCB;
        addBtn.onClick.AddListener(ShowPerkPopup);

        TMP_Text plusTxt = MakeTextChild("PlusText", addButton.transform, "+", 30, Color.white, TextAlignmentOptions.Center);

        // Tube count text (below tube)
        tubeCountText = MakeText("CountText", tubeOuterGO.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),
            new Vector2(0, -4), new Vector2(0, 22),
            "0/10", 13, new Color(0.5f, 0.8f, 0.5f, 0.6f), TextAlignmentOptions.Center);

        // ─── Acid Pool ───
        GameObject acidGO = MakeUI("AcidPool", machineGO.transform);
        RectTransform acidRT = acidGO.GetComponent<RectTransform>();
        acidRT.anchorMin = new Vector2(0.06f, 0.08f);
        acidRT.anchorMax = new Vector2(0.94f, 0.22f);
        acidRT.offsetMin = Vector2.zero;
        acidRT.offsetMax = Vector2.zero;
        acidPoolImage = acidGO.AddComponent<Image>();
        acidPoolImage.color = new Color(0.08f, 0.5f, 0.08f, 0.55f);

        MakeTextChild("AcidLabel", acidGO.transform, "~ ACID ~", 13,
            new Color(0.3f, 1f, 0.3f, 0.35f), TextAlignmentOptions.Center);

        // ─── Lever ───
        BuildLever(panel.transform);

        // ─── Reward Slots ───
        BuildRewardSlots(panel.transform);

        // ─── Status Text ───
        statusText = MakeText("StatusText", panel.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-50, 55), new Vector2(550, 35),
            "", 18, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);

        // ─── Leave Button ───
        BuildLeaveButton(panel.transform);

        panel.SetActive(false);
    }

    // ─── Lever build ───
    private void BuildLever(Transform parent)
    {
        GameObject leverGO = MakeUI("Lever", parent);
        RectTransform leverRT = leverGO.GetComponent<RectTransform>();
        SetAnchored(leverRT, new Vector2(0.5f, 0.5f), new Vector2(-330, 20), new Vector2(80, 280));

        // Base background
        Image baseBG = leverGO.AddComponent<Image>();
        baseBG.color = new Color(0.09f, 0.09f, 0.11f, 0.85f);

        // Shaft
        GameObject shaftGO = MakeUI("Shaft", leverGO.transform);
        RectTransform shaftRT = shaftGO.GetComponent<RectTransform>();
        shaftRT.anchorMin = new Vector2(0.5f, 0.15f);
        shaftRT.anchorMax = new Vector2(0.5f, 0.8f);
        shaftRT.sizeDelta = new Vector2(8, 0);
        Image shaftImg = shaftGO.AddComponent<Image>();
        shaftImg.color = new Color(0.45f, 0.45f, 0.5f);
        shaftImg.raycastTarget = false;

        // Handle (top of shaft)
        GameObject handleGO = MakeUI("Handle", leverGO.transform);
        leverHandle = handleGO.GetComponent<RectTransform>();
        leverHandle.anchorMin = new Vector2(0.5f, 0.8f);
        leverHandle.anchorMax = new Vector2(0.5f, 0.8f);
        leverHandle.sizeDelta = new Vector2(36, 36);
        leverHandleImage = handleGO.AddComponent<Image>();
        leverHandleImage.color = Color.white;

        // Lever button (whole area clickable)
        leverButton = leverGO.AddComponent<Button>();
        leverButton.targetGraphic = baseBG;
        leverButton.onClick.AddListener(OnLeverClicked);

        // "PULL" text
        leverText = MakeText("PullText", leverGO.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),
            new Vector2(0, -5), new Vector2(0, 28),
            "PULL", 15, Color.white, TextAlignmentOptions.Center);
    }

    // ─── Reward slots build ───
    private void BuildRewardSlots(Transform parent)
    {
        GameObject rowGO = MakeUI("RewardRow", parent);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        SetAnchored(rowRT, new Vector2(0.5f, 0.5f), new Vector2(-50, -290), new Vector2(480, 160));

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        rareSlot = CreateRewardSlotUI("RareSlot", rowGO.transform, PerkRarity.Rare, 2);
        epicSlot = CreateRewardSlotUI("EpicSlot", rowGO.transform, PerkRarity.Epic, 4);
        legendarySlot = CreateRewardSlotUI("LegendarySlot", rowGO.transform, PerkRarity.Legendary, 6);
    }

    private SacrificeRewardSlot CreateRewardSlotUI(string name, Transform parent, PerkRarity rarity, int cost)
    {
        Color rc = SacrificeRewardSlot.GetRarityColor(rarity);

        GameObject slotGO = MakeUI(name, parent);
        Image bg = slotGO.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);

        // Glow border
        GameObject glowGO = MakeUI("Glow", slotGO.transform);
        Stretch(glowGO);
        glowGO.GetComponent<RectTransform>().offsetMin = new Vector2(-3, -3);
        glowGO.GetComponent<RectTransform>().offsetMax = new Vector2(3, 3);
        glowGO.transform.SetAsFirstSibling();
        Image glowImg = glowGO.AddComponent<Image>();
        glowImg.color = new Color(0.25f, 0.25f, 0.25f, 0.25f);
        glowImg.raycastTarget = false;

        // Rarity label (top)
        GameObject rarGO = MakeUI("Rarity", slotGO.transform);
        RectTransform rarRT = rarGO.GetComponent<RectTransform>();
        rarRT.anchorMin = new Vector2(0, 0.85f);
        rarRT.anchorMax = new Vector2(1, 1f);
        rarRT.offsetMin = new Vector2(4, 0);
        rarRT.offsetMax = new Vector2(-4, 0);
        TMP_Text rarTxt = rarGO.AddComponent<TextMeshProUGUI>();
        rarTxt.text = rarity.ToString().ToUpper();
        rarTxt.fontSize = 12;
        rarTxt.alignment = TextAlignmentOptions.Center;
        rarTxt.color = rc;
        rarTxt.raycastTarget = false;
        if (cachedFont != null) rarTxt.font = cachedFont;

        // Icon
        GameObject iconGO = MakeUI("Icon", slotGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.48f);
        iconRT.anchorMax = new Vector2(0.5f, 0.48f);
        iconRT.sizeDelta = new Vector2(50, 50);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Name
        GameObject nameGO = MakeUI("Name", slotGO.transform);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.2f);
        nameRT.anchorMax = new Vector2(1, 0.4f);
        nameRT.offsetMin = new Vector2(4, 0);
        nameRT.offsetMax = new Vector2(-4, 0);
        TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text = "???";
        nameTxt.fontSize = 13;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;
        nameTxt.raycastTarget = false;
        if (cachedFont != null) nameTxt.font = cachedFont;

        // Cost
        GameObject costGO = MakeUI("Cost", slotGO.transform);
        RectTransform costRT = costGO.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0, 0.02f);
        costRT.anchorMax = new Vector2(1, 0.18f);
        costRT.offsetMin = new Vector2(4, 0);
        costRT.offsetMax = new Vector2(-4, 0);
        TMP_Text costTxt = costGO.AddComponent<TextMeshProUGUI>();
        costTxt.text = $"{cost} PERKS";
        costTxt.fontSize = 11;
        costTxt.alignment = TextAlignmentOptions.Center;
        costTxt.color = new Color(0.6f, 0.6f, 0.6f);
        costTxt.raycastTarget = false;
        if (cachedFont != null) costTxt.font = cachedFont;

        // Attach component
        SacrificeRewardSlot slot = slotGO.AddComponent<SacrificeRewardSlot>();
        slot.background = bg;
        slot.iconImage = iconImg;
        slot.glowBorder = glowImg;
        slot.nameText = nameTxt;
        slot.costText = costTxt;
        slot.rarityLabel = rarTxt;

        return slot;
    }

    // ─── Leave button build ───
    private void BuildLeaveButton(Transform parent)
    {
        GameObject btnGO = MakeUI("LeaveButton", parent);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        SetAnchored(btnRT, new Vector2(0.5f, 0f), new Vector2(-50, 15), new Vector2(170, 42));
        btnRT.pivot = new Vector2(0.5f, 0);

        Image btnBG = btnGO.AddComponent<Image>();
        btnBG.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);

        leaveButton = btnGO.AddComponent<Button>();
        ColorBlock lcb = leaveButton.colors;
        lcb.highlightedColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        leaveButton.colors = lcb;
        leaveButton.onClick.AddListener(OnLeaveClicked);

        MakeTextChild("Text", btnGO.transform, "LEAVE", 20, Color.white, TextAlignmentOptions.Center);
    }

    // ─── Popup build ───
    private void BuildPopup()
    {
        popupPanel = MakeUI("PerkPopup", panel.transform);
        Stretch(popupPanel);
        Image popBG = popupPanel.AddComponent<Image>();
        popBG.color = new Color(0, 0, 0, 0.75f);

        Button closeBgBtn = popupPanel.AddComponent<Button>();
        closeBgBtn.onClick.AddListener(HidePerkPopup);

        // Content box
        GameObject contentGO = MakeUI("Content", popupPanel.transform);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.28f, 0.12f);
        contentRT.anchorMax = new Vector2(0.72f, 0.88f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        Image contentBG = contentGO.AddComponent<Image>();
        contentBG.color = new Color(0.07f, 0.07f, 0.1f, 0.97f);

        // Stop click-through to close button
        contentGO.AddComponent<Button>(); // Catches clicks so they don't propagate

        // Popup title
        MakeText("PopupTitle", contentGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            Vector2.zero, new Vector2(0, 50),
            "SELECT A PERK", 24, new Color(0.8f, 0.2f, 0.6f), TextAlignmentOptions.Center);

        // Scroll viewport
        GameObject scrollGO = MakeUI("Scroll", contentGO.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(10, 10);
        scrollRT.offsetMax = new Vector2(-10, -55);

        // Grid (vertical list)
        GameObject gridGO = MakeUI("Grid", scrollGO.transform);
        Stretch(gridGO);
        VerticalLayoutGroup vlg = gridGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = gridGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        popupGrid = gridGO.transform;

        popupPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // UI HELPERS
    // ═══════════════════════════════════════════

    private static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }

    private static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private TMP_Text MakeText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = align;
        txt.raycastTarget = false;
        if (cachedFont != null) txt.font = cachedFont;
        return txt;
    }

    private TMP_Text MakeTextChild(string name, Transform parent,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = MakeUI(name, parent);
        Stretch(go);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = align;
        txt.raycastTarget = false;
        if (cachedFont != null) txt.font = cachedFont;
        return txt;
    }
}
