using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Perk envanter paneli — drag & drop ile perk yönetimi.
/// Aktif slotlar (üst) ve stash (alt) arasında sürükle-bırak.
/// Editor tool ile sahnede olusturulur (Tools > Setup Perk Inventory).
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

    // Spawnlanan slot objeleri
    private readonly List<GameObject> spawnedActiveSlots = new List<GameObject>();
    private readonly List<GameObject> spawnedStashSlots = new List<GameObject>();

    // Drag state
    private GameObject dragGhost;
    private bool isDragging;
    private bool dragIsActiveSlot;
    private int dragIndex;
    private Canvas rootCanvas;

    // Onceki timeScale
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
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (canvasGO != null) canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();

        // Ghost mouse takibi (unscaled çünkü timeScale=0)
        if (isDragging && dragGhost != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform, Input.mousePosition, null, out pos);
            dragGhost.GetComponent<RectTransform>().anchoredPosition = pos;
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
    // ACMA / KAPAMA
    // ======================================================

    public void Open()
    {
        if (canvasGO == null) BuildUI();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        canvasGO.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        rootCanvas = canvasGO.GetComponent<Canvas>();
        RefreshUI();
    }

    public void Close()
    {
        CancelDrag();
        if (canvasGO != null) canvasGO.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    // ======================================================
    // DRAG & DROP
    // ======================================================

    private void BeginDrag(bool isActiveSlot, int index, BasePerk perk, PointerEventData eventData)
    {
        if (perk == null) return;

        isDragging = true;
        dragIsActiveSlot = isActiveSlot;
        dragIndex = index;

        // Ghost ikonu oluştur — canvas'ın en üstünde, mouse takip eder
        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragGhost.transform.SetParent(canvasGO.transform, false);
        RectTransform ghostRT = dragGhost.GetComponent<RectTransform>();
        ghostRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);

        Image ghostImg = dragGhost.GetComponent<Image>();
        if (perk.icon != null)
        {
            ghostImg.sprite = perk.icon;
            ghostImg.color = new Color(1f, 1f, 1f, 0.8f);
        }
        else
        {
            ghostImg.color = new Color(0.5f, 0.5f, 0.6f, 0.8f);
        }
        ghostImg.raycastTarget = false;

        // Rarity outline
        Color rarityColor;
        ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out rarityColor);
        Outline outline = dragGhost.AddComponent<Outline>();
        outline.effectColor = rarityColor;
        outline.effectDistance = new Vector2(2f, 2f);

        // Ghost'u en üste getir
        dragGhost.transform.SetAsLastSibling();

        // Kaynak slot'u soluk yap
        HighlightSourceSlot(true);
    }

    private void EndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Hedef slot'u bul
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool handled = false;
        foreach (var result in results)
        {
            PerkSlotTag tag = result.gameObject.GetComponent<PerkSlotTag>();
            if (tag == null) tag = result.gameObject.GetComponentInParent<PerkSlotTag>();
            if (tag != null)
            {
                HandleDrop(tag.isActiveSlot, tag.slotIndex);
                handled = true;
                break;
            }
        }

        // Panel dışına bırakma: aktif perk'i stash'e at
        if (!handled && dragIsActiveSlot && RunManager.instance != null)
        {
            if (dragIndex < RunManager.instance.activePerks.Count && RunManager.instance.activePerks.Count > 1)
            {
                RunManager.instance.MoveToInventory(dragIndex);
            }
        }

        CancelDrag();
        RefreshUI();
    }

    private void HandleDrop(bool targetIsActive, int targetIndex)
    {
        if (RunManager.instance == null) return;

        // Kendine bırakma
        if (dragIsActiveSlot == targetIsActive && dragIndex == targetIndex) return;

        if (dragIsActiveSlot && targetIsActive)
        {
            // Aktif → Aktif: iki aktif perk yer değiştirir
            SwapActivePerks(dragIndex, targetIndex);
        }
        else if (dragIsActiveSlot && !targetIsActive)
        {
            // Aktif → Stash slot: swap yap (stash'teki perk aktife, aktifteki stash'e)
            if (targetIndex < RunManager.instance.inventoryPerks.Count)
            {
                RunManager.instance.SwapPerk(dragIndex, targetIndex);
            }
            else
            {
                // Boş stash alanına bırakma: aktif'ten stash'e taşı
                if (RunManager.instance.activePerks.Count > 1)
                    RunManager.instance.MoveToInventory(dragIndex);
            }
        }
        else if (!dragIsActiveSlot && targetIsActive)
        {
            // Stash → Aktif slot: hedef dolu ise swap, boş ise taşı
            if (targetIndex < RunManager.instance.activePerks.Count)
            {
                // Aktif slot dolu — swap
                RunManager.instance.SwapPerk(targetIndex, dragIndex);
            }
            else
            {
                // Aktif slot boş — taşı
                if (RunManager.instance.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
                    RunManager.instance.MoveToActive(dragIndex);
            }
        }
        else
        {
            // Stash → Stash: sıra değiştir
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
    // UI OLUSTURMA
    // ======================================================

    public void BuildUI()
    {
        canvasGO = new GameObject("PerkInventoryCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        overlayGO = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(canvasGO.transform, false);
        StretchFull(overlayGO.GetComponent<RectTransform>());
        Image overlayImg = overlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.65f);
        overlayImg.raycastTarget = true;

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
        panelRoot.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.96f);

        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 15, 15);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Baslik
        GameObject titleGO = MakeText("Title", panelRoot.transform, "PERK INVENTORY", 24, TextAlignmentOptions.Center);
        titleGO.GetComponent<TextMeshProUGUI>().color = new Color(0.9f, 0.85f, 0.6f);
        titleGO.AddComponent<LayoutElement>().preferredHeight = 35f;

        MakeSeparator(panelRoot.transform);

        // Active section
        GameObject activeSectionGO = MakeText("ActiveTitle", panelRoot.transform, "ACTIVE (0/6)", 16, TextAlignmentOptions.Left);
        activeTitleText = activeSectionGO.GetComponent<TextMeshProUGUI>();
        activeTitleText.color = new Color(0.6f, 0.9f, 0.6f);
        activeSectionGO.AddComponent<LayoutElement>().preferredHeight = 24f;

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

        MakeSeparator(panelRoot.transform);

        // Stash section
        GameObject stashSectionGO = MakeText("StashTitle", panelRoot.transform, "STASH", 16, TextAlignmentOptions.Left);
        stashTitleText = stashSectionGO.GetComponent<TextMeshProUGUI>();
        stashTitleText.color = new Color(0.7f, 0.7f, 0.9f);
        stashSectionGO.AddComponent<LayoutElement>().preferredHeight = 24f;

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

        // Close button
        GameObject closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(panelRoot.transform, false);
        closeBtnGO.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform closeBtnRT = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1f, 1f);
        closeBtnRT.anchorMax = new Vector2(1f, 1f);
        closeBtnRT.pivot = new Vector2(1f, 1f);
        closeBtnRT.anchoredPosition = new Vector2(-5f, -5f);
        closeBtnRT.sizeDelta = new Vector2(32f, 32f);
        closeBtnGO.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f, 0.9f);

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
            if (go != null) Destroy(go);
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

        // İkon alanı
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = SLOT_SIZE;
        iconLE.preferredHeight = SLOT_SIZE;

        Image iconImg = iconGO.GetComponent<Image>();

        // Drop hedefi için tag ekle (perk olsun olmasın)
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

            // Level gösterge
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

            // İsim etiketi
            GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(slot.transform, false);
            nameGO.GetComponent<RectTransform>().sizeDelta = new Vector2(SLOT_SIZE + 40f, 20f);
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

            // Drag event'leri
            EventTrigger trigger = iconGO.AddComponent<EventTrigger>();

            int ci = index;
            bool cia = isActiveSlot;
            BasePerk cp = perk;

            var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginEntry.callback.AddListener((data) => BeginDrag(cia, ci, cp, (PointerEventData)data));
            trigger.triggers.Add(beginEntry);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => { }); // Update() handles ghost position
            trigger.triggers.Add(dragEntry);

            var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endEntry.callback.AddListener((data) => EndDrag((PointerEventData)data));
            trigger.triggers.Add(endEntry);
        }
        else
        {
            // Boş slot
            iconImg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);

            GameObject emptyNameGO = new GameObject("EmptyName", typeof(RectTransform));
            emptyNameGO.transform.SetParent(slot.transform, false);
            var emptyLE = emptyNameGO.AddComponent<LayoutElement>();
            emptyLE.preferredWidth = SLOT_SIZE + 40f;
            emptyLE.preferredHeight = 20f;
        }

        return slot;
    }

    // ======================================================
    // YARDIMCI
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
        sepGO.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
        sepGO.GetComponent<Image>().raycastTarget = false;
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

/// <summary>
/// Slot'u tanımlayan tag — drop hedefini bulmak için kullanılır.
/// </summary>
public class PerkSlotTag : MonoBehaviour
{
    public bool isActiveSlot;
    public int slotIndex;
}
