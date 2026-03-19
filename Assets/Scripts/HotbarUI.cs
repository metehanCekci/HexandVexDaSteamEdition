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
    public Color emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);
    public Color occupiedSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
    public Color hoverColor = new Color(0.4f, 0.4f, 0.2f, 1f);

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

        // Block hotbar input when shop or targeting is active
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        for (int i = 0; i < hotkeys.Length && i < maxVisibleSlots; i++)
        {
            if (Input.GetKeyDown(hotkeys[i]))
            {
                UseSlot(i);
            }
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

        for (int i = 0; i < slots.Count && i < maxVisibleSlots; i++)
        {
            BaseItem item = InventoryManager.instance.GetItem(i);
            HotbarSlotUI slot = slots[i];
            if (slot == null) continue;
            if (slot.background == null || slot.button == null || slot.iconImage == null) continue;

            if (item != null)
            {
                slot.background.color = occupiedSlotColor;
                slot.button.interactable = true;

                if (item.icon != null)
                {
                    slot.iconImage.sprite = item.icon;
                    slot.iconImage.color = Color.white;
                    slot.iconImage.enabled = true;
                }
                else
                {
                    slot.iconImage.enabled = false;
                }

                if (slot.tooltip != null)
                {
                    slot.tooltip.itemName = item.itemName;
                    slot.tooltip.itemDesc = item.description;
                    slot.tooltip.hasItem = true;
                }
            }
            else
            {
                slot.background.color = emptySlotColor;
                slot.button.interactable = false;
                slot.iconImage.enabled = false;
                if (slot.tooltip != null)
                    slot.tooltip.hasItem = false;
            }
        }
    }

    private void UseSlot(int index)
    {
        if (InventoryManager.instance == null) return;
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
        descText.fontSize = 13;
        descText.alignment = TextAlignmentOptions.Left;
        descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        descText.raycastTarget = false;
        descText.enableWordWrapping = true;

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
