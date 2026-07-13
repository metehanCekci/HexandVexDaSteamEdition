using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Oyun içinde (veya ana menüde) perk unlock olduğunda sağdan kayan toast notification.
/// Canvas ve container'ı runtime'da kendisi oluşturur — scene'de sadece bu script olması yeterli.
/// </summary>
public class CollectionUnlockToast : MonoBehaviour
{
    public static CollectionUnlockToast instance;

    [Header("Toast Prefab")]
    public GameObject toastPrefab;

    [Header("Ayarlar")]
    public float displayDuration = 4f;
    public float slideInDuration = 0.5f;
    public float slideOutDuration = 0.4f;
    public float slideDistance = 400f;
    [Tooltip("Birden fazla toast arası dikey boşluk")]
    public float toastSpacing = 10f;
    [Tooltip("Ekranın sağ kenarından içeri padding")]
    public float rightPadding = 20f;
    [Tooltip("Ekranın üst kenarından aşağı padding")]
    public float topPadding = 20f;

    [Header("Canvas Sort Order")]
    public int canvasSortOrder = 1000;

    [Header("Debug / Test")]
    public PerkCollectionData testEntry;

    private Queue<PerkCollectionData> toastQueue = new Queue<PerkCollectionData>();
    private int activeToasts = 0;
    private const int MAX_VISIBLE = 3;
    private List<RectTransform> activeToastRTs = new List<RectTransform>();

    // Runtime oluşturulan canvas & container
    private Canvas runtimeCanvas;
    private RectTransform containerRT;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateRuntimeCanvas();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Scene'den bağımsız, runtime'da oluşturulan overlay canvas + sağ üst container.
    /// </summary>
    private void CreateRuntimeCanvas()
    {
        // Canvas objesi — parent'sız, bağımsız overlay canvas
        GameObject canvasGO = new GameObject("ToastCanvas_Runtime");
        DontDestroyOnLoad(canvasGO);
        runtimeCanvas = canvasGO.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Container — tam ekran stretch, toast'lar kendi anchor'ıyla pozisyonlanır
        GameObject containerGO = new GameObject("ToastContainer");
        containerGO.transform.SetParent(canvasGO.transform, false);
        containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = Vector2.zero;
        containerRT.anchorMax = Vector2.one;
        containerRT.offsetMin = Vector2.zero;
        containerRT.offsetMax = Vector2.zero;
    }

    void OnEnable()
    {
        GameEvents.OnPerkUnlocked += OnPerkUnlocked;
    }

    void OnDisable()
    {
        GameEvents.OnPerkUnlocked -= OnPerkUnlocked;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            if (runtimeCanvas != null) Destroy(runtimeCanvas.gameObject);
        }
    }

    public void TriggerTestToast()
    {
        if (testEntry != null)
            toastQueue.Enqueue(testEntry);
        else
            Debug.LogWarning("[TOAST] testEntry atanmamış — Inspector'dan bir PerkCollectionData sürükleyin.");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            TriggerTestToast();

        if (toastQueue.Count > 0 && activeToasts < MAX_VISIBLE)
        {
            var data = toastQueue.Dequeue();
            StartCoroutine(ShowToast(data));
        }
    }

    private void OnPerkUnlocked(string perkId)
    {
        if (PerkCollectionManager.instance == null || PerkCollectionManager.instance.database == null) return;
        var entry = PerkCollectionManager.instance.database.entries.Find(e => e.perkId == perkId);
        if (entry != null) toastQueue.Enqueue(entry);
    }

    public void ShowUnlock(PerkCollectionData entry)
    {
        if (entry != null) toastQueue.Enqueue(entry);
    }

    private IEnumerator ShowToast(PerkCollectionData entry)
    {
        activeToasts++;

        if (toastPrefab == null || containerRT == null)
        {
            activeToasts--;
            yield break;
        }

        BasePerk perk = entry.perkPrefab != null ? entry.perkPrefab.GetComponent<BasePerk>() : null;
        if (perk == null)
        {
            activeToasts--;
            yield break;
        }

        // Toast oluştur — runtime canvas'ın container'ına
        GameObject toastGO = Instantiate(toastPrefab, containerRT);
        toastGO.SetActive(true);
        RectTransform toastRT = toastGO.GetComponent<RectTransform>();
        CanvasGroup cg = toastGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = toastGO.AddComponent<CanvasGroup>();

        // Toast anchor: sağ üst, pivot sağ üst → anchoredPosition (0,0) = sağ üst köşe
        toastRT.anchorMin = new Vector2(1f, 1f);
        toastRT.anchorMax = new Vector2(1f, 1f);
        toastRT.pivot = new Vector2(1f, 1f);
        toastRT.localScale = Vector3.one;
        toastRT.localRotation = Quaternion.identity;

        // Görünmez başlat
        cg.alpha = 0f;

        // Y offset hesapla (alt alta dizilim)
        activeToastRTs.RemoveAll(rt => rt == null);
        float yOffset = -topPadding;
        foreach (var existingRT in activeToastRTs)
        {
            yOffset -= existingRT.sizeDelta.y + toastSpacing;
        }
        activeToastRTs.Add(toastRT);

        // endPos: sağ kenara yapışık (x = -rightPadding), y = hesaplanan offset
        // startPos: ekran dışı sağda
        Vector2 endPos = new Vector2(-rightPadding, yOffset);
        Vector2 startPos = new Vector2(slideDistance, yOffset);
        toastRT.anchoredPosition = startPos;

        // Child'ları bul ve doldur
        TMP_Text titleText = null;
        TMP_Text subtitleText = null;
        Image iconImg = null;
        Image glowImg = null;
        Image borderImg = null;

        foreach (Transform child in toastGO.GetComponentsInChildren<Transform>(true))
        {
            switch (child.name)
            {
                case "ToastTitle": titleText = child.GetComponent<TMP_Text>(); break;
                case "ToastSubtitle": subtitleText = child.GetComponent<TMP_Text>(); break;
                case "ToastIcon": iconImg = child.GetComponent<Image>(); break;
                case "ToastGlow": glowImg = child.GetComponent<Image>(); break;
                case "ToastBorder": borderImg = child.GetComponent<Image>(); break;
            }
        }

        Color rarityColor = CollectionUI.GetRarityColor(entry.rarity);

        // Arkaplan: #00050C
        Image bgImg = toastGO.GetComponent<Image>();
        if (bgImg != null) bgImg.color = new Color(0f, 0.02f, 0.047f, 1f);

        if (titleText != null) { titleText.text = "NEW PERK UNLOCKED!"; titleText.color = new Color(1f, 0.85f, 0.2f); titleText.fontSize = 12; }
        string hint = !string.IsNullOrEmpty(entry.unlockHint) ? entry.unlockHint : CollectionUI.GetDefaultHint(entry);
        if (subtitleText != null)
        {
            string rarityHex = ColorUtility.ToHtmlStringRGB(rarityColor);
            subtitleText.text = $"<color=#{rarityHex}><size=18>{perk.perkName}</size></color>\n<color=#8899AA><size=11>{hint}</size></color>";
            subtitleText.color = Color.white;
            subtitleText.richText = true;
        }
        if (iconImg != null) { iconImg.sprite = perk.icon; iconImg.color = Color.white; }
        if (borderImg != null) borderImg.color = rarityColor;
        if (glowImg != null) glowImg.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);

        // Ses
        if (AudioManager.instance != null) AudioManager.instance.PlayUnlockFanfare();

        // ── Slide In ──
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideInDuration);
            float ease = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 3f);
            toastRT.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            cg.alpha = Mathf.Clamp01(t * 3f);
            yield return null;
        }
        toastRT.anchoredPosition = endPos;
        cg.alpha = 1f;

        // ── Glow pulse ──
        if (glowImg != null)
            StartCoroutine(ToastGlowPulse(glowImg, rarityColor, displayDuration));

        // ── Scale punch ──
        float punchDur = 0.2f;
        float punchElapsed = 0f;
        while (punchElapsed < punchDur)
        {
            punchElapsed += Time.unscaledDeltaTime;
            float t = punchElapsed / punchDur;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
            toastRT.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        toastRT.localScale = Vector3.one;

        // ── Bekleme ──
        yield return new WaitForSecondsRealtime(displayDuration);

        // ── Slide Out ──
        startPos = toastRT.anchoredPosition;
        endPos = startPos + new Vector2(slideDistance, 0);
        elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideOutDuration);
            float ease = t * t;
            toastRT.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            cg.alpha = 1f - ease;
            yield return null;
        }

        activeToastRTs.Remove(toastRT);
        Destroy(toastGO);
        activeToasts--;
    }

    private IEnumerator ToastGlowPulse(Image glowImg, Color baseColor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && glowImg != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(elapsed * 4f) * 0.2f + 0.3f;
            glowImg.color = new Color(baseColor.r, baseColor.g, baseColor.b, pulse);
            yield return null;
        }
    }
}
