using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SacrificeRewardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image background;
    public Image iconImage;
    [HideInInspector] public Image glowBorder; // unused, kept for compatibility
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text costText;
    public TMP_Text rarityLabel;

    private PerkRarity slotRarity;
    private int cost;
    private bool isLocked;
    private int lockedUntilVisit;
    private GameObject currentPerkPrefab;

    public void Setup(PerkRarity rarity, GameObject perkPrefab, int perkCost)
    {
        Setup(rarity, perkPrefab, perkCost, false, 0);
    }

    // lockedUntilVisit: 1-based sacrifice visit number at which this slot unlocks.
    // Only meaningful when locked=true.
    public void Setup(PerkRarity rarity, GameObject perkPrefab, int perkCost, bool locked, int lockedUntilVisit)
    {
        slotRarity = rarity;
        cost = perkCost;
        isLocked = locked;
        this.lockedUntilVisit = lockedUntilVisit;
        this.currentPerkPrefab = perkPrefab;

        // Slot needs a raycastable graphic so PointerEnter/Exit fire
        if (background != null) background.raycastTarget = true;

        if (locked)
        {
            if (nameText != null) nameText.text = "LOCKED";
            if (descText != null) descText.text = $"Unlocks at Sacrifice #{lockedUntilVisit}";
            if (iconImage != null) iconImage.enabled = false;
        }
        else if (perkPrefab != null)
        {
            BasePerk bp = perkPrefab.GetComponent<BasePerk>();
            if (bp != null)
            {
                bp.RebuildDescription();
                if (nameText != null) nameText.text = bp.perkName;
                if (descText != null) descText.text = bp.renderedDescription;
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

        if (costText != null) costText.text = locked ? "LOCKED" : $"{cost} PERKS";
        if (rarityLabel != null)
        {
            rarityLabel.text = rarity.ToString().ToUpper();
            rarityLabel.color = GetRarityColor(rarity);
        }
    }

    public void SetHighlighted(bool active)
    {
        if (isLocked)
        {
            // Locked — very dim, red-tinted cost, rarity label muted
            if (background != null) background.color = new Color(0.04f, 0.02f, 0.04f, 0.85f);
            if (glowBorder != null) glowBorder.gameObject.SetActive(false);
            if (iconImage != null) iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            if (nameText != null) nameText.color = new Color(0.55f, 0.2f, 0.2f);
            if (descText != null) descText.color = new Color(0.65f, 0.5f, 0.3f);
            if (costText != null) costText.color = new Color(0.5f, 0.2f, 0.2f);
            if (rarityLabel != null)
            {
                Color rc = GetRarityColor(slotRarity);
                rarityLabel.color = new Color(rc.r * 0.4f, rc.g * 0.4f, rc.b * 0.4f, 1f);
            }
            return;
        }

        if (active)
        {
            // Aktif — karartma yok, her şey net görünür
            if (background != null) background.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            if (glowBorder != null) glowBorder.gameObject.SetActive(false);
            if (iconImage != null) iconImage.color = Color.white;
            if (nameText != null) nameText.color = Color.white;
            if (descText != null) descText.color = new Color(0.85f, 0.85f, 0.85f);
            if (costText != null) costText.color = new Color(0.9f, 0.9f, 0.9f);
        }
        else
        {
            // Pasif — hafif karartma
            if (background != null) background.color = new Color(0.06f, 0.06f, 0.08f, 0.7f);
            if (glowBorder != null) glowBorder.gameObject.SetActive(false);
            if (iconImage != null) iconImage.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            if (nameText != null) nameText.color = new Color(0.4f, 0.4f, 0.4f);
            if (descText != null) descText.color = new Color(0.3f, 0.3f, 0.3f);
            if (costText != null) costText.color = new Color(0.35f, 0.35f, 0.35f);
        }
    }

    public static Color GetRarityColor(PerkRarity rarity) => UIColors.GetRarityColor(rarity);

    // ─── Tooltip (uses the shared perk tooltip on ActivePerkBar) ───

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ActivePerkBar.instance == null) return;
        RectTransform anchor = transform as RectTransform;
        if (anchor == null) return;

        if (isLocked)
        {
            string title = $"{slotRarity.ToString().ToUpper()} LOCKED";
            string body = $"Unlocks at Sacrifice #{lockedUntilVisit}.\nVisit more sacrifice nodes to access this tier.";
            ActivePerkBar.instance.ShowGenericTooltip(title, body, GetRarityColor(slotRarity), anchor);
        }
        else if (currentPerkPrefab != null)
        {
            BasePerk bp = currentPerkPrefab.GetComponent<BasePerk>();
            if (bp != null)
                ActivePerkBar.instance.ShowPerkTooltip(bp, anchor);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.HideSharedTooltip();
    }
}
