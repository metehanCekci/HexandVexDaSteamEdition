using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Harita node'u UI — hover/click efektleri, glow ring, pulse animasyonları.
/// Her node tipi kendi renk ve efekt stiline sahip.
/// </summary>
public class MapNodeUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public Image backgroundImage;
    public Button button;
    public TMP_Text labelText;

    [Header("Effect Layers (BuildNodePrefab atar)")]
    public Image glowRing;       // Dış parlama halkası
    public Image innerShine;     // İç parlama overlay
    public Image checkmark;      // Visited tik işareti
    public Image pulseRing;      // Reachable pulse ring

    [HideInInspector] public int nodeId;

    // State
    private Color baseColor;
    private bool isReachable;
    private bool isVisited;
    private bool isCurrent;
    private bool isBoss;
    private MapNodeType nodeType;

    // Animasyon state
    private Vector3 originalScale;
    private Vector2 originalPos;
    private Coroutine hoverCoroutine;
    private Coroutine pulseCoroutine;
    private Coroutine floatCoroutine;
    private Coroutine bossCoroutine;
    private Coroutine clickCoroutine;
    private bool isHovered;

    // ─── Node tipine göre icon sprite'ları ───
    private static Sprite combatIcon;
    private static Sprite eliteIcon;
    private static Sprite shopIcon;
    private static Sprite perkIcon;
    private static Sprite restIcon;
    private static Sprite eventIcon;
    private static Sprite bossIcon;

    public static void SetIcons(Sprite combat, Sprite elite, Sprite shop, Sprite perk, Sprite rest, Sprite evt, Sprite boss)
    {
        combatIcon = combat;
        eliteIcon = elite;
        shopIcon = shop;
        perkIcon = perk;
        restIcon = rest;
        eventIcon = evt;
        bossIcon = boss;
    }

    void Awake()
    {
        originalScale = Vector3.one;
    }

    void OnEnable()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null) originalPos = rt.anchoredPosition;
    }

    public void Setup(MapNode node)
    {
        nodeId = node.id;
        nodeType = node.nodeType;
        isBoss = node.nodeType == MapNodeType.Boss;

        if (labelText != null)
            labelText.text = GetNodeLabel(node.nodeType);

        baseColor = GetNodeColor(node.nodeType);
        if (backgroundImage != null)
            backgroundImage.color = baseColor;

        UpdateIcon(node.nodeType);
        SetupEvents();

        // Glow ring başlangıçta gizli
        if (glowRing != null) { glowRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f); glowRing.enabled = true; }
        if (innerShine != null) { innerShine.color = new Color(1f, 1f, 1f, 0f); innerShine.enabled = true; }
        if (checkmark != null) checkmark.enabled = false;
        if (pulseRing != null) { pulseRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f); pulseRing.enabled = true; }
    }

    private void SetupEvents()
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        // Hover enter
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => OnHoverEnter());
        trigger.triggers.Add(enterEntry);

        // Hover exit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => OnHoverExit());
        trigger.triggers.Add(exitEntry);

        // Click
        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener((_) => OnNodeClick());
        trigger.triggers.Add(clickEntry);
    }

    // ═══════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════

    public void SetState(bool reachable, bool visited, bool current)
    {
        isReachable = reachable;
        isVisited = visited;
        isCurrent = current;

        if (button != null)
            button.interactable = reachable && !visited;

        // Eski animasyonları durdur
        StopEffects();

        if (isCurrent)
        {
            ApplyCurrentState();
        }
        else if (isVisited)
        {
            ApplyVisitedState();
        }
        else if (isReachable)
        {
            ApplyReachableState();
        }
        else
        {
            ApplyLockedState();
        }

        // Boss her zaman kendi efektini çalıştırır (visited değilse)
        if (isBoss && !isVisited)
        {
            bossCoroutine = StartCoroutine(BossPulseLoop());
        }
    }

    // ─── Current node: parlak + pulse ring ───
    private void ApplyCurrentState()
    {
        if (backgroundImage != null)
            backgroundImage.color = Color.Lerp(baseColor, Color.white, 0.3f);
        if (glowRing != null)
            glowRing.color = new Color(0.3f, 1f, 0.5f, 0.7f);
        if (labelText != null)
            labelText.color = new Color(0.3f, 1f, 0.5f);
        if (iconImage != null)
            iconImage.color = Color.white;
        if (checkmark != null)
            checkmark.enabled = false;

        pulseCoroutine = StartCoroutine(CurrentPulseLoop());
    }

    // ─── Visited: soluk + checkmark ───
    private void ApplyVisitedState()
    {
        float dim = 0.3f;
        if (backgroundImage != null)
            backgroundImage.color = new Color(baseColor.r * dim, baseColor.g * dim, baseColor.b * dim, 0.5f);
        if (glowRing != null)
            glowRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        if (innerShine != null)
            innerShine.color = new Color(1f, 1f, 1f, 0f);
        if (labelText != null)
            labelText.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, 0.25f);
        if (pulseRing != null)
            pulseRing.color = new Color(1f, 1f, 1f, 0f);

        // Checkmark göster
        if (checkmark != null)
        {
            checkmark.enabled = true;
            checkmark.color = new Color(0.5f, 1f, 0.5f, 0.6f);
        }
    }

    // ─── Reachable: normal + idle float + shimmer ───
    private void ApplyReachableState()
    {
        if (backgroundImage != null)
            backgroundImage.color = baseColor;
        if (glowRing != null)
            glowRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        if (labelText != null)
            labelText.color = Color.white;
        if (iconImage != null)
            iconImage.color = Color.white;
        if (checkmark != null)
            checkmark.enabled = false;

        floatCoroutine = StartCoroutine(IdleFloatLoop());
        pulseCoroutine = StartCoroutine(ReachableShimmerLoop());
    }

    // ─── Locked: çok soluk, efekt yok ───
    private void ApplyLockedState()
    {
        float dim = 0.2f;
        if (backgroundImage != null)
            backgroundImage.color = new Color(baseColor.r * dim, baseColor.g * dim, baseColor.b * dim, 0.3f);
        if (glowRing != null)
            glowRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        if (innerShine != null)
            innerShine.color = new Color(1f, 1f, 1f, 0f);
        if (pulseRing != null)
            pulseRing.color = new Color(1f, 1f, 1f, 0f);
        if (labelText != null)
            labelText.color = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, 0.2f);
        if (checkmark != null)
            checkmark.enabled = false;
    }

    private void StopEffects()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (floatCoroutine != null) { StopCoroutine(floatCoroutine); floatCoroutine = null; }
        if (bossCoroutine != null) { StopCoroutine(bossCoroutine); bossCoroutine = null; }
        if (hoverCoroutine != null) { StopCoroutine(hoverCoroutine); hoverCoroutine = null; }
        if (clickCoroutine != null) { StopCoroutine(clickCoroutine); clickCoroutine = null; }

        // Reset position & scale
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = originalPos;
        transform.localScale = originalScale;
    }

    // ═══════════════════════════════════════════════════════
    // HOVER EFEKTLERİ
    // ═══════════════════════════════════════════════════════

    private void OnHoverEnter()
    {
        if (isVisited && !isCurrent) return;
        isHovered = true;
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(HoverEnterAnim());
    }

    private void OnHoverExit()
    {
        isHovered = false;
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(HoverExitAnim());
    }

    /// <summary>Hover: scale up + glow ring parlat + inner shine</summary>
    private IEnumerator HoverEnterAnim()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.15f;

        float startGlow = glowRing != null ? glowRing.color.a : 0f;
        float targetGlow = 0.8f;
        float startShine = innerShine != null ? innerShine.color.a : 0f;
        float targetShine = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (glowRing != null)
            {
                Color c = glowRing.color;
                c.a = Mathf.Lerp(startGlow, targetGlow, t);
                glowRing.color = c;
            }
            if (innerShine != null)
            {
                Color c = innerShine.color;
                c.a = Mathf.Lerp(startShine, targetShine, t);
                innerShine.color = c;
            }

            yield return null;
        }

        transform.localScale = targetScale;
    }

    /// <summary>Hover çıkış: scale down + glow azalt</summary>
    private IEnumerator HoverExitAnim()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        float startGlow = glowRing != null ? glowRing.color.a : 0f;
        float targetGlow = isReachable ? 0.35f : 0f;
        float startShine = innerShine != null ? innerShine.color.a : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.localScale = Vector3.Lerp(startScale, originalScale, t);

            if (glowRing != null)
            {
                Color c = glowRing.color;
                c.a = Mathf.Lerp(startGlow, targetGlow, t);
                glowRing.color = c;
            }
            if (innerShine != null)
            {
                Color c = innerShine.color;
                c.a = Mathf.Lerp(startShine, 0f, t);
                innerShine.color = c;
            }

            yield return null;
        }

        transform.localScale = originalScale;
    }

    // ═══════════════════════════════════════════════════════
    // CLICK EFEKTİ — Burst ring + flash
    // ═══════════════════════════════════════════════════════

    private void OnNodeClick()
    {
        if (!isReachable || isVisited) return;
        if (clickCoroutine != null) StopCoroutine(clickCoroutine);
        clickCoroutine = StartCoroutine(ClickBurstAnim());
    }

    private IEnumerator ClickBurstAnim()
    {
        // 1. Hızlı scale punch
        float punchDuration = 0.1f;
        float elapsed = 0f;
        Vector3 punchScale = originalScale * 1.25f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            transform.localScale = Vector3.Lerp(originalScale, punchScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        transform.localScale = originalScale;

        // 2. Inner flash
        if (innerShine != null)
        {
            elapsed = 0f;
            float flashDuration = 0.25f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / flashDuration);
                Color c = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 0f, t));
                innerShine.color = c;
                yield return null;
            }
        }

        // 3. Pulse ring burst — genişleyerek kaybol
        if (pulseRing != null)
        {
            RectTransform prt = pulseRing.GetComponent<RectTransform>();
            Vector2 origSize = prt.sizeDelta;
            elapsed = 0f;
            float burstDuration = 0.4f;

            while (elapsed < burstDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);

                float scale = 1f + t * 0.8f; // %80 büyü
                prt.sizeDelta = origSize * scale;

                Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.6f, 0f, t));
                pulseRing.color = c;

                yield return null;
            }

            prt.sizeDelta = origSize;
            pulseRing.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }
    }

    // ═══════════════════════════════════════════════════════
    // LOOP ANİMASYONLARI
    // ═══════════════════════════════════════════════════════

    /// <summary>Current node: glow ring pulse</summary>
    private IEnumerator CurrentPulseLoop()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) / 2f; // 0-1 oscillation
            if (glowRing != null)
            {
                Color c = new Color(0.3f, 1f, 0.5f, Mathf.Lerp(0.4f, 0.9f, t));
                glowRing.color = c;
            }
            if (pulseRing != null && !isHovered)
            {
                RectTransform prt = pulseRing.GetComponent<RectTransform>();
                float scale = 1f + t * 0.05f;
                Color c = new Color(0.3f, 1f, 0.5f, Mathf.Lerp(0.1f, 0.3f, t));
                pulseRing.color = c;
            }
            yield return null;
        }
    }

    /// <summary>Reachable node: hafif shimmer efekti</summary>
    private IEnumerator ReachableShimmerLoop()
    {
        // Her node'a farklı phase ver — aynı anda titreşmesinler
        float phase = nodeId * 1.7f;
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 2f + phase) + 1f) / 2f;
            if (glowRing != null && !isHovered)
            {
                Color c = glowRing.color;
                c.a = Mathf.Lerp(0.15f, 0.45f, t);
                glowRing.color = c;
            }
            yield return null;
        }
    }

    /// <summary>Reachable node: hafif yukarı-aşağı float</summary>
    private IEnumerator IdleFloatLoop()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        float phase = nodeId * 2.3f;
        float amplitude = 3f;

        while (true)
        {
            if (!isHovered)
            {
                float yOffset = Mathf.Sin(Time.unscaledTime * 1.5f + phase) * amplitude;
                rt.anchoredPosition = originalPos + new Vector2(0f, yOffset);
            }
            yield return null;
        }
    }

    /// <summary>Boss node: kırmızı menacing pulse + hafif shake</summary>
    private IEnumerator BossPulseLoop()
    {
        RectTransform rt = GetComponent<RectTransform>();
        while (true)
        {
            // Kırmızı glow pulse
            float t = (Mathf.Sin(Time.unscaledTime * 2.5f) + 1f) / 2f;
            if (glowRing != null)
            {
                glowRing.color = new Color(1f, 0.1f, 0.1f, Mathf.Lerp(0.3f, 0.85f, t));
            }

            // Hafif scale pulse
            float s = 1f + Mathf.Sin(Time.unscaledTime * 2.5f) * 0.03f;
            if (!isHovered)
                transform.localScale = new Vector3(s, s, 1f);

            // Ara sıra shake
            if (rt != null && !isHovered)
            {
                float shake = Mathf.Sin(Time.unscaledTime * 15f) * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 0.8f)) * 1.5f;
                rt.anchoredPosition = originalPos + new Vector2(shake, 0f);
            }

            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════
    // YARDIMCI
    // ═══════════════════════════════════════════════════════

    private string GetNodeLabel(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:        return "FIGHT";
            case MapNodeType.EliteCombat:   return "ELITE";
            case MapNodeType.Shop:          return "SHOP";
            case MapNodeType.PerkSelection: return "PERK";
            case MapNodeType.Rest:          return "REST";
            case MapNodeType.Event:         return "EVENT";
            case MapNodeType.Boss:          return "BOSS";
            default:                        return "?";
        }
    }

    private void UpdateIcon(MapNodeType type)
    {
        if (iconImage == null) return;

        Sprite icon = null;
        switch (type)
        {
            case MapNodeType.Combat:       icon = combatIcon; break;
            case MapNodeType.EliteCombat:   icon = eliteIcon; break;
            case MapNodeType.Shop:          icon = shopIcon; break;
            case MapNodeType.PerkSelection: icon = perkIcon; break;
            case MapNodeType.Rest:          icon = restIcon; break;
            case MapNodeType.Event:         icon = eventIcon; break;
            case MapNodeType.Boss:          icon = bossIcon; break;
        }

        if (icon != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color = GetNodeColor(type);
        }
    }

    private Color GetNodeColor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:       return new Color(0.7f, 0.25f, 0.25f);
            case MapNodeType.EliteCombat:   return new Color(1f, 0.5f, 0.1f);
            case MapNodeType.Shop:          return new Color(1f, 0.85f, 0.25f);
            case MapNodeType.PerkSelection: return new Color(0.6f, 0.25f, 1f);
            case MapNodeType.Rest:          return new Color(0.25f, 0.75f, 0.4f);
            case MapNodeType.Event:         return new Color(0.3f, 0.6f, 1f);
            case MapNodeType.Boss:          return new Color(0.9f, 0.05f, 0.05f);
            default:                        return Color.white;
        }
    }

    /// <summary>EaseOutBack — slight overshoot for juicy feel</summary>
    private static float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
