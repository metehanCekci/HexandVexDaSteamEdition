using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Perk envanter yan paneli — map ekranında her zaman görünür.
/// Drag & drop ile aktif slotlar ve stash arasında perk yönetimi.
/// Açılış/kapanış, slot geçiş, hover animasyonları dahil.
/// </summary>
public class PerkInventoryUI : MonoBehaviour
{
    public static PerkInventoryUI instance;

    public bool IsOpen => canvasGO != null && canvasGO.activeSelf;

    [Header("Referanslar (Editor Tool Atar)")]
    public GameObject canvasGO;
    public GameObject panelRoot;
    public RectTransform activeSlotsContainer;
    public RectTransform stashGrid;
    public TextMeshProUGUI activeTitleText;
    public TextMeshProUGUI stashTitleText;

    [Header("Tooltip (Editor Tool Atar)")]
    public GameObject tooltipObj;
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipLevelText;
    public TextMeshProUGUI tooltipDescText;
    public CanvasGroup tooltipCanvasGroup;

    [Header("Stash (Editor Tool Atar)")]
    public GameObject stashSection;

    // Spawnlanan slot objeleri
    private readonly List<GameObject> spawnedActiveSlots = new List<GameObject>();
    private readonly List<GameObject> spawnedStashSlots = new List<GameObject>();

    // Stash scroll
    private SimpleScrollArea stashScroll;

    // Drag state
    private GameObject dragGhost;
    private bool isDragging;
    private bool dragIsActiveSlot;
    private int dragIndex;
    private Canvas rootCanvas;

    // Animasyon state
    private Coroutine stashExpandCoroutine;
    private Coroutine tooltipFadeCoroutine;
    private bool stashWasOpen;

    // Campfire upgrade mode — tıklanan perk upgrade olur
    private System.Action<BasePerk> upgradeCallback;
    public bool IsUpgradeMode => upgradeCallback != null;

    // Fly animasyonu state — perk taşınırken eski pozisyondan yenisine uçar
    private BasePerk flyingPerk;
    private Vector3 flyStartWorldPos;
    private Sprite flyingSprite;
    private Color flyingRarityColor;
    private const float FLY_DURATION = 0.3f;

    private const float SLOT_SIZE = 56f;
    private const float SLOT_SPACING = 6f;
    private const float PANEL_WIDTH = 280f;

    // Animasyon sabitleri
    private const float SLOT_STAGGER_DELAY = 0.04f;
    private const float SLOT_POP_DURATION = 0.25f;
    private const float STASH_EXPAND_DURATION = 0.25f;
    private const float TOOLTIP_FADE_DURATION = 0.12f;
    private const float HOVER_SCALE = 1.08f;
    private const float HOVER_LERP_SPEED = 12f;


    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Scene'deki objenin referansları boşsa BuildUI ile oluştur
        // böylece Show()'da tekrar oluşturup duplikasyon yapmaz
        if (canvasGO == null) BuildUI();
    }

    void Start()
    {
        if (canvasGO != null) canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        // Ghost mouse takibi + hafif dönme
        if (isDragging && dragGhost != null)
        {
            // Güvenlik: mouse bırakıldıysa ama EndDrag tetiklenmediyse iptal et
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                CancelDrag();
                RefreshUI();
                return;
            }

            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform, Input.mousePosition, null, out pos);
            RectTransform ghostRT = dragGhost.GetComponent<RectTransform>();
            ghostRT.anchoredPosition = pos;

            // Hafif sallanma
            float wobble = Mathf.Sin(Time.unscaledTime * 8f) * 3f;
            ghostRT.localRotation = Quaternion.Euler(0f, 0f, wobble);
        }
    }

    public static void CreateFromCode()
    {
        if (instance != null) return;
        var existing = Object.FindFirstObjectByType<PerkInventoryUI>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }
        GameObject rootGO = new GameObject("PerkInventoryUI");
        PerkInventoryUI ui = rootGO.AddComponent<PerkInventoryUI>();
        ui.BuildUI();
    }

    // ======================================================
    // GÖSTER / GİZLE — animasyonlu
    // ======================================================

    public void Show()
    {
        if (canvasGO == null) BuildUI();

        // Eski stash yapisi scroll desteklemiyorsa yeniden olustur
        if (stashScroll == null && stashSection != null)
        {
            Destroy(stashSection);
            stashSection = null;
            RebuildStashSection();
        }

        canvasGO.SetActive(true);
        rootCanvas = canvasGO.GetComponent<Canvas>();

        // Canvas ayarlarını her Show()'da garantile — sahne geçişlerinde bozulabilir
        if (rootCanvas != null)
        {
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 100;
        }
        // GraphicRaycaster yoksa ekle — hover/drag için gerekli
        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        stashWasOpen = false;
        RefreshUI();

        // Slide animasyonu yok — direkt göster
        if (panelRoot != null)
        {
            RectTransform panelRT = panelRoot.GetComponent<RectTransform>();
            panelRT.anchoredPosition = new Vector2(-10f, -10f);
            CanvasGroup panelCG = panelRoot.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = panelRoot.AddComponent<CanvasGroup>();
            panelCG.alpha = 1f;
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
        }
    }

    public void Hide()
    {
        CancelDrag();
        HideTooltip();
        ExitUpgradeMode();
        if (canvasGO == null) return;

        canvasGO.SetActive(false);
    }

    /// <summary>
    /// Campfire upgrade modu — tiklanan perk upgrade olur.
    /// Sadece upgradeable (currentLevel < maxLevel) perkler tiklanabilir.
    /// </summary>
    public void EnterUpgradeMode(System.Action<BasePerk> callback)
    {
        upgradeCallback = callback;
        RefreshUI();
    }

    public void ExitUpgradeMode()
    {
        upgradeCallback = null;
    }

    // PanelSlideIn / PanelSlideOut kaldırıldı — panel artık animasyonsuz açılıp kapanıyor.

    // ======================================================
    // SLOT ANIMASYONLARI
    // ======================================================

    private IEnumerator StaggerSlotPopIn(List<GameObject> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                StartCoroutine(SlotPopIn(slots[i]));
            }
            yield return new WaitForSecondsRealtime(SLOT_STAGGER_DELAY);
        }
    }

    private IEnumerator SlotPopIn(GameObject slotGO)
    {
        if (slotGO == null) yield break;
        RectTransform rt = slotGO.GetComponent<RectTransform>();
        CanvasGroup cg = slotGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = slotGO.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        rt.localScale = Vector3.one * 0.5f;

        float popDur = 0.12f;
        float elapsed = 0f;
        while (elapsed < popDur)
        {
            if (slotGO == null) yield break;
            float t = elapsed / popDur;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f * (1f - t);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, scale, EaseOutBack(t));
            cg.alpha = Mathf.Clamp01(t * 3f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = Vector3.one;
        cg.alpha = 1f;
    }

    private IEnumerator SlotLandEffect(GameObject slotGO, Color rarityColor)
    {
        if (slotGO == null) yield break;
        RectTransform rt = slotGO.GetComponent<RectTransform>();

        // Bounce
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            if (slotGO == null) yield break;
            float t = elapsed / duration;
            float bounce = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * (1f - t);
            rt.localScale = Vector3.one * bounce;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = Vector3.one;

        // Rarity flash — ikon'un Image'ına kısa renk flash
        Transform iconTr = slotGO.transform.Find("Icon");
        if (iconTr != null)
        {
            Image img = iconTr.GetComponent<Image>();
            if (img != null)
            {
                Color orig = img.color;
                Color flash = Color.Lerp(orig, rarityColor, 0.6f);
                elapsed = 0f;
                float flashDur = 0.2f;
                while (elapsed < flashDur)
                {
                    if (slotGO == null || img == null) yield break;
                    img.color = Color.Lerp(flash, orig, elapsed / flashDur);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                img.color = orig;
            }
        }
    }

    // ======================================================
    // FLY ANIMASYONU — perk eski slottan yeni slota uçar
    // ======================================================

    /// <summary>Dışarıdan (ActivePerkBar gibi) fly animasyonu başlatmak için.</summary>
    public void BeginFlyAnimExternal(BasePerk perk, GameObject sourceIconGO)
    {
        BeginFlyAnim(perk, sourceIconGO);
    }

    /// <summary>Taşıma öncesi çağır — uçuş başlangıç bilgilerini kaydeder.</summary>
    private void BeginFlyAnim(BasePerk perk, GameObject sourceIconGO)
    {
        if (perk == null || sourceIconGO == null) { flyingPerk = null; return; }
        flyingPerk = perk;
        flyStartWorldPos = sourceIconGO.transform.position;
        flyingSprite = perk.icon;
        ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out flyingRarityColor);
    }

    /// <summary>RefreshUI sonrası çağır — hedef slot'u bulup uçuş animasyonu başlatır.</summary>
    private void TryStartFlyAnim()
    {
        if (flyingPerk == null || canvasGO == null) return;

        // Hedef slot'u bul — aktif mi stash'te mi?
        GameObject targetSlot = FindSlotForPerk(flyingPerk);
        BasePerk perk = flyingPerk;
        flyingPerk = null; // bir kere kullan

        if (targetSlot == null) return;

        Transform iconTr = targetSlot.transform.Find("Icon");
        if (iconTr == null) return;

        // Hedef ikonu geçici gizle
        CanvasGroup targetCG = iconTr.GetComponent<CanvasGroup>();
        if (targetCG == null) targetCG = iconTr.gameObject.AddComponent<CanvasGroup>();
        targetCG.alpha = 0f;

        // 1 frame bekleyip layout'un oturmasını sağla, sonra fly başlat
        StartCoroutine(DelayedFlyStart(targetCG, iconTr));
    }

    private IEnumerator DelayedFlyStart(CanvasGroup targetCG, Transform iconTr)
    {
        // Layout'un tam oturması için 2 frame bekle
        yield return null;
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();

        Vector3 targetWorldPos = iconTr.position;
        StartCoroutine(FlyIconCoroutine(flyStartWorldPos, targetWorldPos, targetCG));
    }

    private IEnumerator FlyIconCoroutine(Vector3 startWorld, Vector3 endWorld, CanvasGroup targetCG)
    {
        // Canvas üzerinde geçici ikon oluştur
        GameObject flyGO = new GameObject("FlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flyGO.transform.SetParent(canvasGO.transform, false);
        flyGO.transform.SetAsLastSibling();

        RectTransform flyRT = flyGO.GetComponent<RectTransform>();
        flyRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);

        Image flyImg = flyGO.GetComponent<Image>();
        if (flyingSprite != null)
        {
            flyImg.sprite = flyingSprite;
            flyImg.color = Color.white;
        }
        else
        {
            flyImg.color = new Color(0.35f, 0.35f, 0.4f, 0.8f);
        }
        flyImg.raycastTarget = false;

        Outline outline = flyGO.AddComponent<Outline>();
        outline.effectColor = flyingRarityColor;
        outline.effectDistance = new Vector2(2f, 2f);

        // Hedef ikonun Transform'unu tut — her frame güncel pozisyonunu takip et
        // (stash expand animasyonu sırasında pozisyon değişebilir)
        Transform targetIconTr = targetCG != null ? targetCG.transform : null;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

        float elapsed = 0f;
        while (elapsed < FLY_DURATION)
        {
            if (flyGO == null) yield break;
            float t = elapsed / FLY_DURATION;
            float ease = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic

            // Hedef pozisyonu her frame güncelle
            Vector3 currentEnd = (targetIconTr != null) ? targetIconTr.position : endWorld;
            Vector3 currentWorld = Vector3.Lerp(startWorld, currentEnd, ease);
            Vector3 localPos = canvasRT.InverseTransformPoint(currentWorld);
            flyRT.localPosition = localPos;

            // Hafif scale efekti
            float scale = Mathf.Lerp(1.15f, 1f, ease);
            flyRT.localScale = Vector3.one * scale;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Varış — hedef ikonu göster, ghost'u sil, pop efekti
        if (targetCG != null)
        {
            targetCG.alpha = 1f;
            // Varış pop animasyonu
            RectTransform slotRT = targetCG.transform.parent != null
                ? targetCG.transform.parent.GetComponent<RectTransform>()
                : targetCG.GetComponent<RectTransform>();
            if (slotRT != null)
                StartCoroutine(ArrivalPop(slotRT));
        }
        if (flyGO != null) Destroy(flyGO);
    }

    /// <summary>Slot'a varış anında ani pop efekti</summary>
    private IEnumerator ArrivalPop(RectTransform rt)
    {
        if (rt == null) yield break;
        float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (rt == null) yield break;
            float t = elapsed / duration;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            rt.localScale = new Vector3(s, s, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    /// <summary>Belirli bir perkin inventory slot'unda pop animasyonu tetikler.</summary>
    public void TriggerPopForPerk(BasePerk perk)
    {
        if (perk == null) return;
        GameObject slot = FindSlotForPerk(perk);
        if (slot != null)
            StartCoroutine(ArrivalPop(slot.GetComponent<RectTransform>()));
    }

    /// <summary>Perk'in şu an hangi slot'ta olduğunu bulur.</summary>
    private GameObject FindSlotForPerk(BasePerk perk)
    {
        if (RunManager.instance == null) return null;

        // Aktif slotlarda ara
        for (int i = 0; i < RunManager.instance.activePerks.Count; i++)
        {
            if (RunManager.instance.activePerks[i] == perk && i < spawnedActiveSlots.Count)
                return spawnedActiveSlots[i];
        }

        // Stash'te ara
        for (int i = 0; i < RunManager.instance.inventoryPerks.Count; i++)
        {
            if (RunManager.instance.inventoryPerks[i] == perk && i < spawnedStashSlots.Count)
                return spawnedStashSlots[i];
        }

        return null;
    }

    // ======================================================
    // STASH EXPAND/COLLAPSE ANIMASYONU
    // ======================================================

    private IEnumerator StashExpand(bool expanding)
    {
        if (stashSection == null) yield break;

        CanvasGroup cg = stashSection.GetComponent<CanvasGroup>();
        if (cg == null) cg = stashSection.AddComponent<CanvasGroup>();

        // LayoutElement ile yüksekliği kontrol et — ContentSizeFitter paneli smooth büyütür
        LayoutElement sle = stashSection.GetComponent<LayoutElement>();
        if (sle == null) sle = stashSection.AddComponent<LayoutElement>();

        if (expanding)
        {
            // Önce aktif et ama yükseklik 0 — panel henüz büyümez
            sle.preferredHeight = 0f;
            sle.flexibleHeight = -1f;
            cg.alpha = 0f;
            stashSection.SetActive(true);

            // Hedef yüksekliği hesapla: layout'un gerçek boyutunu al
            LayoutRebuilder.ForceRebuildLayoutImmediate(stashSection.GetComponent<RectTransform>());
            // stashSection child'larının toplam preferred height'ı
            float targetH = LayoutUtility.GetPreferredHeight(stashSection.GetComponent<RectTransform>());
            // LayoutElement override ettiği için gerçek değeri almak lazım — geçici kaldır
            sle.preferredHeight = -1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(stashSection.GetComponent<RectTransform>());
            targetH = LayoutUtility.GetPreferredHeight(stashSection.GetComponent<RectTransform>());
            sle.preferredHeight = 0f;

            float elapsed = 0f;
            float dur = STASH_EXPAND_DURATION * 1.2f; // biraz daha uzun — smooth
            while (elapsed < dur)
            {
                float t = elapsed / dur;
                float ease = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                sle.preferredHeight = Mathf.Lerp(0f, targetH, ease);
                cg.alpha = Mathf.Clamp01(t * 2.5f);
                LayoutRebuilder.MarkLayoutForRebuild(panelRoot.GetComponent<RectTransform>());
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            sle.preferredHeight = -1f; // LayoutElement override'ı kaldır — doğal boyut
            cg.alpha = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot.GetComponent<RectTransform>());
        }
        else
        {
            // Mevcut yüksekliği al
            float startH = stashSection.GetComponent<RectTransform>().rect.height;
            sle.preferredHeight = startH;

            float elapsed = 0f;
            float dur = STASH_EXPAND_DURATION * 0.8f;
            while (elapsed < dur)
            {
                float t = elapsed / dur;
                float ease = t * t; // EaseInQuad
                sle.preferredHeight = Mathf.Lerp(startH, 0f, ease);
                cg.alpha = 1f - t;
                LayoutRebuilder.MarkLayoutForRebuild(panelRoot.GetComponent<RectTransform>());
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            sle.preferredHeight = -1f;
            stashSection.SetActive(false);
            cg.alpha = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot.GetComponent<RectTransform>());
        }
    }

    /// <summary>Stash kapanırken: mevcut slot'lar görünürken smooth küçült, sonra temizle.</summary>
    private IEnumerator StashCollapseAndClear()
    {
        if (stashSection == null) yield break;

        CanvasGroup cg = stashSection.GetComponent<CanvasGroup>();
        if (cg == null) cg = stashSection.AddComponent<CanvasGroup>();

        LayoutElement sle = stashSection.GetComponent<LayoutElement>();
        if (sle == null) sle = stashSection.AddComponent<LayoutElement>();

        float startH = stashSection.GetComponent<RectTransform>().rect.height;
        sle.preferredHeight = startH;

        float elapsed = 0f;
        float dur = STASH_EXPAND_DURATION * 0.8f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float ease = t * t; // EaseInQuad
            sle.preferredHeight = Mathf.Lerp(startH, 0f, ease);
            cg.alpha = 1f - t;
            LayoutRebuilder.MarkLayoutForRebuild(panelRoot.GetComponent<RectTransform>());
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Animasyon bitti — temizle
        ClearSlots(spawnedStashSlots);
        sle.preferredHeight = -1f;
        stashSection.SetActive(false);
        cg.alpha = 1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot.GetComponent<RectTransform>());
    }

    // ======================================================
    // DRAG & DROP
    // ======================================================

    private void BeginDrag(bool isActiveSlot, int index, BasePerk perk, PointerEventData eventData)
    {
        if (perk == null) return;

        HideTooltip();
        isDragging = true;
        dragIsActiveSlot = isActiveSlot;
        dragIndex = index;

        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragGhost.transform.SetParent(canvasGO.transform, false);
        RectTransform ghostRT = dragGhost.GetComponent<RectTransform>();
        ghostRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        ghostRT.localScale = Vector3.one * 1.15f;

        Image ghostImg = dragGhost.GetComponent<Image>();
        if (perk.icon != null)
        {
            ghostImg.sprite = perk.icon;
            ghostImg.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            ghostImg.color = new Color(0.5f, 0.5f, 0.6f, 0.85f);
        }
        ghostImg.raycastTarget = false;

        Color rarityColor;
        ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out rarityColor);
        Outline outline = dragGhost.AddComponent<Outline>();
        outline.effectColor = rarityColor;
        outline.effectDistance = new Vector2(3f, 3f);

        // Glow efekti — ghost arkasında hafif parlama
        Shadow glow = dragGhost.AddComponent<Shadow>();
        glow.effectColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.4f);
        glow.effectDistance = new Vector2(4f, -4f);

        dragGhost.transform.SetAsLastSibling();
        HighlightSourceSlot(true);

        // Ghost pick-up pop
        StartCoroutine(GhostPickupAnim(ghostRT));
    }

    private IEnumerator GhostPickupAnim(RectTransform rt)
    {
        float elapsed = 0f;
        float dur = 0.15f;
        while (elapsed < dur)
        {
            if (rt == null) yield break;
            float t = elapsed / dur;
            float s = Mathf.Lerp(0.8f, 1.15f, EaseOutBack(t));
            rt.localScale = Vector3.one * s;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one * 1.15f;
    }

    private void EndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool handled = false;
        int targetSlotIndex = -1;
        bool targetIsActive = false;

        foreach (var result in results)
        {
            PerkSlotTag tag = result.gameObject.GetComponent<PerkSlotTag>();
            if (tag == null) tag = result.gameObject.GetComponentInParent<PerkSlotTag>();
            if (tag != null)
            {
                targetSlotIndex = tag.slotIndex;
                targetIsActive = tag.isActiveSlot;
                HandleDrop(tag.isActiveSlot, tag.slotIndex);
                handled = true;
                break;
            }
        }

        if (!handled && dragIsActiveSlot && RunManager.instance != null)
        {
            if (dragIndex < RunManager.instance.activePerks.Count && RunManager.instance.activePerks.Count > 1)
            {
                RunManager.instance.MoveToInventory(dragIndex);
            }
        }

        CancelDrag();
        RefreshUI();

        // Drop land efekti
        if (handled && targetSlotIndex >= 0)
        {
            List<GameObject> targetList = targetIsActive ? spawnedActiveSlots : spawnedStashSlots;
            if (targetSlotIndex < targetList.Count && targetList[targetSlotIndex] != null)
            {
                BasePerk landedPerk = null;
                if (targetIsActive && RunManager.instance != null && targetSlotIndex < RunManager.instance.activePerks.Count)
                    landedPerk = RunManager.instance.activePerks[targetSlotIndex];

                Color rc = Color.white;
                if (landedPerk != null)
                    ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(landedPerk.rarity), out rc);

                StartCoroutine(SlotLandEffect(targetList[targetSlotIndex], rc));
            }
        }
    }

    private void HandleDrop(bool targetIsActive, int targetIndex)
    {
        if (RunManager.instance == null) return;
        if (dragIsActiveSlot == targetIsActive && dragIndex == targetIndex) return;

        if (dragIsActiveSlot && targetIsActive)
        {
            SwapActivePerks(dragIndex, targetIndex);
        }
        else if (dragIsActiveSlot && !targetIsActive)
        {
            if (targetIndex < RunManager.instance.inventoryPerks.Count)
                RunManager.instance.SwapPerk(dragIndex, targetIndex);
            else if (RunManager.instance.activePerks.Count > 1)
                RunManager.instance.MoveToInventory(dragIndex);
        }
        else if (!dragIsActiveSlot && targetIsActive)
        {
            if (targetIndex < RunManager.instance.activePerks.Count)
                RunManager.instance.SwapPerk(targetIndex, dragIndex);
            else if (RunManager.instance.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
                RunManager.instance.MoveToActive(dragIndex);
        }
        else
        {
            SwapStashPerks(dragIndex, targetIndex);
        }
    }

    private void SwapActivePerks(int indexA, int indexB)
    {
        if (RunManager.instance == null) return;
        var perks = RunManager.instance.activePerks;
        if (indexA < 0 || indexA >= perks.Count || indexB < 0 || indexB >= perks.Count) return;
        var temp = perks[indexA];
        perks[indexA] = perks[indexB];
        perks[indexB] = temp;
        UpdatePerkPriorities();
        RunManager.instance.RefreshPerkUI();
    }

    private void SwapStashPerks(int indexA, int indexB)
    {
        if (RunManager.instance == null) return;
        var perks = RunManager.instance.inventoryPerks;
        if (indexA < 0 || indexA >= perks.Count || indexB < 0 || indexB >= perks.Count) return;
        var temp = perks[indexA];
        perks[indexA] = perks[indexB];
        perks[indexB] = temp;
    }

    private void UpdatePerkPriorities()
    {
        if (RunManager.instance == null) return;
        for (int i = 0; i < RunManager.instance.activePerks.Count; i++)
        {
            if (RunManager.instance.activePerks[i] != null)
                RunManager.instance.activePerks[i].priority = i;
        }
    }

    private void CancelDrag()
    {
        HighlightSourceSlot(false);
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
        isDragging = false;
    }

    private void HighlightSourceSlot(bool dim)
    {
        List<GameObject> list = dragIsActiveSlot ? spawnedActiveSlots : spawnedStashSlots;
        if (dragIndex >= 0 && dragIndex < list.Count && list[dragIndex] != null)
        {
            CanvasGroup cg = list[dragIndex].GetComponent<CanvasGroup>();
            if (cg == null) cg = list[dragIndex].AddComponent<CanvasGroup>();
            cg.alpha = dim ? 0.35f : 1f;
        }
    }

    // ======================================================
    // UI OLUŞTURMA
    // ======================================================

    public void BuildUI()
    {
        canvasGO = new GameObject("PerkSidePanelCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 91;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        panelRoot = new GameObject("SidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta = new Vector2(PANEL_WIDTH, 0f);
        panelRoot.GetComponent<Image>().color = new Color(0f, 0.0196f, 0.047f, 0.88f);

        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter panelCSF = panelRoot.AddComponent<ContentSizeFitter>();
        panelCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Başlık
        MakeLabel("Title", panelRoot.transform, "PERKS", 20, TextAlignmentOptions.Center, Color.white, 28f);

        // Active başlık
        activeTitleText = MakeLabel("ActiveTitle", panelRoot.transform, "ACTIVE (0/6)", 14, TextAlignmentOptions.Left, Color.white, 20f);

        // Active slots grid
        float activeGridH = (SLOT_SIZE + 20f) * 2 + SLOT_SPACING + 4f;
        GameObject activeContainerGO = new GameObject("ActiveSlots", typeof(RectTransform));
        activeContainerGO.transform.SetParent(panelRoot.transform, false);
        activeSlotsContainer = activeContainerGO.GetComponent<RectTransform>();
        GridLayoutGroup activeGLG = activeContainerGO.AddComponent<GridLayoutGroup>();
        activeGLG.cellSize = new Vector2(SLOT_SIZE + 20f, SLOT_SIZE + 20f);
        activeGLG.spacing = new Vector2(SLOT_SPACING, SLOT_SPACING);
        activeGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        activeGLG.constraintCount = 3;
        activeGLG.childAlignment = TextAnchor.UpperCenter;
        var activeLE = activeContainerGO.AddComponent<LayoutElement>();
        activeLE.preferredHeight = activeGridH;

        // Ayraç
        MakeSeparator(panelRoot.transform);

        // Stash bölümü
        stashSection = new GameObject("StashSection", typeof(RectTransform));
        stashSection.transform.SetParent(panelRoot.transform, false);

        VerticalLayoutGroup stashVLG = stashSection.AddComponent<VerticalLayoutGroup>();
        stashVLG.spacing = 4f;
        stashVLG.childControlWidth = true;
        stashVLG.childControlHeight = true;
        stashVLG.childForceExpandWidth = true;
        stashVLG.childForceExpandHeight = false;

        stashTitleText = MakeLabel("StashTitle", stashSection.transform, "STASH", 14, TextAlignmentOptions.Left, new Color(0.7f, 0.7f, 0.7f), 20f);

        BuildStashGrid(stashSection.transform);

        stashSection.SetActive(false);

        // Tooltip
        BuildTooltip(canvasGO.transform);

        canvasGO.SetActive(false);
    }

    // ======================================================
    // UI YENİLEME
    // ======================================================

    public void RefreshUI()
    {
        HideTooltip();
        if (panelRoot == null || RunManager.instance == null) return;

        // Priority güncelle
        UpdatePerkPriorities();

        if (stashSection == null && canvasGO != null)
            RebuildStashSection();

        if (activeTitleText != null)
        {
            if (IsUpgradeMode)
                activeTitleText.text = "<color=#00FF00>CHOOSE A PERK TO UPGRADE</color>";
            else
                activeTitleText.text = $"ACTIVE ({RunManager.instance.activePerks.Count}/{RunManager.MAX_ACTIVE_PERKS})";
        }

        int stashCount = RunManager.instance.inventoryPerks.Count;

        // Active slot'ları yenile
        ClearSlots(spawnedActiveSlots);
        for (int i = 0; i < RunManager.MAX_ACTIVE_PERKS; i++)
        {
            bool hasPerk = i < RunManager.instance.activePerks.Count;
            BasePerk perk = hasPerk ? RunManager.instance.activePerks[i] : null;
            GameObject slot = CreateSlot(perk, true, i);
            slot.transform.SetParent(activeSlotsContainer, false);
            spawnedActiveSlots.Add(slot);
        }

        // Stash göster/gizle — animasyonlu
        bool shouldShowStash = stashCount > 0;
        bool stashIsShowing = stashSection != null && stashSection.activeSelf;
        bool stashClosing = !shouldShowStash && stashIsShowing;

        if (stashClosing)
        {
            // Stash kapanıyorsa: slot'ları hemen silme — animasyon sırasında görünsünler
            if (stashExpandCoroutine != null) StopCoroutine(stashExpandCoroutine);
            stashExpandCoroutine = StartCoroutine(StashCollapseAndClear());
        }
        else
        {
            // Normal akış: stash slot'larını yenile
            ClearSlots(spawnedStashSlots);
            if (stashGrid != null)
            {
                for (int i = 0; i < stashCount; i++)
                {
                    BasePerk perk = RunManager.instance.inventoryPerks[i];
                    GameObject slot = CreateSlot(perk, false, i);
                    slot.transform.SetParent(stashGrid, false);
                    spawnedStashSlots.Add(slot);
                }

                // Scroll pozisyonunu sifirla
                if (stashScroll != null)
                    stashScroll.ResetScroll();
            }

            if (stashSection != null)
            {
                if (stashTitleText != null)
                    stashTitleText.text = $"STASH ({stashCount})";

                if (shouldShowStash != stashIsShowing)
                {
                    if (stashExpandCoroutine != null) StopCoroutine(stashExpandCoroutine);
                    stashExpandCoroutine = StartCoroutine(StashExpand(shouldShowStash));
                }
            }
        }

        // Fly animasyonu — perk eski pozisyondan yeni slota uçsun
        TryStartFlyAnim();
    }

    private void ClearSlots(List<GameObject> list)
    {
        foreach (var go in list)
            if (go != null) Destroy(go);
        list.Clear();
    }

    private void RebuildStashSection()
    {
        stashSection = new GameObject("StashSection", typeof(RectTransform));
        stashSection.transform.SetParent(panelRoot.transform, false);

        VerticalLayoutGroup stashVLG = stashSection.AddComponent<VerticalLayoutGroup>();
        stashVLG.spacing = 4f;
        stashVLG.childControlWidth = true;
        stashVLG.childControlHeight = true;
        stashVLG.childForceExpandWidth = true;
        stashVLG.childForceExpandHeight = false;

        stashTitleText = MakeLabel("StashTitle", stashSection.transform, "STASH", 14, TextAlignmentOptions.Left, new Color(0.7f, 0.7f, 0.7f), 20f);

        BuildStashGrid(stashSection.transform);

        stashSection.SetActive(false);
    }

    private void BuildStashGrid(Transform parent)
    {
        float cellH = SLOT_SIZE + 20f;
        float maxVisibleH = cellH * 3 + SLOT_SPACING * 2 + 4f; // max 3 satir gorunur

        // Sabit yukseklikte gorunum alani — RectMask2D ile kirpma
        GameObject clipGO = new GameObject("StashClip", typeof(RectTransform));
        clipGO.transform.SetParent(parent, false);
        clipGO.AddComponent<RectMask2D>();
        // Scroll event'lerini yakalamak icin gorünmez Image lazim
        Image clipImg = clipGO.AddComponent<Image>();
        clipImg.color = Color.clear;
        clipImg.raycastTarget = true;
        var clipLE = clipGO.AddComponent<LayoutElement>();
        clipLE.preferredHeight = maxVisibleH;
        clipLE.flexibleHeight = 0;

        // SimpleScrollArea ekle
        stashScroll = clipGO.AddComponent<SimpleScrollArea>();
        stashScroll.scrollSpeed = 30f;

        // Grid — clip'in icinde, yukari dogru buyur
        GameObject gridGO = new GameObject("StashGrid", typeof(RectTransform));
        gridGO.transform.SetParent(clipGO.transform, false);
        stashGrid = gridGO.GetComponent<RectTransform>();
        stashGrid.anchorMin = new Vector2(0, 1);
        stashGrid.anchorMax = new Vector2(1, 1);
        stashGrid.pivot = new Vector2(0.5f, 1f);
        stashGrid.anchoredPosition = Vector2.zero;
        stashGrid.sizeDelta = new Vector2(0, 0);

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(cellH, cellH);
        glg.spacing = new Vector2(SLOT_SPACING, SLOT_SPACING);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = gridGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        stashScroll.content = stashGrid;
    }

    // ======================================================
    // SLOT OLUŞTURMA
    // ======================================================

    private GameObject CreateSlot(BasePerk perk, bool isActiveSlot, int index)
    {
        GameObject slot = new GameObject("Slot", typeof(RectTransform));

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 1f);
        iconRT.anchorMax = new Vector2(0.5f, 1f);
        iconRT.pivot = new Vector2(0.5f, 1f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);

        Image iconImg = iconGO.GetComponent<Image>();

        PerkSlotTag tag = iconGO.AddComponent<PerkSlotTag>();
        tag.isActiveSlot = isActiveSlot;
        tag.slotIndex = index;

        if (perk != null)
        {
            if (perk.icon != null)
            {
                iconImg.sprite = perk.icon;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = new Color(0.35f, 0.35f, 0.4f, 0.8f);
            }

            Color rarityColor;
            ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out rarityColor);

            Outline outline = iconGO.AddComponent<Outline>();
            outline.effectColor = rarityColor;
            outline.effectDistance = new Vector2(2f, 2f);

            if (perk.currentLevel > 1)
            {
                GameObject lvGO = new GameObject("Lv", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lvGO.transform.SetParent(iconGO.transform, false);
                RectTransform lvRT = lvGO.GetComponent<RectTransform>();
                lvRT.anchorMin = new Vector2(1f, 0f);
                lvRT.anchorMax = new Vector2(1f, 0f);
                lvRT.pivot = new Vector2(1f, 0f);
                lvRT.anchoredPosition = new Vector2(2f, -2f);
                lvRT.sizeDelta = new Vector2(22f, 16f);
                TextMeshProUGUI lvTMP = lvGO.GetComponent<TextMeshProUGUI>();
                var alagardFont = Resources.Load<TMP_FontAsset>("alagard SDF");
                if (alagardFont != null) lvTMP.font = alagardFont;
                lvTMP.text = perk.currentLevel.ToString();
                lvTMP.fontSize = 11;
                lvTMP.fontStyle = FontStyles.Bold;
                lvTMP.alignment = TextAlignmentOptions.BottomRight;
                lvTMP.color = new Color(1f, 0.9f, 0.4f);
                lvTMP.outlineWidth = 0.25f;
                lvTMP.outlineColor = Color.black;
                lvTMP.raycastTarget = false;
            }

            if (isActiveSlot)
            {
                GameObject prioGO = new GameObject("Prio", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                prioGO.transform.SetParent(iconGO.transform, false);
                RectTransform prioRT = prioGO.GetComponent<RectTransform>();
                prioRT.anchorMin = new Vector2(0f, 0f);
                prioRT.anchorMax = new Vector2(0f, 0f);
                prioRT.pivot = new Vector2(0f, 0f);
                prioRT.anchoredPosition = new Vector2(-2f, -2f);
                prioRT.sizeDelta = new Vector2(18f, 14f);
                TextMeshProUGUI prioTMP = prioGO.GetComponent<TextMeshProUGUI>();
                prioTMP.text = (index + 1).ToString();
                prioTMP.fontSize = 9;
                prioTMP.alignment = TextAlignmentOptions.BottomLeft;
                prioTMP.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                prioTMP.raycastTarget = false;
            }


            // Upgrade modunda max level perkleri soluk goster
            bool canUpgrade = perk.currentLevel < perk.maxLevel;
            if (IsUpgradeMode && !canUpgrade)
            {
                iconImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }

            // Event'ler
            EventTrigger trigger = iconGO.AddComponent<EventTrigger>();
            int ci = index;
            bool cia = isActiveSlot;
            BasePerk cp = perk;

            // Sol tık — upgrade modunda perk upgrade et
            // Sağ tık — fly animasyonu ile taşı
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) =>
            {
                PointerEventData ped = (PointerEventData)data;

                // Upgrade mode: sol tikla upgrade
                if (IsUpgradeMode && ped.button == PointerEventData.InputButton.Left)
                {
                    if (cp != null && cp.currentLevel < cp.maxLevel && upgradeCallback != null)
                    {
                        var cb = upgradeCallback;
                        ExitUpgradeMode();
                        cb.Invoke(cp);
                    }
                    return;
                }

                if (ped.button == PointerEventData.InputButton.Right)
                {
                    if (IsUpgradeMode) return; // Upgrade modunda sag tik engelle
                    if (cia)
                    {
                        if (RunManager.instance != null && ci < RunManager.instance.activePerks.Count)
                        {
                            // Uçuş başlangıç pozisyonunu kaydet
                            BeginFlyAnim(cp, iconGO);
                            if (ActivePerkBar.instance != null) ActivePerkBar.instance.skipBounce = true;
                            RunManager.instance.MoveToInventory(ci);
                            if (ActivePerkBar.instance != null) ActivePerkBar.instance.skipBounce = false;
                        }
                    }
                    else
                    {
                        if (RunManager.instance != null && RunManager.instance.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
                        {
                            BeginFlyAnim(cp, iconGO);
                            if (ActivePerkBar.instance != null) ActivePerkBar.instance.skipBounce = true;
                            RunManager.instance.MoveToActive(ci);
                            if (ActivePerkBar.instance != null) ActivePerkBar.instance.skipBounce = false;
                        }
                    }
                }
            });
            trigger.triggers.Add(clickEntry);

            // Drag (upgrade modunda devre disi)
            var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginEntry.callback.AddListener((data) => { if (!IsUpgradeMode) BeginDrag(cia, ci, cp, (PointerEventData)data); });
            trigger.triggers.Add(beginEntry);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => { });
            trigger.triggers.Add(dragEntry);

            var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endEntry.callback.AddListener((data) => EndDrag((PointerEventData)data));
            trigger.triggers.Add(endEntry);

            // Hover — tooltip + scale
            RectTransform hoverRT = iconGO.GetComponent<RectTransform>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) =>
            {
                if (!isDragging)
                {
                    ShowTooltip(cp, hoverRT);
                    StartCoroutine(HoverScale(hoverRT, true));
                }
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) =>
            {
                HideTooltip();
                StartCoroutine(HoverScale(hoverRT, false));
            });
            trigger.triggers.Add(exitEntry);
        }
        else
        {
            iconImg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);
        }

        return slot;
    }

    // ======================================================
    // HOVER SCALE ANIMASYONU
    // ======================================================

    private IEnumerator HoverScale(RectTransform rt, bool enter)
    {
        if (rt == null) yield break;
        float target = enter ? HOVER_SCALE : 1f;
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startScale = rt.localScale;

        while (elapsed < duration)
        {
            if (rt == null) yield break;
            float t = elapsed / duration;
            float s = Mathf.Lerp(startScale.x, target, EaseOutBack(t));
            rt.localScale = new Vector3(s, s, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localScale = new Vector3(target, target, 1f);
    }

    // ======================================================
    // TOOLTIP — fade animasyonlu
    // ======================================================

    public void BuildTooltip(Transform canvasTransform)
    {
        float ttWidth = 240f;
        float pad = 8f;
        float topRowH = 22f;

        tooltipObj = new GameObject("PerkTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObj.transform.SetParent(canvasTransform, false);
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        ttRT.sizeDelta = new Vector2(ttWidth, 70f);
        ttRT.anchorMin = new Vector2(0.5f, 0.5f);
        ttRT.anchorMax = new Vector2(0.5f, 0.5f);
        ttRT.pivot = new Vector2(1f, 0.5f);
        ttRT.anchoredPosition = new Vector2(-10f, 0f);

        Image bg = tooltipObj.GetComponent<Image>();
        bg.color = new Color(0f, 0.02f, 0.047f, 0.95f);
        bg.raycastTarget = false;

        tooltipCanvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;

        // Perk adı
        GameObject nameGO = new GameObject("TooltipName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(tooltipObj.transform, false);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(0.65f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.offsetMin = new Vector2(pad, -pad - topRowH);
        nameRT.offsetMax = new Vector2(0f, -pad);
        tooltipNameText = nameGO.GetComponent<TextMeshProUGUI>();
        tooltipNameText.fontSize = 16;
        tooltipNameText.color = Color.white;
        tooltipNameText.alignment = TextAlignmentOptions.TopLeft;
        tooltipNameText.enableWordWrapping = false;
        tooltipNameText.richText = true;
        tooltipNameText.raycastTarget = false;

        // Level
        GameObject lvGO = new GameObject("TooltipLevel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lvGO.transform.SetParent(tooltipObj.transform, false);
        RectTransform lvRT = lvGO.GetComponent<RectTransform>();
        lvRT.anchorMin = new Vector2(0.65f, 1f);
        lvRT.anchorMax = new Vector2(1f, 1f);
        lvRT.pivot = new Vector2(1f, 1f);
        lvRT.offsetMin = new Vector2(0f, -pad - topRowH);
        lvRT.offsetMax = new Vector2(-pad, -pad);
        tooltipLevelText = lvGO.GetComponent<TextMeshProUGUI>();
        tooltipLevelText.fontSize = 16;
        tooltipLevelText.color = new Color(0.8f, 0.8f, 0.8f);
        tooltipLevelText.alignment = TextAlignmentOptions.TopRight;
        tooltipLevelText.enableWordWrapping = false;
        tooltipLevelText.raycastTarget = false;

        // Açıklama
        GameObject descGO = new GameObject("TooltipDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(tooltipObj.transform, false);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 1f);
        descRT.pivot = new Vector2(0f, 1f);
        descRT.offsetMin = new Vector2(pad, pad);
        descRT.offsetMax = new Vector2(-pad, -pad - topRowH - 2f);
        tooltipDescText = descGO.GetComponent<TextMeshProUGUI>();
        tooltipDescText.fontSize = 16;
        tooltipDescText.color = new Color(0.67f, 0.67f, 0.67f);
        tooltipDescText.alignment = TextAlignmentOptions.TopLeft;
        tooltipDescText.enableWordWrapping = true;
        tooltipDescText.richText = true;
        tooltipDescText.raycastTarget = false;

        tooltipObj.SetActive(false);
    }

    private void ShowTooltip(BasePerk perk, RectTransform slotRT)
    {
        if (perk == null) return;

        if (tooltipObj == null || tooltipNameText == null)
        {
            if (canvasGO != null) BuildTooltip(canvasGO.transform);
            else return;
        }

        string rarityHex = PerkListUI.GetRarityHex(perk.rarity);
        Color rarityColor;
        ColorUtility.TryParseHtmlString(rarityHex, out rarityColor);

        tooltipNameText.text = perk.perkName;
        tooltipNameText.color = rarityColor;

        if (tooltipLevelText != null)
            tooltipLevelText.text = $"Lv {perk.currentLevel}/{perk.maxLevel}";

        if (tooltipDescText != null)
            tooltipDescText.text = string.IsNullOrEmpty(perk.description) ? "" : perk.description;

        tooltipObj.SetActive(true);
        tooltipObj.transform.SetAsLastSibling();

        Image ttBg = tooltipObj.GetComponent<Image>();
        if (ttBg != null) ttBg.color = new Color(0f, 0.02f, 0.047f, 0.95f);

        // Yükseklik ayarla
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        float pad = 8f;
        float topRowH = 22f;
        float descH = 0f;
        if (tooltipDescText != null && !string.IsNullOrEmpty(tooltipDescText.text))
        {
            tooltipDescText.ForceMeshUpdate();
            descH = tooltipDescText.preferredHeight;
        }
        float totalH = pad + topRowH + 2f + descH + pad;
        if (totalH < 70f) totalH = 70f;
        ttRT.sizeDelta = new Vector2(ttRT.sizeDelta.x, totalH);

        // Pozisyon
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        Vector3[] slotCorners = new Vector3[4];
        slotRT.GetWorldCorners(slotCorners);
        Vector3 slotLeftCenter = (slotCorners[0] + slotCorners[1]) / 2f;
        Vector3 localPoint = canvasRT.InverseTransformPoint(slotLeftCenter);
        ttRT.localPosition = new Vector3(localPoint.x - 10f, localPoint.y, 0f);

        // Fade in
        if (tooltipFadeCoroutine != null) StopCoroutine(tooltipFadeCoroutine);
        tooltipFadeCoroutine = StartCoroutine(TooltipFade(true));
    }

    private void HideTooltip()
    {
        if (tooltipObj == null) return;
        if (tooltipFadeCoroutine != null) StopCoroutine(tooltipFadeCoroutine);

        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        tooltipObj.SetActive(false);
    }

    private IEnumerator TooltipFade(bool fadeIn)
    {
        if (tooltipCanvasGroup == null) yield break;

        float start = tooltipCanvasGroup.alpha;
        float target = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        // Tooltip küçükten büyüğe scale
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        Vector3 startScale = fadeIn ? Vector3.one * 0.9f : Vector3.one;
        Vector3 targetScale = fadeIn ? Vector3.one : Vector3.one * 0.9f;

        while (elapsed < TOOLTIP_FADE_DURATION)
        {
            float t = elapsed / TOOLTIP_FADE_DURATION;
            tooltipCanvasGroup.alpha = Mathf.Lerp(start, target, t);
            ttRT.localScale = Vector3.Lerp(startScale, targetScale, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        tooltipCanvasGroup.alpha = target;
        ttRT.localScale = targetScale;

        if (!fadeIn) tooltipObj.SetActive(false);
    }

    // ======================================================
    // YARDIMCI
    // ======================================================

    private TextMeshProUGUI MakeLabel(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment, Color color, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.richText = true;
        tmp.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return tmp;
    }

    private void MakeSeparator(Transform parent)
    {
        GameObject sepGO = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sepGO.transform.SetParent(parent, false);
        sepGO.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        sepGO.GetComponent<Image>().raycastTarget = false;
        var sepLE = sepGO.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 1f;
    }

    /// <summary>EaseOutBack easing — t=0..1 → yumuşak overshoot.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

/// <summary>
/// Slot'u tanımlayan tag — drop hedefini bulmak için kullanılır.
/// </summary>
public class PerkSlotTag : MonoBehaviour
{
    public bool isActiveSlot;
    public int slotIndex;
}
