using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ekranin ust-ortasinda aktif perk ikonlarini gosteren kalici bar.
/// Koddan otomatik olusturulur, DontDestroyOnLoad ile sahne gecislerinde hayatta kalir.
/// </summary>
public class ActivePerkBar : MonoBehaviour
{
    public static ActivePerkBar instance;

    private Canvas barCanvas;
    private RectTransform container;
    private GameObject tooltipObj;
    private TextMeshProUGUI tooltipText;
    private CanvasGroup tooltipCanvasGroup;

    // Spawnlanan ikon objeleri — RefreshBar'da yeniden olusturulur
    private readonly List<GameObject> spawnedIcons = new List<GameObject>();
    private readonly List<BasePerk> spawnedPerks = new List<BasePerk>();

    // Envanter butonu referansi
    private GameObject inventoryButton;

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
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Koddan tum UI'yi olusturur ve DontDestroyOnLoad yapar.</summary>
    public static void CreateFromCode()
    {
        if (instance != null) return;

        // Root obje
        GameObject rootGO = new GameObject("ActivePerkBar");
        DontDestroyOnLoad(rootGO);
        ActivePerkBar bar = rootGO.AddComponent<ActivePerkBar>();

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

        // Tooltip — baslangicta gizli
        bar.BuildTooltip(canvasGO.transform);

        // Envanter butonu — sag ust kose
        bar.BuildInventoryButton(canvasGO.transform);
    }

    private void BuildTooltip(Transform canvasTransform)
    {
        tooltipObj = new GameObject("PerkTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObj.transform.SetParent(canvasTransform, false);
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        ttRT.sizeDelta = new Vector2(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
        // Tooltip ekranin ust kismindan biraz asagida gorunecek
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

        // Tooltip text
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

        // ContentSizeFitter ile tooltipun boyutunu texte gore ayarla
        var ttCSF = tooltipObj.AddComponent<ContentSizeFitter>();
        ttCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ttCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // LayoutElement ile minimum boyut
        var ttLE = tooltipObj.AddComponent<LayoutElement>();
        ttLE.preferredWidth = TOOLTIP_WIDTH;

        tooltipObj.SetActive(false);
    }

    private void BuildInventoryButton(Transform canvasTransform)
    {
        inventoryButton = new GameObject("InventoryButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        inventoryButton.transform.SetParent(canvasTransform, false);

        RectTransform btnRT = inventoryButton.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 1f);
        btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot = new Vector2(1f, 1f);
        btnRT.anchoredPosition = new Vector2(-20f, -15f);
        btnRT.sizeDelta = new Vector2(40f, 40f);

        Image btnImg = inventoryButton.GetComponent<Image>();
        btnImg.color = new Color(0.2f, 0.2f, 0.3f, 0.85f);

        Button btn = inventoryButton.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.2f, 0.2f, 0.3f, 0.85f);
        cb.highlightedColor = new Color(0.3f, 0.3f, 0.5f, 1f);
        cb.pressedColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(OnInventoryButtonClicked);

        // Buton ikonu — basit bir canta/disi sembol (TMP ile)
        GameObject btnTextGO = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        btnTextGO.transform.SetParent(inventoryButton.transform, false);
        RectTransform textRT = btnTextGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTMP = btnTextGO.GetComponent<TextMeshProUGUI>();
        btnTMP.text = "INV";
        btnTMP.fontSize = 14;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = new Color(0.85f, 0.85f, 0.9f);
        btnTMP.raycastTarget = false;
    }

    private void OnInventoryButtonClicked()
    {
        if (PerkInventoryUI.instance != null && PerkInventoryUI.instance.IsOpen)
        {
            PerkInventoryUI.instance.Close();
        }
        else
        {
            if (PerkInventoryUI.instance == null)
                PerkInventoryUI.CreateFromCode();
            PerkInventoryUI.instance.Open();
        }
    }

    // ======================================================
    // BAR YENILEME
    // ======================================================

    /// <summary>Ikoncuklari RunManager.activePerks'ten yeniden olusturur.</summary>
    public void RefreshBar()
    {
        // Eski ikonlari temizle
        foreach (var icon in spawnedIcons)
        {
            if (icon != null) Destroy(icon);
        }
        spawnedIcons.Clear();
        spawnedPerks.Clear();

        if (RunManager.instance == null) return;

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
        // Dış kapsayıcı — kare, arka plan yok (sadece layout için)
        GameObject iconGO = new GameObject("PerkIcon_" + perk.perkName, typeof(RectTransform));

        RectTransform rt = iconGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

        // LayoutElement
        LayoutElement le = iconGO.AddComponent<LayoutElement>();
        le.preferredWidth = ICON_SIZE;
        le.preferredHeight = ICON_SIZE;

        // Arka plan (koyu kare) — her zaman görünür, ikon yoksa placeholder olur
        GameObject bgGO = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(iconGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        bgImg.raycastTarget = true; // hover eventleri bu objeye gelsin

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

        // Rarity renk kenarlığı — Outline component ile kare çerçeve
        Color rarityColor;
        ColorUtility.TryParseHtmlString(PerkListUI.GetRarityHex(perk.rarity), out rarityColor);
        Outline outline = bgGO.AddComponent<Outline>();
        outline.effectColor = rarityColor;
        outline.effectDistance = new Vector2(2f, 2f);

        // Level gosterge — kucuk sayi sag-alt kosede
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

        // Raycast alabilmesi için iconGO'ya şeffaf Image ekle
        Image raycastTarget = iconGO.AddComponent<Image>();
        raycastTarget.color = new Color(0f, 0f, 0f, 0f); // Tamamen şeffaf

        // Hover event'leri — EventTrigger ile
        EventTrigger trigger = iconGO.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        BasePerk capturedPerk = perk; // closure icin
        enterEntry.callback.AddListener((_) => ShowTooltip(capturedPerk, iconGO.GetComponent<RectTransform>()));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exitEntry);

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
        tooltipText.text = $"<color={rarityHex}>{perk.perkName}</color>  <color=#CCCCCC>Lv {perk.currentLevel}</color>{desc}";

        tooltipObj.SetActive(true);

        // Tooltip'i ikonun altinda ortala
        RectTransform ttRT = tooltipObj.GetComponent<RectTransform>();
        // Icon'un ekrandaki pozisyonunu tooltipun anchor'una cevir
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
    // GORSEL ANIMASYONLAR
    // ======================================================

    /// <summary>Belirli bir perk icin ikonun ziplamasi (pop/shake) animasyonu tetikler.</summary>
    public void TriggerPopForPerk(BasePerk perk)
    {
        int idx = spawnedPerks.IndexOf(perk);
        if (idx < 0 || idx >= spawnedIcons.Count) return;

        GameObject iconGO = spawnedIcons[idx];
        if (iconGO != null && iconGO.activeInHierarchy)
            StartCoroutine(IconPopAnim(iconGO.GetComponent<RectTransform>()));
    }

    /// <summary>Belirli bir perk icin ikonun shake animasyonu tetikler.</summary>
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
