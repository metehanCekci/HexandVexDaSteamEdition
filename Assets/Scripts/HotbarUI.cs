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
/// Builds its own UI from code — no prefab/scene setup needed.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    public static HotbarUI instance;

    [Header("Config")]
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

    // ─── Internal ───
    private GameObject hotbarCanvas;
    private GameObject hotbarPanel;
    private List<HotbarSlotUI> slotUIs = new List<HotbarSlotUI>();

    private bool uiBuilt = false;

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
            return;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        BuildUI();
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
    // UI CONSTRUCTION (Pure Code — No Prefabs)
    // ═══════════════════════════════════════════

    private void BuildUI()
    {
        if (uiBuilt) return;
        uiBuilt = true;

        // ─── Canvas ───
        hotbarCanvas = new GameObject("HotbarCanvas");
        hotbarCanvas.transform.SetParent(transform, false);
        Canvas canvas = hotbarCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15; // Above game HUD, below map/shop
        var scaler = hotbarCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        hotbarCanvas.AddComponent<GraphicRaycaster>();

        // ─── Panel (bottom-center anchor) ───
        hotbarPanel = new GameObject("HotbarPanel", typeof(RectTransform));
        hotbarPanel.transform.SetParent(hotbarCanvas.transform, false);
        RectTransform panelRT = hotbarPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 12f);

        float totalWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * slotSpacing + 20f;
        panelRT.sizeDelta = new Vector2(totalWidth, slotSize + 30f);

        // Panel background
        Image panelBG = hotbarPanel.AddComponent<Image>();
        panelBG.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        panelBG.raycastTarget = false;

        // ─── Create slots ───
        slotUIs.Clear();
        for (int i = 0; i < maxVisibleSlots; i++)
        {
            CreateSlotUI(i);
        }
    }

    private void CreateSlotUI(int index)
    {
        // Slot root
        GameObject slotGO = new GameObject($"HotbarSlot_{index}", typeof(RectTransform));
        slotGO.transform.SetParent(hotbarPanel.transform, false);

        RectTransform slotRT = slotGO.GetComponent<RectTransform>();
        slotRT.anchorMin = new Vector2(0f, 0.5f);
        slotRT.anchorMax = new Vector2(0f, 0.5f);
        slotRT.pivot = new Vector2(0f, 0.5f);
        slotRT.sizeDelta = new Vector2(slotSize, slotSize);

        float startX = 10f;
        slotRT.anchoredPosition = new Vector2(startX + index * (slotSize + slotSpacing), 5f);

        // Background image
        Image slotBG = slotGO.AddComponent<Image>();
        slotBG.color = emptySlotColor;
        slotBG.raycastTarget = true;

        // Button
        Button btn = slotGO.AddComponent<Button>();
        int idx = index;
        btn.onClick.AddListener(() => UseSlot(idx));
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = hoverColor;
        cb.pressedColor = new Color(0.6f, 0.6f, 0.2f, 1f);
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        btn.colors = cb;

        // Item icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(slotGO.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;

        // Keybind label (top-left corner)
        GameObject keyGO = new GameObject("KeyLabel", typeof(RectTransform));
        keyGO.transform.SetParent(slotGO.transform, false);
        RectTransform keyRT = keyGO.GetComponent<RectTransform>();
        keyRT.anchorMin = new Vector2(0f, 1f);
        keyRT.anchorMax = new Vector2(0f, 1f);
        keyRT.pivot = new Vector2(0f, 1f);
        keyRT.anchoredPosition = new Vector2(3f, -2f);
        keyRT.sizeDelta = new Vector2(20f, 18f);
        TMP_Text keyText = keyGO.AddComponent<TextMeshProUGUI>();
        keyText.text = (index + 1).ToString();
        keyText.fontSize = 11;
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        keyText.raycastTarget = false;

        // Tooltip handler
        HotbarSlotTooltip tooltip = slotGO.AddComponent<HotbarSlotTooltip>();

        HotbarSlotUI slotUI = new HotbarSlotUI
        {
            root = slotGO,
            background = slotBG,
            button = btn,
            iconImage = iconImg,
            keyLabel = keyText,
            tooltip = tooltip
        };

        slotUIs.Add(slotUI);
    }

    // ═══════════════════════════════════════════
    // REFRESH & USAGE
    // ═══════════════════════════════════════════

    public void RefreshSlots()
    {
        if (InventoryManager.instance == null) return;
        if (!uiBuilt) return;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            BaseItem item = InventoryManager.instance.GetItem(i);
            HotbarSlotUI slot = slotUIs[i];

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

                // Update tooltip data
                slot.tooltip.itemName = item.itemName;
                slot.tooltip.itemDesc = item.description;
                slot.tooltip.hasItem = true;
            }
            else
            {
                slot.background.color = emptySlotColor;
                slot.button.interactable = false;
                slot.iconImage.enabled = false;
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
        if (!uiBuilt) return;

        maxVisibleSlots++;
        CreateSlotUI(maxVisibleSlots - 1);

        // Recalculate panel width
        RectTransform panelRT = hotbarPanel.GetComponent<RectTransform>();
        float totalWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * slotSpacing + 20f;
        panelRT.sizeDelta = new Vector2(totalWidth, slotSize + 30f);

        // Reposition all slots
        for (int i = 0; i < slotUIs.Count; i++)
        {
            RectTransform slotRT = slotUIs[i].root.GetComponent<RectTransform>();
            slotRT.anchoredPosition = new Vector2(10f + i * (slotSize + slotSpacing), 5f);
        }

        RefreshSlots();
    }

    /// <summary>
    /// Show/hide the hotbar. Hidden during shop, map, etc.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (hotbarCanvas != null)
            hotbarCanvas.SetActive(visible);
    }

    // ═══════════════════════════════════════════
    // INTERNAL DATA CLASS
    // ═══════════════════════════════════════════

    private class HotbarSlotUI
    {
        public GameObject root;
        public Image background;
        public Button button;
        public Image iconImage;
        public TMP_Text keyLabel;
        public HotbarSlotTooltip tooltip;
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

        // Build tooltip (same style as ShopSlot)
        tooltipObj = new GameObject("HotbarTooltip", typeof(RectTransform));
        tooltipObj.transform.SetParent(transform, false);
        tooltipObj.layer = gameObject.layer;

        tooltipCanvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.alpha = 0f;

        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        // Position: above the slot
        ttRT.anchorMin = new Vector2(0.5f, 1f);
        ttRT.anchorMax = new Vector2(0.5f, 1f);
        ttRT.pivot = new Vector2(0.5f, 0f);
        ttRT.anchoredPosition = new Vector2(0f, 8f);
        ttRT.sizeDelta = new Vector2(280f, 0f);

        // Background
        Image bg = tooltipObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
        bg.raycastTarget = false;

        // Auto-height
        var fitter = tooltipObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Layout
        var vlg = tooltipObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 4;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(tooltipObj.transform, false);
        titleGO.layer = gameObject.layer;
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 16;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;
        titleText.raycastTarget = false;

        // Separator
        GameObject lineGO = new GameObject("Line", typeof(RectTransform));
        lineGO.transform.SetParent(tooltipObj.transform, false);
        lineGO.layer = gameObject.layer;
        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        lineImg.raycastTarget = false;
        LayoutElement lineLE = lineGO.AddComponent<LayoutElement>();
        lineLE.minHeight = 2f;
        lineLE.preferredHeight = 2f;

        // Description
        GameObject descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(tooltipObj.transform, false);
        descGO.layer = gameObject.layer;
        descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 13;
        descText.alignment = TextAlignmentOptions.Left;
        descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        descText.raycastTarget = false;
        descText.enableWordWrapping = true;

        // Overlay canvas for rendering on top
        Canvas ttCanvas = tooltipObj.AddComponent<Canvas>();
        ttCanvas.overrideSorting = true;
        ttCanvas.sortingOrder = 100;
        tooltipObj.AddComponent<GraphicRaycaster>();

        UpdateContent();
        StartFade(1f);
    }

    private void UpdateContent()
    {
        if (titleText != null) titleText.text = itemName != null ? itemName.ToUpper() : "";
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
        // Can't start coroutines on inactive objects — just hide immediately
        if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }
}
