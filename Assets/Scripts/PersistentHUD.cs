using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Persistent HUD that shows Gold and HP on every screen.
/// DontDestroyOnLoad — always visible.
/// Reads visual settings from PersistentHUDConfig ScriptableObject.
/// Edit via Tools → Persistent HUD → Select Config.
/// </summary>
public class PersistentHUD : MonoBehaviour
{
    public static PersistentHUD instance;

    [Header("Config")]
    public PersistentHUDConfig config;

    private GameObject hudCanvas;
    private TMP_Text goldText;
    private TMP_Text hpText;
    private Image coinIconImage;

    private int lastHP = -1;
    private Coroutine hpPulseCoroutine;
    private Coroutine goldPulseCoroutine;

    void Awake()
    {
        // Her sahne yüklendiğinde eski instance varsa yok et, yenisini al
        if (instance != null && instance != this)
        {
            Destroy(instance.gameObject);
        }
        instance = this;
    }

    void Start()
    {
        LoadConfigIfNeeded();
        BuildUI();
        Refresh();
    }

    void OnEnable()  { GameEvents.OnGoldChanged += OnGoldChanged; }
    void OnDisable() { GameEvents.OnGoldChanged -= OnGoldChanged; }

    void Update()
    {
        if (RunManager.instance == null) return;

        int curHP = RunManager.instance.playerCurrentHealth;
        int maxHP = RunManager.instance.playerMaxHealth;

        if (hpText != null) hpText.text = curHP + "/" + maxHP;
        if (goldText != null) goldText.text = RunManager.instance.currentGold.ToString();

        // HP pulse on damage
        if (curHP != lastHP && lastHP >= 0 && curHP < lastHP && hpText != null)
        {
            if (hpPulseCoroutine != null) StopCoroutine(hpPulseCoroutine);
            hpPulseCoroutine = StartCoroutine(PulseText(hpText, C.hpDamagePulseColor));
        }
        lastHP = curHP;

        // Try to assign coin sprite if not yet set
        if (coinIconImage != null && coinIconImage.sprite == null)
            TryAssignCoinSprite();
    }

    private void OnGoldChanged(int amount)
    {
        if (goldText != null)
        {
            goldText.text = amount.ToString();
            if (goldPulseCoroutine != null) StopCoroutine(goldPulseCoroutine);
            goldPulseCoroutine = StartCoroutine(PulseText(goldText, C.goldPulseColor));
        }
    }

    public void Refresh()
    {
        if (RunManager.instance == null) return;
        if (goldText != null) goldText.text = RunManager.instance.currentGold.ToString();
        if (hpText != null)
            hpText.text = RunManager.instance.playerCurrentHealth + "/" + RunManager.instance.playerMaxHealth;
        lastHP = RunManager.instance.playerCurrentHealth;
    }

    /// <summary>Rebuild UI from config at runtime (Tools menu).</summary>
    public void RebuildFromConfig()
    {
        if (hudCanvas != null) Destroy(hudCanvas);
        LoadConfigIfNeeded();
        BuildUI();
        Refresh();
    }

    // Shorthand
    private PersistentHUDConfig C => config;

    private void LoadConfigIfNeeded()
    {
        if (config != null) return;
        config = Resources.Load<PersistentHUDConfig>("PersistentHUDConfig");
        if (config == null)
        {
            // Fallback: create runtime defaults
            config = ScriptableObject.CreateInstance<PersistentHUDConfig>();
        }
    }

    private void TryAssignCoinSprite()
    {
        Sprite s = null;
        if (TurnManager.instance != null && TurnManager.instance.coinSprite != null)
            s = TurnManager.instance.coinSprite;
        if (s == null)
        {
            var vfx = Object.FindFirstObjectByType<CoinDropVFX>();
            if (vfx != null) s = vfx.coinSprite;
        }
        if (s != null)
        {
            coinIconImage.sprite = s;
            coinIconImage.color = Color.white;
        }
    }

    private void BuildUI()
    {
        // Canvas
        hudCanvas = new GameObject("PersistentHUDCanvas");
        hudCanvas.transform.SetParent(transform, false);
        Canvas canvas = hudCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = C.sortingOrder;
        var scaler = hudCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        hudCanvas.AddComponent<GraphicRaycaster>();

        // Container
        GameObject container = new GameObject("HUDContainer", typeof(RectTransform));
        container.transform.SetParent(hudCanvas.transform, false);
        RectTransform crt = container.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = C.containerPosition;
        crt.sizeDelta = C.containerSize;

        Image containerBG = container.AddComponent<Image>();
        containerBG.color = C.backgroundColor;
        containerBG.raycastTarget = false;

        Outline containerOutline = container.AddComponent<Outline>();
        containerOutline.effectColor = C.outlineColor;
        containerOutline.effectDistance = C.outlineDistance;

        var vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(C.paddingLeft, C.paddingRight, C.paddingTop, C.paddingBottom);
        vlg.spacing = C.rowSpacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // HP Row
        hpText = CreateRow(container.transform, "HP",
            C.hpIconColor, C.hpTextColor, C.hpFontSize, C.hpIconSize, C.hpFontStyle, false);

        // Gold Row
        goldText = CreateRow(container.transform, "GOLD",
            C.goldIconTint, C.goldTextColor, C.goldFontSize, C.goldIconSize, C.goldFontStyle, true);
    }

    private TMP_Text CreateRow(Transform parent, string label,
        Color iconColor, Color textColor, float fontSize, float iconSize,
        FontStyles fontStyle, bool isCoin)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = C.iconTextSpacing;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = C.rowHeight;

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(row.transform, false);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.color = iconColor;
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = iconSize;
        iconLE.preferredHeight = iconSize;

        if (isCoin)
            coinIconImage = iconImg;

        Shadow iconGlow = iconGO.AddComponent<Shadow>();
        iconGlow.effectColor = new Color(iconColor.r, iconColor.g, iconColor.b, C.glowAlpha);
        iconGlow.effectDistance = new Vector2(0f, 0f);

        // Value text
        GameObject valueGO = new GameObject("Value", typeof(RectTransform));
        valueGO.transform.SetParent(row.transform, false);
        TMP_Text valueText = valueGO.AddComponent<TextMeshProUGUI>();
        valueText.text = "0";
        valueText.fontSize = fontSize;
        valueText.fontStyle = fontStyle;
        valueText.color = textColor;
        valueText.alignment = TextAlignmentOptions.Left;
        valueText.raycastTarget = false;
        valueText.enableWordWrapping = false;
        var valLE = valueGO.AddComponent<LayoutElement>();
        valLE.preferredWidth = C.textPreferredWidth;
        valLE.flexibleWidth = 1f;

        return valueText;
    }

    private IEnumerator PulseText(TMP_Text text, Color flashColor)
    {
        if (text == null) yield break;
        Color original = text.color;
        float duration = C.pulseDuration;
        float elapsed = 0f;
        Transform t = text.transform;
        Vector3 baseScale = Vector3.one;

        while (elapsed < duration)
        {
            if (text == null) yield break;
            float p = elapsed / duration;
            float scalePunch = 1f + Mathf.Sin(p * Mathf.PI) * C.pulseScaleMultiplier;
            t.localScale = baseScale * scalePunch;
            text.color = Color.Lerp(flashColor, original, p);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            t.localScale = baseScale;
            text.color = original;
        }
    }
}
