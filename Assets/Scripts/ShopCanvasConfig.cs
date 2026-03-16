using UnityEngine;
using TMPro;

/// <summary>
/// ScriptableObject that stores all ShopCanvas visual settings.
/// Tools → Save Shop Canvas writes here.
/// Tools → Setup Shop Canvas reads from here.
/// </summary>
[CreateAssetMenu(fileName = "ShopCanvasConfig", menuName = "Shop/Canvas Config")]
public class ShopCanvasConfig : ScriptableObject
{
    [Header("Canvas")]
    public int sortingOrder = 92;
    public Vector2 referenceResolution = new Vector2(1920, 1080);

    [Header("Background")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("Title")]
    public string titleText = "SHOP";
    public float titleFontSize = 56;
    public Color titleColor = Color.white;
    public FontStyles titleFontStyle = FontStyles.Bold;
    public Vector2 titlePosition = new Vector2(0f, -20f);
    public Vector2 titleSize = new Vector2(500f, 80f);

    [Header("Gold Display")]
    public Vector2 goldRowPosition = new Vector2(-30f, -30f);
    public Vector2 goldRowSize = new Vector2(200f, 55f);
    public float goldRowSpacing = 8f;
    public float goldIconSize = 40f;
    public Sprite goldIconSprite;
    public float goldFontSize = 36;
    public Color goldTextColor = new Color(1f, 0.82f, 0.2f);
    public FontStyles goldFontStyle = FontStyles.Bold;
    public Vector2 goldTextSize = new Vector2(120f, 45f);

    [Header("Card Container")]
    public Vector2 cardContainerPosition = new Vector2(0f, 10f);

    [Header("Cards")]
    public int slotCount = 3;
    public float cardWidth = 300f;
    public float cardHeight = 420f;
    public float cardSpacing = 35f;
    public Color cardBackgroundColor = new Color(0.06f, 0.06f, 0.12f, 0.95f);
    public Color cardOutlineColor = new Color(0.35f, 0.35f, 0.5f, 0.6f);
    public Vector2 cardOutlineDistance = new Vector2(3f, 3f);
    public int cardPaddingLeft = 10, cardPaddingRight = 10, cardPaddingTop = 10, cardPaddingBottom = 14;
    public float cardLayoutSpacing = 6f;

    [Header("Card - Button Colors")]
    public Color cardBtnNormal = Color.white;
    public Color cardBtnHighlighted = new Color(0.85f, 0.75f, 1f);
    public Color cardBtnPressed = new Color(0.4f, 0.3f, 0.6f);
    public Color cardBtnDisabled = new Color(0.15f, 0.15f, 0.25f, 0.6f);

    [Header("Card - Item Name")]
    public float nameFontSize = 30f;
    public Color nameColor = new Color(1f, 0.82f, 0.2f);
    public FontStyles nameFontStyle = FontStyles.Bold;
    public float nameHeight = 40f;

    [Header("Card - Icon")]
    public float iconPreferredWidth = 240f;
    public float iconPreferredHeight = 240f;
    public Color iconPlaceholderColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public float iconBottomPadding = 12f;

    [Header("Card - Description")]
    public float descFontSize = 24f;
    public Color descColor = Color.white;
    public float descPreferredHeight = 70f;

    [Header("Card - Sold Out")]
    public Color soldOutBgColor = new Color(0f, 0f, 0f, 0.7f);
    public float soldOutFontSize = 42f;
    public Color soldOutTextColor = new Color(0.9f, 0.25f, 0.2f, 0.95f);

    [Header("Card - Price Row")]
    public Vector2 priceRowPosition = new Vector2(-10f, 10f);
    public Vector2 priceRowSize = new Vector2(120f, 36f);
    public float priceRowSpacing = 4f;
    public float coinIconSize = 28f;
    public Color coinIconColor = new Color(1f, 0.85f, 0.2f);
    public float priceFontSize = 26f;
    public Color priceTextColor = new Color(1f, 0.85f, 0.2f);
    public Vector2 priceTextSize = new Vector2(60f, 32f);

    [Header("Reroll Button")]
    public Vector2 rerollPosition = new Vector2(-20f, 35f);
    public Vector2 rerollSize = new Vector2(240f, 65f);
    public Color rerollBgColor = new Color(0.18f, 0.12f, 0.32f, 0.95f);
    public Color rerollOutlineColor = new Color(0.35f, 0.35f, 0.5f, 0.6f);
    public Color rerollBtnNormal = Color.white;
    public Color rerollBtnHighlighted = new Color(0.85f, 0.75f, 1f);
    public Color rerollBtnPressed = new Color(0.5f, 0.4f, 0.7f);
    public Color rerollBtnDisabled = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public float rerollFontSize = 26f;
    public Color rerollTextColor = Color.white;

    [Header("Continue Button")]
    public Vector2 continuePosition = new Vector2(20f, 35f);
    public Vector2 continueSize = new Vector2(240f, 65f);
    public Color continueBgColor = new Color(0.08f, 0.25f, 0.5f, 0.95f);
    public Color continueOutlineColor = new Color(0.35f, 0.35f, 0.5f, 0.6f);
    public Color continueBtnNormal = Color.white;
    public Color continueBtnHighlighted = new Color(0.7f, 0.85f, 1f);
    public Color continueBtnPressed = new Color(0.3f, 0.5f, 0.8f);
    public float continueFontSize = 28f;
    public Color continueTextColor = Color.white;
    public string continueText = "CONTINUE";

    [Header("Font")]
    public TMP_FontAsset font;
}
