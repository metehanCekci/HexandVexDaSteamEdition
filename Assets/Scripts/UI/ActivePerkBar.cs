using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ekranin ust-ortasinda aktif perk ikonlarini gosteren kalici bar.
/// Editor tool ile sahnede olusturulur (Tools > Setup Active Perk Bar).
/// Sahnede yoksa runtime'da otomatik olusturulur (fallback).
/// Drag ile perk sıralama — sıra priority'yi belirler.
/// </summary>
public class ActivePerkBar : MonoBehaviour
{
    public static ActivePerkBar instance;

    [Header("Referanslar (Editor Tool Atar)")]
    public Canvas barCanvas;
    public RectTransform container;
    public GameObject tooltipObj;
    public TextMeshProUGUI tooltipText;
    public CanvasGroup tooltipCanvasGroup;

    // Spawnlanan ikon objeleri — RefreshBar'da yeniden olusturulur
    private readonly List<GameObject> spawnedIcons = new List<GameObject>();
    private readonly List<BasePerk> spawnedPerks = new List<BasePerk>();

    // Drag reorder state
    private bool isDragging;
    private int dragIndex = -1;
    private GameObject dragGhost;
    private Canvas rootCanvas;

    private const float ICON_SIZE = 48f;
    private const float ICON_SPACING = 8f;
    private const float TOOLTIP_WIDTH = 260f;
    private const float TOOLTIP_HEIGHT = 80f;

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
        // rootCanvas referansı al
        if (barCanvas != null)
            rootCanvas = barCanvas;
    }

    void Update()
    {
        // Ghost mouse takibi
        if (isDragging && dragGhost != null && rootCanvas != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform, Input.mousePosition, null, out pos);
            dragGhost.GetComponent<RectTransform>().anchoredPosition = pos;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Runtime fallback — sahnede yoksa koddan olusturur.</summary>
    public static void CreateFromCode()
    {
        if (instance != null) return;

        // Sahnede zaten var mi?
        var existing = Object.FindFirstObjectByType<ActivePerkBar>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        // Yoksa koddan olustur (eski fallback yol)
        GameObject rootGO = new GameObject("ActivePerkBar");
        ActivePerkBar bar = rootGO.AddComponent<ActivePerkBar>();
        // Awake zaten instance + DontDestroyOnLoad yapar

        // Canvas
        GameObject canvasGO = new GameObject("PerkBarCanvas");
        canvasGO.transform.SetParent(rootGO.transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        bar.barCanvas = canvas;

        // Ikon container — ust-orta, yatay duzenleme
        GameObject containerGO = new GameObject("IconContainer", typeof(RectTransform));
        containerGO.transform.SetParent(canvasGO.transform, false);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.anchoredPosition = new Vector2(0f, -10f);
        containerRT.sizeDelta = new Vector2(600f, ICON_SIZE + 8f);

        HorizontalLayoutGroup hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = ICON_SPACING;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter csf = containerGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bar.container = containerRT;

        // Tooltip
        bar.BuildTooltip(canvasGO.transform);
    }

    // Editor tool'dan da cagrilabilmesi icin public yapildi
    public void BuildTooltip(Transform canvasTransform)
    {
        tooltipObj = new GameObject("PerkTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObj.transform.SetParent(canvasTransform, false);
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        ttRT.sizeDelta = new Vector2(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
        ttRT.anchorMin = new Vector2(0.5f, 1f);
        ttRT.anchorMax = new Vector2(0.5f, 1f);
        ttRT.pivot = new Vector2(0.5f, 1f);
        ttRT.anchoredPosition = new Vector2(0f, -(ICON_SIZE + 24f));

        Image bg = tooltipObj.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        bg.raycastTarget = false;

        tooltipCanvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;

        GameObject textGO = new GameObject("TooltipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(tooltipObj.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 6f);
        textRT.offsetMax = new Vector2(-8f, -6f);

        tooltipText = textGO.GetComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 13;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltipText.enableWordWrapping = true;
        tooltipText.richText = true;
        tooltipText.raycastTarget = false;

        var ttCSF = tooltipObj.AddComponent<ContentSizeFitter>();
        ttCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ttCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var ttLE = tooltipObj.AddComponent<LayoutElement>();
        ttLE.preferredWidth = TOOLTIP_WIDTH;

        tooltipObj.SetActive(false);
    }

    // ======================================================
    // DRAG REORDER
    // ======================================================

    private void BeginDragIcon(int index, PointerEventData eventData)
    {
        if (RunManager.instance == null) return;
        if (index < 0 || index >= RunManager.instance.activePerks.Count) return;

        isDragging = true;
        dragIndex = index;
        HideTooltip();

        BasePerk perk = RunManager.instance.activePerks[index];

        // Ghost oluştur
        if (rootCanvas == null && barCanvas != null) rootCanvas = barCanvas;
        if (rootCanvas == null) return;

        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragGhost.transform.SetParent(rootCanvas.transform, false);
        RectTransform ghostRT = dragGhost.GetComponent<RectTransform>();
        ghostRT.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

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

        // Kaynak ikonu soluk yap
        if (dragIndex >= 0 && dragIndex < spawnedIcons.Count && spawnedIcons[dragIndex] != null)
        {
            CanvasGroup cg = spawnedIcons[dragIndex].GetComponent<CanvasGroup>();
            if (cg == null) cg = spawnedIcons[dragIndex].AddComponent<CanvasGroup>();
            cg.alpha = 0.35f;
        }
    }

    private void EndDragIcon(PointerEventData eventData)
    {
        if (!isDragging) return;

        // En yakın slot'u bul — mouse X pozisyonuna göre
        int targetIndex = FindClosestIconIndex(eventData.position);

        if (targetIndex >= 0 && targetIndex != dragIndex && RunManager.instance != null)
        {
            var perks = RunManager.instance.activePerks;
            if (dragIndex >= 0 && dragIndex < perks.Count && targetIndex < perks.Count)
            {
                // Perk'i listede taşı (insert, remove mantığı)
                BasePerk moving = perks[dragIndex];
                perks.RemoveAt(dragIndex);
                perks.Insert(targetIndex, moving);

                // Priority güncelle
                UpdatePerkPriorities();
            }
        }

        CancelDrag();
        RunManager.instance?.RefreshPerkUI();
    }

    private int FindClosestIconIndex(Vector2 screenPos)
    {
        if (container == null || spawnedIcons.Count == 0) return -1;

        float minDist = float.MaxValue;
        int closest = -1;

        for (int i = 0; i < spawnedIcons.Count; i++)
        {
            if (spawnedIcons[i] == null) continue;
            RectTransform rt = spawnedIcons[i].GetComponent<RectTransform>();
            Vector3 worldPos = rt.position;
            Vector2 iconScreenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            float dist = Mathf.Abs(iconScreenPos.x - screenPos.x);
            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        return closest;
    }

    private void CancelDrag()
    {
        // Kaynak ikonu eski haline getir
        if (dragIndex >= 0 && dragIndex < spawnedIcons.Count && spawnedIcons[dragIndex] != null)
        {
            CanvasGroup cg = spawnedIcons[dragIndex].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
        isDragging = false;
        dragIndex = -1;
    }

    /// <summary>Aktif perk'i stash'e gönderir (sağ tık ile).</summary>
    private void UnequipPerk(int index)
    {
        if (RunManager.instance == null) return;
        if (index < 0 || index >= RunManager.instance.activePerks.Count) return;

        RunManager.instance.MoveToInventory(index);
        HideTooltip();
        UpdatePerkPriorities();
        RunManager.instance.RefreshPerkUI();
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

    // ======================================================
    // BAR YENILEME
    // ======================================================

    public void RefreshBar()
    {
        foreach (var icon in spawnedIcons)
        {
            if (icon != null) Destroy(icon);
        }
        spawnedIcons.Clear();
        spawnedPerks.Clear();

        if (RunManager.instance == null || container == null) return;

        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk == null) continue;
            GameObject iconGO = CreatePerkIcon(perk);
            iconGO.transform.SetParent(container, false);
            spawnedIcons.Add(iconGO);
            spawnedPerks.Add(perk);
        }
    }

    private GameObject CreatePerkIcon(BasePerk perk)
    {
        // Dış kapsayıcı — kare
        GameObject iconGO = new GameObject("PerkIcon_" + perk.perkName, typeof(RectTransform));

        RectTransform rt = iconGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

        LayoutElement le = iconGO.AddComponent<LayoutElement>();
        le.preferredWidth = ICON_SIZE;
        le.preferredHeight = ICON_SIZE;

        // Arka plan (koyu kare)
        GameObject bgGO = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(iconGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        bgImg.raycastTarget = false;

        // Perk ikonu — sprite varsa göster
        if (perk.icon != null)
        {
            GameObject spriteGO = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            spriteGO.transform.SetParent(iconGO.transform, false);
            RectTransform spriteRT = spriteGO.GetComponent<RectTransform>();
            spriteRT.anchorMin = new Vector2(0.1f, 0.1f);
            spriteRT.anchorMax = new Vector2(0.9f, 0.9f);
            spriteRT.offsetMin = Vector2.zero;
            spriteRT.offsetMax = Vector2.zero;
            Image spriteImg = spriteGO.GetComponent<Image>();
            spriteImg.sprite = perk.icon;
            spriteImg.preserveAspect = true;
            spriteImg.color = Color.white;
            spriteImg.raycastTarget = false;
        }

        // Rarity renk kenarlığı
        Color rarityColor;
        ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out rarityColor);
        Outline outline = bgGO.AddComponent<Outline>();
        outline.effectColor = rarityColor;
        outline.effectDistance = new Vector2(2f, 2f);

        // Level gösterge
        if (perk.currentLevel > 1)
        {
            GameObject lvGO = new GameObject("LvText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lvGO.transform.SetParent(iconGO.transform, false);
            RectTransform lvRT = lvGO.GetComponent<RectTransform>();
            lvRT.anchorMin = new Vector2(1f, 0f);
            lvRT.anchorMax = new Vector2(1f, 0f);
            lvRT.pivot = new Vector2(1f, 0f);
            lvRT.anchoredPosition = new Vector2(2f, -2f);
            lvRT.sizeDelta = new Vector2(20f, 16f);
            TextMeshProUGUI lvTMP = lvGO.GetComponent<TextMeshProUGUI>();
            lvTMP.text = perk.currentLevel.ToString();
            lvTMP.fontSize = 11;
            lvTMP.alignment = TextAlignmentOptions.BottomRight;
            lvTMP.color = new Color(1f, 0.9f, 0.4f);
            lvTMP.raycastTarget = false;
        }

        // Raycast için şeffaf Image
        Image raycastImg = iconGO.AddComponent<Image>();
        raycastImg.color = new Color(0f, 0f, 0f, 0f);

        // Event triggers — hover (tooltip) + drag (reorder)
        EventTrigger trigger = iconGO.AddComponent<EventTrigger>();
        BasePerk capturedPerk = perk;
        int capturedIndex = spawnedPerks.Count; // Henüz eklenmedi, Count = index olacak

        // Hover: tooltip
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => { if (!isDragging) ShowTooltip(capturedPerk, iconGO.GetComponent<RectTransform>()); });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exitEntry);

        // Sağ tık: perk'i çıkar (stash'e gönder)
        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener((data) =>
        {
            PointerEventData ped = (PointerEventData)data;
            if (ped.button == PointerEventData.InputButton.Right)
                UnequipPerk(capturedIndex);
        });
        trigger.triggers.Add(clickEntry);

        // Drag: reorder
        var beginDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDragEntry.callback.AddListener((data) => BeginDragIcon(capturedIndex, (PointerEventData)data));
        trigger.triggers.Add(beginDragEntry);

        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => { }); // Update() handles ghost
        trigger.triggers.Add(dragEntry);

        var endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener((data) => EndDragIcon((PointerEventData)data));
        trigger.triggers.Add(endDragEntry);

        return iconGO;
    }

    // ======================================================
    // TOOLTIP
    // ======================================================

    private void ShowTooltip(BasePerk perk, RectTransform iconRT)
    {
        if (tooltipObj == null || tooltipText == null) return;

        string rarityHex = PerkListUI.GetRarityHex(perk.rarity);
        string desc = string.IsNullOrEmpty(perk.description) ? "" : $"\n<color=#AAAAAA>{perk.description}</color>";
        string prioText = $"  <color=#888888>#{perk.priority + 1}</color>";
        tooltipText.text = $"<color={rarityHex}>{perk.perkName}</color>  <color=#CCCCCC>Lv {perk.currentLevel}</color>{prioText}{desc}";

        tooltipObj.SetActive(true);

        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        Vector3 iconWorldPos = iconRT.position;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            ttRT.parent as RectTransform, iconWorldPos, null, out localPoint);
        ttRT.anchoredPosition = new Vector2(localPoint.x, -(ICON_SIZE + 24f));

        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 1f;
    }

    private void HideTooltip()
    {
        if (tooltipObj == null) return;
        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        tooltipObj.SetActive(false);
    }

    // ======================================================
    // GÖRSEL ANİMASYONLAR
    // ======================================================

    public void TriggerPopForPerk(BasePerk perk)
    {
        int idx = spawnedPerks.IndexOf(perk);
        if (idx < 0 || idx >= spawnedIcons.Count) return;
        GameObject iconGO = spawnedIcons[idx];
        if (iconGO != null && iconGO.activeInHierarchy)
            StartCoroutine(IconPopAnim(iconGO.GetComponent<RectTransform>()));
    }

    public void TriggerShakeForPerk(BasePerk perk)
    {
        int idx = spawnedPerks.IndexOf(perk);
        if (idx < 0 || idx >= spawnedIcons.Count) return;
        GameObject iconGO = spawnedIcons[idx];
        if (iconGO != null && iconGO.activeInHierarchy)
            StartCoroutine(IconShakeAnim(iconGO.GetComponent<RectTransform>()));
    }

    private IEnumerator IconPopAnim(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 baseScale = Vector3.one;
        float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;
            rt.localScale = new Vector3(s, s, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = baseScale;
    }

    private IEnumerator IconShakeAnim(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 origin = rt.localPosition;
        float duration = 0.35f;
        float elapsed = 0f;
        float magnitude = 4f;
        float frequency = 35f;
        while (elapsed < duration)
        {
            float x = Mathf.Sin(elapsed * frequency) * magnitude * (1f - elapsed / duration);
            rt.localPosition = origin + new Vector3(x, 0f, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localPosition = origin;
    }
}
