using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using TMPro;

// Hover tooltip for enchanted (magic) tiles. Self-installs — no scene setup required.
// Raycasts the cursor against the active tiles tracked by MagicTileManager and
// shows a small panel near the mouse describing what the tile does.
public class MagicTileTooltip : MonoBehaviour
{
    private static MagicTileTooltip _instance;

    private Canvas canvas;
    private RectTransform panelRT;
    private CanvasGroup canvasGroup;
    private TMP_Text titleText;
    private TMP_Text bodyText;

    private MagicTileType currentType;
    private bool hasCurrent = false;
    private float fadeSpeed = 10f;

    public static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("[MagicTileTooltip]");
        _instance = go.AddComponent<MagicTileTooltip>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void BuildUI()
    {
        // Canvas — screen-space overlay, sits above most UI
        var canvasGO = new GameObject("Canvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasGO.AddComponent<CanvasScaler>();
        // No GraphicRaycaster — tooltip must not intercept clicks

        // Panel
        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.pivot = new Vector2(0f, 1f); // top-left pivot so positioning is easy near cursor
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(0f, 0f);
        panelRT.sizeDelta = new Vector2(260f, 90f);

        canvasGroup = panelGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        bg.raycastTarget = false;

        // Outline
        var outlineGO = new GameObject("Outline", typeof(RectTransform));
        outlineGO.transform.SetParent(panelGO.transform, false);
        var outlineRT = outlineGO.GetComponent<RectTransform>();
        outlineRT.anchorMin = Vector2.zero;
        outlineRT.anchorMax = Vector2.one;
        outlineRT.offsetMin = new Vector2(-1f, -1f);
        outlineRT.offsetMax = new Vector2(1f, 1f);
        var outlineImg = outlineGO.AddComponent<Image>();
        outlineImg.color = new Color(1f, 1f, 1f, 0.35f);
        outlineImg.raycastTarget = false;
        outlineGO.transform.SetAsFirstSibling();

        // Layout
        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 10);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panelGO.transform, false);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 18f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        titleText.richText = true;

        // Body
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(panelGO.transform, false);
        bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 14f;
        bodyText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        bodyText.raycastTarget = false;
        bodyText.richText = true;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
    }

    void Update()
    {
        bool foundHover = TryGetHoveredTile(out MagicTileType type);

        if (foundHover)
        {
            if (!hasCurrent || type != currentType)
            {
                currentType = type;
                hasCurrent = true;
                UpdateContent(type);
            }
            PositionNearMouse();
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeSpeed * Time.unscaledDeltaTime);
        }
        else
        {
            hasCurrent = false;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
        }
    }

    private bool TryGetHoveredTile(out MagicTileType type)
    {
        type = MagicTileType.Red;
        if (MagicTileManager.instance == null) return false;
        if (LevelGenerator.instance == null || LevelGenerator.instance.groundMap == null) return false;

        // Don't show if cursor is over a UI element (blocks hover when panels are open)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 mouse = Input.mousePosition;
        mouse.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        world.z = 0f;

        Vector3Int cell = LevelGenerator.instance.groundMap.WorldToCell(world);
        return MagicTileManager.instance.TryGetTileAt(cell, out type);
    }

    private void PositionNearMouse()
    {
        if (panelRT == null) return;

        Vector2 mouse = Input.mousePosition;
        Vector2 offset = new Vector2(18f, -18f); // to the right, slightly below cursor
        Vector2 pos = mouse + offset;

        // Keep inside screen bounds
        float w = panelRT.rect.width;
        float h = panelRT.rect.height;
        if (pos.x + w > Screen.width)  pos.x = mouse.x - w - 18f;
        if (pos.y - h < 0f)            pos.y = h + 18f;
        if (pos.x < 0f)                pos.x = 4f;
        if (pos.y > Screen.height)     pos.y = Screen.height - 4f;

        panelRT.anchoredPosition = pos;
    }

    private void UpdateContent(MagicTileType type)
    {
        string title = GetTitle(type);
        Color tint = GetTitleColor(type);
        titleText.text = title;
        titleText.color = tint;
        bodyText.text = GetBody(type);
    }

    private static string GetTitle(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red:    return "Red Rune";
            case MagicTileType.Blue:   return "Blue Rune";
            case MagicTileType.Green:  return "Green Rune";
            case MagicTileType.Yellow: return "Yellow Rune";
            case MagicTileType.Orange: return "Orange Rune";
        }
        return "Enchanted Tile";
    }

    private static Color GetTitleColor(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red:    return new Color(1.0f, 0.45f, 0.45f);
            case MagicTileType.Blue:   return new Color(0.55f, 0.75f, 1.0f);
            case MagicTileType.Green:  return new Color(0.55f, 1.0f, 0.6f);
            case MagicTileType.Yellow: return new Color(1.0f, 0.95f, 0.5f);
            case MagicTileType.Orange: return new Color(1.0f, 0.7f, 0.4f);
        }
        return Color.white;
    }

    private static string GetBody(MagicTileType type)
    {
        string mult   = UIColors.Mult;
        string chips  = UIColors.Chips;
        string gold   = UIColors.Gold;
        string damage = UIColors.Damage;
        switch (type)
        {
            case MagicTileType.Red:
                return $"Attack from this tile to deal <color=#{mult}>x2 damage</color>.";
            case MagicTileType.Blue:
                return $"Grants <color=#{chips}>extended movement range</color>. Consumed after moving 2 hexes away.";
            case MagicTileType.Green:
                return $"Attack from this tile to roll <color=#{chips}>+1 dice</color>.";
            case MagicTileType.Yellow:
                return $"Step onto this tile to gain <color=#{gold}>+10 gold</color>.";
            case MagicTileType.Orange:
                return $"Grants <color=#{chips}>+1 extra move</color> while standing here. Consumed when you leave the tile.";
        }
        return "";
    }
}
