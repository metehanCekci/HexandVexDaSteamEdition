using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public Image backgroundImage;
    public Image outlineImage;
    public Button button;
    public TMP_Text labelText;

    [HideInInspector] public int nodeId;

    private RectTransform outlineRT;
    private static readonly Color outlineColor = new Color(1f, 1f, 1f, 1f); // bembeyaz

    // Hover lerp
    private bool isHovered;
    private float hoverT; // 0 = normal, 1 = full hover
    private const float lerpSpeed = 8f;
    private const float hoverScale = 1.06f;
    private const float normalOutline = 2f;
    private const float hoverOutline = 4f;

    // State-driven visuals
    private bool isNextStep;      // player can click this right now
    private bool isCurrentNode;   // player is on this node
    private bool isLocked;        // future-unreachable
    private float pulseTime;      // accumulator for "next step" pulse
    private float currentPulseTime; // accumulator for "you are here" pulse
    private const float pulseSpeed = 3.5f;

    // ─── Node tipine göre icon sprite'ları (MapUI'dan atanacak) ───
    private static Sprite combatIcon;
    private static Sprite eliteIcon;
    private static Sprite bossIcon;
    private static Sprite restIcon;
    private static Sprite shopIcon;
    private static Sprite sacrificeIcon;
    private static Sprite enchantIcon;
    private static Sprite treasureIcon;

    public static void SetIcons(Sprite combat, Sprite elite, Sprite boss, Sprite rest, Sprite shop, Sprite sacrifice, Sprite enchant, Sprite treasure)
    {
        combatIcon = combat;
        eliteIcon = elite;
        bossIcon = boss;
        restIcon = rest;
        shopIcon = shop;
        sacrificeIcon = sacrifice;
        enchantIcon = enchant;
        treasureIcon = treasure;
    }

    public void Setup(MapNode node)
    {
        nodeId = node.id;

        if (outlineImage != null)
            outlineRT = outlineImage.GetComponent<RectTransform>();

        // Node tipini label olarak yaz (sprite yoksa okunabilsin)
        if (labelText != null)
            labelText.text = GetNodeLabel(node.nodeType);

        // Arka plan rengini node tipine göre ayarla
        baseColor = GetFallbackColor(node.nodeType);
        if (backgroundImage != null)
            backgroundImage.color = baseColor;

        UpdateIcon(node.nodeType);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (MapManager.instance != null)
                    MapManager.instance.SelectNode(nodeId);
            });
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    // Dev cheat: Ctrl+Click any node to force-select it
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (MapManager.instance != null)
                MapManager.instance.SelectNode(nodeId, force: true);
        }
    }

    void Update()
    {
        // ── Current node: steady bright ring so player always sees where they are ──
        if (isCurrentNode)
        {
            currentPulseTime += Time.unscaledDeltaTime * (pulseSpeed * 0.7f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(currentPulseTime);
            float scale = 1.08f + 0.03f * pulse;
            transform.localScale = new Vector3(scale, scale, 1f);

            if (outlineRT != null)
            {
                float size = Mathf.Lerp(3f, 6f, pulse);
                outlineRT.offsetMin = new Vector2(-size, -size);
                outlineRT.offsetMax = new Vector2(size, size);
            }
            if (outlineImage != null)
            {
                float a = Mathf.Lerp(0.7f, 1f, pulse);
                outlineImage.color = new Color(1f, 0.95f, 0.4f, a); // gold
            }
            return;
        }

        // ── Next-step nodes: pulse to draw the eye ──
        if (isNextStep)
        {
            pulseTime += Time.unscaledDeltaTime * pulseSpeed;
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime);

            // Hover still takes priority — blend hover on top of pulse
            float target = isHovered ? 1f : 0f;
            hoverT = Mathf.Lerp(hoverT, target, Time.unscaledDeltaTime * lerpSpeed);

            float baseScale = Mathf.Lerp(1.04f, 1.1f, pulse);
            float scale = Mathf.Lerp(baseScale, hoverScale + 0.04f, hoverT);
            transform.localScale = new Vector3(scale, scale, 1f);

            if (outlineRT != null)
            {
                float baseSize = Mathf.Lerp(3f, 5.5f, pulse);
                float size = Mathf.Lerp(baseSize, hoverOutline + 1.5f, hoverT);
                outlineRT.offsetMin = new Vector2(-size, -size);
                outlineRT.offsetMax = new Vector2(size, size);
            }
            if (outlineImage != null)
            {
                float a = Mathf.Lerp(0.85f, 1f, pulse);
                outlineImage.color = new Color(1f, 1f, 1f, a);
            }
            return;
        }

        // ── Locked / unreachable / visited: no interaction, no pulse ──
        if (isLocked)
        {
            transform.localScale = Vector3.one;
            return;
        }

        // ── Default hover behaviour for any other state (e.g. visited-but-hoverable) ──
        float hoverTarget = isHovered ? 1f : 0f;
        if (Mathf.Abs(hoverT - hoverTarget) < 0.001f)
        {
            hoverT = hoverTarget;
            return;
        }

        hoverT = Mathf.Lerp(hoverT, hoverTarget, Time.unscaledDeltaTime * lerpSpeed);

        float hScale = Mathf.Lerp(1f, hoverScale, hoverT);
        transform.localScale = new Vector3(hScale, hScale, 1f);

        if (outlineRT != null && showOutline)
        {
            float size = Mathf.Lerp(normalOutline, hoverOutline, hoverT);
            outlineRT.offsetMin = new Vector2(-size, -size);
            outlineRT.offsetMax = new Vector2(size, size);
        }
    }

    private string GetNodeLabel(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:        return "FIGHT";
            case MapNodeType.EliteCombat:   return "ELITE";
            case MapNodeType.Rest:          return "REST";
            case MapNodeType.Enchant:       return "ENCHANT";
            case MapNodeType.Boss:          return "BOSS";
            case MapNodeType.Sacrifice:     return "SACRIFICE";
            case MapNodeType.Treasure:      return "TREASURE";
            default:                        return "?";
        }
    }

    private Color baseColor;
    private bool showOutline; // Outline sadece seçilebilir node'larda
    private bool hasIcon; // Icon sprite atanmış mı

    public void SetState(bool isReachable, bool isVisited, bool isCurrent, bool isFutureReachable = true)
    {
        // Only next-step (reachable AND not visited AND not the node we're already on) is clickable.
        bool canClick = isReachable && !isVisited && !isCurrent;
        if (button != null)
            button.interactable = canClick;

        isNextStep = canClick;
        isCurrentNode = isCurrent;
        isLocked = !isFutureReachable && !isVisited && !isCurrent;

        if (backgroundImage != null)
        {
            if (hasIcon)
            {
                // Icon varsa background her zaman gizli
                backgroundImage.color = new Color(0f, 0f, 0f, 0f);
            }
            else if (canClick)
            {
                backgroundImage.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            }
            else
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        // Text and icon: next-step = bright, current = dim gold, visited = dim grey, locked = very faint & desaturated.
        if (labelText != null)
        {
            if (isCurrent)
                labelText.color = new Color(1f, 0.9f, 0.5f, 0.9f); // gold tint — "you are here"
            else if (isVisited)
                labelText.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            else if (isLocked)
                labelText.color = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            else if (canClick)
                labelText.color = Color.white;
            else
                labelText.color = new Color(0.75f, 0.75f, 0.75f, 0.7f);
        }

        if (iconImage != null && hasIcon)
        {
            if (isCurrent)
                iconImage.color = new Color(1f, 0.95f, 0.55f, 1f); // gold-tinted, full alpha
            else if (isVisited)
                iconImage.color = new Color(0.45f, 0.45f, 0.45f, 0.5f); // desaturated
            else if (isLocked)
                iconImage.color = new Color(0.35f, 0.35f, 0.35f, 0.35f); // greyed out
            else if (canClick)
                iconImage.color = Color.white;
            else
                iconImage.color = new Color(0.85f, 0.85f, 0.85f, 0.8f); // future-reachable but not next
        }

        // Outline base color — Update() overrides this for current/next-step pulse.
        showOutline = true;
        if (outlineImage != null)
        {
            if (isCurrent)
                outlineImage.color = new Color(1f, 0.95f, 0.4f, 1f); // gold
            else if (canClick)
                outlineImage.color = new Color(1f, 1f, 1f, 1f);
            else if (isVisited)
                outlineImage.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            else if (isLocked)
                outlineImage.color = new Color(0.4f, 0.4f, 0.4f, 0.2f);
            else
                outlineImage.color = new Color(1f, 1f, 1f, 0.4f);
        }

        // Reset hover — Update() will re-drive pulse/scale next frame for current/next-step.
        isHovered = false;
        hoverT = 0f;

        // Offset per-node so pulses don't all tick in lockstep (adds visual life when multiple next-step nodes).
        pulseTime = (nodeId * 0.37f) % (Mathf.PI * 2f);
        currentPulseTime = 0f;

        // Apply the pulse state immediately instead of waiting a frame — otherwise first-open nodes
        // briefly render at flat scale before Update() runs.
        if (isNextStep)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime);
            float s = Mathf.Lerp(1.04f, 1.1f, pulse);
            transform.localScale = new Vector3(s, s, 1f);
        }
        else if (isCurrentNode)
        {
            transform.localScale = new Vector3(1.08f, 1.08f, 1f);
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        if (outlineRT != null)
        {
            float size = normalOutline;
            outlineRT.offsetMin = new Vector2(-size, -size);
            outlineRT.offsetMax = new Vector2(size, size);
        }
    }

    private void UpdateIcon(MapNodeType type)
    {
        if (iconImage == null) return;

        Sprite icon = null;
        switch (type)
        {
            case MapNodeType.Combat:        icon = combatIcon; break;
            case MapNodeType.EliteCombat:   icon = eliteIcon; break;
            case MapNodeType.Boss:          icon = bossIcon; break;
            case MapNodeType.Rest:          icon = restIcon; break;
            case MapNodeType.Sacrifice:     icon = sacrificeIcon; break;
            case MapNodeType.Enchant:       icon = enchantIcon; break;
            case MapNodeType.Treasure:      icon = treasureIcon; break;
        }

        if (icon != null)
        {
            hasIcon = true;
            iconImage.enabled = true;
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            if (labelText != null) labelText.gameObject.SetActive(false);
            if (backgroundImage != null) backgroundImage.color = new Color(0f, 0f, 0f, 0f);

            // Outline'a da aynı sprite'ı ata — böylece sprite şeklinde glow olur, beyaz kare olmaz
            if (outlineImage != null)
            {
                outlineImage.enabled = true;
                outlineImage.sprite = icon;
            }
        }
        else
        {
            hasIcon = false;
            iconImage.enabled = false;
            iconImage.sprite = null;
            if (outlineImage != null) outlineImage.enabled = false;
        }
    }

    private Color GetFallbackColor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:        return new Color(0.8f, 0.2f, 0.2f);
            case MapNodeType.EliteCombat:   return new Color(1f, 0.4f, 0f);
            case MapNodeType.Rest:          return new Color(0.2f, 0.8f, 0.4f);
            case MapNodeType.Enchant:       return new Color(0.3f, 0.8f, 1f);
            case MapNodeType.Boss:          return new Color(1f, 0f, 0f);
            case MapNodeType.Sacrifice:     return new Color(0.8f, 0.1f, 0.6f);
            case MapNodeType.Treasure:      return new Color(1f, 0.85f, 0.2f); // gold/yellow
            default:                        return Color.white;
        }
    }
}
