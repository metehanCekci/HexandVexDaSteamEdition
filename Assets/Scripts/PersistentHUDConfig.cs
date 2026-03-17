using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "PersistentHUDConfig", menuName = "UI/Persistent HUD Config")]
public class PersistentHUDConfig : ScriptableObject
{
    [Header("Canvas")]
    public int sortingOrder = 10;

    [Header("Container")]
    public Vector2 containerPosition = new Vector2(14f, -14f);
    public Vector2 containerSize = new Vector2(140f, 62f);
    public Color backgroundColor = new Color(0f, 5f / 255f, 12f / 255f, 0.92f);
    public Color outlineColor = new Color(0.2f, 0.25f, 0.4f, 0.5f);
    public Vector2 outlineDistance = new Vector2(1.5f, -1.5f);
    public int paddingLeft = 10, paddingRight = 10, paddingTop = 6, paddingBottom = 6;
    public float rowSpacing = 4f;

    [Header("HP Row")]
    public Color hpIconColor = new Color(0.9f, 0.25f, 0.25f);
    public Color hpTextColor = new Color(0.9f, 0.25f, 0.25f);
    public float hpFontSize = 16f;
    public float hpIconSize = 18f;
    public FontStyles hpFontStyle = FontStyles.Bold;

    [Header("Gold Row")]
    public Color goldIconTint = new Color(1f, 0.85f, 0.2f);
    public Color goldTextColor = new Color(1f, 0.92f, 0.5f);
    public float goldFontSize = 16f;
    public float goldIconSize = 18f;
    public FontStyles goldFontStyle = FontStyles.Bold;

    [Header("Row Layout")]
    public float rowHeight = 22f;
    public float iconTextSpacing = 6f;
    public float textPreferredWidth = 80f;

    [Header("Pulse Animation")]
    public float pulseDuration = 0.35f;
    public float pulseScaleMultiplier = 0.2f;
    public Color hpDamagePulseColor = new Color(1f, 0.2f, 0.2f);
    public Color goldPulseColor = new Color(1f, 0.95f, 0.4f);

    [Header("Icon Glow")]
    public float glowAlpha = 0.4f;
}
