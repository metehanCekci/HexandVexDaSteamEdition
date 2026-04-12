using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    // ─── Node tipine göre icon sprite'ları (MapUI'dan atanacak) ───
    private static Sprite combatIcon;
    private static Sprite eliteIcon;
    private static Sprite restIcon;
    private static Sprite eventIcon;
    private static Sprite bossIcon;
    private static Sprite sacrificeIcon;

    public static void SetIcons(Sprite combat, Sprite elite, Sprite rest, Sprite evt, Sprite boss, Sprite sacrifice = null)
    {
        combatIcon = combat;
        eliteIcon = elite;
        restIcon = rest;
        eventIcon = evt;
        bossIcon = boss;
        sacrificeIcon = sacrifice;
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

    void Update()
    {
        // Lerp hover
        float target = isHovered ? 1f : 0f;
        if (Mathf.Abs(hoverT - target) < 0.001f)
        {
            hoverT = target;
            return;
        }

        hoverT = Mathf.Lerp(hoverT, target, Time.unscaledDeltaTime * lerpSpeed);

        float scale = Mathf.Lerp(1f, hoverScale, hoverT);
        transform.localScale = new Vector3(scale, scale, 1f);

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
        if (button != null)
            button.interactable = isReachable && !isVisited;

        if (backgroundImage != null)
        {
            if (hasIcon)
            {
                // Icon varsa background her zaman gizli
                backgroundImage.color = new Color(0f, 0f, 0f, 0f);
            }
            else if (isReachable && !isVisited)
            {
                backgroundImage.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            }
            else
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        if (labelText != null)
        {
            if (isVisited || isCurrent)
                labelText.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            else if (!isFutureReachable)
                labelText.color = new Color(0.5f, 0.5f, 0.5f, 0.15f);
            else
                labelText.color = Color.white;
        }

        if (iconImage != null && hasIcon)
        {
            if (isVisited || isCurrent)
                iconImage.color = new Color(1f, 1f, 1f, 0.3f);
            else if (!isFutureReachable)
                iconImage.color = new Color(1f, 1f, 1f, 0.1f);
            else
                iconImage.color = Color.white;
        }

        // Outline: tüm node'larda beyaz, seçilebilir olanlarda daha parlak
        showOutline = true;
        if (outlineImage != null)
        {
            if (isReachable && !isVisited)
                outlineImage.color = new Color(1f, 1f, 1f, 1f);
            else if (isVisited || isCurrent)
                outlineImage.color = new Color(1f, 1f, 1f, 0.25f);
            else if (!isFutureReachable)
                outlineImage.color = new Color(1f, 1f, 1f, 0.15f);
            else
                outlineImage.color = new Color(1f, 1f, 1f, 0.4f);
        }

        // Hover resetle
        isHovered = false;
        hoverT = 0f;
        transform.localScale = Vector3.one;
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
            case MapNodeType.Rest:          icon = restIcon; break;
            case MapNodeType.Enchant:       icon = eventIcon; break;
            case MapNodeType.Boss:          icon = bossIcon; break;
            case MapNodeType.Sacrifice:     icon = sacrificeIcon; break;
            case MapNodeType.Treasure:      icon = eventIcon; break; // TODO: treasure icon
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
