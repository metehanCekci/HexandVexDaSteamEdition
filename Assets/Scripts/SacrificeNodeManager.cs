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

    [Header("Progression Bar")]
    public Image[] progressionDots = new Image[3];   // ○  ○  ○  (3 sacrifice nodes)
    public Image[] progressionLines = new Image[2];  // connecting segments between dots
    private const int TOTAL_SACRIFICE_NODES = 3;

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

    // Cached perk lists (because MergedShopManager doesn't persist across scenes)
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

        // Older scenes (built via editor tool) may not have the progression bar — inject it on first Show.
        EnsureProgressionBar();

        ReturnAllTubePerks();

        panel.SetActive(true);
        Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);

        // Re-wire buttons every Show
        if (leverButton != null) { leverButton.onClick.RemoveAllListeners(); leverButton.onClick.AddListener(OnLeverClicked); }
        if (leaveButton != null) { leaveButton.onClick.RemoveAllListeners(); leaveButton.onClick.AddListener(OnLeaveClicked); }

        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.Show();

        // Last-chance cache attempt right before we need the lists
        CachePerkLists();

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
    /// Cache perk lists from MergedShopManager. Call this early (e.g. MapManager.Start)
    /// while MergedShopManager is still alive.
    /// </summary>
    public void CachePerkLists()
    {
        // Already cached successfully?
        if (cachedRarePerks != null && cachedRarePerks.Count > 0) return;

        Debug.Log("[SACRIFICE] CachePerkLists called...");

        try
        {
            MergedShopManager shop = MergedShopManager.instance;
            if (shop == null) shop = FindFirstObjectByType<MergedShopManager>();
            if (shop == null)
            {
                var allShops = Resources.FindObjectsOfTypeAll<MergedShopManager>();
                if (allShops.Length > 0) shop = allShops[0];
            }

            if (shop == null)
            {
                Debug.Log("[SACRIFICE] CachePerkLists: MergedShopManager not found anywhere. Will retry later.");
                return;
            }

            var rare = shop.rarePerks;
            var epic = shop.epicPerks;
            var legendary = shop.legendaryPerks;

            Debug.Log("[SACRIFICE] MergedShopManager lists: rare=" + (rare != null ? rare.Count.ToString() : "null")
                + ", epic=" + (epic != null ? epic.Count.ToString() : "null")
                + ", legendary=" + (legendary != null ? legendary.Count.ToString() : "null"));

            cachedRarePerks = new List<GameObject>(rare != null ? rare : new List<GameObject>());
            cachedEpicPerks = new List<GameObject>(epic != null ? epic : new List<GameObject>());
            cachedLegendaryPerks = new List<GameObject>(legendary != null ? legendary : new List<GameObject>());

            // Secret perks
            cachedSecretPerks = new List<GameObject>();
            var secretOrb = Resources.Load<SecretPerkOrb>("SecretPerkOrb");
            Debug.Log("[SACRIFICE] SecretPerkOrb Resources.Load: " + (secretOrb != null ? "FOUND" : "NULL"));
            if (secretOrb != null && secretOrb.secretPerkPool != null)
            {
                cachedSecretPerks = new List<GameObject>(secretOrb.secretPerkPool);
                Debug.Log("[SACRIFICE] Secret pool from orb: " + cachedSecretPerks.Count + " perks");
            }
            if (cachedSecretPerks.Count == 0)
            {
                Debug.Log("[SACRIFICE] No secret perks found! Using legendary as fallback.");
                if (cachedLegendaryPerks.Count > 0)
                    cachedSecretPerks = new List<GameObject>(cachedLegendaryPerks);
            }

            Debug.Log("[SACRIFICE] Perk lists cached OK! rare=" + cachedRarePerks.Count + ", epic=" + cachedEpicPerks.Count + ", legendary=" + cachedLegendaryPerks.Count + ", secret=" + cachedSecretPerks.Count);
        }
        catch (System.Exception ex)
        {
            Debug.Log("[SACRIFICE] CachePerkLists EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    /// <summary>
    /// Directly inject perk lists from another manager that already has them.
    /// </summary>
    public void InjectPerkLists(List<GameObject> rare, List<GameObject> epic, List<GameObject> legendary, List<GameObject> secret = null)
    {
        if (cachedRarePerks != null && cachedRarePerks.Count > 0) return;
        if (rare == null || rare.Count == 0) return;

        cachedRarePerks = new List<GameObject>(rare);
        cachedEpicPerks = new List<GameObject>(epic ?? new List<GameObject>());
        cachedLegendaryPerks = new List<GameObject>(legendary ?? new List<GameObject>());

        if (secret != null && secret.Count > 0)
            cachedSecretPerks = new List<GameObject>(secret);
        else
        {
            cachedSecretPerks = new List<GameObject>();
            var loaded = Resources.Load<SecretPerkOrb>("SecretPerkOrb");
            if (loaded != null && loaded.secretPerkPool != null)
                cachedSecretPerks = new List<GameObject>(loaded.secretPerkPool);
        }
        if (cachedSecretPerks.Count == 0 && cachedLegendaryPerks.Count > 0)
            cachedSecretPerks = new List<GameObject>(cachedLegendaryPerks);

        Debug.Log($"[SACRIFICE] Perk lists INJECTED! rare={cachedRarePerks.Count}, epic={cachedEpicPerks.Count}, legendary={cachedLegendaryPerks.Count}, secret={cachedSecretPerks.Count}");
    }

    private void EnsureCachedLists()
    {
        if (cachedRarePerks != null && cachedRarePerks.Count > 0) return;

        // Method 1: Try via MergedShopManager primary lookup
        CachePerkLists();

        if (cachedRarePerks != null && cachedRarePerks.Count > 0) return;

        // Method 2: Steal from MergedShopManager which caches its own copies
        try
        {
            var shop = FindFirstObjectByType<MergedShopManager>();
            if (shop == null)
            {
                var allShops = Resources.FindObjectsOfTypeAll<MergedShopManager>();
                if (allShops.Length > 0) shop = allShops[0];
            }
            if (shop != null && shop.rarePerks != null && shop.rarePerks.Count > 0)
            {
                Debug.Log("[SACRIFICE] Stealing perk lists from MergedShopManager!");
                List<GameObject> secretPool = null;
                if (shop.secretItem is SecretPerkOrb orb && orb.secretPerkPool != null && orb.secretPerkPool.Count > 0)
                    secretPool = orb.secretPerkPool;
                InjectPerkLists(shop.rarePerks, shop.epicPerks, shop.legendaryPerks, secretPool);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SACRIFICE] MergedShopManager fallback failed: {ex.Message}");
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

        // Oyuncunun sahip olduğu perk tiplerini topla
        HashSet<System.Type> ownedTypes = new HashSet<System.Type>();
        if (RunManager.instance != null)
        {
            foreach (var p in RunManager.instance.activePerks)
                if (p != null) ownedTypes.Add(p.GetType());
            foreach (var p in RunManager.instance.inventoryPerks)
                if (p != null) ownedTypes.Add(p.GetType());
        }

        // Sahip olunmayan perkleri filtrele
        List<GameObject> available = new List<GameObject>();
        foreach (var go in pool)
        {
            if (go == null) continue;
            BasePerk bp = go.GetComponent<BasePerk>();
            if (bp != null && !ownedTypes.Contains(bp.GetType()))
                available.Add(go);
        }

        // Hepsi alınmışsa tüm havuzdan seç (fallback)
        if (available.Count == 0) return pool[Random.Range(0, pool.Count)];
        return available[Random.Range(0, available.Count)];
    }

    public void ResetForNewRun()
    {
        poolGenerated = false;
        persistentRarePerk = null;
        persistentEpicPerk = null;
        persistentLegendaryPerk = null;
        persistentSecretPerk = null;
        if (RunManager.instance != null)
            RunManager.instance.sacrificeNodesVisited = 0;
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
        var rm = RunManager.instance;
        if (rm.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
            rm.activePerks.Add(perk);
        else if (rm.inventoryPerks.Count < RunManager.MAX_INVENTORY_PERKS)
            rm.inventoryPerks.Add(perk);

        if (index < tubePerkIcons.Count) { Destroy(tubePerkIcons[index]); tubePerkIcons.RemoveAt(index); }

        // Son perkin ikonunu göster (varsa)
        ClearTubeIcons();
        if (tubePerks.Count > 0)
            CreateTubeIcon(tubePerks[tubePerks.Count - 1]);

        RunManager.instance.RefreshPerkUI();
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
        RefreshUI();
    }

    public void RemoveLastPerkFromTube()
    {
        if (isAnimating || tubePerks.Count == 0) return;
        RemovePerkFromTube(tubePerks[tubePerks.Count - 1]);
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
        if (tubeArea == null) return;

        // Önceki tüm ikonları kaldır — sadece son eklenen görünecek
        ClearTubeIcons();

        GameObject slotGO = MakeUI("TubePerk", tubeArea);
        RectTransform rt = slotGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(80, 80);

        Image img = slotGO.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (perk.icon != null)
        {
            img.sprite = perk.icon;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

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

    // 1-based visit number for the CURRENT open sacrifice node.
    // sacrificeNodesVisited counts completed visits, so the current one is +1.
    private int CurrentVisitNumber()
    {
        if (RunManager.instance == null) return 1;
        return RunManager.instance.sacrificeNodesVisited + 1;
    }

    private bool IsTierUnlocked(PerkRarity tier)
    {
        int v = CurrentVisitNumber();
        switch (tier)
        {
            case PerkRarity.Rare:      return v >= 1;
            case PerkRarity.Epic:      return v >= 2;
            case PerkRarity.Legendary: return v >= 3;
            case PerkRarity.Secret:    return v >= 3;
        }
        return false;
    }

    // Required visit number to unlock a given tier (for locked-slot labels).
    private static int UnlockVisitFor(PerkRarity tier)
    {
        switch (tier)
        {
            case PerkRarity.Rare:      return 1;
            case PerkRarity.Epic:      return 2;
            case PerkRarity.Legendary: return 3;
            case PerkRarity.Secret:    return 3;
        }
        return 1;
    }

    private bool CanPullLever()
    {
        if (isAnimating) return false;
        int c = tubePerks.Count;
        if (c == 1) return true; // reroll her zaman
        if (c == 2) return persistentRarePerk != null && IsTierUnlocked(PerkRarity.Rare);
        if (c == 4) return persistentEpicPerk != null && IsTierUnlocked(PerkRarity.Epic);
        if (c == 6) return persistentLegendaryPerk != null && IsTierUnlocked(PerkRarity.Legendary);
        if (c == 10) return IsTierUnlocked(PerkRarity.Secret);
        return false;
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
        else if (count == 10)
        {
            // ─── SECRET PERK — use SecretPerkOrb directly ───
            Debug.Log("[SACRIFICE] count=10, triggering SecretPerkOrb.Use()");

            // Ensure SecretPerkCinematic exists for the animation
            if (SecretPerkCinematic.instance == null)
            {
                GameObject cinGO = new GameObject("SecretPerkCinematic");
                cinGO.AddComponent<SecretPerkCinematic>();
                Debug.Log("[SACRIFICE] Created SecretPerkCinematic at runtime");
            }

            SecretPerkOrb orb = FindSecretPerkOrb();
            Debug.Log($"[SACRIFICE] SecretPerkOrb found={orb != null}, pool={orb?.secretPerkPool?.Count ?? -1}");
            // Let duplicate perks stack when the reward comes from sacrifice.
            if (RunManager.instance != null) RunManager.instance.allowDuplicatePerk = true;
            bool orbResult = orb != null && orb.Use();
            Debug.Log($"[SACRIFICE] SecretPerkOrb.Use() returned {orbResult}");
            if (orbResult)
            {
                Debug.Log("[SACRIFICE] SecretPerkOrb.Use() SUCCESS");
                // SecretPerkOrb.Use() already calls SecretPerkCinematic.Play() and RunManager.AddPerk()
                // Wait for cinematic to finish
                if (SecretPerkCinematic.instance != null)
                {
                    while (SecretPerkCinematic.instance.IsPlaying)
                        yield return null;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(1.5f);
                }

                if (statusText != null) statusText.text = "SECRET MUTATION ACQUIRED!";
                if (RunManager.instance != null)
                {
                    RunManager.instance.sacrificeNodesVisited++;
                    RunManager.instance.RefreshPerkUI();
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else
            {
                // Clear the unused dupe flag since AddPerk was never reached.
                if (RunManager.instance != null) RunManager.instance.allowDuplicatePerk = false;
                Debug.LogWarning("[SACRIFICE] SecretPerkOrb not found or Use() failed!");
                if (statusText != null) statusText.text = "NO SECRET AVAILABLE";
                yield return new WaitForSecondsRealtime(1f);
            }

            isAnimating = false;
            Hide();
            if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
            if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
        }
        else
        {
            // ─── GRANT REWARD (rare/epic/legendary) ───
            GameObject rewardPrefab = null;
            if (count == 2) rewardPrefab = persistentRarePerk;
            else if (count == 4) rewardPrefab = persistentEpicPerk;
            else if (count == 6) rewardPrefab = persistentLegendaryPerk;

            // FALLBACK: if pool failed, try generating now from cached lists
            if (rewardPrefab == null)
            {
                Debug.LogWarning($"[SACRIFICE] rewardPrefab is null for count={count}! Regenerating pool...");
                poolGenerated = false;
                GeneratePool();
                if (count == 2) rewardPrefab = persistentRarePerk;
                else if (count == 4) rewardPrefab = persistentEpicPerk;
                else if (count == 6) rewardPrefab = persistentLegendaryPerk;

                // LAST RESORT: pick directly from cached lists
                if (rewardPrefab == null)
                {
                    EnsureCachedLists();
                    if (count == 2) rewardPrefab = PickRandom(cachedRarePerks);
                    else if (count == 4) rewardPrefab = PickRandom(cachedEpicPerks);
                    else if (count == 6) rewardPrefab = PickRandom(cachedLegendaryPerks);
                    Debug.Log($"[SACRIFICE] LAST RESORT picked: {rewardPrefab?.name ?? "STILL NULL"}");
                }
            }

            Debug.Log($"[SACRIFICE] LeverSequence: count={count}, rewardPrefab={rewardPrefab?.name ?? "NULL!"}");

            if (rewardPrefab != null)
            {
                BasePerk bp = rewardPrefab.GetComponent<BasePerk>();
                string perkName = bp != null ? bp.perkName : "???";
                Debug.Log($"[SACRIFICE] Granting: {perkName}");

                // Show reward perk appearing in tube
                yield return StartCoroutine(ShowRewardInTube(rewardPrefab));

                // Actually grant the perk — allow duplicate (sacrifice-only rule)
                if (RunManager.instance != null)
                {
                    RunManager.instance.allowDuplicatePerk = true;
                    RunManager.instance.AddPerk(rewardPrefab);
                    RunManager.instance.sacrificeNodesVisited++;
                    RunManager.instance.RefreshPerkUI();
                    Debug.Log($"[SACRIFICE] AddPerk SUCCESS: {perkName}");
                }

                // Bu slotu tüket — aynı perk tekrar alınamasın
                if (count == 2) persistentRarePerk = null;
                else if (count == 4) persistentEpicPerk = null;
                else if (count == 6) persistentLegendaryPerk = null;

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
            if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
            if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
        }
    }

    private SecretPerkOrb FindSecretPerkOrb()
    {
        // Method 1: Resources.Load (asset is in Assets/Resources/SecretPerkOrb.asset)
        var loaded = Resources.Load<SecretPerkOrb>("SecretPerkOrb");
        if (loaded != null && loaded.secretPerkPool != null && loaded.secretPerkPool.Count > 0)
        {
            Debug.Log($"[SACRIFICE] Found SecretPerkOrb via Resources.Load, pool={loaded.secretPerkPool.Count}");
            return loaded;
        }

        // Method 2: MergedShopManager.instance.secretItem
        if (MergedShopManager.instance != null && MergedShopManager.instance.secretItem is SecretPerkOrb orb)
        {
            Debug.Log($"[SACRIFICE] Found SecretPerkOrb from MergedShopManager, pool={orb.secretPerkPool?.Count ?? 0}");
            return orb;
        }

        // Method 3: search all loaded assets
        var orbs = Resources.FindObjectsOfTypeAll<SecretPerkOrb>();
        if (orbs.Length > 0)
        {
            Debug.Log($"[SACRIFICE] Found SecretPerkOrb via FindObjectsOfTypeAll, pool={orbs[0].secretPerkPool?.Count ?? 0}");
            return orbs[0];
        }

        Debug.LogWarning("[SACRIFICE] SecretPerkOrb NOT FOUND anywhere!");
        return null;
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
        // Envanter açık kalsın — haritada da erişilebilir olmalı
        if (PerkInventoryUI.instance != null) PerkInventoryUI.instance.RefreshUI();
        if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
    }

    // ═══════════════════════════════════════════
    // UI REFRESH
    // ═══════════════════════════════════════════

    private void RefreshUI()
    {
        int count = tubePerks.Count;
        int visit = CurrentVisitNumber();

        // Sacrifice progression label in the title
        if (titleText != null)
            titleText.text = $"SACRIFICE MACHINE  <size=22><color=#888888>#{visit}</color></size>";

        RefreshProgressionBar();

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

        // Reward slots — lock tiers not yet unlocked for this visit number
        bool rareUnlocked = IsTierUnlocked(PerkRarity.Rare);
        bool epicUnlocked = IsTierUnlocked(PerkRarity.Epic);
        bool legendaryUnlocked = IsTierUnlocked(PerkRarity.Legendary);

        if (rareSlot != null)
        {
            rareSlot.Setup(PerkRarity.Rare, persistentRarePerk, 2, !rareUnlocked, UnlockVisitFor(PerkRarity.Rare));
            rareSlot.SetHighlighted(count == 2 && rareUnlocked);
        }
        if (epicSlot != null)
        {
            epicSlot.Setup(PerkRarity.Epic, persistentEpicPerk, 4, !epicUnlocked, UnlockVisitFor(PerkRarity.Epic));
            epicSlot.SetHighlighted(count == 4 && epicUnlocked);
        }
        if (legendarySlot != null)
        {
            legendarySlot.Setup(PerkRarity.Legendary, persistentLegendaryPerk, 6, !legendaryUnlocked, UnlockVisitFor(PerkRarity.Legendary));
            legendarySlot.SetHighlighted(count == 6 && legendaryUnlocked);
        }

        // Status — gate messages by unlock state
        if (statusText != null)
        {
            string s;
            if (count == 0) s = "DRAG PERKS INTO THE TUBE";
            else if (count == 1) s = "PULL LEVER TO REROLL REWARDS";
            else if (count == 2) s = rareUnlocked ? "PULL LEVER FOR RARE PERK" : $"RARE LOCKED — UNLOCKS AT SACRIFICE #{UnlockVisitFor(PerkRarity.Rare)}";
            else if (count == 3) s = "NEED 1 MORE FOR EPIC (4)";
            else if (count == 4) s = epicUnlocked ? "PULL LEVER FOR EPIC PERK" : $"EPIC LOCKED — UNLOCKS AT SACRIFICE #{UnlockVisitFor(PerkRarity.Epic)}";
            else if (count == 5) s = "NEED 1 MORE FOR LEGENDARY (6)";
            else if (count == 6) s = legendaryUnlocked ? "PULL LEVER FOR LEGENDARY PERK" : $"LEGENDARY LOCKED — UNLOCKS AT SACRIFICE #{UnlockVisitFor(PerkRarity.Legendary)}";
            else if (count >= 7 && count <= 9) s = $"NEED {10 - count} MORE FOR ???";
            else if (count == 10) s = IsTierUnlocked(PerkRarity.Secret) ? "PULL LEVER FOR ??? PERK" : $"??? LOCKED — UNLOCKS AT SACRIFICE #{UnlockVisitFor(PerkRarity.Secret)}";
            else s = "";
            statusText.text = s;
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

        // Progression bar — three dots connected by short lines, sits under the title
        BuildProgressionBar(panel.transform);

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

    // Build the progression bar lazily for scenes assembled via the editor setup tool
    // (which predates this feature). Idempotent — does nothing once dots exist.
    private void EnsureProgressionBar()
    {
        if (progressionDots != null && progressionDots.Length > 0 && progressionDots[0] != null) return;
        if (panel == null) return;

        // If a previous run left a stale "ProgressionBar" child around, clear it first.
        Transform existing = panel.transform.Find("ProgressionBar");
        if (existing != null) Destroy(existing.gameObject);

        progressionDots = new Image[3];
        progressionLines = new Image[2];
        BuildProgressionBar(panel.transform);
    }

    // Three dots (○─○─○) showing sacrifice progression. Position: directly under the title.
    private void BuildProgressionBar(Transform parent)
    {
        const float DOT_SIZE = 18f;
        const float LINE_W = 36f;
        const float LINE_H = 3f;
        const float SPACING = 4f; // gap between dot edge and line start

        GameObject barGO = MakeUI("ProgressionBar", parent);
        RectTransform brt = barGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 1f);
        brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(-50f, -62f); // matches title's -50 X offset, sits below it
        brt.sizeDelta = new Vector2(DOT_SIZE * 3 + LINE_W * 2 + SPACING * 4, DOT_SIZE + 6f);

        // Layout: dot, line, dot, line, dot — manually positioned for precise alignment
        float totalW = DOT_SIZE * 3 + LINE_W * 2 + SPACING * 4;
        float cursorX = -totalW / 2f;

        for (int i = 0; i < TOTAL_SACRIFICE_NODES; i++)
        {
            // Dot
            GameObject dotGO = MakeUI($"Dot{i}", barGO.transform);
            RectTransform drt = dotGO.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(DOT_SIZE, DOT_SIZE);
            drt.anchoredPosition = new Vector2(cursorX + DOT_SIZE / 2f, 0f);
            Image dotImg = dotGO.AddComponent<Image>();
            dotImg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);
            dotImg.raycastTarget = false;
            progressionDots[i] = dotImg;

            cursorX += DOT_SIZE;

            // Connecting line (skip after last dot)
            if (i < TOTAL_SACRIFICE_NODES - 1)
            {
                cursorX += SPACING;
                GameObject lineGO = MakeUI($"Line{i}", barGO.transform);
                RectTransform lrt = lineGO.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.5f, 0.5f);
                lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(LINE_W, LINE_H);
                lrt.anchoredPosition = new Vector2(cursorX + LINE_W / 2f, 0f);
                Image lineImg = lineGO.AddComponent<Image>();
                lineImg.color = new Color(0.2f, 0.2f, 0.25f, 0.85f);
                lineImg.raycastTarget = false;
                progressionLines[i] = lineImg;

                cursorX += LINE_W + SPACING;
            }
        }
    }

    // Update dot/line colors based on the current visit number (1..3).
    // Visit N is "current" (highlight), visits < N are "completed" (filled), visits > N are "future" (dim).
    private void RefreshProgressionBar()
    {
        if (progressionDots == null || progressionDots.Length == 0) return;

        int currentVisit = CurrentVisitNumber(); // 1-based
        Color completedColor = new Color(0.85f, 0.3f, 0.65f, 1f);   // pink — matches title
        Color currentColor   = new Color(1f, 0.55f, 0.85f, 1f);     // brighter pink for "now"
        Color futureColor    = new Color(0.22f, 0.22f, 0.27f, 0.9f);
        Color lineCompleted  = new Color(0.7f, 0.25f, 0.55f, 0.9f);
        Color lineFuture     = new Color(0.18f, 0.18f, 0.22f, 0.85f);

        for (int i = 0; i < progressionDots.Length; i++)
        {
            if (progressionDots[i] == null) continue;
            int visitNumber = i + 1;
            if (visitNumber < currentVisit)
                progressionDots[i].color = completedColor;
            else if (visitNumber == currentVisit)
                progressionDots[i].color = currentColor;
            else
                progressionDots[i].color = futureColor;

            // Subtle scale pulse for current — bigger than the others
            RectTransform drt = progressionDots[i].rectTransform;
            drt.localScale = (visitNumber == currentVisit) ? Vector3.one * 1.25f : Vector3.one;
        }

        // Lines: filled if the visit before them is completed (i.e. line i is between dot i and dot i+1)
        for (int i = 0; i < progressionLines.Length; i++)
        {
            if (progressionLines[i] == null) continue;
            int leftVisit = i + 1;
            progressionLines[i].color = (leftVisit < currentVisit) ? lineCompleted : lineFuture;
        }
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
        descTxt.textWrappingMode = TextWrappingModes.Normal;
        descTxt.overflowMode = TextOverflowModes.Truncate;

        // Cost (bottom)
        TMP_Text costTxt = MakeAnchText("Cost", slotGO.transform,
            new Vector2(0, 0f), new Vector2(1, 0.12f), $"{cost} PERKS", 10, new Color(0.6f, 0.6f, 0.6f));

        SacrificeRewardSlot slot = slotGO.AddComponent<SacrificeRewardSlot>();
        slot.background = bg;
        slot.iconImage = iconImg;
        slot.glowBorder = null;
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
        txt.textWrappingMode = TextWrappingModes.NoWrap;
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
