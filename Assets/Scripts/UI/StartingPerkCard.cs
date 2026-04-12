using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartingPerkCard : MonoBehaviour
{
    [Header("References")]
    public Image background;
    public Image iconImage;
    public Image selectionBorder;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text rarityText;
    public Button button;
    [HideInInspector] public CanvasGroup canvasGroup;

    private int cardIndex;
    private StartingPerkSelectionUI owner;
    private bool isSelected;

    // Shop card colors (#00050c)
    private static readonly Color cardBgColor = new Color(0f, 0.02f, 0.047f, 1f);
    private static readonly Color cardSelectedColor = new Color(0.02f, 0.08f, 0.06f, 1f);

    public void Setup(BasePerk perk, int index, StartingPerkSelectionUI owner)
    {
        this.cardIndex = index;
        this.owner = owner;
        this.isSelected = false;

        // Background — same as shop cards
        if (background != null)
            background.color = cardBgColor;

        // Icon
        if (iconImage != null)
        {
            if (perk.icon != null)
            {
                iconImage.sprite = perk.icon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        // Name — uppercase, rarity color (same as shop)
        Color rarityColor = GetRarityColor(perk.rarity);
        if (nameText != null)
        {
            nameText.text = perk.perkName.ToUpperInvariant();
            nameText.color = rarityColor;
        }

        // Rarity label
        if (rarityText != null)
        {
            rarityText.text = perk.rarity.ToString().ToUpperInvariant();
            rarityText.color = rarityColor;
        }

        // Description
        if (descriptionText != null)
            descriptionText.text = perk.description;

        // Selection border off
        if (selectionBorder != null)
            selectionBorder.gameObject.SetActive(false);

        // Button listener
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        // Add shop card effects (CardTilt3D, ShopCardHover, PerkCardRarityEffect)
        EnsureShopEffects(perk);
    }

    private void EnsureShopEffects(BasePerk perk)
    {
        // CardTilt3D — 3D rotation on mouse hover
        CardTilt3D tilt = GetComponent<CardTilt3D>();
        if (tilt == null) tilt = gameObject.AddComponent<CardTilt3D>();
        tilt.maxTiltAngle = 15f;
        tilt.tiltSpeed = 12f;
        tilt.returnSpeed = 10f;

        // ShopCardHover — scale up on hover
        ShopCardHover hover = GetComponent<ShopCardHover>();
        if (hover == null) hover = gameObject.AddComponent<ShopCardHover>();
        hover.hoverScale = 1.18f;
        hover.animSpeed = 18f;

        // Wire EventTrigger for ShopCardHover
        var trigger = GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        trigger.triggers.Clear();

        var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        entryEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((_) => hover.HoverIn());
        trigger.triggers.Add(entryEnter);

        var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        entryExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        entryExit.callback.AddListener((_) => hover.HoverOut());
        trigger.triggers.Add(entryExit);

        // PerkCardRarityEffect — particles based on rarity
        PerkCardRarityEffect fx = GetComponent<PerkCardRarityEffect>();
        if (fx == null) fx = gameObject.AddComponent<PerkCardRarityEffect>();
        RectTransform iconRT = iconImage != null ? iconImage.GetComponent<RectTransform>() : null;
        fx.Setup(perk.rarity, iconRT);
    }

    private void OnClicked()
    {
        if (owner != null)
            owner.ToggleSelection(cardIndex);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (background != null)
            background.color = selected ? cardSelectedColor : cardBgColor;

        if (selectionBorder != null)
            selectionBorder.gameObject.SetActive(selected);
    }

    private static Color GetRarityColor(PerkRarity r)
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
