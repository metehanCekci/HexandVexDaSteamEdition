using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class SacrificeNodeManager : MonoBehaviour
{
    public static SacrificeNodeManager instance;

    [Header("UI References")]
    public GameObject panel;
    public CanvasGroup canvasGroup;
    public TMP_Text titleText;
    public TMP_Text statusText;

    [Header("Tube")]
    public RectTransform tubeArea;
    public Transform perkGridParent;
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

    // Runtime
    private List<BasePerk> tubePerks = new List<BasePerk>();
    private List<GameObject> tubePerkIcons = new List<GameObject>();
    private bool isAnimating;

    // Persistent rewards (run-wide)
    private GameObject persistentRarePerk;
    private GameObject persistentEpicPerk;
    private GameObject persistentLegendaryPerk;
    private GameObject persistentSecretPerk;
    private bool poolGenerated;

    // Cached perk lists (because LevelUpManager doesn't persist across scenes)
    private List<GameObject> cachedRarePerks;
    private List<GameObject> cachedEpicPerks;
    private List<GameObject> cachedLegendaryPerks;
    private List<GameObject> cachedSecretPerks;

    // Font
    private TMP_FontAsset cachedFont;

    // ═══════════════════════════════════════════
    // SINGLETON
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        // Try to cache perk lists immediately
        CachePerkLists();
    }

    void Start()
    {
        // Retry caching in Start (all Awakes have run by now)
        if (cachedRarePerks == null || cachedRarePerks.Count == 0)
            CachePerkLists();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ═══════════════════════════════════════════
    // SHOW / HIDE
    // ═══════════════════════════════════════════

    public void Show()
    {
        if (panel == null) BuildFromCode();

        ReturnAllTubePerks();

        panel.SetActive(true);
        Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);

        // Re-wire buttons every Show
        if (leverButton != null) { leverButton.onClick.RemoveAllListeners(); leverButton.onClick.AddListener(OnLeverClicked); }
        if (leaveButton != null) { leaveButton.onClick.RemoveAllListeners(); leaveButton.onClick.AddListener(OnLeaveClicked); }

        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.Show();

        // Generate pool — retry every Show in case it failed before
        if (!poolGenerated || persistentRarePerk == null)
        {
            poolGenerated = false;
            GeneratePool();
        }

        isAnimating = false;
        RefreshUI();
        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        ReturnAllTubePerks();
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
    // POOL
    // ═══════════════════════════════════════════

    /// <summary>
    /// Cache perk lists from LevelUpManager. Call this early (e.g. MapManager.Start)
    /// while LevelUpManager is still alive. LevelUpManager doesn't use DontDestroyOnLoad
    /// so it gets destroyed on scene transitions.
    /// </summary>
    public void CachePerkLists()
    {
        // Already cached successfully?
        if (cachedRarePerks != null && cachedRarePerks.Count > 0) return;

        Debug.Log("[SACRIFICE] CachePerkLists called...");

        var lum = LevelUpManager.instance;
        if (lum == null)
        {
            // Try every way possible to find it
            lum = FindObjectOfType<LevelUpManager>();
            if (lum == null)
            {
                // Search including inactive objects
                var allLums = Resources.FindObjectsOfTypeAll<LevelUpManager>();
                if (allLums.Length > 0) lum = allLums[0];
            }
        }

        if (lum == null)
        {
            Debug.LogWarning("[SACRIFICE] CachePerkLists: LevelUpManager not found yet — will retry later");
            return;
        }

        Debug.Log($"[SACRIFICE] Found LevelUpManager: {lum.name}, rare={lum.rarePerks?.Count ?? -1}, epic={lum.epicPerks?.Count ?? -1}, legendary={lum.legendaryPerks?.Count ?? -1}");

        cachedRarePerks = new List<GameObject>(lum.rarePerks ?? new List<GameObject>());
        cachedEpicPerks = new List<GameObject>(lum.epicPerks ?? new List<GameObject>());
        cachedLegendaryPerks = new List<GameObject>(lum.legendaryPerks ?? new List<GameObject>());

        // Secret perks
        cachedSecretPerks = new List<GameObject>();
        var orbs = Resources.FindObjectsOfTypeAll<SecretPerkOrb>();
        if (orbs.Length > 0 && orbs[0].secretPerkPool != null)
            cachedSecretPerks = new List<GameObject>(orbs[0].secretPerkPool);
        if (cachedSecretPerks.Count == 0 && cachedLegendaryPerks.Count > 0)
            cachedSecretPerks = new List<GameObject>(cachedLegendaryPerks);

        Debug.Log($"[SACRIFICE] Perk lists cached OK! rare={cachedRarePerks.Count}, epic={cachedEpicPerks.Count}, legendary={cachedLegendaryPerks.Count}, secret={cachedSecretPerks.Count}");
    }

    private void EnsureCachedLists()
    {
        // Try to cache if not done yet
        if (cachedRarePerks == null || cachedRarePerks.Count == 0)
        {
            CachePerkLists();
        }
    }

    private void GeneratePool()
    {
        EnsureCachedLists();

        if (cachedRarePerks == null || cachedRarePerks.Count == 0)
        {
            Debug.LogError("[SACRIFICE] No cached perk lists! Pool cannot be generated.");
            return;
        }

        persistentRarePerk = PickRandom(cachedRarePerks);
        persistentEpicPerk = PickRandom(cachedEpicPerks);
        persistentLegendaryPerk = PickRandom(cachedLegendaryPerks);
        persistentSecretPerk = PickRandom(cachedSecretPerks);
        poolGenerated = true;
        Debug.Log($"[SACRIFICE] Pool generated: rare={persistentRarePerk?.name}, epic={persistentEpicPerk?.name}, legendary={persistentLegendaryPerk?.name}, secret={persistentSecretPerk?.name}");
    }

    private void RerollRewards()
    {
        EnsureCachedLists();
        persistentRarePerk = PickRandom(cachedRarePerks);
        persistentEpicPerk = PickRandom(cachedEpicPerks);
        persistentLegendaryPerk = PickRandom(cachedLegendaryPerks);
        persistentSecretPerk = PickRandom(cachedSecretPerks);
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
    // TUBE
    // ═══════════════════════════════════════════

    public void AddPerkToTube(BasePerk perk)
    {
        if (isAnimating || perk == null) return;
        if (tubePerks.Contains(perk)) return;
        if (tubePerks.Count >= 10) return;

        var rm = RunManager.instance;
        if (rm.activePerks.Contains(perk)) { perk.OnUnequip(); rm.activePerks.Remove(perk); }
        else { rm.inventoryPerks.Remove(perk); }

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
        if (RunManager.instance.inventoryPerks.Count < RunManager.MAX_INVENTORY_PERKS)
            RunManager.instance.inventoryPerks.Add(perk);

        if (index < tubePerkIcons.Count) { Destroy(tubePerkIcons[index]); tubePerkIcons.RemoveAt(index); }

        RunManager.instance.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
        RefreshUI();
    }

    private void ReturnAllTubePerks()
    {
        if (RunManager.instance == null) return;
        for (int i = tubePerks.Count - 1; i >= 0; i--)
            if (tubePerks[i] != null) RunManager.instance.inventoryPerks.Add(tubePerks[i]);
        tubePerks.Clear();
        ClearTubeIcons();
        RunManager.instance.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
    }

    private void CreateTubeIcon(BasePerk perk)
    {
        if (perkGridParent == null) return;

        Debug.Log($"[SACRIFICE] CreateTubeIcon: {perk.perkName}, icon={perk.icon?.name ?? "NULL"}");

        // Single GO with Image — simplest possible approach
        // GridLayoutGroup on parent controls size (60x65)
        GameObject slotGO = MakeUI("TubePerk_" + perk.perkName, perkGridParent);

        // Background/border — this is the slot itself, sized by GridLayoutGroup
        Image slotBG = slotGO.AddComponent<Image>();
        slotBG.color = SacrificeRewardSlot.GetRarityColor(perk.rarity) * 0.5f;
        slotBG.color = new Color(slotBG.color.r, slotBG.color.g, slotBG.color.b, 0.6f);

        // Icon — stretched to fill most of slot
        GameObject iconGO = MakeUI("Icon", slotGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.25f);
        iconRT.anchorMax = new Vector2(0.9f, 0.95f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image img = iconGO.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.type = Image.Type.Simple;
        if (perk.icon != null)
        {
            img.sprite = perk.icon;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = SacrificeRewardSlot.GetRarityColor(perk.rarity);
        }

        // Name label at bottom
        GameObject nameGO = MakeUI("Name", slotGO.transform);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0.25f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;
        TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text = perk.perkName;
        nameTxt.fontSize = 8;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.raycastTarget = false;
        nameTxt.enableWordWrapping = false;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;
        if (cachedFont != null) nameTxt.font = cachedFont;

        // Click to remove
        Button btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = slotBG;
        BasePerk captured = perk;
        btn.onClick.AddListener(() => RemovePerkFromTube(captured));

        tubePerkIcons.Add(slotGO);
        StartCoroutine(DropAnimation(slotGO));
    }

    private void ClearTubeIcons()
    {
        foreach (var go in tubePerkIcons) if (go != null) Destroy(go);
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
    // LEVER
    // ═══════════════════════════════════════════

    private bool CanPullLever()
    {
        int c = tubePerks.Count;
        return !isAnimating && (c == 1 || c == 2 || c == 4 || c == 6 || c == 10);
    }

    private void OnLeverClicked()
    {
        Debug.Log($"[SACRIFICE] Lever clicked! tubePerks={tubePerks.Count}, canPull={CanPullLever()}, isAnimating={isAnimating}");
        if (!CanPullLever()) return;
        // Ensure pool is generated before pulling
        if (!poolGenerated) GeneratePool();
        StartCoroutine(LeverSequence());
    }

    private IEnumerator LeverSequence()
    {
        isAnimating = true;
        int count = tubePerks.Count;

        // Determine reward BEFORE consuming
        GameObject rewardPrefab = null;
        if (count == 2) rewardPrefab = persistentRarePerk;
        else if (count == 4) rewardPrefab = persistentEpicPerk;
        else if (count == 6) rewardPrefab = persistentLegendaryPerk;
        else if (count == 10) rewardPrefab = persistentSecretPerk;

        // FALLBACK: if pool failed, try generating now from cached lists
        if (rewardPrefab == null && count > 1)
        {
            Debug.LogWarning($"[SACRIFICE] rewardPrefab is null for count={count}! Regenerating pool...");
            poolGenerated = false;
            GeneratePool();
            if (count == 2) rewardPrefab = persistentRarePerk;
            else if (count == 4) rewardPrefab = persistentEpicPerk;
            else if (count == 6) rewardPrefab = persistentLegendaryPerk;
            else if (count == 10) rewardPrefab = persistentSecretPerk;

            // LAST RESORT: pick directly from cached lists
            if (rewardPrefab == null)
            {
                EnsureCachedLists();
                if (count == 2) rewardPrefab = PickRandom(cachedRarePerks);
                else if (count == 4) rewardPrefab = PickRandom(cachedEpicPerks);
                else if (count >= 6) rewardPrefab = PickRandom(cachedLegendaryPerks);
                Debug.Log($"[SACRIFICE] LAST RESORT picked: {rewardPrefab?.name ?? "STILL NULL"}");
            }
        }

        Debug.Log($"[SACRIFICE] LeverSequence: count={count}, rewardPrefab={rewardPrefab?.name ?? "NULL!"}");

        // Lever pull animation
        yield return StartCoroutine(AnimateLeverPull());

        // Acid dissolve — perks melt
        yield return StartCoroutine(AcidDissolveAnimation());

        // Destroy tube perks
        ConsumeTubePerks();

        if (count == 1)
        {
            // ─── REROLL ───
            RerollRewards();
            RefreshUI();
            if (statusText != null) statusText.text = "REWARDS REROLLED!";
            isAnimating = false;
            yield return new WaitForSecondsRealtime(1f);
            if (statusText != null) statusText.text = "DRAG PERKS INTO THE TUBE";
        }
        else
        {
            // ─── GRANT REWARD ───
            if (rewardPrefab != null)
            {
                BasePerk bp = rewardPrefab.GetComponent<BasePerk>();
                string perkName = bp != null ? bp.perkName : "???";
                Debug.Log($"[SACRIFICE] Granting: {perkName}");

                // Show reward perk appearing in tube
                yield return StartCoroutine(ShowRewardInTube(rewardPrefab));

                // Actually grant the perk
                if (RunManager.instance != null)
                {
                    RunManager.instance.AddPerk(rewardPrefab);
                    RunManager.instance.RefreshPerkUI();
                    Debug.Log($"[SACRIFICE] AddPerk SUCCESS: {perkName}");
                }

                if (statusText != null) statusText.text = $"ACQUIRED: {perkName.ToUpper()}!";

                yield return new WaitForSecondsRealtime(1.5f);
            }
            else
            {
                Debug.LogWarning("[SACRIFICE] rewardPrefab is NULL!");
                if (statusText != null) statusText.text = "NO REWARD AVAILABLE";
                yield return new WaitForSecondsRealtime(1f);
            }

            isAnimating = false;
            Hide();
            if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.Hide();
            if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
        }
    }

    /// <summary>
    /// Reward perk tübün içinde belirir, sonra sağ üste (perk bar'a) uçar.
    /// </summary>
    private IEnumerator ShowRewardInTube(GameObject perkPrefab)
    {
        BasePerk bp = perkPrefab.GetComponent<BasePerk>();
        if (bp == null) yield break;

        // Create reward icon in tube center
        GameObject rewardGO = new GameObject("RewardPerk", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rewardGO.transform.SetParent(tubeArea != null ? tubeArea : panel.transform, false);
        rewardGO.layer = 5;

        RectTransform rrt = rewardGO.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0.5f);
        rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(70, 70);
        rrt.anchoredPosition = Vector2.zero;

        Image rImg = rewardGO.GetComponent<Image>();
        if (bp.icon != null)
        {
            rImg.sprite = bp.icon;
            rImg.color = Color.white;
        }
        else
        {
            rImg.color = SacrificeRewardSlot.GetRarityColor(bp.rarity);
        }
        rImg.preserveAspect = true;

        // Glow border
        GameObject glowGO = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGO.transform.SetParent(rewardGO.transform, false);
        glowGO.layer = 5;
        RectTransform grt = glowGO.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = new Vector2(-6, -6);
        grt.offsetMax = new Vector2(6, 6);
        grt.SetAsFirstSibling();
        Image glowImg = glowGO.GetComponent<Image>();
        Color rc = SacrificeRewardSlot.GetRarityColor(bp.rarity);
        glowImg.color = rc;
        glowImg.raycastTarget = false;

        // Name below
        GameObject nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(rewardGO.transform, false);
        nameGO.layer = 5;
        RectTransform nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0.5f, 0);
        nrt.anchorMax = new Vector2(0.5f, 0);
        nrt.pivot = new Vector2(0.5f, 1);
        nrt.anchoredPosition = new Vector2(0, -5);
        nrt.sizeDelta = new Vector2(200, 25);
        TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text = bp.perkName.ToUpper();
        nameTxt.fontSize = 16;
        nameTxt.color = rc;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.raycastTarget = false;
        if (cachedFont != null) nameTxt.font = cachedFont;

        // Scale-up animation (appear from nothing)
        float t = 0f;
        CanvasGroup rcg = rewardGO.AddComponent<CanvasGroup>();
        rcg.alpha = 0f;
        rewardGO.transform.localScale = Vector3.one * 0.1f;

        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.5f);
            float ease = 1f - (1f - p) * (1f - p); // EaseOutQuad
            rewardGO.transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, Vector3.one * 1.1f, ease);
            rcg.alpha = ease;
            // Glow pulse
            glowImg.color = new Color(rc.r, rc.g, rc.b, 0.5f + Mathf.PingPong(t * 4f, 0.5f));
            yield return null;
        }

        // Settle
        t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            rewardGO.transform.localScale = Vector3.Lerp(Vector3.one * 1.1f, Vector3.one, Mathf.Clamp01(t / 0.15f));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.8f);

        // Fly to top-right (perk bar area)
        Vector2 startPos = rrt.anchoredPosition;
        Vector2 endPos = new Vector2(600, 300);
        t = 0f;
        while (t < 0.4f)
        {
            if (rrt == null) yield break;
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            rrt.anchoredPosition = Vector2.Lerp(startPos, endPos, p * p);
            rewardGO.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.3f, p);
            rcg.alpha = 1f - p;
            yield return null;
        }

        Destroy(rewardGO);
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

        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            leverHandle.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.Clamp01(t / 0.3f));
            yield return null;
        }
        leverHandle.anchoredPosition = endPos;
        yield return new WaitForSecondsRealtime(0.15f);

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

        Color acidOrig = acidPoolImage != null ? acidPoolImage.color : Color.green;
        Color acidBright = new Color(0.2f, 1f, 0.2f, 0.85f);

        float dur = 0.8f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            foreach (var rt in rts)
                if (rt != null) rt.anchoredPosition += new Vector2(0, -200f * Time.unscaledDeltaTime);
            foreach (var cg in cgs)
            {
                if (cg == null) continue;
                cg.alpha = 1f - p;
                cg.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, p);
            }
            if (acidPoolImage != null)
                acidPoolImage.color = Color.Lerp(acidOrig, acidBright, Mathf.PingPong(t * 3f, 1f));
            yield return null;
        }
        if (acidPoolImage != null) acidPoolImage.color = acidOrig;
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

        // Lever
        bool canPull = CanPullLever();
        if (leverButton != null) leverButton.interactable = canPull;
        if (leverHandleImage != null)
            leverHandleImage.color = canPull ? new Color(0.9f, 0.95f, 1f) : new Color(0.25f, 0.25f, 0.25f);
        if (leverText != null)
        {
            leverText.text = count == 1 ? "REROLL" : (canPull ? "PULL" : "---");
            leverText.color = canPull ? Color.white : new Color(0.35f, 0.35f, 0.35f);
        }

        // Reward slots — always show info
        if (rareSlot != null) { rareSlot.Setup(PerkRarity.Rare, persistentRarePerk, 2); rareSlot.SetHighlighted(count == 2); }
        if (epicSlot != null) { epicSlot.Setup(PerkRarity.Epic, persistentEpicPerk, 4); epicSlot.SetHighlighted(count == 4); }
        if (legendarySlot != null) { legendarySlot.Setup(PerkRarity.Legendary, persistentLegendaryPerk, 6); legendarySlot.SetHighlighted(count == 6); }

        // Status
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
    // BUILD UI FROM CODE
    // ═══════════════════════════════════════════

    private void BuildFromCode()
    {
        cachedFont = Resources.Load<TMP_FontAsset>("alagard SDF");

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

        panel = MakeUI("SacrificePanel", canvasGO.transform);
        Stretch(panel);
        panel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        canvasGroup = panel.AddComponent<CanvasGroup>();

        // Title
        titleText = MakeText("Title", panel.transform,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-50, -15), new Vector2(500, 55),
            "SACRIFICE MACHINE", 36, new Color(0.8f, 0.2f, 0.6f), TextAlignmentOptions.Center);

        // Machine
        GameObject machineGO = MakeUI("MachineBody", panel.transform);
        SetAnchored(machineGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-50, 20), new Vector2(420, 520));
        machineGO.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 1f);

        // Header
        GameObject headerGO = MakeUI("Header", machineGO.transform);
        RectTransform hrt = headerGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1); hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0, 35);
        headerGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 1f);

        // Tube
        GameObject tubeOuterGO = MakeUI("TubeOuter", machineGO.transform);
        RectTransform toRT = tubeOuterGO.GetComponent<RectTransform>();
        toRT.anchorMin = new Vector2(0.06f, 0.15f); toRT.anchorMax = new Vector2(0.94f, 0.93f);
        toRT.offsetMin = Vector2.zero; toRT.offsetMax = Vector2.zero;
        tubeOuterGO.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f, 0.45f);

        GameObject tubeInnerGO = MakeUI("TubeInner", tubeOuterGO.transform);
        Stretch(tubeInnerGO);
        tubeInnerGO.GetComponent<RectTransform>().offsetMin = new Vector2(3, 3);
        tubeInnerGO.GetComponent<RectTransform>().offsetMax = new Vector2(-3, -3);
        tubeImage = tubeInnerGO.AddComponent<Image>();
        tubeImage.color = new Color(0.12f, 0.35f, 0.18f, 0.1f);
        tubeArea = tubeInnerGO.GetComponent<RectTransform>();

        SacrificeTubeDropZone dropZone = tubeInnerGO.AddComponent<SacrificeTubeDropZone>();
        dropZone.highlightImage = tubeImage;

        // Grid
        GameObject gridGO = MakeUI("PerkGrid", tubeInnerGO.transform);
        Stretch(gridGO);
        gridGO.GetComponent<RectTransform>().offsetMin = new Vector2(8, 8);
        gridGO.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -8);
        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(60, 65);
        grid.spacing = new Vector2(8, 6);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        perkGridParent = gridGO.transform;

        // Acid pool
        GameObject acidGO = MakeUI("AcidPool", machineGO.transform);
        RectTransform acidRT = acidGO.GetComponent<RectTransform>();
        acidRT.anchorMin = new Vector2(0.06f, 0.04f); acidRT.anchorMax = new Vector2(0.94f, 0.15f);
        acidRT.offsetMin = Vector2.zero; acidRT.offsetMax = Vector2.zero;
        acidPoolImage = acidGO.AddComponent<Image>();
        acidPoolImage.color = new Color(0.08f, 0.5f, 0.08f, 0.55f);

        // Lever
        BuildLever(panel.transform);

        // Reward slots
        BuildRewardSlots(panel.transform);

        // Status
        statusText = MakeText("StatusText", panel.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-50, 55), new Vector2(550, 35),
            "", 18, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);

        // Leave
        BuildLeaveButton(panel.transform);

        panel.SetActive(false);
    }

    private void BuildLever(Transform parent)
    {
        GameObject leverGO = MakeUI("Lever", parent);
        SetAnchored(leverGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-330, 20), new Vector2(80, 280));
        Image baseBG = leverGO.AddComponent<Image>();
        baseBG.color = new Color(0.09f, 0.09f, 0.11f, 0.85f);

        GameObject shaftGO = MakeUI("Shaft", leverGO.transform);
        RectTransform srt = shaftGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.15f); srt.anchorMax = new Vector2(0.5f, 0.8f); srt.sizeDelta = new Vector2(8, 0);
        shaftGO.AddComponent<Image>().color = new Color(0.45f, 0.45f, 0.5f);

        GameObject handleGO = MakeUI("Handle", leverGO.transform);
        leverHandle = handleGO.GetComponent<RectTransform>();
        leverHandle.anchorMin = new Vector2(0.5f, 0.8f); leverHandle.anchorMax = new Vector2(0.5f, 0.8f);
        leverHandle.sizeDelta = new Vector2(36, 36);
        leverHandleImage = handleGO.AddComponent<Image>();
        leverHandleImage.color = Color.white;

        leverButton = leverGO.AddComponent<Button>();
        leverButton.targetGraphic = baseBG;
        leverButton.onClick.AddListener(OnLeverClicked);

        leverText = MakeText("PullText", leverGO.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),
            new Vector2(0, -5), new Vector2(0, 28), "PULL", 15, Color.white, TextAlignmentOptions.Center);
    }

    private void BuildRewardSlots(Transform parent)
    {
        GameObject rowGO = MakeUI("RewardRow", parent);
        SetAnchored(rowGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-50, -290), new Vector2(520, 180));
        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

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

        // Glow
        GameObject glowGO = MakeUI("Glow", slotGO.transform);
        Stretch(glowGO);
        glowGO.GetComponent<RectTransform>().offsetMin = new Vector2(-3, -3);
        glowGO.GetComponent<RectTransform>().offsetMax = new Vector2(3, 3);
        glowGO.transform.SetAsFirstSibling();
        Image glowImg = glowGO.AddComponent<Image>();
        glowImg.color = new Color(0.25f, 0.25f, 0.25f, 0.25f);
        glowImg.raycastTarget = false;

        // Rarity label (top)
        TMP_Text rarTxt = MakeAnchText("Rarity", slotGO.transform,
            new Vector2(0, 0.88f), new Vector2(1, 1), rarity.ToString().ToUpper(), 12, rc);

        // Icon (center)
        GameObject iconGO = MakeUI("Icon", slotGO.transform);
        RectTransform irt = iconGO.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.55f); irt.anchorMax = new Vector2(0.5f, 0.55f);
        irt.sizeDelta = new Vector2(48, 48);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Name
        TMP_Text nameTxt = MakeAnchText("Name", slotGO.transform,
            new Vector2(0, 0.30f), new Vector2(1, 0.44f), "???", 12, Color.white);

        // Description
        TMP_Text descTxt = MakeAnchText("Desc", slotGO.transform,
            new Vector2(0, 0.12f), new Vector2(1, 0.30f), "", 9, new Color(0.65f, 0.65f, 0.65f));
        descTxt.enableWordWrapping = true;
        descTxt.overflowMode = TextOverflowModes.Truncate;

        // Cost (bottom)
        TMP_Text costTxt = MakeAnchText("Cost", slotGO.transform,
            new Vector2(0, 0f), new Vector2(1, 0.12f), $"{cost} PERKS", 10, new Color(0.6f, 0.6f, 0.6f));

        SacrificeRewardSlot slot = slotGO.AddComponent<SacrificeRewardSlot>();
        slot.background = bg;
        slot.iconImage = iconImg;
        slot.glowBorder = glowImg;
        slot.nameText = nameTxt;
        slot.descText = descTxt;
        slot.costText = costTxt;
        slot.rarityLabel = rarTxt;
        return slot;
    }

    private void BuildLeaveButton(Transform parent)
    {
        GameObject btnGO = MakeUI("LeaveButton", parent);
        RectTransform brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0); brt.anchorMax = new Vector2(0.5f, 0);
        brt.pivot = new Vector2(0.5f, 0);
        brt.anchoredPosition = new Vector2(-50, 15); brt.sizeDelta = new Vector2(170, 42);
        btnGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        leaveButton = btnGO.AddComponent<Button>();
        leaveButton.onClick.AddListener(OnLeaveClicked);
        MakeTextChild("Text", btnGO.transform, "LEAVE", 20, Color.white, TextAlignmentOptions.Center);
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
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private TMP_Text MakeText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = fontSize; txt.color = color;
        txt.alignment = align; txt.raycastTarget = false;
        if (cachedFont != null) txt.font = cachedFont;
        return txt;
    }

    /// <summary>Anchor-stretched text inside a slot (anchorMin/Max define area, offset=padding).</summary>
    private TMP_Text MakeAnchText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, string text, float fontSize, Color color)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(4, 0); rt.offsetMax = new Vector2(-4, 0);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = fontSize; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        if (cachedFont != null) txt.font = cachedFont;
        return txt;
    }

    private TMP_Text MakeTextChild(string name, Transform parent,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = MakeUI(name, parent);
        Stretch(go);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = fontSize; txt.color = color;
        txt.alignment = align; txt.raycastTarget = false;
        if (cachedFont != null) txt.font = cachedFont;
        return txt;
    }
}
