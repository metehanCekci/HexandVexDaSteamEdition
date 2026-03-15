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

        if (labelText != null)
            labelText.text = ""; // Label kullanılmayacaksa boş bırak

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

    public void SetState(bool isReachable, bool isVisited, bool isCurrent)
    {
        if (button != null)
            button.interactable = isReachable && !isVisited;

        if (backgroundImage != null)
        {
            if (isCurrent)
                backgroundImage.color = new Color(0.2f, 1f, 0.4f, 1f); // Yeşil — şu an burada
            else if (isVisited)
                backgroundImage.color = new Color(0.3f, 0.3f, 0.3f, 0.6f); // Koyu gri — ziyaret edildi
            else if (isReachable)
                backgroundImage.color = new Color(1f, 0.85f, 0.2f, 1f); // Altın — gidilebilir
            else
                backgroundImage.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // Gri — kilitli
        }

        if (iconImage != null)
        {
            iconImage.color = isVisited && !isCurrent
                ? new Color(1f, 1f, 1f, 0.3f)
                : Color.white;
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
