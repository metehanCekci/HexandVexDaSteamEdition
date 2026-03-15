using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor tool: Tools → Setup Shop Canvas
/// Creates the ShopCanvas as a real GameObject in the hierarchy so it can be
/// inspected and tweaked in the Unity Editor. Mirrors the code-built UI in
/// Shopmanager.BuildShopUI() but as a persistent scene object.
/// </summary>
public static class ShopCanvasSetupTool
{
    [MenuItem("Tools/Setup Shop Canvas")]
    public static void SetupShopCanvas()
    {
        // Remove old one if it exists
        GameObject existing = GameObject.Find("ShopCanvas");
        if (existing != null)
            Object.DestroyImmediate(existing);

        // ─── Canvas ───
        GameObject canvasGO = new GameObject("ShopCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 92;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Full-screen panel ───
        GameObject panelGO = MakeUIObj("ShopPanel", canvasGO.transform);
        StretchFull(panelGO.GetComponent<RectTransform>());
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.03f, 0.03f, 0.06f, 0.95f);
        panelGO.AddComponent<CanvasGroup>();

        // ─── Title ───
        GameObject titleGO = MakeUIObj("ShopTitle", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta = new Vector2(500f, 70f);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "SHOP";
        titleTxt.fontSize = 48;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.85f, 0.3f);
        titleTxt.fontStyle = FontStyles.Bold;

        // ─── Gold Display (top-right) ───
        GameObject goldRowGO = MakeUIObj("GoldRow", panelGO.transform);
        RectTransform goldRowRT = goldRowGO.GetComponent<RectTransform>();
        goldRowRT.anchorMin = new Vector2(1f, 1f);
        goldRowRT.anchorMax = new Vector2(1f, 1f);
        goldRowRT.pivot = new Vector2(1f, 1f);
        goldRowRT.anchoredPosition = new Vector2(-30f, -35f);
        goldRowRT.sizeDelta = new Vector2(200f, 50f);

        HorizontalLayoutGroup goldHLG = goldRowGO.AddComponent<HorizontalLayoutGroup>();
        goldHLG.spacing = 8f;
        goldHLG.childAlignment = TextAnchor.MiddleRight;
        goldHLG.childControlWidth = false;
        goldHLG.childControlHeight = false;
        goldHLG.childForceExpandWidth = false;
        goldHLG.childForceExpandHeight = false;

        // Coin Icon
        GameObject goldIconGO = MakeUIObj("CoinIcon", goldRowGO.transform);
        Image goldCoinIcon = goldIconGO.AddComponent<Image>();
        goldCoinIcon.preserveAspect = true;
        goldCoinIcon.raycastTarget = false;
        goldIconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 36f);
        var goldIconLE = goldIconGO.AddComponent<LayoutElement>();
        goldIconLE.preferredWidth = 36f;
        goldIconLE.preferredHeight = 36f;

        // Gold Text
        GameObject goldTxtGO = MakeUIObj("GoldText", goldRowGO.transform);
        var goldText = goldTxtGO.AddComponent<TextMeshProUGUI>();
        goldText.text = "0";
        goldText.fontSize = 32;
        goldText.alignment = TextAlignmentOptions.Left;
        goldText.color = new Color(1f, 0.85f, 0.2f);
        goldText.fontStyle = FontStyles.Bold;
        goldText.raycastTarget = false;
        goldTxtGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 40f);
        var goldTxtLE = goldTxtGO.AddComponent<LayoutElement>();
        goldTxtLE.preferredWidth = 120f;
        goldTxtLE.preferredHeight = 40f;

        // ─── Card Container ───
        int slotCount = 3;
        float cardWidth = 280f;
        float cardSpacing = 30f;
        float totalW = slotCount * cardWidth + (slotCount - 1) * cardSpacing;

        GameObject cardContainer = MakeUIObj("CardContainer", panelGO.transform);
        RectTransform cardContRT = cardContainer.GetComponent<RectTransform>();
        cardContRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardContRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardContRT.pivot = new Vector2(0.5f, 0.5f);
        cardContRT.anchoredPosition = new Vector2(0f, 20f);
        cardContRT.sizeDelta = new Vector2(totalW, 420f);

        // ─── Create placeholder cards ───
        for (int i = 0; i < slotCount; i++)
        {
            float startX = -totalW / 2f + cardWidth / 2f;

            GameObject cardGO = MakeUIObj($"ShopCard_{i}", cardContainer.transform);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(startX + i * (cardWidth + cardSpacing), 0f);
            cardRT.sizeDelta = new Vector2(cardWidth, 400f);

            Image cardBG = cardGO.AddComponent<Image>();
            cardBG.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

            var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Name
            var nameText = MakeUIObj("ItemName", cardGO.transform).AddComponent<TextMeshProUGUI>();
            nameText.text = "ITEM NAME";
            nameText.fontSize = 22;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.fontStyle = FontStyles.Bold;
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

            // Sep
            var sep1 = MakeUIObj("Sep1", cardGO.transform).AddComponent<Image>();
            sep1.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            var sep1LE = sep1.gameObject.AddComponent<LayoutElement>();
            sep1LE.preferredHeight = 2f; sep1LE.minHeight = 2f;

            // Icon placeholder
            var iconImg = MakeUIObj("Icon", cardGO.transform).AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            iconImg.preserveAspect = true;
            var iconLE = iconImg.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredHeight = 120f; iconLE.preferredWidth = 120f;

            // Description
            var descText = MakeUIObj("Description", cardGO.transform).AddComponent<TextMeshProUGUI>();
            descText.text = "Item description goes here.";
            descText.fontSize = 15;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = new Color(0.85f, 0.85f, 0.85f);
            descText.enableWordWrapping = true;
            var descLE = descText.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 80f; descLE.flexibleHeight = 1f;

            // Sep2
            var sep2 = MakeUIObj("Sep2", cardGO.transform).AddComponent<Image>();
            sep2.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            var sep2LE = sep2.gameObject.AddComponent<LayoutElement>();
            sep2LE.preferredHeight = 2f; sep2LE.minHeight = 2f;

            // Price row
            GameObject priceRow = MakeUIObj("PriceRow", cardGO.transform);
            var priceHLG = priceRow.AddComponent<HorizontalLayoutGroup>();
            priceHLG.spacing = 6f;
            priceHLG.childAlignment = TextAnchor.MiddleCenter;
            priceHLG.childControlWidth = false;
            priceHLG.childControlHeight = false;
            priceRow.AddComponent<LayoutElement>().preferredHeight = 36f;

            var coinImg = MakeUIObj("CoinIcon", priceRow.transform).AddComponent<Image>();
            coinImg.color = new Color(1f, 0.85f, 0.2f);
            coinImg.GetComponent<RectTransform>().sizeDelta = new Vector2(26f, 26f);
            var coinLE = coinImg.gameObject.AddComponent<LayoutElement>();
            coinLE.preferredWidth = 26f; coinLE.preferredHeight = 26f;

            var priceText = MakeUIObj("PriceText", priceRow.transform).AddComponent<TextMeshProUGUI>();
            priceText.text = "10";
            priceText.fontSize = 24;
            priceText.alignment = TextAlignmentOptions.Left;
            priceText.color = new Color(1f, 0.85f, 0.2f);
            priceText.fontStyle = FontStyles.Bold;
            priceText.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 30f);
            var pLE = priceText.gameObject.AddComponent<LayoutElement>();
            pLE.preferredWidth = 60f; pLE.preferredHeight = 30f;
        }

        // ─── Reroll Button (bottom-left) ───
        GameObject rerollGO = MakeUIObj("RerollBtn", panelGO.transform);
        RectTransform rerollRT = rerollGO.GetComponent<RectTransform>();
        rerollRT.anchorMin = new Vector2(0.5f, 0f);
        rerollRT.anchorMax = new Vector2(0.5f, 0f);
        rerollRT.pivot = new Vector2(1f, 0f);
        rerollRT.anchoredPosition = new Vector2(-20f, 40f);
        rerollRT.sizeDelta = new Vector2(220f, 60f);
        Image rerollBG = rerollGO.AddComponent<Image>();
        rerollBG.color = new Color(0.25f, 0.2f, 0.4f, 0.9f);

        var rerollTxtGO = MakeUIObj("RerollText", rerollGO.transform);
        StretchFull(rerollTxtGO.GetComponent<RectTransform>());
        var rerollTxt = rerollTxtGO.AddComponent<TextMeshProUGUI>();
        rerollTxt.text = "REROLL  <color=#FFD933>10</color>";
        rerollTxt.fontSize = 22;
        rerollTxt.alignment = TextAlignmentOptions.Center;
        rerollTxt.color = Color.white;

        // ─── Continue Button (bottom-right) ───
        GameObject contGO = MakeUIObj("ContinueBtn", panelGO.transform);
        RectTransform contRT = contGO.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0.5f, 0f);
        contRT.anchorMax = new Vector2(0.5f, 0f);
        contRT.pivot = new Vector2(0f, 0f);
        contRT.anchoredPosition = new Vector2(20f, 40f);
        contRT.sizeDelta = new Vector2(220f, 60f);
        Image contBG = contGO.AddComponent<Image>();
        contBG.color = new Color(0.2f, 0.5f, 0.8f, 0.9f);

        var contTxtGO = MakeUIObj("ContText", contGO.transform);
        StretchFull(contTxtGO.GetComponent<RectTransform>());
        var contTxt = contTxtGO.AddComponent<TextMeshProUGUI>();
        contTxt.text = "CONTINUE";
        contTxt.fontSize = 24;
        contTxt.alignment = TextAlignmentOptions.Center;
        contTxt.color = Color.white;
        contTxt.fontStyle = FontStyles.Bold;

        // Mark scene dirty
        EditorUtility.SetDirty(canvasGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Selection.activeGameObject = canvasGO;
        Debug.Log("ShopCanvas oluşturuldu — hierarchy'ye eklendi. İstediğiniz gibi düzenleyebilirsiniz.");
    }

    private static GameObject MakeUIObj(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
