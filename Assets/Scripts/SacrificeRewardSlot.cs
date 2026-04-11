using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SacrificeRewardSlot : MonoBehaviour
{
    public Image background;
    public Image iconImage;
    public Image glowBorder;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text costText;
    public TMP_Text rarityLabel;

    private PerkRarity slotRarity;
    private int cost;

    public void Setup(PerkRarity rarity, GameObject perkPrefab, int perkCost)
    {
        slotRarity = rarity;
        cost = perkCost;

        if (perkPrefab != null)
        {
            BasePerk bp = perkPrefab.GetComponent<BasePerk>();
            if (bp != null)
            {
                if (nameText != null) nameText.text = bp.perkName;
                if (descText != null) descText.text = bp.description;
                if (iconImage != null && bp.icon != null)
                {
                    iconImage.sprite = bp.icon;
                    iconImage.enabled = true;
                }
            }
        }
        else
        {
            if (nameText != null) nameText.text = "???";
            if (descText != null) descText.text = "";
            if (iconImage != null) iconImage.enabled = false;
        }

        if (costText != null) costText.text = $"{cost} PERKS";
        if (rarityLabel != null)
        {
            rarityLabel.text = rarity.ToString().ToUpper();
            rarityLabel.color = GetRarityColor(rarity);
        }
    }

    public void SetHighlighted(bool active)
    {
        Color rc = GetRarityColor(slotRarity);

        if (active)
        {
            if (background != null) background.color = new Color(rc.r * 0.25f, rc.g * 0.25f, rc.b * 0.25f, 0.95f);
            if (glowBorder != null) glowBorder.color = new Color(rc.r, rc.g, rc.b, 0.9f);
            if (iconImage != null) iconImage.color = Color.white;
            if (nameText != null) nameText.color = Color.white;
            if (descText != null) descText.color = new Color(0.85f, 0.85f, 0.85f);
            if (costText != null) costText.color = new Color(0.9f, 0.9f, 0.9f);
        }
        else
        {
            if (background != null) background.color = new Color(0.12f, 0.12f, 0.12f, 0.7f);
            if (glowBorder != null) glowBorder.color = new Color(0.25f, 0.25f, 0.25f, 0.25f);
            if (iconImage != null) iconImage.color = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            if (nameText != null) nameText.color = new Color(0.5f, 0.5f, 0.5f);
            if (descText != null) descText.color = new Color(0.35f, 0.35f, 0.35f);
            if (costText != null) costText.color = new Color(0.4f, 0.4f, 0.4f);
        }
    }

    public static Color GetRarityColor(PerkRarity rarity)
    {
        switch (rarity)
        {
            case PerkRarity.Rare:      return new Color(0.27f, 0.53f, 1f);
            case PerkRarity.Epic:      return new Color(0.67f, 0.27f, 1f);
            case PerkRarity.Legendary: return new Color(1f, 0.67f, 0f);
            case PerkRarity.Secret:    return new Color(1f, 0.27f, 0.27f);
            default:                   return Color.white;
        }
    }
}
