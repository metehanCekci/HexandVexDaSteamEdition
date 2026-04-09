using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    public TMP_Text[] choiceTitleTexts;
    public TMP_Text[] choiceDescTexts;
    public Image[] choiceIcons;

    private MagicTileType[] currentChoices;
    private bool hasChosen;
    private int hoveredCardIndex = -1;
    private CanvasGroup panelCG;
    private bool[] cardAnimDone;
    private GameObject canvasGO;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    public void Show()
    {
        hasChosen = false;
        hoveredCardIndex = -1;

        if (enchantPanel == null)
            BuildUI();

        if (enchantPanel != null)
        {
            // panelCG yoksa al/oluştur — fade in/out için gerekli
            if (panelCG == null) panelCG = enchantPanel.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = enchantPanel.AddComponent<CanvasGroup>();

            // Flash önleme: alpha 0 ÖNCE, sonra panel aktif
            panelCG.alpha = 0f;

            if (enchantPanel.transform.parent != null)
                enchantPanel.transform.parent.gameObject.SetActive(true);
            enchantPanel.SetActive(true);
        }

        Time.timeScale = 0f;

        // Pick 3 random tile types (no duplicates)
        currentChoices = PickThreeChoices();

        // Kartları scale 0 yap (flash önleme — panel aktifken görünmesinler)
        for (int i = 0; i < 3; i++)
        {
            if (choiceButtons != null && i < choiceButtons.Length && choiceButtons[i] != null)
            {
                choiceButtons[i].transform.localScale = Vector3.zero;
                var tilt = choiceButtons[i].GetComponent<CardTilt3D>();
                if (tilt != null) tilt.scaleOverridden = true;
                var hover = choiceButtons[i].GetComponent<ShopCardHover>();
                if (hover != null) hover.scaleOverridden = true;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (i < currentChoices.Length)
            {
                SetupChoice(i, currentChoices[i]);
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].transform.localScale = Vector3.zero;
                choiceButtons[i].transform.localRotation = Quaternion.identity;
            }
            else
            {
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(false);
            }
        }

        if (titleText != null) titleText.text = "Choose an Enchantment";

        StartCoroutine(FadeInAndPopRoutine());
    }

    private void SetupChoice(int index, MagicTileType type)
    {
        Color tileColor = GetTileColor(type);

        if (choiceTitleTexts != null && index < choiceTitleTexts.Length && choiceTitleTexts[index] != null)
        {
            choiceTitleTexts[index].text = GetTileName(type);
            choiceTitleTexts[index].color = tileColor;
        }

        if (choiceDescTexts != null && index < choiceDescTexts.Length && choiceDescTexts[index] != null)
        {
            choiceDescTexts[index].text = GetTileDescription(type);
            choiceDescTexts[index].color = Color.Lerp(tileColor, new Color(0.85f, 0.85f, 0.85f, 1f), 0.4f);
        }

        if (choiceIcons != null && index < choiceIcons.Length && choiceIcons[index] != null)
            choiceIcons[index].color = tileColor;

        if (choiceButtons != null && index < choiceButtons.Length && choiceButtons[index] != null)
        {
            int idx = index;
            choiceButtons[index].onClick.RemoveAllListeners();
            choiceButtons[index].onClick.AddListener(() => OnChoose(idx));
            choiceButtons[index].interactable = true;
        }
    }

    // ─── Entrance Animation ───

    private IEnumerator FadeInAndPopRoutine()
    {
        cardAnimDone = new bool[3];

        // Panel fade in
        if (panelCG != null)
        {
            panelCG.alpha = 0f;
            float fadeDur = 0.2f;
            float elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                panelCG.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDur);
                yield return null;
            }
            panelCG.alpha = 1f;
        }

        // Cards pop in sequentially
        for (int i = 0; i < 3; i++)
        {
            if (i < currentChoices.Length && choiceButtons[i] != null && choiceButtons[i].gameObject.activeSelf)
            {
                StartCoroutine(PopInCard(choiceButtons[i].transform, i));
                if (AudioManager.instance != null) AudioManager.instance.PlayCard();
                yield return new WaitForSecondsRealtime(0.15f);
            }
        }
    }

    private IEnumerator PopInCard(Transform card, int index)
    {
        // Pop up: 0 → 1.08
        float popDur = 0.25f;
        float elapsed = 0f;
        while (elapsed < popDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / popDur;
            float ease = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(0f, 1.08f, ease);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // Settle: 1.08 → 1.0
        float settleDur = 0.1f;
        elapsed = 0f;
        while (elapsed < settleDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / settleDur;
            float s = Mathf.Lerp(1.08f, 1f, t);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
        cardAnimDone[index] = true;

        var tilt = card.GetComponent<CardTilt3D>();
        if (tilt != null) tilt.RefreshBaseScale();
        var hoverFx = card.GetComponent<ShopCardHover>();
        if (hoverFx != null) hoverFx.RefreshBaseScale();

        StartCoroutine(CardIdleBounce(card, index));
    }

    private IEnumerator CardIdleBounce(Transform card, int cardIdx)
    {
        while (card != null && card.gameObject.activeInHierarchy)
        {
            if (!cardAnimDone[cardIdx]) { yield return null; continue; }
            float target = (hoveredCardIndex == cardIdx) ? 1.1f : 1f;
            float cur = card.localScale.x;
            float s = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * 3f);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ─── Selection ───

    private void OnChoose(int index)
    {
        if (hasChosen) return;
        if (index < 0 || index >= currentChoices.Length) return;
        hasChosen = true;

        MagicTileType chosen = currentChoices[index];

        if (RunManager.instance != null)
            RunManager.instance.acquiredMagicTiles.Add(chosen);

        foreach (var btn in choiceButtons)
            if (btn != null) btn.interactable = false;

        if (titleText != null)
            titleText.text = $"{GetTileName(chosen)} Tile Acquired!";

        StartCoroutine(FadeOutAndShrinkRoutine(index));
    }

    private IEnumerator FadeOutAndShrinkRoutine(int chosenIndex)
    {
        float dur = 0.35f;
        float elapsed = 0f;

        CanvasGroup[] cardCGs = new CanvasGroup[3];
        Vector3[] startScales = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            if (choiceButtons[i] != null)
            {
                cardCGs[i] = choiceButtons[i].GetComponent<CanvasGroup>();
                if (cardCGs[i] == null) cardCGs[i] = choiceButtons[i].gameObject.AddComponent<CanvasGroup>();
                startScales[i] = choiceButtons[i].transform.localScale;
            }
        }

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float easeIn = t * t;

            for (int i = 0; i < 3; i++)
            {
                if (choiceButtons[i] == null || cardCGs[i] == null) continue;
                if (i == chosenIndex)
                {
                    cardCGs[i].alpha = Mathf.Lerp(1f, 0f, t);
                }
                else
                {
                    float scale = Mathf.Lerp(1f, 0f, easeIn);
                    choiceButtons[i].transform.localScale = startScales[i] * scale;
                    cardCGs[i].alpha = Mathf.Lerp(1f, 0f, t);
                }
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.3f);
        Close();
    }

    // ─── Close ───

    private void Close()
    {
        Time.timeScale = 1f;
        StartCoroutine(CloseWithFade());
    }

    private IEnumerator CloseWithFade()
    {
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

        // Reset card CanvasGroup alpha for next time
        for (int i = 0; i < 3; i++)
        {
            if (choiceButtons[i] != null)
            {
                var cg = choiceButtons[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    // ─── Choice Generation ───

    private MagicTileType[] PickThreeChoices()
    {
        List<MagicTileType> all = new List<MagicTileType>
        {
            MagicTileType.Red, MagicTileType.Blue,
            MagicTileType.Green, MagicTileType.Yellow,
            MagicTileType.Orange
        };

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
            case MagicTileType.Orange: return "Haste";
            default: return "Magic";
        }
    }

    private string GetTileDescription(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red: return "Deal <b>double damage</b>\nwhile standing on it";
            case MagicTileType.Blue: return "Gain <b>extended range</b>\nmovement while standing on it";
            case MagicTileType.Green: return "Roll an <b>extra die</b>\nwhile standing on it";
            case MagicTileType.Yellow: return "Earn <b>+10 bonus gold</b>\nwhen you step on it";
            case MagicTileType.Orange: return "Gain <b>+1 extra move</b>\neach turn while standing on it";
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
            case MagicTileType.Orange: return new Color(1f, 0.5f, 0.1f);
            default: return Color.white;
        }
    }

    // ─── Procedural UI Build (perk-card style) ───

    private void BuildUI()
    {
        // Load alagard font
        TMP_FontAsset alagardFont = Resources.Load<TMP_FontAsset>("alagard SDF");

        // Card color #00050C
        Color cardColor = new Color(0f, 5f / 255f, 12f / 255f, 0.95f);

        // ── Canvas ──
        canvasGO = new GameObject("EnchantCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // ── Dark overlay panel ──
        GameObject panelGO = new GameObject("EnchantPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.02f, 0.02f, 0.06f, 0f);
        panelCG = panelGO.AddComponent<CanvasGroup>();
        enchantPanel = panelGO;

        // ── Title text ──
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panelGO.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = new Vector2(0f, 250f);
        titleRT.sizeDelta = new Vector2(800f, 70f);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "Choose an Enchantment";
        titleText.fontSize = 42;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.8f, 0.9f, 1f);
        titleText.fontStyle = FontStyles.Bold;
        if (alagardFont != null) titleText.font = alagardFont;

        // ── Subtitle ──
        GameObject subGO = new GameObject("Subtitle", typeof(RectTransform));
        subGO.transform.SetParent(panelGO.transform, false);
        RectTransform subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0.5f);
        subRT.anchorMax = new Vector2(0.5f, 0.5f);
        subRT.anchoredPosition = new Vector2(0f, 210f);
        subRT.sizeDelta = new Vector2(600f, 30f);
        TMP_Text subText = subGO.AddComponent<TextMeshProUGUI>();
        subText.text = "Enchanted tiles spawn on every combat map";
        subText.fontSize = 18;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(0.55f, 0.55f, 0.65f);
        if (alagardFont != null) subText.font = alagardFont;

        // ── Cards ──
        float cardW = 280f;
        float cardH = 380f;
        float spacing = 310f;
        float startX = -spacing;

        choiceButtons = new Button[3];
        choiceTitleTexts = new TMP_Text[3];
        choiceDescTexts = new TMP_Text[3];
        choiceIcons = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            // ── Card root ──
            GameObject cardGO = new GameObject($"EnchantCard{i}", typeof(RectTransform));
            cardGO.transform.SetParent(panelGO.transform, false);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(startX + i * spacing, -20f);
            cardRT.sizeDelta = new Vector2(cardW, cardH);

            // Card background — #00005C
            Image cardBG = cardGO.AddComponent<Image>();
            cardBG.color = cardColor;

            Button btn = cardGO.AddComponent<Button>();
            btn.targetGraphic = cardBG;

            // Remove default button color transition — we handle hover ourselves
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            choiceButtons[i] = btn;

            // ── Icon (diamond shape, centered) ──
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(cardGO.transform, false);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 1f);
            iconRT.anchorMax = new Vector2(0.5f, 1f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = new Vector2(0f, -80f);
            iconRT.sizeDelta = new Vector2(90f, 90f);
            iconGO.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
            choiceIcons[i] = iconImg;

            // ── "ENCHANTMENT" badge ──
            GameObject badgeGO = new GameObject("Badge", typeof(RectTransform));
            badgeGO.transform.SetParent(cardGO.transform, false);
            RectTransform badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRT.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRT.anchoredPosition = new Vector2(0f, 30f);
            badgeRT.sizeDelta = new Vector2(250f, 22f);
            TMP_Text badgeText = badgeGO.AddComponent<TextMeshProUGUI>();
            badgeText.text = "ENCHANTMENT";
            badgeText.fontSize = 14;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = new Color(0.5f, 0.5f, 0.6f);
            badgeText.characterSpacing = 6f;
            if (alagardFont != null) badgeText.font = alagardFont;

            // ── Tile name (title) ──
            GameObject nameGO = new GameObject("TileName", typeof(RectTransform));
            nameGO.transform.SetParent(cardGO.transform, false);
            RectTransform nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.5f, 0.5f);
            nameRT.anchorMax = new Vector2(0.5f, 0.5f);
            nameRT.anchoredPosition = new Vector2(0f, 0f);
            nameRT.sizeDelta = new Vector2(250f, 40f);
            TMP_Text nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 30;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontStyle = FontStyles.Bold;
            if (alagardFont != null) nameText.font = alagardFont;
            choiceTitleTexts[i] = nameText;

            // ── Description text ──
            GameObject descGO = new GameObject("Description", typeof(RectTransform));
            descGO.transform.SetParent(cardGO.transform, false);
            RectTransform descRT = descGO.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0f, 0f);
            descRT.anchorMax = new Vector2(1f, 0.5f);
            descRT.offsetMin = new Vector2(16f, 20f);
            descRT.offsetMax = new Vector2(-16f, -30f);
            TMP_Text descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.fontSize = 17;
            descText.alignment = TextAlignmentOptions.Center;
            descText.enableWordWrapping = true;
            if (alagardFont != null) descText.font = alagardFont;
            choiceDescTexts[i] = descText;

            // ── Divider line between title and description ──
            GameObject divGO = new GameObject("Divider", typeof(RectTransform));
            divGO.transform.SetParent(cardGO.transform, false);
            RectTransform divRT = divGO.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.15f, 0.5f);
            divRT.anchorMax = new Vector2(0.85f, 0.5f);
            divRT.anchoredPosition = new Vector2(0f, -22f);
            divRT.sizeDelta = new Vector2(0f, 1f);
            Image divImg = divGO.AddComponent<Image>();
            divImg.color = new Color(0.4f, 0.4f, 0.5f, 0.3f);
            divImg.raycastTarget = false;

            // ── Add CardTilt3D for 3D mouse tilt ──
            cardGO.AddComponent<CardTilt3D>();

            // ── Hover listeners ──
            SetupCardHoverListeners(cardGO, i);
        }

        canvasGO.SetActive(false);
    }

    private void SetupCardHoverListeners(GameObject cardGO, int index)
    {
        EventTrigger trigger = cardGO.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardGO.AddComponent<EventTrigger>();

        int idx = index;

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { hoveredCardIndex = idx; });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { if (hoveredCardIndex == idx) hoveredCardIndex = -1; });
        trigger.triggers.Add(exitEntry);
    }
}
