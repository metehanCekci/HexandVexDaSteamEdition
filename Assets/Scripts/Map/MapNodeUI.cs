using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public Image backgroundImage;
    public Button button;
    public TMP_Text labelText;

    [HideInInspector] public int nodeId;

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

    public void SetState(bool isReachable, bool isVisited, bool isCurrent)
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
                // Normal node rengi + hafif parlama
                backgroundImage.color = baseColor * 1.2f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 1f);
            }
            else
            {
                // Kilitli — çok soluk
                backgroundImage.color = baseColor * 0.25f;
                backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 0.35f);
            }
        }

        if (labelText != null)
        {
            labelText.color = (isVisited && !isCurrent) ? new Color(0.7f, 0.7f, 0.7f, 0.4f) : Color.white;
        }

        if (iconImage != null)
        {
            iconImage.color = (isVisited && !isCurrent) ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
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
