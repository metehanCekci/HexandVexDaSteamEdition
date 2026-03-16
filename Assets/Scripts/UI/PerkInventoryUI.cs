using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Perk envanter paneli. Aktif slotlari ve stash'i gosterir.
/// Buton tiklayarak perkler arasi takas yapilabilir.
/// Oyunu duraklatir (Time.timeScale = 0) acikken.
/// Editor tool ile sahnede olusturulur (Tools > Setup Perk Inventory).
/// Sahnede yoksa runtime'da otomatik olusturulur (fallback).
/// </summary>
public class PerkInventoryUI : MonoBehaviour
{
    public static PerkInventoryUI instance;

    public bool IsOpen => canvasGO != null && canvasGO.activeSelf;

    [Header("Referanslar (Editor Tool Atar)")]
    public GameObject canvasGO;
    public GameObject panelRoot;
    public GameObject overlayGO;
    public RectTransform activeSlotsContainer;
    public RectTransform stashGrid;
    public TextMeshProUGUI activeTitleText;
    public TextMeshProUGUI stashTitleText;
    public Button closeButton;

    // Swap mekanigi icin secili aktif perk index'i
    private int selectedActiveIndex = -1;

    // Spawnlanan slot objeleri
    private readonly List<GameObject> spawnedActiveSlots = new List<GameObject>();
    private readonly List<GameObject> spawnedStashSlots = new List<GameObject>();

    // Onceki timeScale — kapatirken geri yukle
    private float previousTimeScale = 1f;

    private const float SLOT_SIZE = 64f;
    private const float SLOT_SPACING = 10f;
    private const float PANEL_WIDTH = 700f;
    private const float PANEL_HEIGHT = 520f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Editor tool'dan oluşturulan butonların onClick listener'ları
        // serialize edilmez — runtime'da tekrar bağla
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        // Başlangıçta kapalı olsun
        if (canvasGO != null) canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        // ESC ile kapat
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    /// <summary>Koddan PerkInventoryUI olusturur (fallback).</summary>
    public static void CreateFromCode()
    {
        if (instance != null) return;

        // Sahnede zaten var mi?
        var existing = Object.FindFirstObjectByType<PerkInventoryUI>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        GameObject rootGO = new GameObject("PerkInventoryUI");
        PerkInventoryUI ui = rootGO.AddComponent<PerkInventoryUI>();
        // Awake zaten instance + DontDestroyOnLoad yapar

        ui.BuildUI();
    }

    // ======================================================
    // ACMA / KAPAMA
    // ======================================================

    public void Open()
    {
        if (canvasGO == null) BuildUI();

        selectedActiveIndex = -1;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        canvasGO.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshUI();
    }

    public void Close()
    {
        if (canvasGO != null) canvasGO.SetActive(false);
        Time.timeScale = previousTimeScale;
        selectedActiveIndex = -1;
    }

    // ======================================================
    // UI OLUSTURMA (KODDAN — editor tool veya runtime fallback)
    // ======================================================

    public void BuildUI()
    {
        // Canvas
        canvasGO = new GameObject("PerkInventoryCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Fullscreen overlay (koyu arka plan)
        overlayGO = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(canvasGO.transform, false);
        RectTransform overlayRT = overlayGO.GetComponent<RectTransform>();
        StretchFull(overlayRT);
        Image overlayImg = overlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.65f);
        overlayImg.raycastTarget = true;

        // Ana panel
        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

        Image panelBG = panelRoot.GetComponent<Image>();
        panelBG.color = new Color(0.1f, 0.1f, 0.15f, 0.96f);

        // VerticalLayoutGroup
        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 15, 15);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ── Baslik ──
        GameObject titleGO = MakeText("Title", panelRoot.transform, "PERK INVENTORY", 24, TextAlignmentOptions.Center);
        titleGO.GetComponent<TextMeshProUGUI>().color = new Color(0.9f, 0.85f, 0.6f);
        titleGO.AddComponent<LayoutElement>().preferredHeight = 35f;

        // ── Separator ──
        MakeSeparator(panelRoot.transform);

        // ── Active Section ──
        GameObject activeSectionGO = MakeText("ActiveTitle", panelRoot.transform, "ACTIVE (0/6)", 16, TextAlignmentOptions.Left);
        activeTitleText = activeSectionGO.GetComponent<TextMeshProUGUI>();
        activeTitleText.color = new Color(0.6f, 0.9f, 0.6f);
        activeSectionGO.AddComponent<LayoutElement>().preferredHeight = 24f;

        // Active slot konteyneri — yatay
        GameObject activeContainerGO = new GameObject("ActiveSlots", typeof(RectTransform));
        activeContainerGO.transform.SetParent(panelRoot.transform, false);
        activeSlotsContainer = activeContainerGO.GetComponent<RectTransform>();
        HorizontalLayoutGroup activeHLG = activeContainerGO.AddComponent<HorizontalLayoutGroup>();
        activeHLG.spacing = SLOT_SPACING;
        activeHLG.childAlignment = TextAnchor.MiddleCenter;
        activeHLG.childControlWidth = false;
        activeHLG.childControlHeight = false;
        activeHLG.childForceExpandWidth = false;
        activeHLG.childForceExpandHeight = false;
        var activeLE = activeContainerGO.AddComponent<LayoutElement>();
        activeLE.preferredHeight = SLOT_SIZE + 26f;
        activeLE.flexibleWidth = 1f;

        // ── Separator ──
        MakeSeparator(panelRoot.transform);

        // ── Stash Section ──
        GameObject stashSectionGO = MakeText("StashTitle", panelRoot.transform, "STASH", 16, TextAlignmentOptions.Left);
        stashTitleText = stashSectionGO.GetComponent<TextMeshProUGUI>();
        stashTitleText.color = new Color(0.7f, 0.7f, 0.9f);
        stashSectionGO.AddComponent<LayoutElement>().preferredHeight = 24f;

        // Stash grid konteyneri
        GameObject stashContainerGO = new GameObject("StashGrid", typeof(RectTransform));
        stashContainerGO.transform.SetParent(panelRoot.transform, false);
        stashGrid = stashContainerGO.GetComponent<RectTransform>();
        GridLayoutGroup glg = stashContainerGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(SLOT_SIZE + 40f, SLOT_SIZE + 26f);
        glg.spacing = new Vector2(SLOT_SPACING, SLOT_SPACING);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 6;
        glg.childAlignment = TextAnchor.UpperCenter;
        var stashLE = stashContainerGO.AddComponent<LayoutElement>();
        stashLE.preferredHeight = 200f;
        stashLE.flexibleHeight = 1f;
        stashLE.flexibleWidth = 1f;

        // ── Kapat butonu — sag ust kose ──
        GameObject closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(panelRoot.transform, false);
        closeBtnGO.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform closeBtnRT = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1f, 1f);
        closeBtnRT.anchorMax = new Vector2(1f, 1f);
        closeBtnRT.pivot = new Vector2(1f, 1f);
        closeBtnRT.anchoredPosition = new Vector2(-5f, -5f);
        closeBtnRT.sizeDelta = new Vector2(32f, 32f);

        Image closeBtnImg = closeBtnGO.GetComponent<Image>();
        closeBtnImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);

        closeButton = closeBtnGO.GetComponent<Button>();
        ColorBlock closeCB = closeButton.colors;
        closeCB.normalColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        closeCB.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        closeCB.pressedColor = new Color(0.5f, 0.15f, 0.15f, 1f);
        closeButton.colors = closeCB;
        closeButton.onClick.AddListener(Close);

        GameObject closeTxtGO = MakeText("X", closeBtnGO.transform, "X", 18, TextAlignmentOptions.Center);
        closeTxtGO.GetComponent<TextMeshProUGUI>().color = Color.white;
        closeTxtGO.GetComponent<TextMeshProUGUI>().raycastTarget = false;
        RectTransform closeTxtRT = closeTxtGO.GetComponent<RectTransform>();
        closeTxtRT.anchorMin = Vector2.zero;
        closeTxtRT.anchorMax = Vector2.one;
        closeTxtRT.offsetMin = Vector2.zero;
        closeTxtRT.offsetMax = Vector2.zero;

        canvasGO.SetActive(false);
    }

    // ======================================================
    // UI YENILEME
    // ======================================================

    public void RefreshUI()
    {
        if (panelRoot == null || RunManager.instance == null) return;

        if (activeTitleText != null)
            activeTitleText.text = $"ACTIVE ({RunManager.instance.activePerks.Count}/{RunManager.MAX_ACTIVE_PERKS})";

        if (stashTitleText != null)
        {
            int stashCount = RunManager.instance.inventoryPerks.Count;
            stashTitleText.text = stashCount > 0 ? $"STASH ({stashCount})" : "STASH";
        }

        ClearSlots(spawnedActiveSlots);
        for (int i = 0; i < RunManager.MAX_ACTIVE_PERKS; i++)
        {
            bool hasPerk = i < RunManager.instance.activePerks.Count;
            BasePerk perk = hasPerk ? RunManager.instance.activePerks[i] : null;
            GameObject slot = CreateSlot(perk, true, i);
            slot.transform.SetParent(activeSlotsContainer, false);
            spawnedActiveSlots.Add(slot);
        }

        ClearSlots(spawnedStashSlots);
        for (int i = 0; i < RunManager.instance.inventoryPerks.Count; i++)
        {
            BasePerk perk = RunManager.instance.inventoryPerks[i];
            GameObject slot = CreateSlot(perk, false, i);
            slot.transform.SetParent(stashGrid, false);
            spawnedStashSlots.Add(slot);
        }
    }

    private void ClearSlots(List<GameObject> list)
    {
        foreach (var go in list)
        {
            if (go != null) Destroy(go);
        }
        list.Clear();
    }

    // ======================================================
    // SLOT OLUSTURMA
    // ======================================================

    private GameObject CreateSlot(BasePerk perk, bool isActiveSlot, int index)
    {
        GameObject slot = new GameObject("Slot", typeof(RectTransform));
        VerticalLayoutGroup slotVLG = slot.AddComponent<VerticalLayoutGroup>();
        slotVLG.spacing = 2f;
        slotVLG.childAlignment = TextAnchor.UpperCenter;
        slotVLG.childControlWidth = false;
        slotVLG.childControlHeight = false;
        slotVLG.childForceExpandWidth = false;
        slotVLG.childForceExpandHeight = false;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        iconGO.transform.SetParent(slot.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = SLOT_SIZE;
        iconLE.preferredHeight = SLOT_SIZE;

        Image iconImg = iconGO.GetComponent<Image>();
        Button iconBtn = iconGO.GetComponent<Button>();

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

            if (isActiveSlot && index == selectedActiveIndex)
            {
                iconImg.color = new Color(1f, 1f, 0.7f, 1f);
                outline.effectColor = Color.yellow;
                outline.effectDistance = new Vector2(3f, 3f);
            }

            ColorBlock cb = iconBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.95f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.8f);
            iconBtn.colors = cb;

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
                lvTMP.text = perk.currentLevel.ToString();
                lvTMP.fontSize = 12;
                lvTMP.alignment = TextAlignmentOptions.BottomRight;
                lvTMP.color = new Color(1f, 0.9f, 0.4f);
                lvTMP.raycastTarget = false;
            }

            GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(slot.transform, false);
            RectTransform nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.sizeDelta = new Vector2(SLOT_SIZE + 40f, 20f);
            var nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.preferredWidth = SLOT_SIZE + 40f;
            nameLE.preferredHeight = 20f;
            TextMeshProUGUI nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
            nameTMP.text = perk.perkName;
            nameTMP.fontSize = 10;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = rarityColor;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;

            int capturedIndex = index;
            bool capturedIsActive = isActiveSlot;
            iconBtn.onClick.AddListener(() => OnSlotClicked(capturedIsActive, capturedIndex));
        }
        else
        {
            iconImg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);
            iconBtn.interactable = false;

            GameObject emptyNameGO = new GameObject("EmptyName", typeof(RectTransform));
            emptyNameGO.transform.SetParent(slot.transform, false);
            var emptyLE = emptyNameGO.AddComponent<LayoutElement>();
            emptyLE.preferredWidth = SLOT_SIZE + 40f;
            emptyLE.preferredHeight = 20f;
        }

        return slot;
    }

    // ======================================================
    // SLOT TIKLAMA MEKANIGI
    // ======================================================

    private void OnSlotClicked(bool isActiveSlot, int index)
    {
        if (RunManager.instance == null) return;

        if (isActiveSlot)
        {
            if (selectedActiveIndex == index)
            {
                selectedActiveIndex = -1;
                RefreshUI();
                return;
            }

            if (selectedActiveIndex >= 0)
            {
                selectedActiveIndex = -1;
                RefreshUI();
                return;
            }

            if (RunManager.instance.inventoryPerks.Count > 0)
            {
                selectedActiveIndex = index;
                RefreshUI();
            }
            else
            {
                if (RunManager.instance.activePerks.Count > 1 && index < RunManager.instance.activePerks.Count)
                {
                    RunManager.instance.MoveToInventory(index);
                    selectedActiveIndex = -1;
                }
            }
        }
        else
        {
            if (selectedActiveIndex >= 0 && selectedActiveIndex < RunManager.instance.activePerks.Count)
            {
                RunManager.instance.SwapPerk(selectedActiveIndex, index);
                selectedActiveIndex = -1;
            }
            else if (RunManager.instance.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
            {
                RunManager.instance.MoveToActive(index);
                selectedActiveIndex = -1;
            }
            else
            {
                selectedActiveIndex = -1;
                RefreshUI();
            }
        }
    }

    // ======================================================
    // YARDIMCI METODLAR
    // ======================================================

    private GameObject MakeText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return go;
    }

    private void MakeSeparator(Transform parent)
    {
        GameObject sepGO = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sepGO.transform.SetParent(parent, false);
        Image sepImg = sepGO.GetComponent<Image>();
        sepImg.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
        sepImg.raycastTarget = false;
        var sepLE = sepGO.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 1f;
        sepLE.flexibleWidth = 1f;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
