using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Sol paneldeki feda edilebilir perk listesinde tek bir satır.
/// Prefab olarak oluşturup SacrificeNodeManager.sacrificePerkEntryPrefab'a bağla.
/// </summary>
public class SacrificePerkEntry : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text rarityText;
    public TMP_Text locationText;   // "Aktif" veya "Stash"
    public Image background;
    public Image selectedOverlay;
    public Button button;

    private BasePerk perk;
    private SacrificeNodeManager manager;
    private bool isSelected;

    public void Setup(BasePerk p, SacrificeNodeManager mgr)
    {
        perk    = p;
        manager = mgr;

        if (nameText != null)
            nameText.text = p.perkName.ToUpperInvariant();

        if (rarityText != null)
        {
            rarityText.text  = p.rarity.ToString().ToUpperInvariant();
            rarityText.color = GetRarityColor(p.rarity);
        }

        if (locationText != null)
        {
            bool isActive = RunManager.instance != null && RunManager.instance.activePerks.Contains(p);
            locationText.text = isActive ? "AKTİF" : "STASH";
        }

        if (selectedOverlay != null)
            selectedOverlay.enabled = false;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        manager.OnPerkEntryClicked(perk);
    }

    public void SetSelected(bool value)
    {
        isSelected = value;
        if (selectedOverlay != null)
            selectedOverlay.enabled = value;
    }

    public void RefreshVisual(List<BasePerk> selectedList, bool hasTarget)
    {
        bool sel = selectedList.Contains(perk);
        SetSelected(sel);

        // Hedef seçilmemişse tüm entry'ler soluk
        if (background != null)
        {
            Color c = background.color;
            c.a = hasTarget ? (sel ? 1f : 0.7f) : 0.4f;
            background.color = c;
        }

        if (button != null)
            button.interactable = hasTarget;
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
