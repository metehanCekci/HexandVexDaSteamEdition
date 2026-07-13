using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ana menüdeki perk koleksiyon ekranı.
/// Grid halinde tüm perkleri gösterir — kilitliler siluet, açıklar renkli.
/// Hover'da tooltip, unlock animasyonları, filtreler, parçacık efektleri.
/// </summary>
public class CollectionUI : MonoBehaviour
{
    public static CollectionUI instance;

    [Header("Referanslar")]
    public PerkCollectionDatabase database;
    public GameObject panelRoot;          // Ana panel (açılıp kapanacak)
    public Transform gridContainer;       // GridLayoutGroup parent
    public GameObject cardPrefab;         // Her perk kartı template
    public CanvasGroup panelCanvasGroup;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipName;
    public TMP_Text tooltipDescription;
    public TMP_Text tooltipLore;
    public TMP_Text tooltipRarity;
    public TMP_Text tooltipUnlockHint;
    public Image tooltipIcon;
    public Slider tooltipProgressBar;
    public TMP_Text tooltipProgressText;
    public Image tooltipBorderGlow;

    [Header("Header")]
    public TMP_Text headerTitle;
    public TMP_Text headerCounter;

    [Header("Filtreler")]
    public Button filterAllBtn;
    public Button filterCommonBtn;
    public Button filterRareBtn;
    public Button filterEpicBtn;
    public Button filterLegendaryBtn;

    [Header("Ses")]
    public AudioClip unlockFanfareClip;
    public AudioClip cardHoverClip;
    public AudioClip cardClickClip;
    public AudioClip openClip;
    public AudioClip closeClip;

    [Header("Efekt Ayarları")]
    public Color lockedTint = new Color(0.08f, 0.08f, 0.12f, 1f);
    public Color lockedIconTint = new Color(0.15f, 0.15f, 0.2f, 1f);
    public Color glowColor = new Color(1f, 0.85f, 0.2f, 0.8f);

    [Header("Header Ayarları")]
    [Tooltip("Header başlık metni")]
    public string headerTitleText = "COLLECTION";
    [Tooltip("Header başlık rengi")]
    public Color headerTitleColor = Color.white;

    [Header("Varsayılan Filtre")]
    [Tooltip("Koleksiyon açıldığında hangi rarity filtresiyle başlasın")]
    public PerkRarity defaultFilter = PerkRarity.Common;

    [Header("Font")]
    [Tooltip("Tüm Collection UI textlerine uygulanacak TMP font (Alagard vb.)")]
    public TMP_FontAsset collectionFont;

    // Runtime
    private List<CollectionCardUI> cards = new List<CollectionCardUI>();
    private PerkRarity? currentFilter = null;
    private bool isOpen = false;
    private int hoveredIndex = -1;
    private Coroutine tooltipFollowCo;

    // Yeni unlock animasyon kuyruğu
    private Queue<CollectionCardUI> pendingUnlockAnims = new Queue<CollectionCardUI>();
    private bool isPlayingUnlockAnim = false;

    void Awake()
    {
        instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        if (database == null && PerkCollectionManager.instance != null)
            database = PerkCollectionManager.instance.database;
        if (database == null)
            database = Resources.Load<PerkCollectionDatabase>("PerkCollectionDatabase");

        SetupFilterButtons();
        FixLegacyMasks();
        ApplyFontToAll();
        FixTooltipTextSizing();
    }

    /// <summary>
    /// Eski Mask + şeffaf Image kurulumunu RectMask2D'ye çevirir.
    /// Mask şeffaf image ile child'ları render etmez — RectMask2D image gerektirmez.
    /// </summary>
    private void FixLegacyMasks()
    {
        if (gridContainer == null) return;

        // ScrollView ve Viewport'taki Mask'ları RectMask2D ile değiştir
        Transform scrollView = gridContainer.parent; // Viewport
        if (scrollView != null)
        {
            ReplaceMaskWithRectMask(scrollView.gameObject);       // Viewport
            if (scrollView.parent != null)
                ReplaceMaskWithRectMask(scrollView.parent.gameObject); // ScrollView
        }
    }

    private void ReplaceMaskWithRectMask(GameObject go)
    {
        Mask oldMask = go.GetComponent<Mask>();
        if (oldMask == null) return;

        // Mask'ı kaldır
        Destroy(oldMask);

        // Şeffaf Image varsa onu da kaldır (RectMask2D'ye gerek yok)
        Image img = go.GetComponent<Image>();
        if (img != null && img.color.a < 0.01f)
            Destroy(img);

        // RectMask2D ekle (yoksa)
        if (go.GetComponent<RectMask2D>() == null)
            go.AddComponent<RectMask2D>();
    }

    void Update()
    {
        // Tooltip fare takibi
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 mousePos = Input.mousePosition;
            RectTransform tooltipRT = tooltipPanel.GetComponent<RectTransform>();
            Canvas canvas = tooltipPanel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Tooltip'i farenin sağ üstüne pozisyonla, ekrandan taşmasın
                float xOffset = 20f;
                float yOffset = 10f;
                float x = mousePos.x + xOffset;
                float y = mousePos.y + yOffset;

                // Ekran sınırı kontrolü
                Vector2 size = tooltipRT.sizeDelta * canvas.scaleFactor;
                if (x + size.x > Screen.width) x = mousePos.x - xOffset - size.x;
                if (y + size.y > Screen.height) y = mousePos.y - yOffset - size.y;

                tooltipRT.position = new Vector3(x, y, 0f);
            }
        }

        // Escape ile kapat
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();

        // Unlock animasyon kuyruğu
        if (!isPlayingUnlockAnim && pendingUnlockAnims.Count > 0)
            StartCoroutine(PlayUnlockAnimation(pendingUnlockAnims.Dequeue()));
    }

    // ═══════════════════════════════════════════════════════
    //  OPEN / CLOSE
    // ═══════════════════════════════════════════════════════

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        if (database == null && PerkCollectionManager.instance != null)
            database = PerkCollectionManager.instance.database;

        // Hâlâ null ise Resources'tan yükle
        if (database == null)
            database = Resources.Load<PerkCollectionDatabase>("PerkCollectionDatabase");

        // Her açılışta varsayılan filtreyle başla
        currentFilter = defaultFilter;

        // Önce paneli aktif et (child'lar activeInHierarchy olsun)
        // Alpha sıfır — kullanıcı henüz görmez, animasyon açacak
        panelRoot.SetActive(true);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;

        BuildGrid();

        PlaySound(openClip);
        StartCoroutine(OpenAnimation());
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        HideTooltip();
        PlaySound(closeClip);
        StartCoroutine(CloseAnimation());
    }

    public void Toggle()
    {
        if (isOpen) Close(); else Open();
    }

    private IEnumerator OpenAnimation()
    {
        // Panel fade in + scale bounce
        float duration = 0.35f;
        float elapsed = 0f;

        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        panelRoot.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - t) * (1f - t); // ease out quad

            if (panelCanvasGroup != null) panelCanvasGroup.alpha = ease;
            float s = Mathf.Lerp(0.85f, 1f, ease);
            panelRoot.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        panelRoot.transform.localScale = Vector3.one;
    }

    private IEnumerator PopInCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            // Scale'ı sıfırla, coroutine büyütecek
            cards[i].transform.localScale = Vector3.zero;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            StartCoroutine(SingleCardPopIn(cards[i], i * 0.015f));
        }
        yield return null;
    }

    private IEnumerator SingleCardPopIn(CollectionCardUI card, float delay)
    {
        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Pop in with overshoot
        float dur = 0.2f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            // Elastic-ish ease
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f * (1f - t);
            s *= t;
            if (card != null) card.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (card != null) card.transform.localScale = Vector3.one;
    }

    private IEnumerator CloseAnimation()
    {
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = t * t; // ease in quad

            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f - ease;
            float s = Mathf.Lerp(1f, 0.85f, ease);
            panelRoot.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        panelRoot.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════
    //  GRID BUILD
    // ═══════════════════════════════════════════════════════

    public void BuildGrid()
    {
        // Eski kartları temizle
        foreach (var card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();

        Debug.Log($"[Collection] BuildGrid: database={database != null}, PerkCollectionManager={PerkCollectionManager.instance != null}, cardPrefab={cardPrefab != null}, gridContainer={gridContainer != null}, filter={currentFilter}");
        if (database == null || cardPrefab == null || gridContainer == null) return;

        Debug.Log($"[Collection] Database entry count: {database.entries.Count}");

        PerkCollectionManager mgr = PerkCollectionManager.instance;

        // Önce filtrelenmiş entry'leri topla ve sırala (açıklar önce, kilitliler sonra)
        var filtered = new List<(PerkCollectionData entry, BasePerk perk, bool unlocked)>();

        int skippedNull = 0, skippedNoPrefab = 0, skippedSecret = 0, skippedNoScript = 0, skippedFilter = 0;
        foreach (var entry in database.entries)
        {
            if (entry == null) { skippedNull++; continue; }
            if (entry.perkPrefab == null) { skippedNoPrefab++; continue; }

            // Secret perkleri asla gösterme
            if (entry.rarity == PerkRarity.Secret) { skippedSecret++; continue; }

            BasePerk perkScript = entry.perkPrefab.GetComponent<BasePerk>();
            if (perkScript == null) { skippedNoScript++; Debug.LogWarning($"[Collection] Missing BasePerk on prefab: {entry.perkId} ({entry.perkPrefab.name})"); continue; }

            // Filtre kontrolü
            if (currentFilter.HasValue && entry.rarity != currentFilter.Value) { skippedFilter++; continue; }

            bool unlocked = mgr != null ? mgr.IsUnlockedById(entry.perkId) : false;
            filtered.Add((entry, perkScript, unlocked));
        }
        Debug.Log($"[Collection] Filtered: {filtered.Count} shown, skipped: null={skippedNull}, noPrefab={skippedNoPrefab}, secret={skippedSecret}, noScript={skippedNoScript}, filtered={skippedFilter}");

        // Sırala: açıklar önce, kilitliler sonra
        filtered.Sort((a, b) =>
        {
            if (a.unlocked != b.unlocked) return a.unlocked ? -1 : 1;
            return 0;
        });

        int unlockedCount = 0;
        foreach (var (entry, perkScript, unlocked) in filtered)
        {
            if (unlocked) unlockedCount++;

            GameObject cardGO = Instantiate(cardPrefab, gridContainer);
            cardGO.SetActive(true);
            CollectionCardUI card = cardGO.GetComponent<CollectionCardUI>();
            if (card == null) card = cardGO.AddComponent<CollectionCardUI>();

            card.Setup(entry, perkScript, unlocked, this);
            cards.Add(card);
        }

        UpdateHeader(unlockedCount, filtered.Count);
    }

    // ═══════════════════════════════════════════════════════
    //  TOOLTIP
    // ═══════════════════════════════════════════════════════

    public void ShowTooltip(PerkCollectionData entry, BasePerk perk, bool unlocked)
    {
        if (tooltipPanel == null) return;
        tooltipPanel.SetActive(true);

        Color rarityColor = GetRarityColor(entry.rarity);

        if (unlocked)
        {
            if (tooltipName != null) { tooltipName.text = perk.perkName; tooltipName.color = rarityColor; }
            perk.RebuildDescription();
            if (tooltipDescription != null) { tooltipDescription.text = perk.renderedDescription; tooltipDescription.gameObject.SetActive(true); }
            if (tooltipLore != null)
            {
                tooltipLore.text = !string.IsNullOrEmpty(entry.loreText) ? $"<i>{entry.loreText}</i>" : "";
                tooltipLore.gameObject.SetActive(!string.IsNullOrEmpty(entry.loreText));
            }
            if (tooltipRarity != null)
            {
                tooltipRarity.text = perk.rarity.ToString().ToUpperInvariant();
                tooltipRarity.color = rarityColor;
            }
            if (tooltipIcon != null)
            {
                tooltipIcon.sprite = perk.icon;
                tooltipIcon.color = Color.white;
            }
            if (tooltipUnlockHint != null) tooltipUnlockHint.gameObject.SetActive(false);
            if (tooltipProgressBar != null) tooltipProgressBar.gameObject.SetActive(false);
            if (tooltipProgressText != null) tooltipProgressText.gameObject.SetActive(false);
        }
        else
        {
            // Kilitli — gizem havası
            if (tooltipName != null) { tooltipName.text = "???"; tooltipName.color = new Color(0.5f, 0.5f, 0.5f); }
            if (tooltipDescription != null) tooltipDescription.gameObject.SetActive(false);
            if (tooltipLore != null) tooltipLore.gameObject.SetActive(false);
            if (tooltipRarity != null)
            {
                tooltipRarity.text = perk.rarity.ToString().ToUpperInvariant();
                tooltipRarity.color = Color.Lerp(rarityColor, Color.gray, 0.6f);
            }
            if (tooltipIcon != null)
            {
                tooltipIcon.sprite = perk.icon;
                tooltipIcon.color = perk.icon != null ? lockedIconTint : Color.clear;
            }

            // Unlock koşulu ve progress bar
            if (tooltipUnlockHint != null)
            {
                string hint = !string.IsNullOrEmpty(entry.unlockHint) ? entry.unlockHint : GetDefaultHint(entry);
                tooltipUnlockHint.text = hint;
                tooltipUnlockHint.gameObject.SetActive(true);
                tooltipUnlockHint.color = new Color(1f, 0.7f, 0.3f);
            }

            if (tooltipProgressBar != null && entry.requiredAmount > 0)
            {
                int progress = PerkCollectionManager.instance != null
                    ? PerkCollectionManager.instance.GetProgress(entry.perkId)
                    : 0;
                tooltipProgressBar.gameObject.SetActive(true);
                tooltipProgressBar.maxValue = entry.requiredAmount;
                tooltipProgressBar.value = Mathf.Min(progress, entry.requiredAmount);

                if (tooltipProgressText != null)
                {
                    tooltipProgressText.gameObject.SetActive(true);
                    tooltipProgressText.text = $"{progress} / {entry.requiredAmount}";
                }
            }
            else
            {
                if (tooltipProgressBar != null) tooltipProgressBar.gameObject.SetActive(false);
                if (tooltipProgressText != null) tooltipProgressText.gameObject.SetActive(false);
            }
        }

        // Rarity glow
        if (tooltipBorderGlow != null)
        {
            tooltipBorderGlow.color = unlocked ? rarityColor : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        hoveredIndex = -1;
    }

    // ═══════════════════════════════════════════════════════
    //  UNLOCK ANIMATION (oyun içi notification'dan sonra menüde tetiklenir)
    // ═══════════════════════════════════════════════════════

    public void QueueUnlockAnimation(string perkId)
    {
        var card = cards.Find(c => c.entryData != null && c.entryData.perkId == perkId);
        if (card != null && !card.isUnlocked)
        {
            card.SetUnlocked(true);
            pendingUnlockAnims.Enqueue(card);
        }
    }

    private IEnumerator PlayUnlockAnimation(CollectionCardUI card)
    {
        isPlayingUnlockAnim = true;

        // 1. Kart titreşim efekti
        float shakeDur = 0.4f;
        float shakeElapsed = 0f;
        Vector3 originalPos = card.transform.localPosition;
        while (shakeElapsed < shakeDur)
        {
            shakeElapsed += Time.unscaledDeltaTime;
            float intensity = (1f - shakeElapsed / shakeDur) * 5f;
            card.transform.localPosition = originalPos + (Vector3)Random.insideUnitCircle * intensity;
            yield return null;
        }
        card.transform.localPosition = originalPos;

        // 2. Flash + ses
        PlaySound(unlockFanfareClip);
        if (card.glowOverlay != null)
        {
            card.glowOverlay.gameObject.SetActive(true);
            card.glowOverlay.color = new Color(1f, 1f, 1f, 1f);
        }

        // 3. Scale punch
        float punchDur = 0.3f;
        float punchElapsed = 0f;
        while (punchElapsed < punchDur)
        {
            punchElapsed += Time.unscaledDeltaTime;
            float t = punchElapsed / punchDur;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            card.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.transform.localScale = Vector3.one;

        // 4. Glow fade out (rarity rengine)
        if (card.glowOverlay != null)
        {
            Color targetGlow = GetRarityColor(card.perkRarity);
            targetGlow.a = 0.4f;
            float glowDur = 0.5f;
            float glowElapsed = 0f;
            while (glowElapsed < glowDur)
            {
                glowElapsed += Time.unscaledDeltaTime;
                float t = glowElapsed / glowDur;
                card.glowOverlay.color = Color.Lerp(Color.white, targetGlow, t);
                yield return null;
            }
        }

        // 5. Kartı renklendir (kilitli görünümden açık görünüme)
        card.RefreshVisuals();

        // Kısa bekleme (ardışık animasyonlar için)
        yield return new WaitForSecondsRealtime(0.3f);

        isPlayingUnlockAnim = false;
    }

    // ═══════════════════════════════════════════════════════
    //  FILTER
    // ═══════════════════════════════════════════════════════

    private void SetupFilterButtons()
    {
        // "TÜMÜ" butonu: aktif filtreyi kaldırır (hepsini gösterir)
        // Ama UI'da gözükmesin — deaktif et
        if (filterAllBtn != null) filterAllBtn.gameObject.SetActive(false);

        if (filterCommonBtn != null) filterCommonBtn.onClick.AddListener(() => ToggleFilter(PerkRarity.Common));
        if (filterRareBtn != null) filterRareBtn.onClick.AddListener(() => ToggleFilter(PerkRarity.Rare));
        if (filterEpicBtn != null) filterEpicBtn.onClick.AddListener(() => ToggleFilter(PerkRarity.Epic));
        if (filterLegendaryBtn != null) filterLegendaryBtn.onClick.AddListener(() => ToggleFilter(PerkRarity.Legendary));
    }

    private void ToggleFilter(PerkRarity rarity)
    {
        // Zaten bu filtredeyse bir şey yapma
        if (currentFilter.HasValue && currentFilter.Value == rarity) return;

        currentFilter = rarity;
        PlaySound(cardClickClip);
        BuildGrid();
        StartCoroutine(PopInCards());
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Tooltip'teki description ve lore text'lerini içeriğe göre otomatik boyutlandırır.
    /// Setup tool sabit yükseklik veriyor — uzun text'ler üst üste biniyor.
    /// ContentSizeFitter + LayoutElement.flexibleHeight ile düzeltir.
    /// </summary>
    private void FixTooltipTextSizing()
    {
        TMP_Text[] tooltipTexts = { tooltipDescription, tooltipLore, tooltipUnlockHint };
        foreach (var txt in tooltipTexts)
        {
            if (txt == null) continue;
            // ContentSizeFitter ekle (yoksa)
            ContentSizeFitter csf = txt.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = txt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // LayoutElement'in sabit height'ını kaldır, flexible yap
            LayoutElement le = txt.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = -1;
                le.flexibleHeight = 0;
            }
        }
    }

    /// <summary>
    /// Sahnedeki tüm Collection UI text'lerine collectionFont uygular.
    /// Header, counter, tooltip, filtre butonları dahil.
    /// </summary>
    private void ApplyFontToAll()
    {
        if (collectionFont == null) return;

        // Header
        if (headerTitle != null) headerTitle.font = collectionFont;
        if (headerCounter != null) headerCounter.font = collectionFont;

        // Tooltip
        if (tooltipName != null) tooltipName.font = collectionFont;
        if (tooltipDescription != null) tooltipDescription.font = collectionFont;
        if (tooltipLore != null) tooltipLore.font = collectionFont;
        if (tooltipRarity != null) tooltipRarity.font = collectionFont;
        if (tooltipUnlockHint != null) tooltipUnlockHint.font = collectionFont;
        if (tooltipProgressText != null) tooltipProgressText.font = collectionFont;

        // Filtre butonlarındaki text'ler
        Button[] filterBtns = { filterCommonBtn, filterRareBtn, filterEpicBtn, filterLegendaryBtn };
        foreach (var btn in filterBtns)
        {
            if (btn == null) continue;
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.font = collectionFont;
        }
    }

    /// <summary>Tek bir karta collectionFont uygular.</summary>
    public void ApplyFontToCard(TMP_Text label)
    {
        if (collectionFont != null && label != null)
            label.font = collectionFont;
    }

    private void UpdateHeader(int unlocked, int total)
    {
        // Secret perkleri sayıdan çıkar
        int globalUnlocked = 0;
        int globalTotal = 0;
        if (PerkCollectionManager.instance != null && database != null)
        {
            foreach (var entry in database.entries)
            {
                if (entry == null || entry.rarity == PerkRarity.Secret) continue;
                globalTotal++;
                if (PerkCollectionManager.instance.IsUnlockedById(entry.perkId)) globalUnlocked++;
            }
        }

        if (headerTitle != null)
        {
            headerTitle.text = headerTitleText;
            headerTitle.color = headerTitleColor;
        }

        if (headerCounter != null)
            headerCounter.text = $"{globalUnlocked} / {globalTotal}";
    }

    public static string GetDefaultHint(PerkCollectionData entry)
    {
        switch (entry.unlockCondition)
        {
            case UnlockCondition.KillEnemies: return $"Kill {entry.requiredAmount} enemies in total";
            case UnlockCondition.CompleteRuns: return $"Complete {entry.requiredAmount} runs";
            case UnlockCondition.WinRuns: return $"Win {entry.requiredAmount} runs";
            case UnlockCondition.CollectGold: return $"Collect {entry.requiredAmount} gold in total";
            case UnlockCondition.DefeatBoss: return "Defeat a boss";
            case UnlockCondition.UseSpecificPerk: return $"Use the \"{entry.conditionParam}\" perk";
            case UnlockCondition.ClearLevels: return $"Clear {entry.requiredAmount} rooms in total";
            case UnlockCondition.KillEnemiesSingleRun: return $"Kill {entry.requiredAmount} enemies in a single run";
            case UnlockCondition.UnlockOtherPerks: return $"Unlock {entry.requiredAmount} different perks";
            case UnlockCondition.PushEnemyIntoSpike: return $"Push enemies into spikes {entry.requiredAmount} times";
            case UnlockCondition.KillBeforeBoss: return $"Kill {entry.requiredAmount} enemies before reaching the boss";
            case UnlockCondition.HoldGold: return $"Hold {entry.requiredAmount} gold at once";
            case UnlockCondition.MoveTiles: return $"Walk {entry.requiredAmount} tiles in total";
            case UnlockCondition.SkipTurns: return $"Skip {entry.requiredAmount} turns in total";
            default: return "";
        }
    }

    public static Color GetRarityColor(PerkRarity rarity)
    {
        switch (rarity)
        {
            case PerkRarity.Common: return new Color(0.75f, 0.75f, 0.75f);
            case PerkRarity.Rare: return new Color(0.2f, 0.5f, 1f);
            case PerkRarity.Epic: return new Color(0.6f, 0.2f, 1f);
            case PerkRarity.Legendary: return new Color(1f, 0.6f, 0f);
            case PerkRarity.Secret: return new Color(1f, 0.27f, 0.27f);
            default: return Color.white;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.instance != null)
        {
            // AudioManager'ın ses sistemi üzerinden çal
            AudioSource src = AudioManager.instance.GetComponent<AudioSource>();
            if (src != null) src.PlayOneShot(clip, 0.8f);
        }
    }
}

// ═══════════════════════════════════════════════════════════
//  TEK KART UI COMPONENT
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Grid'deki tek bir perk kartı. Hover, click, lock/unlock görünümü.
/// </summary>
public class CollectionCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [HideInInspector] public PerkCollectionData entryData;
    [HideInInspector] public BasePerk perkRef;
    [HideInInspector] public bool isUnlocked;
    [HideInInspector] public PerkRarity perkRarity;
    [HideInInspector] public CollectionUI parentUI;

    // Child referansları (Setup sırasında bulunur)
    public Image cardBackground;
    public Image iconImage;
    public Image rarityStripe;
    public Image glowOverlay;
    public Image lockIcon;
    public TMP_Text nameLabel;
    public GameObject newBadge; // "YENİ!" rozeti

    private bool isHovered = false;
    private Coroutine hoverAnim;
    private Coroutine idlePulseCo;
    private Vector3 baseScale = Vector3.one;

    public void Setup(PerkCollectionData entry, BasePerk perk, bool unlocked, CollectionUI ui)
    {
        entryData = entry;
        perkRef = perk;
        isUnlocked = unlocked;
        perkRarity = entry.rarity; // Prefab'daki değil, database'deki rarity'i kullan
        parentUI = ui;

        // Child'ları bul (tag yerine isimle)
        FindChildren();
        RefreshVisuals();

        // Font uygula
        if (parentUI != null) parentUI.ApplyFontToCard(nameLabel);

        // Idle pulse coroutine'i sadece obje aktifse başlat
        // (deaktifse OnEnable'da başlayacak)
    }

    void OnEnable()
    {
        if (isUnlocked && glowOverlay != null && idlePulseCo == null)
            idlePulseCo = StartCoroutine(IdlePulse());
    }

    void OnDisable()
    {
        if (idlePulseCo != null)
        {
            StopCoroutine(idlePulseCo);
            idlePulseCo = null;
        }
    }

    private void FindChildren()
    {
        // cardPrefab içindeki child'ları isimle bul
        Transform t = transform;
        foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
        {
            switch (child.name)
            {
                case "CardBG": cardBackground = child.GetComponent<Image>(); break;
                case "Icon": iconImage = child.GetComponent<Image>(); break;
                case "RarityStripe": rarityStripe = child.GetComponent<Image>(); break;
                case "GlowOverlay": glowOverlay = child.GetComponent<Image>(); break;
                case "LockIcon": lockIcon = child.GetComponent<Image>(); break;
                case "NameLabel": nameLabel = child.GetComponent<TMP_Text>(); break;
                case "NewBadge": newBadge = child.gameObject; break;
            }
        }
    }

    public void RefreshVisuals()
    {
        Color rarityColor = CollectionUI.GetRarityColor(perkRarity);

        if (isUnlocked)
        {
            // Açık: renkli ikon, rarity border, isim
            if (iconImage != null)
            {
                if (perkRef.icon != null)
                {
                    iconImage.sprite = perkRef.icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = rarityColor * 0.5f; // İkon yoksa rarity renginde placeholder
                }
            }
            if (cardBackground != null) cardBackground.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            if (rarityStripe != null) { rarityStripe.color = rarityColor; rarityStripe.gameObject.SetActive(true); }
            if (lockIcon != null) lockIcon.gameObject.SetActive(false);
            if (nameLabel != null) { nameLabel.text = perkRef.perkName; nameLabel.color = rarityColor; }
            if (glowOverlay != null) { glowOverlay.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.08f); }
        }
        else
        {
            // Kilitli: siluet, koyu, kilit ikonu
            if (iconImage != null)
            {
                if (perkRef.icon != null)
                {
                    iconImage.sprite = perkRef.icon;
                    iconImage.color = parentUI != null ? parentUI.lockedIconTint : new Color(0.15f, 0.15f, 0.2f, 1f);
                }
                else
                {
                    // Sprite yoksa tamamen saydam yap
                    iconImage.sprite = null;
                    iconImage.color = Color.clear;
                }
            }
            if (cardBackground != null) cardBackground.color = parentUI != null ? parentUI.lockedTint : new Color(0.08f, 0.08f, 0.12f, 1f);
            if (rarityStripe != null) { rarityStripe.color = new Color(0.2f, 0.2f, 0.2f, 0.4f); rarityStripe.gameObject.SetActive(true); }
            if (lockIcon != null) lockIcon.gameObject.SetActive(lockIcon.sprite != null);
            if (nameLabel != null) { nameLabel.text = "???"; nameLabel.color = new Color(0.4f, 0.4f, 0.4f); }
            if (glowOverlay != null) glowOverlay.color = new Color(0, 0, 0, 0);
        }

        // NewBadge — açılmış ama henüz görülmemiş perklerde ünlem göster
        bool isNew = isUnlocked && entryData != null
            && PlayerPrefs.GetInt($"perk_seen_{entryData.perkId}", 0) == 0;
        if (isNew && newBadge == null)
            CreateNewBadge();
        if (newBadge != null)
            newBadge.SetActive(isNew);
    }

    public void SetUnlocked(bool val)
    {
        isUnlocked = val;
    }

    private void MarkAsSeen()
    {
        if (!isUnlocked || entryData == null) return;
        if (PlayerPrefs.GetInt($"perk_seen_{entryData.perkId}", 0) == 1) return;
        PlayerPrefs.SetInt($"perk_seen_{entryData.perkId}", 1);
        PlayerPrefs.Save();
        if (newBadge != null) newBadge.SetActive(false);
    }

    private void CreateNewBadge()
    {
        newBadge = new GameObject("NewBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        newBadge.transform.SetParent(transform, false);
        RectTransform rt = newBadge.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-2f, -2f);
        rt.sizeDelta = new Vector2(28f, 28f);

        Image bg = newBadge.GetComponent<Image>();
        bg.color = new Color(1f, 0.3f, 0.1f, 1f);
        bg.raycastTarget = false;

        // "!" text
        GameObject textGO = new GameObject("ExcText", typeof(RectTransform), typeof(CanvasRenderer));
        textGO.transform.SetParent(newBadge.transform, false);
        TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = "!";
        txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;
        if (parentUI != null && parentUI.collectionFont != null)
            txt.font = parentUI.collectionFont;
        RectTransform txtRT = textGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
    }

    // ── Hover / Click ──────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (parentUI != null)
            parentUI.ShowTooltip(entryData, perkRef, isUnlocked);

        // Yeni perk rozetini kaldır
        MarkAsSeen();

        // Scale up
        if (hoverAnim != null) StopCoroutine(hoverAnim);
        hoverAnim = StartCoroutine(HoverScale(1.12f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (parentUI != null)
            parentUI.HideTooltip();

        // Scale down
        if (hoverAnim != null) StopCoroutine(hoverAnim);
        hoverAnim = StartCoroutine(HoverScale(1f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Tıklanınca hafif "punch" efekti + ses
        if (hoverAnim != null) StopCoroutine(hoverAnim);
        hoverAnim = StartCoroutine(ClickPunch());
    }

    private IEnumerator HoverScale(float target)
    {
        float speed = 8f;
        while (Mathf.Abs(transform.localScale.x - target) > 0.01f)
        {
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * speed);
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = new Vector3(target, target, 1f);
    }

    private IEnumerator ClickPunch()
    {
        // Küçült → büyüt
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float s = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.08f;
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        float target = isHovered ? 1.12f : 1f;
        transform.localScale = new Vector3(target, target, 1f);
    }

    private IEnumerator IdlePulse()
    {
        // Açık perklerin glow'u hafifçe pulse eder
        if (glowOverlay == null) yield break;
        Color baseColor = glowOverlay.color;
        while (true)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 1.5f + transform.GetSiblingIndex() * 0.3f) * 0.04f;
            Color c = baseColor;
            c.a = Mathf.Max(0, baseColor.a + pulse);
            glowOverlay.color = c;
            yield return null;
        }
    }
}
