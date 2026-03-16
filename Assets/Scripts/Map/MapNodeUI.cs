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
    private Vector3 normalScale = Vector3.one;
    private const float hoverScale = 1.08f;
    private const float normalOutline = 3f;
    private const float hoverOutline = 5f;

    // ─── Node tipine göre icon sprite'ları (MapUI'dan atanacak) ───
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

        transform.localScale = normalScale * hoverScale;
        SetOutlineSize(hoverOutline);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = normalScale;
        SetOutlineSize(normalOutline);
    }

    private void SetOutlineSize(float size)
    {
        if (outlineRT == null) return;
        outlineRT.offsetMin = new Vector2(-size, -size);
        outlineRT.offsetMax = new Vector2(size, size);
    }

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

    private Color baseColor; // Setup'ta atanan node rengi

    public void SetState(bool isReachable, bool isVisited, bool isCurrent, bool isFutureReachable = true)
    {
        if (button != null)
            button.interactable = isReachable && !isVisited;

        if (backgroundImage != null)
        {
            if (isCurrent)
            {
                // Yeşil parlak çerçeve efekti — node rengini koru ama parlaklık ekle
                backgroundImage.color = Color.Lerp(baseColor, new Color(0.2f, 1f, 0.4f), 0.5f);
            }
            else if (isVisited)
            {
                // Soluk / karartılmış
                backgroundImage.color = baseColor * 0.35f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 0.5f);
            }
            else if (isReachable)
            {
                // Seçilebilir — normal node rengi + hafif parlama
                backgroundImage.color = baseColor * 1.2f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 1f);
            }
            else if (!isFutureReachable)
            {
                // Artık ulaşılamaz — çok karanlık, öldü bu yol
                backgroundImage.color = baseColor * 0.15f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 0.2f);
            }
            else
            {
                // Henüz kilitli ama ileride ulaşılabilir — orta soluk
                backgroundImage.color = baseColor * 0.25f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 0.35f);
            }
        }

        if (labelText != null)
        {
            if (!isFutureReachable && !isVisited)
                labelText.color = new Color(0.5f, 0.5f, 0.5f, 0.15f);
            else if (isVisited && !isCurrent)
                labelText.color = new Color(0.7f, 0.7f, 0.7f, 0.4f);
            else
                labelText.color = Color.white;
        }

        if (iconImage != null)
        {
            if (!isFutureReachable && !isVisited)
                iconImage.color = new Color(1f, 1f, 1f, 0.1f);
            else if (isVisited && !isCurrent)
                iconImage.color = new Color(1f, 1f, 1f, 0.3f);
            else
                iconImage.color = Color.white;
        }

        // Outline: seçilebilir node'larda görünür, diğerlerinde soluk/gizli
        if (outlineImage != null)
        {
            Color outlineBase = new Color(0f / 255f, 5f / 255f, 12f / 255f); // #00050C
            if (isReachable && !isVisited)
                outlineImage.color = new Color(outlineBase.r, outlineBase.g, outlineBase.b, 1f);
            else if (isCurrent)
                outlineImage.color = new Color(outlineBase.r, outlineBase.g, outlineBase.b, 0.8f);
            else
                outlineImage.color = new Color(outlineBase.r, outlineBase.g, outlineBase.b, 0.1f);
        }

        // Hover'dan kalan scale'i resetle
        transform.localScale = normalScale;
        SetOutlineSize(normalOutline);
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
            // Icon yoksa node tipine göre renk ver
            iconImage.sprite = null;
            iconImage.color = GetFallbackColor(type);
        }
    }

    private Color GetFallbackColor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:       return new Color(0.8f, 0.2f, 0.2f); // Kırmızı
            case MapNodeType.EliteCombat:   return new Color(1f, 0.4f, 0f);     // Turuncu
            case MapNodeType.Shop:          return new Color(1f, 0.85f, 0.2f);  // Altın
            case MapNodeType.PerkSelection: return new Color(0.6f, 0.2f, 1f);   // Mor
            case MapNodeType.Rest:          return new Color(0.2f, 0.8f, 0.4f); // Yeşil
            case MapNodeType.Event:         return new Color(0.2f, 0.6f, 1f);   // Mavi
            case MapNodeType.Boss:          return new Color(1f, 0f, 0f);        // Parlak Kırmızı
            default:                        return Color.white;
        }
    }
}
