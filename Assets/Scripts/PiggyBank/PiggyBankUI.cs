using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Piggy bank HUD row — sits under the gold row on the PersistentHUD.
///
/// Scene setup (all references assigned via Inspector):
///   PersistentHUD (GameObject with PersistentHUD component)
///   └── PIGGYRow (RectTransform)
///       ├── PiggyIcon  (Image)             → piggyIcon
///       └── Value      (TMP_Text "0/10")   → valueText
///
///   Optional tooltip (hidden until hovered):
///   └── PiggyTooltip (RectTransform, inactive by default)
///       └── Text (TMP_Text)                → tooltipText
///
/// Shows a live "preview/cap" readout and animates coins flying into the gold counter on payout.
/// Art (piggyBankSprite / piggyBankFullSprite) comes from PiggyBankConfig so the artist can slot it in later.
/// </summary>
public class PiggyBankUI : MonoBehaviour
{
    public static PiggyBankUI instance;

    [Header("Scene references (assign in Inspector)")]
    [Tooltip("Row RectTransform that contains the icon + value. Used as the origin point for flying coins.")]
    public RectTransform piggyRoot;
    [Tooltip("The piggy bank icon. Sprite is pulled from PiggyBankConfig at runtime — leave the sprite slot here empty.")]
    public Image piggyIcon;
    [Tooltip("Text that shows 'preview/cap' (e.g. '7/10').")]
    public TMP_Text valueText;
    [Tooltip("Target the flying coins land on. Defaults to PersistentHUD.goldText if null.")]
    public RectTransform coinLandingTarget;

    [Header("Tooltip (optional)")]
    public GameObject tooltipRoot;
    public TMP_Text tooltipText;

    [Header("Animation")]
    [Tooltip("Arc height for flying coins (UI pixels).")]
    public float flyArcHeight = 40f;
    [Tooltip("How long a single coin spends in the air.")]
    public float flyDuration = 0.35f;

    [Header("Flying coin visual")]
    [Tooltip("Sprite used for each flying coin. If null, falls back to the piggy icon sprite.")]
    public Sprite flyingCoinSprite;
    [Tooltip("Size of each spawned flying coin.")]
    public Vector2 flyingCoinSize = new Vector2(18f, 18f);

    private PiggyBankConfig config;
    private int currentPreview = 0;
    private int currentCap = 10;
    private Coroutine shakeRoutine;
    private bool tooltipHooked;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    void OnEnable()
    {
        GameEvents.OnInterestPreview += OnPreview;
        GameEvents.OnInterestPaid += OnPaid;
    }

    void OnDisable()
    {
        GameEvents.OnInterestPreview -= OnPreview;
        GameEvents.OnInterestPaid -= OnPaid;
    }

    void Start()
    {
        config = PiggyBankManager.instance != null
            ? PiggyBankManager.instance.config
            : PiggyBankConfig.LoadOrDefault();

        ValidateReferences();
        ApplyArt();
        HookTooltip();

        if (tooltipRoot != null) tooltipRoot.SetActive(false);

        // Prime the preview if the manager hasn't pushed one yet.
        if (PiggyBankManager.instance != null)
        {
            var peek = PiggyBankManager.instance.PeekInterest();
            currentPreview = peek.Amount;
            currentCap = peek.Cap;
        }
        RenderValue();
        UpdateTint();
    }

    private void ValidateReferences()
    {
        if (piggyRoot == null)
            Debug.LogWarning("[PiggyBankUI] piggyRoot not assigned. Build PIGGYRow under PersistentHUD and drag it here.");
        if (piggyIcon == null)
            Debug.LogWarning("[PiggyBankUI] piggyIcon not assigned. Add an Image child under PIGGYRow and drag it here.");
        if (valueText == null)
            Debug.LogWarning("[PiggyBankUI] valueText not assigned. Add a TMP_Text child under PIGGYRow and drag it here.");

        // Fall back to PersistentHUD.goldText as the landing target.
        if (coinLandingTarget == null && PersistentHUD.instance != null && PersistentHUD.instance.goldText != null)
            coinLandingTarget = PersistentHUD.instance.goldText.rectTransform;
        if (coinLandingTarget == null)
            Debug.LogWarning("[PiggyBankUI] coinLandingTarget not set and PersistentHUD.goldText unavailable; flying coins will not render.");

        // Flying coin sprite fallback: PersistentHUD.coinIconImage -> PIGGYRow icon -> null (solid yellow sqr)
        if (flyingCoinSprite == null)
        {
            if (PersistentHUD.instance != null && PersistentHUD.instance.coinIconImage != null
                && PersistentHUD.instance.coinIconImage.sprite != null)
                flyingCoinSprite = PersistentHUD.instance.coinIconImage.sprite;
            else if (piggyIcon != null && piggyIcon.sprite != null)
                flyingCoinSprite = piggyIcon.sprite;
        }

        Debug.Log($"[PiggyBankUI] wired. piggyIcon={(piggyIcon!=null)} landing={(coinLandingTarget!=null)} " +
                  $"flyingSprite={(flyingCoinSprite!=null?flyingCoinSprite.name:"<null, will use yellow square>")} " +
                  $"canvas={(piggyIcon!=null && piggyIcon.canvas!=null ? piggyIcon.canvas.name : "<none>")}");
    }

    private void ApplyArt()
    {
        if (piggyIcon == null) return;
        Sprite sprite = config != null ? config.piggyBankSprite : null;
        if (sprite != null)
        {
            piggyIcon.sprite = sprite;
            piggyIcon.enabled = true;
        }
        else
        {
            // Leave whatever placeholder the designer put in the scene visible.
            // The artist will drop the real sprite into PiggyBankConfig later.
        }
    }

    private void HookTooltip()
    {
        if (tooltipHooked) return;
        if (piggyIcon == null || tooltipRoot == null) return;

        var trigger = piggyIcon.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = piggyIcon.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(true));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ShowTooltip(false));
        trigger.triggers.Add(exit);

        piggyIcon.raycastTarget = true;
        tooltipHooked = true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Rendering
    // ────────────────────────────────────────────────────────────────────────────
    private void OnPreview(int preview, int cap)
    {
        currentPreview = preview;
        currentCap = cap;
        RenderValue();
        UpdateTint();
    }

    private void RenderValue()
    {
        if (valueText == null) return;
        valueText.text = currentPreview + "/" + currentCap;
    }

    private void UpdateTint()
    {
        if (piggyIcon == null || config == null) return;
        float t = currentCap > 0 ? Mathf.Clamp01((float)currentPreview / currentCap) : 0f;
        piggyIcon.color = Color.Lerp(config.piggyTintEmpty, config.piggyTintFull, t);

        if (config.piggyBankFullSprite != null && t >= 0.999f && piggyIcon.sprite != config.piggyBankFullSprite)
            piggyIcon.sprite = config.piggyBankFullSprite;
        else if (config.piggyBankSprite != null && t < 0.999f && piggyIcon.sprite != config.piggyBankSprite)
            piggyIcon.sprite = config.piggyBankSprite;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Payout animation — coins fly from piggy to gold counter
    // ────────────────────────────────────────────────────────────────────────────
    private void OnPaid(int amount)
    {
        Debug.Log($"[PiggyBankUI] OnPaid({amount}) - spawning flying coins");
        if (amount <= 0) return;
        if (config == null) { Debug.LogWarning("[PiggyBankUI] config null, paying without animation"); FailSafeCredit(amount); return; }

        if (piggyIcon != null)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(Shake(piggyIcon.transform));
        }

        bool oneByOne = amount <= config.hybridOneByOneThreshold;
        if (oneByOne) StartCoroutine(FlyOneByOne(amount));
        else StartCoroutine(FlyBurst(amount));
    }

    private void FailSafeCredit(int amount)
    {
        if (PiggyBankManager.instance == null) return;
        for (int i = 0; i < amount; i++) PiggyBankManager.instance.CreditOne();
    }

    private IEnumerator FlyOneByOne(int amount)
    {
        // Tek tek: her coin kendi credit'ini tasir - amount kadar gorsel + amount kadar credit
        for (int i = 0; i < amount; i++)
        {
            SpawnFlyingCoin(creditOnLand: true);
            yield return new WaitForSecondsRealtime(config.perCoinDelay);
        }
    }

    private IEnumerator FlyBurst(int amount)
    {
        // Burst: gorsel olarak en fazla 5 coin, ama gercek amount her coin'e dagitilir
        int visualCount = Mathf.Clamp(amount, 3, 5);
        // Her goruntu coin'ine dusen credit: ceil(amount / visual) - son coin kalanı tasir
        int totalRemaining = amount;
        for (int i = 0; i < visualCount; i++)
        {
            int coinsLeft = visualCount - i;
            int creditThisCoin = Mathf.CeilToInt((float)totalRemaining / coinsLeft);
            totalRemaining -= creditThisCoin;
            SpawnFlyingCoin(creditOnLand: true, creditAmount: creditThisCoin);
            yield return new WaitForSecondsRealtime(0.04f);
        }
    }

    private void SpawnFlyingCoin(bool creditOnLand, int creditAmount = 1)
    {
        // Lazy-resolve landing target each spawn — PersistentHUD.goldText may wire up after our Start().
        if (coinLandingTarget == null && PersistentHUD.instance != null && PersistentHUD.instance.goldText != null)
            coinLandingTarget = PersistentHUD.instance.goldText.rectTransform;

        if (piggyIcon == null || coinLandingTarget == null)
        {
            Debug.LogWarning($"[PiggyBankUI] SpawnFlyingCoin fail-safe. piggyIcon={(piggyIcon!=null)} landing={(coinLandingTarget!=null)}");
            if (creditOnLand && PiggyBankManager.instance != null)
                for (int i = 0; i < creditAmount; i++) PiggyBankManager.instance.CreditOne();
            return;
        }

        // Parent olarak en ustteki canvas'i kullan ki coin her seyin uzerinde gorunsun
        Canvas canvas = piggyIcon.canvas;
        if (canvas == null)
        {
            Debug.LogWarning("[PiggyBankUI] piggyIcon.canvas null - cannot spawn flying coin");
            if (creditOnLand && PiggyBankManager.instance != null)
                for (int i = 0; i < creditAmount; i++) PiggyBankManager.instance.CreditOne();
            return;
        }
        // root canvas'a cik (nested canvas varsa)
        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        RectTransform parent = rootCanvas.transform as RectTransform;

        var coinGO = new GameObject("FlyingCoin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coinGO.transform.SetParent(parent, false);
        coinGO.transform.SetAsLastSibling(); // hersey ustunde olsun
        var rt = (RectTransform)coinGO.transform;
        rt.sizeDelta = flyingCoinSize;
        var img = coinGO.GetComponent<Image>();

        // Sprite yoksa parlak sari solid kare - goster ki varligi fark edelim
        if (flyingCoinSprite != null)
        {
            img.sprite = flyingCoinSprite;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(1f, 0.85f, 0.2f, 1f); // solid gold
        }
        img.raycastTarget = false;
        img.preserveAspect = true;

        Vector3 startWorld = piggyIcon.rectTransform.position;
        Vector3 endWorld = coinLandingTarget.position;
        rt.position = startWorld;

        Debug.Log($"[PiggyBankUI] flying coin spawned. start={startWorld} end={endWorld} size={flyingCoinSize}");

        StartCoroutine(FlyCoin(rt, startWorld, endWorld, creditOnLand ? creditAmount : 0));
    }

    private IEnumerator FlyCoin(RectTransform rt, Vector3 startWorld, Vector3 endWorld, int creditOnLand)
    {
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            if (rt == null) yield break;
            float t = elapsed / flyDuration;
            Vector3 pos = Vector3.Lerp(startWorld, endWorld, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * flyArcHeight;
            rt.position = pos;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        // Coin yere degdi - gold'a ekle
        if (creditOnLand > 0 && PiggyBankManager.instance != null)
            for (int i = 0; i < creditOnLand; i++) PiggyBankManager.instance.CreditOne();

        if (rt != null) Destroy(rt.gameObject);
    }

    private IEnumerator Shake(Transform t)
    {
        if (t == null) yield break;
        Vector3 basePos = t.localPosition;
        float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float amp = Mathf.Lerp(4f, 0f, elapsed / duration);
            t.localPosition = basePos + new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp), 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (t != null) t.localPosition = basePos;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Tooltip
    // ────────────────────────────────────────────────────────────────────────────
    private void ShowTooltip(bool show)
    {
        if (tooltipRoot == null) return;
        tooltipRoot.SetActive(show);
        if (show && tooltipText != null)
        {
            int per = config != null ? config.goldPerInterest : 10;
            tooltipText.text = $"Every {per} gold earns 1 gold at the end of a wave (max {currentCap}).";
        }
    }
}
