using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Visual hotbar (action bar) that displays the player's inventory items during gameplay.
/// Listens to GameEvents.OnInventoryChanged to refresh.
/// Players can click slots or press 1-5 keys to use items.
/// Tüm UI elemanları hierarchy'de — Inspector'dan düzenlenebilir.
/// Tools > Setup Hotbar UI ile sahneye eklenir.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    public static HotbarUI instance;

    [Header("Hierarchy References")]
    [Tooltip("Hotbar'ın kendi Canvas'ı")]
    public Canvas hotbarCanvas;

    [Tooltip("Slot'ları tutan panel")]
    public RectTransform hotbarPanel;

    [Tooltip("Panel arka plan Image'ı")]
    public Image panelBackground;

    [Tooltip("Sahnedeki tüm slot'lar (sıralı). Ilk maxVisibleSlots tanesi aktif.")]
    public List<HotbarSlotUI> slots = new List<HotbarSlotUI>();

    [Header("Config")]
    [Tooltip("Başlangıçta görünen slot sayısı (OrganPouch ile artabilir)")]
    public int maxVisibleSlots = 3;

    public KeyCode[] hotkeys = new KeyCode[]
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5
    };

    [Header("Style")]
    public float slotSize = 60f;
    public float slotSpacing = 6f;
    public Color occupiedSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);

    // ── Item Drag-to-Sell state ──
    private const float HOLD_THRESHOLD = 0.35f; // basılı tutma süresi (saniye)
    private int holdSlotIndex = -1;
    private float holdTimer;
    private bool isItemDragging;
    private GameObject itemDragGhost;
    private int dragItemSlot = -1;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        // Wire button click listeners & ensure tooltip references
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            // Ensure tooltip component exists (add at runtime if missing)
            if (slots[i].tooltip == null)
                slots[i].tooltip = slots[i].GetComponent<HotbarSlotTooltip>();
            if (slots[i].tooltip == null)
                slots[i].tooltip = slots[i].gameObject.AddComponent<HotbarSlotTooltip>();

            if (slots[i].button == null) continue;
            int idx = i;
            slots[i].button.onClick.RemoveAllListeners();
            slots[i].button.onClick.AddListener(() => UseSlot(idx));
        }

        // InventoryManager DontDestroyOnLoad — sahne değişince slot sayısını senkronize et
        if (InventoryManager.instance != null && InventoryManager.instance.SlotCount > maxVisibleSlots)
            maxVisibleSlots = Mathf.Min(InventoryManager.instance.SlotCount, slots.Count);

        ApplySlotVisibility();
        RefreshSlots();
    }

    void OnEnable()
    {
        GameEvents.OnInventoryChanged += RefreshSlots;
    }

    void OnDisable()
    {
        GameEvents.OnInventoryChanged -= RefreshSlots;
    }

    void Update()
    {
        if (InventoryManager.instance == null) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        // ── Item drag sırasında: ghost takip + bırakma kontrolü ──
        if (isItemDragging)
        {
            UpdateItemDrag();
            return;
        }

        // Targeting aktifken aynı slot tuşu/tıkı iptal eder
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive && !TurnManager.instance.isMitsuriTargeting)
        {
            int cancelSlot = TurnManager.instance.pendingTargetingSlot;
            if (cancelSlot >= 0)
            {
                // Tuşla iptal
                for (int i = 0; i < hotkeys.Length && i < maxVisibleSlots; i++)
                {
                    if (Input.GetKeyDown(hotkeys[i]) && i == cancelSlot)
                    {
                        TurnManager.instance.CancelTargeting();
                        return;
                    }
                }

                // Tıklamayla iptal
                if (Input.GetMouseButtonDown(0) && cancelSlot < slots.Count && slots[cancelSlot] != null)
                {
                    RectTransform rt = slots[cancelSlot].GetComponent<RectTransform>();
                    if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, hotbarCanvas != null ? hotbarCanvas.worldCamera : null))
                    {
                        TurnManager.instance.CancelTargeting();
                        return;
                    }
                }
            }
            return;
        }

        for (int i = 0; i < hotkeys.Length && i < maxVisibleSlots; i++)
        {
            if (Input.GetKeyDown(hotkeys[i]))
            {
                UseSlot(i);
                return;
            }
        }

        // ── Basılı tutma algılama (hold-to-drag) ──
        if (Input.GetMouseButtonDown(0))
        {
            holdSlotIndex = GetSlotUnderMouse();
            holdTimer = 0f;
        }

        if (Input.GetMouseButton(0) && holdSlotIndex >= 0)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer >= HOLD_THRESHOLD)
            {
                BaseItem item = InventoryManager.instance.GetItem(holdSlotIndex);
                if (item != null)
                {
                    BeginItemDrag(holdSlotIndex, item);
                }
                holdSlotIndex = -1;
                holdTimer = 0f;
            }
        }

        // Mouse bırakıldığında: eğer hold threshold'a ulaşılmadıysa normal tıklama
        if (Input.GetMouseButtonUp(0) && holdSlotIndex >= 0)
        {
            if (holdTimer < HOLD_THRESHOLD)
            {
                int slotIdx = GetSlotUnderMouse();
                if (slotIdx >= 0 && slotIdx == holdSlotIndex)
                    UseSlot(slotIdx);
            }
            holdSlotIndex = -1;
            holdTimer = 0f;
        }
    }

    // ═══════════════════════════════════════════
    // SLOT VISIBILITY
    // ═══════════════════════════════════════════

    private void ApplySlotVisibility()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].gameObject.SetActive(i < maxVisibleSlots);
        }
        ResizePanel();
    }

    private void ResizePanel()
    {
        if (hotbarPanel == null) return;
        float totalWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * slotSpacing + 20f;
        hotbarPanel.sizeDelta = new Vector2(totalWidth, hotbarPanel.sizeDelta.y);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            RectTransform slotRT = slots[i].GetComponent<RectTransform>();
            slotRT.anchoredPosition = new Vector2(10f + i * (slotSize + slotSpacing), slotRT.anchoredPosition.y);
        }
    }

    // ═══════════════════════════════════════════
    // REFRESH & USAGE
    // ═══════════════════════════════════════════

    public void RefreshSlots()
    {
        if (InventoryManager.instance == null) return;
        if (slots == null || slots.Count == 0) return;

        // Targeting aktifken pending slot'u tıklanabilir tut (iptal için)
        bool hasTargeting = TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive && !TurnManager.instance.isMitsuriTargeting;
        int targetSlot = hasTargeting ? TurnManager.instance.pendingTargetingSlot : -1;

        for (int i = 0; i < slots.Count && i < maxVisibleSlots; i++)
        {
            BaseItem item = InventoryManager.instance.GetItem(i);
            HotbarSlotUI slot = slots[i];
            if (slot == null) continue;
            if (slot.background == null || slot.button == null || slot.iconImage == null) continue;

            if (item != null)
            {
                slot.background.color = occupiedSlotColor;

                bool onCooldown = item.usedThisCombat;
                slot.button.interactable = !onCooldown;

                if (item.icon != null)
                {
                    slot.iconImage.sprite = item.icon;
                    slot.iconImage.color = onCooldown ? new Color(0.4f, 0.4f, 0.4f, 0.6f) : Color.white;
                    slot.iconImage.enabled = true;
                }
                else
                {
                    slot.iconImage.enabled = false;
                }

                // Cooldown overlay
                EnsureCooldownOverlay(slot);
                if (slot.cooldownOverlay != null)
                    slot.cooldownOverlay.enabled = onCooldown;

                HotbarSlotTooltip tt = slot.tooltip ?? slot.GetComponent<HotbarSlotTooltip>();
                if (tt != null)
                {
                    tt.itemName = item.itemName ?? "";
                    tt.itemDesc = item.description ?? "";
                    tt.hasItem = true;
                }
            }
            else
            {
                slot.iconImage.enabled = false;

                // Hide cooldown overlay for empty slots
                if (slot.cooldownOverlay != null)
                    slot.cooldownOverlay.enabled = false;

                // Targeting slotu: görsel boş ama tıklanabilir (iptal için)
                slot.button.interactable = (i == targetSlot);

                HotbarSlotTooltip tt2 = slot.tooltip ?? slot.GetComponent<HotbarSlotTooltip>();
                if (tt2 != null)
                    tt2.hasItem = false;
            }
        }
    }

    private void EnsureCooldownOverlay(HotbarSlotUI slot)
    {
        if (slot.cooldownOverlay != null) return;

        GameObject overlayGO = new GameObject("CooldownOverlay", typeof(RectTransform));
        overlayGO.transform.SetParent(slot.transform, false);
        overlayGO.layer = slot.gameObject.layer;

        RectTransform rt = overlayGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = overlayGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = false;
        img.enabled = false;

        slot.cooldownOverlay = img;
    }

    // ═══════════════════════════════════════════
    // ITEM DRAG-TO-SELL
    // ═══════════════════════════════════════════

    private int GetSlotUnderMouse()
    {
        for (int i = 0; i < slots.Count && i < maxVisibleSlots; i++)
        {
            if (slots[i] == null) continue;
            RectTransform rt = slots[i].GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(
                rt, Input.mousePosition, hotbarCanvas != null ? hotbarCanvas.worldCamera : null))
            {
                return i;
            }
        }
        return -1;
    }

    private void BeginItemDrag(int slotIndex, BaseItem item)
    {
        isItemDragging = true;
        dragItemSlot = slotIndex;

        // Ghost oluştur
        itemDragGhost = new GameObject("ItemDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Canvas ghostCanvas = itemDragGhost.AddComponent<Canvas>();
        ghostCanvas.overrideSorting = true;
        ghostCanvas.sortingOrder = 200;
        itemDragGhost.AddComponent<GraphicRaycaster>();

        // Hotbar canvas'ın üstüne koy
        itemDragGhost.transform.SetParent(hotbarCanvas.transform, false);
        itemDragGhost.transform.SetAsLastSibling();

        RectTransform ghostRT = itemDragGhost.GetComponent<RectTransform>();
        ghostRT.sizeDelta = new Vector2(slotSize, slotSize);
        ghostRT.localScale = Vector3.one * 1.15f;

        Image ghostImg = itemDragGhost.GetComponent<Image>();
        if (item.icon != null)
        {
            ghostImg.sprite = item.icon;
            ghostImg.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            ghostImg.color = new Color(0.5f, 0.5f, 0.6f, 0.85f);
        }
        ghostImg.raycastTarget = false;

        // Kaynak slotu soluklaştır
        if (slotIndex < slots.Count && slots[slotIndex] != null)
        {
            CanvasGroup cg = slots[slotIndex].GetComponent<CanvasGroup>();
            if (cg == null) cg = slots[slotIndex].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0.35f;
        }

        // SellBox'ı aç
        if (SellBoxController.instance != null)
            SellBoxController.instance.ShowForItem(item, slotIndex);
    }

    private void UpdateItemDrag()
    {
        // Ghost mouse takibi
        if (itemDragGhost != null)
        {
            RectTransform canvasRT = hotbarCanvas.GetComponent<RectTransform>();
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, Input.mousePosition, null, out pos);
            RectTransform ghostRT = itemDragGhost.GetComponent<RectTransform>();
            ghostRT.anchoredPosition = pos;

            // Hafif sallanma
            float wobble = Mathf.Sin(Time.unscaledTime * 8f) * 3f;
            ghostRT.localRotation = Quaternion.Euler(0f, 0f, wobble);
        }

        // Mouse bırakıldı — SellBox'a mı düştü kontrol et
        if (!Input.GetMouseButton(0))
        {
            bool sold = false;

            // SellBox kontrolü — perk drag yoluyla aynı: EventSystem RaycastAll ile
            // farklı Canvas'lardaki SellBoxDropZone'u bul. Rect-only kontrolü
            // canvas sıralaması/ghost overlay nedeniyle güvenilmez oluyordu.
            if (SellBoxController.instance != null && EventSystem.current != null)
            {
                PointerEventData ped = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);

                foreach (var r in results)
                {
                    SellBoxDropZone zone = r.gameObject.GetComponent<SellBoxDropZone>();
                    if (zone == null) zone = r.gameObject.GetComponentInParent<SellBoxDropZone>();
                    if (zone != null)
                    {
                        sold = SellBoxController.instance.TrySell();
                        break;
                    }
                }

                // Fallback: RaycastAll ghost/overlay tarafından engellenirse
                // doğrudan rect kontrolü yap (eski davranış).
                if (!sold && SellBoxController.instance.IsMouseOverSellBox())
                {
                    sold = SellBoxController.instance.TrySell();
                }
            }

            EndItemDrag();

            if (!sold)
            {
                // Satış olmadıysa slotu eski haline getir
                RefreshSlots();
            }
        }
    }

    private void EndItemDrag()
    {
        // Ghost sil
        if (itemDragGhost != null) Destroy(itemDragGhost);
        itemDragGhost = null;

        // Kaynak slotu düzelt
        if (dragItemSlot >= 0 && dragItemSlot < slots.Count && slots[dragItemSlot] != null)
        {
            CanvasGroup cg = slots[dragItemSlot].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        isItemDragging = false;
        dragItemSlot = -1;

        // SellBox'ı kapat
        if (SellBoxController.instance != null)
            SellBoxController.instance.HideSellBox();
    }

    private void UseSlot(int index)
    {
        if (InventoryManager.instance == null) return;

        // Targeting aktifken: aynı slot iptal eder, farklı slot engellenir
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive && !TurnManager.instance.isMitsuriTargeting)
        {
            if (TurnManager.instance.pendingTargetingSlot == index)
                TurnManager.instance.CancelTargeting();
            return;
        }

        InventoryManager.instance.UseItem(index);
    }

    /// <summary>
    /// Dynamically add one slot to the hotbar (called by OrganPouch perk).
    /// Respects a hard cap of 5 slots.
    /// </summary>
    public void AddSlot()
    {
        if (maxVisibleSlots >= 5) return;
        if (maxVisibleSlots >= slots.Count) return;

        maxVisibleSlots++;
        ApplySlotVisibility();
        RefreshSlots();
    }

    /// <summary>
    /// Remove one slot from the hotbar (called when OrganPouch is unequipped).
    /// </summary>
    public void RemoveSlot()
    {
        if (maxVisibleSlots <= 3) return;
        maxVisibleSlots--;
        ApplySlotVisibility();
        RefreshSlots();
    }

    /// <summary>
    /// Show/hide the hotbar. Hidden during shop, map, etc.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (hotbarCanvas != null)
            hotbarCanvas.gameObject.SetActive(visible);
    }
}

/// <summary>
/// Handles hover tooltips for a hotbar slot.
/// Style matches ShopSlot tooltips (dark panel, title, description).
/// </summary>
public class HotbarSlotTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public string itemName;
    [HideInInspector] public string itemDesc;
    [HideInInspector] public bool hasItem;

    private GameObject tooltipObj;
    private CanvasGroup tooltipCanvasGroup;
    private TMP_Text titleText;
    private TMP_Text descText;
    private Coroutine fadeCoroutine;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasItem) return;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void ShowTooltip()
    {
        if (tooltipObj != null)
        {
            tooltipObj.SetActive(true);
            UpdateContent();
            StartFade(1f);
            return;
        }

        tooltipObj = new GameObject("HotbarTooltip", typeof(RectTransform));
        tooltipObj.transform.SetParent(transform, false);
        tooltipObj.layer = gameObject.layer;

        tooltipCanvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;

        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        ttRT.anchorMin = new Vector2(0.5f, 1f);
        ttRT.anchorMax = new Vector2(0.5f, 1f);
        ttRT.pivot = new Vector2(0.5f, 0f);
        ttRT.anchoredPosition = new Vector2(0f, 8f);
        ttRT.sizeDelta = new Vector2(280f, 0f);

        Image bg = tooltipObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
        bg.raycastTarget = false;

        var fitter = tooltipObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var vlg = tooltipObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 4;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(tooltipObj.transform, false);
        titleGO.layer = gameObject.layer;
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset alagardFont = Resources.Load<TMP_FontAsset>("alagard SDF");
        if (alagardFont != null) titleText.font = alagardFont;
        titleText.fontSize = 16;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;
        titleText.raycastTarget = false;

        GameObject lineGO = new GameObject("Line", typeof(RectTransform));
        lineGO.transform.SetParent(tooltipObj.transform, false);
        lineGO.layer = gameObject.layer;
        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        lineImg.raycastTarget = false;
        LayoutElement lineLE = lineGO.AddComponent<LayoutElement>();
        lineLE.minHeight = 2f;
        lineLE.preferredHeight = 2f;

        GameObject descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(tooltipObj.transform, false);
        descGO.layer = gameObject.layer;
        descText = descGO.AddComponent<TextMeshProUGUI>();
        if (alagardFont != null) descText.font = alagardFont;
        descText.fontSize = 13;
        descText.alignment = TextAlignmentOptions.Left;
        descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        descText.raycastTarget = false;
        descText.textWrappingMode = TextWrappingModes.Normal;

        Canvas ttCanvas = tooltipObj.AddComponent<Canvas>();
        ttCanvas.overrideSorting = true;
        ttCanvas.sortingOrder = 100;
        tooltipObj.AddComponent<GraphicRaycaster>();

        UpdateContent();
        StartFade(1f);
    }

    private void UpdateContent()
    {
        if (titleText != null) titleText.text = itemName != null ? itemName.ToUpperInvariant() : "";
        if (descText != null) descText.text = itemDesc ?? "";
    }

    private void HideTooltip()
    {
        StartFade(0f);
    }

    private void StartFade(float targetAlpha)
    {
        if (tooltipCanvasGroup == null) return;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, 0.1f));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = tooltipCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            tooltipCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        tooltipCanvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0f && tooltipObj != null)
            tooltipObj.SetActive(false);
    }

    void OnDisable()
    {
        if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }
}
