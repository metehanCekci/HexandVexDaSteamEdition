using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sacrifice ekranındaki tek bir reward slot'u.
/// Inspector'da 7 adet oluşturup SacrificeNodeManager.rewardSlots listesine bağla.
/// </summary>
public class SacrificeRewardSlot : MonoBehaviour
{
    [Header("UI")]
    public Image background;
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text rarityText;
    public TMP_Text costText;        // "5 perk"
    public GameObject soldOutOverlay;
    public Button button;
    public Image selectedOutline;    // Seçiliyken parlayan çerçeve

    private int slotIndex;
    private SacrificeNodeManager manager;

    public void Setup(SacrificeSlotData data, int index, SacrificeNodeManager mgr)
    {
        slotIndex = index;
        manager   = mgr;

        // Perk bilgilerini göster
        BasePerk perk = null;
        if (data.perkPrefab != null)
            perk = data.perkPrefab.GetComponent<BasePerk>();

        if (nameText != null)
            nameText.text = perk != null ? perk.perkName.ToUpperInvariant() : "???";

        if (rarityText != null)
            rarityText.text = data.rarity.ToString().ToUpperInvariant();

        if (costText != null)
            costText.text = SacrificeNodeManager.SacrificeCost[data.rarity] + " PERK";

        // Rarity rengi
        Color rarityColor = GetRarityColor(data.rarity);
        if (rarityText != null) rarityText.color = rarityColor;
        if (background != null) background.color = new Color(rarityColor.r * 0.2f, rarityColor.g * 0.2f, rarityColor.b * 0.2f, 0.9f);

        // Icon
        if (iconImage != null && perk != null)
        {
            var spriteField = perk.GetType().GetField("icon");
            if (spriteField != null) iconImage.sprite = spriteField.GetValue(perk) as Sprite;
        }

        // Sold out
        if (soldOutOverlay != null)
            soldOutOverlay.SetActive(data.purchased);

        if (button != null)
        {
            button.interactable = !data.purchased;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => manager.OnRewardSlotClicked(slotIndex));
        }

        if (selectedOutline != null)
            selectedOutline.enabled = false;
    }

    public void RefreshVisual(bool isSelected, int selectedCount, int required)
    {
        if (selectedOutline != null)
            selectedOutline.enabled = isSelected;

        // Confirm yakınsa slot'u parlat
        if (isSelected && background != null)
        {
            float t = required > 0 ? (float)selectedCount / required : 0f;
            Color baseColor = background.color;
            background.color = Color.Lerp(baseColor, Color.white * 0.3f + baseColor, t * 0.3f);
        }
    }

    private Color GetRarityColor(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Common:    return new Color(0.8f, 0.8f, 0.8f);
            case PerkRarity.Rare:      return new Color(0.27f, 0.53f, 1f);
            case PerkRarity.Epic:      return new Color(0.67f, 0.27f, 1f);
            case PerkRarity.Legendary: return new Color(1f, 0.67f, 0f);
            case PerkRarity.Secret:    return new Color(1f, 0.27f, 0.27f);
            default:                   return Color.white;
        }
    }
}
