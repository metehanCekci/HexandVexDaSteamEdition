using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class EnchantNodeUI : MonoBehaviour
{
    public static EnchantNodeUI instance;

    [Header("Panel")]
    public GameObject enchantPanel;

    [Header("UI Elements")]
    public TMP_Text titleText;
    public Button[] choiceButtons;
    public TMP_Text[] choiceLabels;
    public Image[] choiceIcons;

    private MagicTileType[] currentChoices;
    private bool hasChosen;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    public void Show()
    {
        hasChosen = false;

        if (enchantPanel == null)
            BuildUI();

        if (enchantPanel != null)
        {
            if (enchantPanel.transform.parent != null)
                enchantPanel.transform.parent.gameObject.SetActive(true);
            enchantPanel.SetActive(true);
        }

        Time.timeScale = 0f;

        // Pick 3 random tile types (no duplicates)
        currentChoices = PickThreeChoices();

        for (int i = 0; i < 3; i++)
        {
            if (i < currentChoices.Length)
            {
                SetupChoice(i, currentChoices[i]);
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(true);
            }
            else
            {
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(false);
            }
        }

        if (titleText != null) titleText.text = "Enchant a Tile";
    }

    private void SetupChoice(int index, MagicTileType type)
    {
        if (choiceLabels != null && index < choiceLabels.Length && choiceLabels[index] != null)
            choiceLabels[index].text = GetTileLabel(type);

        if (choiceIcons != null && index < choiceIcons.Length && choiceIcons[index] != null)
            choiceIcons[index].color = GetTileColor(type);

        if (choiceButtons != null && index < choiceButtons.Length && choiceButtons[index] != null)
        {
            int idx = index; // Closure capture
            choiceButtons[index].onClick.RemoveAllListeners();
            choiceButtons[index].onClick.AddListener(() => OnChoose(idx));
            choiceButtons[index].interactable = true;

            // Button background color
            Image bg = choiceButtons[index].GetComponent<Image>();
            if (bg != null)
            {
                Color c = GetTileColor(type);
                bg.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.9f);
            }
        }
    }

    private void OnChoose(int index)
    {
        if (hasChosen) return;
        if (index < 0 || index >= currentChoices.Length) return;
        hasChosen = true;

        MagicTileType chosen = currentChoices[index];

        // Add to RunManager
        if (RunManager.instance != null)
            RunManager.instance.acquiredMagicTiles.Add(chosen);

        // Disable all buttons
        foreach (var btn in choiceButtons)
        {
            if (btn != null) btn.interactable = false;
        }

        if (titleText != null)
            titleText.text = $"{GetTileName(chosen)} Tile Acquired!";

        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        Close();
    }

    private void Close()
    {
        Time.timeScale = 1f;
        StartCoroutine(CloseWithFade());
    }

    private IEnumerator CloseWithFade()
    {
        CanvasGroup panelCG = enchantPanel != null ? enchantPanel.GetComponent<CanvasGroup>() : null;
        if (panelCG == null && enchantPanel != null)
            panelCG = enchantPanel.AddComponent<CanvasGroup>();

        if (panelCG != null)
        {
            float fadeDur = 0.25f;
            float elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                panelCG.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDur);
                yield return null;
            }
            panelCG.alpha = 0f;
        }

        if (enchantPanel != null)
        {
            enchantPanel.SetActive(false);
            if (enchantPanel.transform.parent != null)
                enchantPanel.transform.parent.gameObject.SetActive(false);
        }

        if (panelCG != null) panelCG.alpha = 1f;

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    // ─── Choice Generation ───

    private MagicTileType[] PickThreeChoices()
    {
        List<MagicTileType> all = new List<MagicTileType>
        {
            MagicTileType.Red, MagicTileType.Blue,
            MagicTileType.Green, MagicTileType.Yellow
        };

        // Shuffle
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }

        return new MagicTileType[] { all[0], all[1], all[2] };
    }

    // ─── Tile Info ───

    private string GetTileName(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red: return "Fury";
            case MagicTileType.Blue: return "Swiftness";
            case MagicTileType.Green: return "Fortune";
            case MagicTileType.Yellow: return "Greed";
            default: return "Magic";
        }
    }

    private string GetTileLabel(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red: return "Fury Tile\n<size=70%>Deal double damage\nwhile standing on it</size>";
            case MagicTileType.Blue: return "Swiftness Tile\n<size=70%>Double hex movement\nwhile standing on it</size>";
            case MagicTileType.Green: return "Fortune Tile\n<size=70%>Roll an extra die\nwhile standing on it</size>";
            case MagicTileType.Yellow: return "Greed Tile\n<size=70%>Earn bonus gold on skip\nwhile standing on it</size>";
            default: return "Magic Tile";
        }
    }

    private Color GetTileColor(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red: return new Color(0.9f, 0.2f, 0.2f);
            case MagicTileType.Blue: return new Color(0.2f, 0.5f, 0.9f);
            case MagicTileType.Green: return new Color(0.2f, 0.8f, 0.3f);
            case MagicTileType.Yellow: return new Color(0.9f, 0.8f, 0.2f);
            default: return Color.white;
        }
    }

    // ─── Procedural UI Build ───

    private void BuildUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("EnchantCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Dark overlay panel
        GameObject panelGO = new GameObject("EnchantPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
        enchantPanel = panelGO;

        // Title text
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panelGO.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = new Vector2(0f, 200f);
        titleRT.sizeDelta = new Vector2(600f, 60f);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "Enchant a Tile";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.8f, 0.9f, 1f);

        // Create 3 choice buttons
        choiceButtons = new Button[3];
        choiceLabels = new TMP_Text[3];
        choiceIcons = new Image[3];

        float spacing = 260f;
        float startX = -spacing;

        for (int i = 0; i < 3; i++)
        {
            GameObject btnGO = new GameObject($"Choice{i}", typeof(RectTransform));
            btnGO.transform.SetParent(panelGO.transform, false);
            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(startX + i * spacing, -20f);
            btnRT.sizeDelta = new Vector2(220f, 280f);

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            choiceButtons[i] = btn;

            // Add ButtonAnimator for hover effects
            btnGO.AddComponent<RestButtonAnimator>();

            // Color icon (big square)
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(btnGO.transform, false);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = new Vector2(0f, 50f);
            iconRT.sizeDelta = new Vector2(80f, 80f);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.color = Color.white;
            choiceIcons[i] = iconImg;

            // Label text
            GameObject labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(btnGO.transform, false);
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 0.5f);
            labelRT.offsetMin = new Vector2(10f, 10f);
            labelRT.offsetMax = new Vector2(-10f, -10f);
            TMP_Text label = labelGO.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            choiceLabels[i] = label;
        }

        canvasGO.SetActive(false);
    }
}
