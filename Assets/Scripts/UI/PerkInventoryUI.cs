using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Perk envanter yan paneli — map ekranında her zaman görünür.
/// Drag & drop ile aktif slotlar ve stash arasında perk yönetimi.
/// MapUI.Show/Hide ile birlikte açılır/kapanır.
/// Editor tool ile sahnede olusturulur (Tools > Setup Perk Inventory).
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

    // Spawnlanan slot objeleri
    private readonly List<GameObject> spawnedActiveSlots = new List<GameObject>();
    private readonly List<GameObject> spawnedStashSlots = new List<GameObject>();

    // Drag state
    private GameObject dragGhost;
    private bool isDragging;
    private bool dragIsActiveSlot;
    private int dragIndex;
    private Canvas rootCanvas;

    private const float SLOT_SIZE = 56f;
    private const float SLOT_SPACING = 6f;
    private const float PANEL_WIDTH = 280f;

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
        // Başlangıçta gizli — MapUI.Show() açacak
        if (canvasGO != null) canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        // Ghost mouse takibi
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
    // GÖSTER / GİZLE (MapUI tarafından çağrılır)
    // ======================================================

    public void Show()
    {
        if (canvasGO == null) BuildUI();
        canvasGO.SetActive(true);
        rootCanvas = canvasGO.GetComponent<Canvas>();
        RefreshUI();
    }

    public void Hide()
    {
        CancelDrag();
        if (canvasGO != null) canvasGO.SetActive(false);
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
                RunManager.instance.SwapPerk(targetIndex, dragIndex);
            }
            else
            {
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
        // Priority güncelle — sıra = priority
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

    /// <summary>Aktif perklerin priority'sini liste sırasına göre günceller.</summary>
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
    // UI OLUŞTURMA — Sağ tarafta dikey yan panel
    // ======================================================

    public void BuildUI()
    {
        // Canvas — map canvas'ının üzerinde
        canvasGO = new GameObject("PerkSidePanelCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 91; // Map canvas (90) üzerinde
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel — sağ üst köşe, sabit boyut
        float activeGridH = (SLOT_SIZE + 20f) * 2 + SLOT_SPACING + 4f;
        float stashGridH = (SLOT_SIZE + 20f) * 2 + SLOT_SPACING + 4f;
        // padding(10) + title(28) + sp(2) + activeTitle(20) + sp(2) + activeGrid + sp(2) + sep(45) + sp(2) + stashTitle(20) + sp(2) + stashGrid + padding(10)
        float panelH = 10f + 28f + 2f + 20f + 2f + activeGridH + 2f + 45f + 2f + 20f + 2f + stashGridH + 10f;

        panelRoot = new GameObject("SidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta = new Vector2(PANEL_WIDTH, panelH);
        panelRoot.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Başlık
        GameObject titleGO = MakeText("Title", panelRoot.transform, "PERKS", 20, TextAlignmentOptions.Center);
        titleGO.GetComponent<TextMeshProUGUI>().color = Color.white;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 28f;

        // Active section (ilk separator silindi)
        GameObject activeSectionGO = MakeText("ActiveTitle", panelRoot.transform, "ACTIVE (0/6)", 14, TextAlignmentOptions.Left);
        activeTitleText = activeSectionGO.GetComponent<TextMeshProUGUI>();
        activeTitleText.color = Color.white;
        activeSectionGO.AddComponent<LayoutElement>().preferredHeight = 20f;

        // Active slots — 2 satır 3 sütun grid
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
        activeLE.flexibleWidth = 1f;

        // Kısa separator
        MakeShortSeparator(panelRoot.transform);

        // Stash section
        GameObject stashSectionGO = MakeText("StashTitle", panelRoot.transform, "STASH", 14, TextAlignmentOptions.Left);
        stashTitleText = stashSectionGO.GetComponent<TextMeshProUGUI>();
        stashTitleText.color = Color.white;
        stashSectionGO.AddComponent<LayoutElement>().preferredHeight = 20f;

        // Stash grid
        GameObject stashScrollGO = new GameObject("StashScroll", typeof(RectTransform));
        stashScrollGO.transform.SetParent(panelRoot.transform, false);
        var stashScrollLE = stashScrollGO.AddComponent<LayoutElement>();
        stashScrollLE.preferredHeight = stashGridH;
        stashScrollLE.flexibleWidth = 1f;

        GameObject stashContainerGO = new GameObject("StashGrid", typeof(RectTransform));
        stashContainerGO.transform.SetParent(stashScrollGO.transform, false);
        stashGrid = stashContainerGO.GetComponent<RectTransform>();
        stashGrid.anchorMin = Vector2.zero;
        stashGrid.anchorMax = Vector2.one;
        stashGrid.offsetMin = Vector2.zero;
        stashGrid.offsetMax = Vector2.zero;

        GridLayoutGroup glg = stashContainerGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(SLOT_SIZE + 20f, SLOT_SIZE + 20f);
        glg.spacing = new Vector2(SLOT_SPACING, SLOT_SPACING);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.UpperCenter;

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
    // SLOT OLUŞTURMA
    // ======================================================

    private GameObject CreateSlot(BasePerk perk, bool isActiveSlot, int index)
    {
        GameObject slot = new GameObject("Slot", typeof(RectTransform));

        // İkon alanı
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 1f);
        iconRT.anchorMax = new Vector2(0.5f, 1f);
        iconRT.pivot = new Vector2(0.5f, 1f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);

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
                lvTMP.fontSize = 11;
                lvTMP.alignment = TextAlignmentOptions.BottomRight;
                lvTMP.color = new Color(1f, 0.9f, 0.4f);
                lvTMP.raycastTarget = false;
            }

            // Priority gösterge (aktif slotlar için)
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

            // İsim etiketi
            GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(slot.transform, false);
            RectTransform nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.5f, 1f);
            nameRT.anchorMax = new Vector2(0.5f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.anchoredPosition = new Vector2(0f, -(SLOT_SIZE + 1f));
            nameRT.sizeDelta = new Vector2(SLOT_SIZE + 20f, 16f);
            TextMeshProUGUI nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
            nameTMP.text = perk.perkName;
            nameTMP.fontSize = 9;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = rarityColor;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;

            // Sağ tık: aktif perk'i stash'e / stash perk'i aktife taşı
            EventTrigger trigger = iconGO.AddComponent<EventTrigger>();

            int ci = index;
            bool cia = isActiveSlot;
            BasePerk cp = perk;

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) =>
            {
                PointerEventData ped = (PointerEventData)data;
                if (ped.button == PointerEventData.InputButton.Right)
                {
                    if (cia)
                    {
                        // Aktif → Stash'e gönder
                        if (RunManager.instance != null && ci < RunManager.instance.activePerks.Count)
                        {
                            RunManager.instance.MoveToInventory(ci);
                            UpdatePerkPriorities();
                            RunManager.instance.RefreshPerkUI();
                        }
                    }
                    else
                    {
                        // Stash → Aktife taşı (yer varsa)
                        if (RunManager.instance != null && RunManager.instance.activePerks.Count < RunManager.MAX_ACTIVE_PERKS)
                        {
                            RunManager.instance.MoveToActive(ci);
                            UpdatePerkPriorities();
                            RunManager.instance.RefreshPerkUI();
                        }
                    }
                }
            });
            trigger.triggers.Add(clickEntry);

            // Drag event'leri
            var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginEntry.callback.AddListener((data) => BeginDrag(cia, ci, cp, (PointerEventData)data));
            trigger.triggers.Add(beginEntry);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => { }); // Update() handles ghost position
            trigger.triggers.Add(dragEntry);

            var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endEntry.callback.AddListener((data) => EndDrag((PointerEventData)data));
            trigger.triggers.Add(endEntry);

            // Hover: tooltip
            RectTransform hoverRT = iconGO.GetComponent<RectTransform>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => { if (!isDragging) ShowTooltip(cp, hoverRT); });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => HideTooltip());
            trigger.triggers.Add(exitEntry);
        }
        else
        {
            // Boş slot
            iconImg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);
        }

        return slot;
    }

    // ======================================================
    // TOOLTIP
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
        bg.color = new Color(0f, 0.02f, 0.047f, 0.95f); // #00050c
        bg.raycastTarget = false;

        tooltipCanvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;

        // ── Üst satır: sol üst = başlık, sağ üst = level ──

        // Perk adı — sol üst
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

        // Level — sağ üst
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

        // Açıklama — alt kısım
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
        if (tooltipObj == null || tooltipNameText == null || perk == null) return;

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

        // Arkaplan rengini zorla ayarla (sahneye kayıtlı eski değeri override et)
        Image ttBg = tooltipObj.GetComponent<Image>();
        if (ttBg != null) ttBg.color = new Color(0f, 0.02f, 0.047f, 0.95f);

        // Tooltip yüksekliğini içeriğe göre ayarla
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

        // Slot'un world-space köşelerini al ve canvas local'ine dönüştür
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

        Vector3[] slotCorners = new Vector3[4]; // 0=bottomLeft,1=topLeft,2=topRight,3=bottomRight
        slotRT.GetWorldCorners(slotCorners);
        // Slot'un sol kenarının ortası (world space)
        Vector3 slotLeftCenter = (slotCorners[0] + slotCorners[1]) / 2f;

        // World → canvas local
        Vector3 localPoint = canvasRT.InverseTransformPoint(slotLeftCenter);

        // Tooltip'in pivot'u (1, 0.5) = sağ-orta, yani sağ kenarı slot'un soluna yaslanır
        ttRT.localPosition = new Vector3(localPoint.x - 10f, localPoint.y, 0f);

        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 1f;
    }

    private void HideTooltip()
    {
        if (tooltipObj == null) return;
        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        tooltipObj.SetActive(false);
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

    private void MakeShortSeparator(Transform parent)
    {
        GameObject sepGO = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sepGO.transform.SetParent(parent, false);
        RectTransform sepRT = sepGO.GetComponent<RectTransform>();
        sepRT.sizeDelta = new Vector2(160f, 45f);
        sepGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        sepGO.GetComponent<Image>().raycastTarget = false;
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
