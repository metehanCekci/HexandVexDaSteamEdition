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
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipLevelText;
    public TextMeshProUGUI tooltipDescText;
    public CanvasGroup tooltipCanvasGroup;

    // Spawnlanan ikon objeleri — RefreshBar'da yeniden olusturulur
    private readonly List<GameObject> spawnedIcons = new List<GameObject>();
    private readonly List<BasePerk> spawnedPerks = new List<BasePerk>();

    // Drag reorder state
    private bool isDragging;
    private int dragIndex = -1;
    private GameObject dragGhost;
    private Canvas rootCanvas;

    /// <summary>true iken RefreshBar bounce animasyonu atlanır.</summary>
    [System.NonSerialized] public bool skipBounce;

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
        scaler.matchWidthOrHeight = 0.5f;
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
        float pad = 8f;
        float topRowH = 22f;

        tooltipObj = new GameObject("PerkTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObj.transform.SetParent(canvasTransform, false);
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        ttRT.sizeDelta = new Vector2(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
        ttRT.anchorMin = new Vector2(0.5f, 1f);
        ttRT.anchorMax = new Vector2(0.5f, 1f);
        ttRT.pivot = new Vector2(0.5f, 1f);
        ttRT.anchoredPosition = new Vector2(0f, -(ICON_SIZE + 24f));

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
        tooltipNameText.textWrappingMode = TextWrappingModes.NoWrap;
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
        tooltipLevelText.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipLevelText.raycastTarget = false;

        // Açıklama — alt kısım (üst satırın altından başlar, aşağı uzar)
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
        tooltipDescText.textWrappingMode = TextWrappingModes.Normal;
        tooltipDescText.richText = true;
        tooltipDescText.raycastTarget = false;

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

        // Stash paneli açık değilse sağ tık unequip'i engelle (combat sırasında kazara atmasın)
        if (PerkInventoryUI.instance == null || !PerkInventoryUI.instance.IsOpen) return;

        HideTooltip();

        // PerkInventoryUI'da fly animasyonu başlat (üst bardan stash'e uçsun)
        BasePerk perk = RunManager.instance.activePerks[index];
        GameObject iconGO = (index < spawnedIcons.Count) ? spawnedIcons[index] : null;
        if (PerkInventoryUI.instance != null && iconGO != null)
            PerkInventoryUI.instance.BeginFlyAnimExternal(perk, iconGO);

        skipBounce = true;
        RunManager.instance.MoveToInventory(index);
        skipBounce = false;
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
        HideTooltip();
        foreach (var icon in spawnedIcons)
        {
            if (icon != null) Destroy(icon);
        }
        spawnedIcons.Clear();
        spawnedPerks.Clear();

        if (RunManager.instance == null || container == null) return;

        int i = 0;
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk == null) continue;
            GameObject iconGO = CreatePerkIcon(perk);
            iconGO.transform.SetParent(container, false);
            spawnedIcons.Add(iconGO);
            spawnedPerks.Add(perk);

            // Staggered bounce-in animasyonu (skipBounce aktifse atla)
            if (!skipBounce)
                StartCoroutine(IconBounceIn(iconGO.GetComponent<RectTransform>(), i * 0.05f));
            i++;
        }
    }

    private IEnumerator IconBounceIn(RectTransform rt, float delay)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            if (rt == null) yield break;
            float t = elapsed / duration;
            float s = EaseOutBack(t);
            rt.localScale = Vector3.one * s;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
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
            spriteImg.raycastTarget = false;
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

        // Raycast için şeffaf Image
        Image raycastImg = iconGO.AddComponent<Image>();
        raycastImg.color = new Color(0f, 0f, 0f, 0f);

        // Event triggers — hover (tooltip) + drag (reorder)
        EventTrigger trigger = iconGO.AddComponent<EventTrigger>();
        BasePerk capturedPerk = perk;
        int capturedIndex = spawnedPerks.Count; // Henüz eklenmedi, Count = index olacak

        // Hover: tooltip + scale
        RectTransform hoverIconRT = iconGO.GetComponent<RectTransform>();
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) =>
        {
            if (!isDragging)
            {
                ShowTooltip(capturedPerk, hoverIconRT);
                StartCoroutine(HoverScaleAnim(hoverIconRT, true));
            }
        });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) =>
        {
            HideTooltip();
            StartCoroutine(HoverScaleAnim(hoverIconRT, false));
        });
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
    // HOVER SCALE
    // ======================================================

    private IEnumerator HoverScaleAnim(RectTransform rt, bool enter)
    {
        if (rt == null) yield break;
        float target = enter ? 1.12f : 1f;
        float duration = 0.12f;
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
    // TOOLTIP
    // ======================================================

    private Coroutine tooltipFadeCoroutine;

    private void ShowTooltip(BasePerk perk, RectTransform iconRT)
    {
        if (perk == null) return;

        // Tooltip yoksa yeniden oluştur
        if (tooltipObj == null || tooltipNameText == null)
        {
            if (barCanvas != null) BuildTooltip(barCanvas.transform);
            else return;
        }

        string rarityHex = PerkListUI.GetRarityHex(perk.rarity);
        Color rarityColor;
        ColorUtility.TryParseHtmlString(rarityHex, out rarityColor);

        tooltipNameText.text = perk.perkName;
        tooltipNameText.color = rarityColor;

        if (tooltipLevelText != null)
            tooltipLevelText.text = $"Lv {perk.currentLevel}";

        perk.RebuildDescription();
        if (tooltipDescText != null)
        {
            string body = string.IsNullOrEmpty(perk.description) ? "" : perk.description;
            if (perk.IsIncompatible())
            {
                string reason = perk.GetIncompatibleReason();
                string badge = $"<color=#{UIColors.CantAfford}><b>INCOMPATIBLE</b></color>";
                string reasonLine = string.IsNullOrEmpty(reason) ? "" : $"\n<color=#{UIColors.CantAfford}>{reason}</color>";
                tooltipDescText.text = $"{badge}{reasonLine}\n\n{body}";
            }
            else
            {
                tooltipDescText.text = body;
            }
        }

        tooltipObj.SetActive(true);
        tooltipObj.transform.SetAsLastSibling();

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
        if (totalH < TOOLTIP_HEIGHT) totalH = TOOLTIP_HEIGHT;
        ttRT.sizeDelta = new Vector2(TOOLTIP_WIDTH, totalH);
        RectTransform canvasRT = barCanvas.transform as RectTransform;

        Vector3[] iconCorners = new Vector3[4]; // 0=bottomLeft,1=topLeft,2=topRight,3=bottomRight
        iconRT.GetWorldCorners(iconCorners);
        Vector3 iconBottomCenter = (iconCorners[0] + iconCorners[3]) / 2f;

        // World → canvas local
        Vector3 localPoint = canvasRT.InverseTransformPoint(iconBottomCenter);

        // Tooltip'in pivot'u (0.5, 1) = üst-orta → ikonun altına yerleş
        ttRT.localPosition = new Vector3(localPoint.x, localPoint.y - 12f, 0f);

        // Fade in
        if (tooltipFadeCoroutine != null) StopCoroutine(tooltipFadeCoroutine);
        tooltipFadeCoroutine = StartCoroutine(TooltipFadeAnim(true));
    }

    private void HideTooltip()
    {
        if (tooltipObj == null) return;
        if (tooltipFadeCoroutine != null) StopCoroutine(tooltipFadeCoroutine);
        if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 0f;
        tooltipObj.SetActive(false);
    }

    private IEnumerator TooltipFadeAnim(bool fadeIn)
    {
        if (tooltipCanvasGroup == null) yield break;
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();

        float start = tooltipCanvasGroup.alpha;
        float target = fadeIn ? 1f : 0f;
        Vector3 startScale = fadeIn ? Vector3.one * 0.92f : Vector3.one;
        Vector3 targetScale = fadeIn ? Vector3.one : Vector3.one * 0.92f;
        float elapsed = 0f;
        float dur = 0.1f;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
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
        float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (rt == null) yield break;
            float t = elapsed / duration;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;
            rt.localScale = new Vector3(s, s, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
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
            if (rt == null) yield break;
            float x = Mathf.Sin(elapsed * frequency) * magnitude * (1f - elapsed / duration);
            rt.localPosition = origin + new Vector3(x, 0f, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) rt.localPosition = origin;
    }

    /// <summary>
    /// GeneSplice parçalanma animasyonu — ikon titreşerek küçülür,
    /// parçacık efektiyle dağılır ve kaybolur.
    /// </summary>
    public void TriggerSpliceAnimation(BasePerk perk)
    {
        int idx = spawnedPerks.IndexOf(perk);
        if (idx < 0 || idx >= spawnedIcons.Count) return;
        GameObject iconGO = spawnedIcons[idx];
        if (iconGO != null && iconGO.activeInHierarchy)
            StartCoroutine(SpliceAnim(iconGO));
    }

    private IEnumerator SpliceAnim(GameObject iconGO)
    {
        RectTransform rt = iconGO.GetComponent<RectTransform>();
        CanvasGroup cg = iconGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = iconGO.AddComponent<CanvasGroup>();

        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();

        // Faz 1: Hızlı titreşim + hafif büyüme (0.3s)
        float phase1 = 0.3f;
        float elapsed = 0f;
        Vector3 origin = rt.localPosition;
        while (elapsed < phase1)
        {
            if (rt == null) yield break;
            float t = elapsed / phase1;
            float shake = Mathf.Sin(elapsed * 50f) * 6f * (1f + t);
            rt.localPosition = origin + new Vector3(shake, Mathf.Sin(elapsed * 40f) * 3f, 0f);
            float s = 1f + t * 0.3f; // hafif büyü
            rt.localScale = new Vector3(s, s, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Faz 2: Patlayarak küçülme + fade out (0.3s)
        float phase2 = 0.3f;
        elapsed = 0f;
        // Başlangıç: büyümüş hali
        float startScale = 1.3f;
        while (elapsed < phase2)
        {
            if (rt == null) yield break;
            float t = elapsed / phase2;
            // Hızlı küçülme (easeInBack efekti)
            float scale = Mathf.Lerp(startScale, 0f, t * t);
            rt.localScale = new Vector3(scale, scale, 1f);
            // Fade out
            cg.alpha = 1f - t;
            // Dönerek kaybolma
            rt.localRotation = Quaternion.Euler(0f, 0f, t * 180f);
            // Titreşim devam
            float shake = Mathf.Sin(elapsed * 60f) * 4f * (1f - t);
            rt.localPosition = origin + new Vector3(shake, shake * 0.5f, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Tamamen gizle
        if (rt != null)
        {
            rt.localScale = Vector3.zero;
            cg.alpha = 0f;
        }

        CameraController.ShakeLight();
    }
}
