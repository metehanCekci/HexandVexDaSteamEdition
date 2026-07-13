using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tek bir hotbar slot'unun Inspector'dan düzenlenebilir referansları.
/// HotbarSetupTool tarafından hierarchy'ye eklenir.
/// </summary>
public class HotbarSlotUI : MonoBehaviour
{
    [Header("References")]
    public Image background;
    public Button button;
    public Image iconImage;
    public TMP_Text keyLabel;
    public HotbarSlotTooltip tooltip;

    [Header("Cooldown Overlay")]
    [Tooltip("Semi-transparent overlay shown when item is on per-combat cooldown. Created at runtime if not assigned.")]
    public Image cooldownOverlay;
}
