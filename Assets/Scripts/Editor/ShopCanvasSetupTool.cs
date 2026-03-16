using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Tools → Setup Shop Canvas  — Config'den okuyarak hierarchy oluşturur + Shopmanager'a bağlar
/// Tools → Save Shop Canvas   — Sahnedeki ShopCanvas'ı okuyarak Config'e yazar
///
/// Config dosyası: Assets/Resources/ShopCanvasConfig.asset
/// Yoksa otomatik oluşturulur.
/// </summary>
public static class ShopCanvasSetupTool
{
    private const string CONFIG_PATH = "Assets/Resources/ShopCanvasConfig.asset";

    static ShopCanvasConfig GetOrCreateConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<ShopCanvasConfig>(CONFIG_PATH);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<ShopCanvasConfig>();
            cfg.font = Resources.Load<TMP_FontAsset>("alagard SDF");
            AssetDatabase.CreateAsset(cfg, CONFIG_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopCanvas] Config oluşturuldu: " + CONFIG_PATH);
        }
        return cfg;
    }

    // ═══════════════════════════════════════════════════════════════
    // SAVE — sahnedeki ShopCanvas'ı Config'e yazar
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Tools/Save Shop Canvas")]
    public static void SaveShopCanvas()
    {
        GameObject canvasGO = GameObject.Find("ShopCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[ShopCanvas] Sahnede 'ShopCanvas' bulunamadı!");
            return;
        }

        var cfg = GetOrCreateConfig();
        Undo.RecordObject(cfg, "Save Shop Canvas");

        // Canvas
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas != null) cfg.sortingOrder = canvas.sortingOrder;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null) cfg.referenceResolution = scaler.referenceResolution;

        // ShopPanel
        Transform panel = canvasGO.transform.Find("ShopPanel");
        if (panel != null)
        {
            Image panelBG = panel.GetComponent<Image>();
            if (panelBG != null) cfg.backgroundColor = panelBG.color;

            // Title
            Transform title = panel.Find("ShopTitle");
            if (title != null)
            {
                RectTransform rt = title.GetComponent<RectTransform>();
                cfg.titlePosition = rt.anchoredPosition;
                cfg.titleSize = rt.sizeDelta;
                TMP_Text txt = title.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    cfg.titleText = txt.text;
                    cfg.titleFontSize = txt.fontSize;
                    cfg.titleColor = txt.color;
                    cfg.titleFontStyle = txt.fontStyle;
                    if (txt.font != null) cfg.font = txt.font;
                }
            }

            // Gold Row
            Transform goldRow = panel.Find("GoldRow");
            if (goldRow != null)
            {
                RectTransform grt = goldRow.GetComponent<RectTransform>();
                cfg.goldRowPosition = grt.anchoredPosition;
                cfg.goldRowSize = grt.sizeDelta;

                HorizontalLayoutGroup ghlg = goldRow.GetComponent<HorizontalLayoutGroup>();
                if (ghlg != null) cfg.goldRowSpacing = ghlg.spacing;

                Transform coinIcon = goldRow.Find("CoinIcon");
                if (coinIcon != null)
                {
                    RectTransform crt = coinIcon.GetComponent<RectTransform>();
                    cfg.goldIconSize = crt.sizeDelta.x;
                    Image coinImg = coinIcon.GetComponent<Image>();
                    if (coinImg != null && coinImg.sprite != null)
                        cfg.goldIconSprite = coinImg.sprite;
                }

                Transform goldTxt = goldRow.Find("GoldText");
                if (goldTxt != null)
                {
                    TMP_Text gt = goldTxt.GetComponent<TMP_Text>();
                    if (gt != null)
                    {
                        cfg.goldFontSize = gt.fontSize;
                        cfg.goldTextColor = gt.color;
                        cfg.goldFontStyle = gt.fontStyle;
                        if (gt.font != null) cfg.font = gt.font;
                    }
                    cfg.goldTextSize = goldTxt.GetComponent<RectTransform>().sizeDelta;
                }
            }

            // Card Container
            Transform cardCont = panel.Find("CardContainer");
            if (cardCont != null)
            {
                cfg.cardContainerPosition = cardCont.GetComponent<RectTransform>().anchoredPosition;

                // Read cards
                int count = 0;
                for (int i = 0; i < cardCont.childCount; i++)
                {
                    Transform card = cardCont.GetChild(i);
                    if (!card.name.StartsWith("ShopCard")) continue;
                    count++;

                    RectTransform crt = card.GetComponent<RectTransform>();
                    cfg.cardWidth = crt.sizeDelta.x;
                    cfg.cardHeight = crt.sizeDelta.y;

                    Image bg = card.GetComponent<Image>();
                    if (bg != null) cfg.cardBackgroundColor = bg.color;

                    Outline outline = card.GetComponent<Outline>();
                    if (outline != null)
                    {
                        cfg.cardOutlineColor = outline.effectColor;
                        cfg.cardOutlineDistance = outline.effectDistance;
                    }

                    Button btn = card.GetComponent<Button>();
                    if (btn != null)
                    {
                        cfg.cardBtnNormal = btn.colors.normalColor;
                        cfg.cardBtnHighlighted = btn.colors.highlightedColor;
                        cfg.cardBtnPressed = btn.colors.pressedColor;
                        cfg.cardBtnDisabled = btn.colors.disabledColor;
                    }

                    VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null)
                    {
                        cfg.cardPaddingLeft = vlg.padding.left;
                        cfg.cardPaddingRight = vlg.padding.right;
                        cfg.cardPaddingTop = vlg.padding.top;
                        cfg.cardPaddingBottom = vlg.padding.bottom;
                        cfg.cardLayoutSpacing = vlg.spacing;
                    }

                    // Tüm child'ları tara — isim bazlı
                    for (int c = 0; c < card.childCount; c++)
                    {
                        Transform child = card.GetChild(c);
                        string cname = child.name;

                        if (cname.Contains("ItemName") || cname.Contains("Name"))
                        {
                            TMP_Text nt = child.GetComponent<TMP_Text>();
                            if (nt != null)
                            {
                                cfg.nameFontSize = nt.fontSize;
                                cfg.nameColor = nt.color;
                                cfg.nameFontStyle = nt.fontStyle;
                            }
                            LayoutElement le = child.GetComponent<LayoutElement>();
                            if (le != null) cfg.nameHeight = le.preferredHeight;
                        }
                        else if (cname.Contains("Spacer"))
                        {
                            LayoutElement sle = child.GetComponent<LayoutElement>();
                            if (sle != null) cfg.iconBottomPadding = sle.preferredHeight;
                        }
                        else if (cname.Contains("Description") || cname.Contains("Desc"))
                        {
                            TMP_Text dt = child.GetComponent<TMP_Text>();
                            if (dt != null)
                            {
                                cfg.descFontSize = dt.fontSize;
                                cfg.descColor = dt.color;
                            }
                            LayoutElement dle = child.GetComponent<LayoutElement>();
                            if (dle != null) cfg.descPreferredHeight = dle.preferredHeight;
                        }
                        else if (cname.Contains("SoldOut") || cname.Contains("Sold"))
                        {
                            Image soldBg = child.GetComponent<Image>();
                            if (soldBg != null) cfg.soldOutBgColor = soldBg.color;
                            // Nested text
                            for (int s = 0; s < child.childCount; s++)
                            {
                                TMP_Text st = child.GetChild(s).GetComponent<TMP_Text>();
                                if (st != null)
                                {
                                    cfg.soldOutFontSize = st.fontSize;
                                    cfg.soldOutTextColor = st.color;
                                }
                            }
                        }
                        else if (cname.Contains("PriceRow") || cname.Contains("Price"))
                        {
                            RectTransform prt = child.GetComponent<RectTransform>();
                            if (prt != null)
                            {
                                cfg.priceRowPosition = prt.anchoredPosition;
                                cfg.priceRowSize = prt.sizeDelta;
                            }
                            HorizontalLayoutGroup hlg = child.GetComponent<HorizontalLayoutGroup>();
                            if (hlg != null) cfg.priceRowSpacing = hlg.spacing;

                            // Price row children
                            for (int p = 0; p < child.childCount; p++)
                            {
                                Transform pchild = child.GetChild(p);
                                TMP_Text pt = pchild.GetComponent<TMP_Text>();
                                if (pt != null)
                                {
                                    cfg.priceFontSize = pt.fontSize;
                                    cfg.priceTextColor = pt.color;
                                    cfg.priceTextSize = pchild.GetComponent<RectTransform>().sizeDelta;
                                }
                                else
                                {
                                    Image coinImg = pchild.GetComponent<Image>();
                                    if (coinImg != null)
                                    {
                                        RectTransform coinRt = pchild.GetComponent<RectTransform>();
                                        cfg.coinIconSize = coinRt.sizeDelta.x;
                                        cfg.coinIconColor = coinImg.color;
                                        if (coinImg.sprite != null) cfg.goldIconSprite = coinImg.sprite;
                                    }
                                }
                            }
                        }
                        else if (cname.Contains("Icon"))
                        {
                            Image iconImg = child.GetComponent<Image>();
                            if (iconImg != null) cfg.iconPlaceholderColor = iconImg.color;
                            LayoutElement ile = child.GetComponent<LayoutElement>();
                            if (ile != null)
                            {
                                cfg.iconPreferredWidth = ile.preferredWidth;
                                cfg.iconPreferredHeight = ile.preferredHeight;
                            }
                        }
                    }

                    // Calculate spacing from first two cards
                    if (count == 1)
                    {
                        Transform next = null;
                        for (int j = i + 1; j < cardCont.childCount; j++)
                        {
                            if (cardCont.GetChild(j).name.StartsWith("ShopCard"))
                            { next = cardCont.GetChild(j); break; }
                        }
                        if (next != null)
                        {
                            float x0 = crt.anchoredPosition.x;
                            float x1 = next.GetComponent<RectTransform>().anchoredPosition.x;
                            cfg.cardSpacing = (x1 - x0) - cfg.cardWidth;
                        }
                    }

                    // İlk karttan okumak yeterli, break ile çık (hepsi aynı config'i paylaşıyor)
                    // Ama slot sayısını saymak için devam et — sadece ilk karttan veri al
                }
                cfg.slotCount = count;
                Debug.Log($"[Save] {count} kart bulundu. cardWidth={cfg.cardWidth} cardHeight={cfg.cardHeight} nameFontSize={cfg.nameFontSize} descFontSize={cfg.descFontSize}");
            }

            // Reroll Button
            Transform reroll = panel.Find("RerollBtn");
            if (reroll != null)
            {
                RectTransform rrt = reroll.GetComponent<RectTransform>();
                cfg.rerollPosition = rrt.anchoredPosition;
                cfg.rerollSize = rrt.sizeDelta;
                Image rbg = reroll.GetComponent<Image>();
                if (rbg != null) cfg.rerollBgColor = rbg.color;
                Outline rol = reroll.GetComponent<Outline>();
                if (rol != null) cfg.rerollOutlineColor = rol.effectColor;
                Button rbtn = reroll.GetComponent<Button>();
                if (rbtn != null)
                {
                    cfg.rerollBtnNormal = rbtn.colors.normalColor;
                    cfg.rerollBtnHighlighted = rbtn.colors.highlightedColor;
                    cfg.rerollBtnPressed = rbtn.colors.pressedColor;
                    cfg.rerollBtnDisabled = rbtn.colors.disabledColor;
                }
                Transform rtxt = reroll.Find("RerollText");
                if (rtxt != null)
                {
                    TMP_Text rt2 = rtxt.GetComponent<TMP_Text>();
                    if (rt2 != null)
                    {
                        cfg.rerollFontSize = rt2.fontSize;
                        cfg.rerollTextColor = rt2.color;
                    }
                }
            }

            // Continue Button
            Transform cont = panel.Find("ContinueBtn");
            if (cont != null)
            {
                RectTransform crt2 = cont.GetComponent<RectTransform>();
                cfg.continuePosition = crt2.anchoredPosition;
                cfg.continueSize = crt2.sizeDelta;
                Image cbg = cont.GetComponent<Image>();
                if (cbg != null) cfg.continueBgColor = cbg.color;
                Outline col = cont.GetComponent<Outline>();
                if (col != null) cfg.continueOutlineColor = col.effectColor;
                Button cbtn = cont.GetComponent<Button>();
                if (cbtn != null)
                {
                    cfg.continueBtnNormal = cbtn.colors.normalColor;
                    cfg.continueBtnHighlighted = cbtn.colors.highlightedColor;
                    cfg.continueBtnPressed = cbtn.colors.pressedColor;
                }
                Transform ctxt = cont.Find("ContText");
                if (ctxt != null)
                {
                    TMP_Text ct2 = ctxt.GetComponent<TMP_Text>();
                    if (ct2 != null)
                    {
                        cfg.continueFontSize = ct2.fontSize;
                        cfg.continueTextColor = ct2.color;
                        cfg.continueText = ct2.text;
                    }
                }
            }
        }

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShopCanvas] Saved! {cfg.slotCount} slot, cardSize={cfg.cardWidth}x{cfg.cardHeight}, spacing={cfg.cardSpacing}");
    }

    // ═══════════════════════════════════════════════════════════════
    // SETUP — Config'den okuyarak ShopCanvas oluşturur
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Tools/Setup Shop Canvas")]
    public static void SetupShopCanvas()
    {
        GameObject existing = GameObject.Find("ShopCanvas");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var cfg = GetOrCreateConfig();
        TMP_FontAsset font = cfg.font;
        if (font == null) font = Resources.Load<TMP_FontAsset>("alagard SDF");

        // ─── Canvas ───
        GameObject canvasGO = new GameObject("ShopCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = cfg.sortingOrder;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = cfg.referenceResolution;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Panel ───
        GameObject panelGO = MakeUI("ShopPanel", canvasGO.transform);
        Stretch(panelGO.GetComponent<RectTransform>());
        panelGO.AddComponent<Image>().color = cfg.backgroundColor;
        CanvasGroup canvasGroup = panelGO.AddComponent<CanvasGroup>();

        // ─── Title ───
        GameObject titleGO = MakeUI("ShopTitle", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = cfg.titlePosition;
        titleRT.sizeDelta = cfg.titleSize;
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = cfg.titleText;
        titleTxt.fontSize = cfg.titleFontSize;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = cfg.titleColor;
        titleTxt.fontStyle = cfg.titleFontStyle;
        if (font != null) titleTxt.font = font;

        // ─── Gold Display ───
        GameObject goldRowGO = MakeUI("GoldRow", panelGO.transform);
        RectTransform goldRowRT = goldRowGO.GetComponent<RectTransform>();
        goldRowRT.anchorMin = new Vector2(1f, 1f);
        goldRowRT.anchorMax = new Vector2(1f, 1f);
        goldRowRT.pivot = new Vector2(1f, 1f);
        goldRowRT.anchoredPosition = cfg.goldRowPosition;
        goldRowRT.sizeDelta = cfg.goldRowSize;
        HorizontalLayoutGroup goldHLG = goldRowGO.AddComponent<HorizontalLayoutGroup>();
        goldHLG.spacing = cfg.goldRowSpacing;
        goldHLG.childAlignment = TextAnchor.MiddleRight;
        goldHLG.childControlWidth = false;
        goldHLG.childControlHeight = false;
        goldHLG.childForceExpandWidth = false;
        goldHLG.childForceExpandHeight = false;

        GameObject goldIconGO = MakeUI("CoinIcon", goldRowGO.transform);
        Image goldCoinIcon = goldIconGO.AddComponent<Image>();
        goldCoinIcon.preserveAspect = true;
        goldCoinIcon.raycastTarget = false;
        if (cfg.goldIconSprite != null) goldCoinIcon.sprite = cfg.goldIconSprite;
        goldIconGO.GetComponent<RectTransform>().sizeDelta = Vector2.one * cfg.goldIconSize;
        var goldIconLE = goldIconGO.AddComponent<LayoutElement>();
        goldIconLE.preferredWidth = cfg.goldIconSize;
        goldIconLE.preferredHeight = cfg.goldIconSize;

        GameObject goldTxtGO = MakeUI("GoldText", goldRowGO.transform);
        var goldText = goldTxtGO.AddComponent<TextMeshProUGUI>();
        goldText.text = "0";
        goldText.fontSize = cfg.goldFontSize;
        goldText.alignment = TextAlignmentOptions.Left;
        goldText.color = cfg.goldTextColor;
        goldText.fontStyle = cfg.goldFontStyle;
        goldText.raycastTarget = false;
        if (font != null) goldText.font = font;
        goldTxtGO.GetComponent<RectTransform>().sizeDelta = cfg.goldTextSize;
        var goldTxtLE = goldTxtGO.AddComponent<LayoutElement>();
        goldTxtLE.preferredWidth = cfg.goldTextSize.x;
        goldTxtLE.preferredHeight = cfg.goldTextSize.y;

        // ─── Card Container ───
        GameObject cardContainer = MakeUI("CardContainer", panelGO.transform);
        RectTransform cardContRT = cardContainer.GetComponent<RectTransform>();
        cardContRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardContRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardContRT.pivot = new Vector2(0.5f, 0.5f);
        cardContRT.anchoredPosition = cfg.cardContainerPosition;
        float totalW = cfg.slotCount * cfg.cardWidth + (cfg.slotCount - 1) * cfg.cardSpacing;
        cardContRT.sizeDelta = new Vector2(totalW, cfg.cardHeight + 40f);

        // ─── Cards ───
        List<ShopCardSlot> slots = new List<ShopCardSlot>();
        for (int i = 0; i < cfg.slotCount; i++)
        {
            float startX = -totalW / 2f + cfg.cardWidth / 2f;

            GameObject cardGO = MakeUI($"ShopCard_{i}", cardContainer.transform);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(startX + i * (cfg.cardWidth + cfg.cardSpacing), 0f);
            cardRT.sizeDelta = new Vector2(cfg.cardWidth, cfg.cardHeight);

            Image cardBGImg = cardGO.AddComponent<Image>();
            cardBGImg.color = cfg.cardBackgroundColor;
            Outline cardOutline = cardGO.AddComponent<Outline>();
            cardOutline.effectColor = cfg.cardOutlineColor;
            cardOutline.effectDistance = cfg.cardOutlineDistance;

            Button buyBtn = cardGO.AddComponent<Button>();
            ColorBlock cb = buyBtn.colors;
            cb.normalColor = cfg.cardBtnNormal;
            cb.highlightedColor = cfg.cardBtnHighlighted;
            cb.pressedColor = cfg.cardBtnPressed;
            cb.disabledColor = cfg.cardBtnDisabled;
            buyBtn.colors = cb;

            var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(cfg.cardPaddingLeft, cfg.cardPaddingRight, cfg.cardPaddingTop, cfg.cardPaddingBottom);
            vlg.spacing = cfg.cardLayoutSpacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Name
            TMP_Text nameTxt = MakeUI("ItemName", cardGO.transform).AddComponent<TextMeshProUGUI>();
            nameTxt.text = "ITEM NAME";
            nameTxt.fontSize = cfg.nameFontSize;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.color = cfg.nameColor;
            nameTxt.fontStyle = cfg.nameFontStyle;
            nameTxt.raycastTarget = false;
            if (font != null) nameTxt.font = font;
            nameTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = cfg.nameHeight;

            // Icon
            Image iconImg = MakeUI("Icon", cardGO.transform).AddComponent<Image>();
            iconImg.color = cfg.iconPlaceholderColor;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var iconLE = iconImg.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredHeight = cfg.iconPreferredHeight;
            iconLE.preferredWidth = cfg.iconPreferredWidth;

            // Spacer
            MakeUI("IconSpacer", cardGO.transform).AddComponent<LayoutElement>().preferredHeight = cfg.iconBottomPadding;

            // Description
            TMP_Text descTxt = MakeUI("Description", cardGO.transform).AddComponent<TextMeshProUGUI>();
            descTxt.text = "Item description goes here.";
            descTxt.fontSize = cfg.descFontSize;
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.color = cfg.descColor;
            descTxt.enableWordWrapping = true;
            descTxt.raycastTarget = false;
            if (font != null) descTxt.font = font;
            var descLE = descTxt.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = cfg.descPreferredHeight;
            descLE.flexibleHeight = 1f;

            // Sold Out
            GameObject soldGO = MakeUI("SoldOut", cardGO.transform);
            soldGO.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(soldGO.GetComponent<RectTransform>());
            soldGO.AddComponent<Image>().color = cfg.soldOutBgColor;
            soldGO.GetComponent<Image>().raycastTarget = false;
            TMP_Text soldTxt = MakeUI("SoldOutText", soldGO.transform).AddComponent<TextMeshProUGUI>();
            Stretch(soldTxt.GetComponent<RectTransform>());
            soldTxt.text = "SOLD";
            soldTxt.fontSize = cfg.soldOutFontSize;
            soldTxt.alignment = TextAlignmentOptions.Center;
            soldTxt.color = cfg.soldOutTextColor;
            soldTxt.fontStyle = FontStyles.Bold;
            soldTxt.raycastTarget = false;
            if (font != null) soldTxt.font = font;
            soldGO.SetActive(false);

            // Price Row
            GameObject priceRowGO = MakeUI("PriceRow", cardGO.transform);
            priceRowGO.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform priceRowRT = priceRowGO.GetComponent<RectTransform>();
            priceRowRT.anchorMin = new Vector2(1f, 0f);
            priceRowRT.anchorMax = new Vector2(1f, 0f);
            priceRowRT.pivot = new Vector2(1f, 0f);
            priceRowRT.anchoredPosition = cfg.priceRowPosition;
            priceRowRT.sizeDelta = cfg.priceRowSize;
            HorizontalLayoutGroup priceHLG = priceRowGO.AddComponent<HorizontalLayoutGroup>();
            priceHLG.spacing = cfg.priceRowSpacing;
            priceHLG.childAlignment = TextAnchor.MiddleRight;
            priceHLG.childControlWidth = false;
            priceHLG.childControlHeight = false;
            priceHLG.childForceExpandWidth = false;
            priceHLG.childForceExpandHeight = false;

            Image coinImg = MakeUI("CoinIcon", priceRowGO.transform).AddComponent<Image>();
            coinImg.color = cfg.coinIconColor;
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;
            coinImg.GetComponent<RectTransform>().sizeDelta = Vector2.one * cfg.coinIconSize;
            var coinLE = coinImg.gameObject.AddComponent<LayoutElement>();
            coinLE.preferredWidth = cfg.coinIconSize;
            coinLE.preferredHeight = cfg.coinIconSize;

            TMP_Text priceTxt = MakeUI("PriceText", priceRowGO.transform).AddComponent<TextMeshProUGUI>();
            priceTxt.text = "10";
            priceTxt.fontSize = cfg.priceFontSize;
            priceTxt.alignment = TextAlignmentOptions.Left;
            priceTxt.color = cfg.priceTextColor;
            priceTxt.fontStyle = FontStyles.Bold;
            priceTxt.raycastTarget = false;
            if (font != null) priceTxt.font = font;
            priceTxt.GetComponent<RectTransform>().sizeDelta = cfg.priceTextSize;
            var pLE = priceTxt.gameObject.AddComponent<LayoutElement>();
            pLE.preferredWidth = cfg.priceTextSize.x;
            pLE.preferredHeight = cfg.priceTextSize.y;

            slots.Add(new ShopCardSlot
            {
                root = cardGO,
                background = cardBGImg,
                button = buyBtn,
                nameText = nameTxt,
                iconImage = iconImg,
                descriptionText = descTxt,
                priceText = priceTxt,
                coinImage = coinImg,
                soldOutOverlay = soldGO
            });
        }

        // ─── Reroll Button ───
        GameObject rerollGO = MakeUI("RerollBtn", panelGO.transform);
        RectTransform rerollRT = rerollGO.GetComponent<RectTransform>();
        rerollRT.anchorMin = new Vector2(0.5f, 0f);
        rerollRT.anchorMax = new Vector2(0.5f, 0f);
        rerollRT.pivot = new Vector2(1f, 0f);
        rerollRT.anchoredPosition = cfg.rerollPosition;
        rerollRT.sizeDelta = cfg.rerollSize;
        rerollGO.AddComponent<Image>().color = cfg.rerollBgColor;
        Outline rerollOL = rerollGO.AddComponent<Outline>();
        rerollOL.effectColor = cfg.rerollOutlineColor;
        rerollOL.effectDistance = new Vector2(2f, 2f);
        Button rerollBtn = rerollGO.AddComponent<Button>();
        ColorBlock rcb = rerollBtn.colors;
        rcb.normalColor = cfg.rerollBtnNormal;
        rcb.highlightedColor = cfg.rerollBtnHighlighted;
        rcb.pressedColor = cfg.rerollBtnPressed;
        rcb.disabledColor = cfg.rerollBtnDisabled;
        rerollBtn.colors = rcb;

        var rerollTxtGO = MakeUI("RerollText", rerollGO.transform);
        Stretch(rerollTxtGO.GetComponent<RectTransform>());
        var rerollTxt = rerollTxtGO.AddComponent<TextMeshProUGUI>();
        rerollTxt.text = "REROLL  <color=#FFD933>10</color>";
        rerollTxt.fontSize = cfg.rerollFontSize;
        rerollTxt.alignment = TextAlignmentOptions.Center;
        rerollTxt.color = cfg.rerollTextColor;
        rerollTxt.richText = true;
        rerollTxt.fontStyle = FontStyles.Bold;
        if (font != null) rerollTxt.font = font;

        // ─── Continue Button ───
        GameObject contGO = MakeUI("ContinueBtn", panelGO.transform);
        RectTransform contRT = contGO.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0.5f, 0f);
        contRT.anchorMax = new Vector2(0.5f, 0f);
        contRT.pivot = new Vector2(0f, 0f);
        contRT.anchoredPosition = cfg.continuePosition;
        contRT.sizeDelta = cfg.continueSize;
        contGO.AddComponent<Image>().color = cfg.continueBgColor;
        Outline contOL = contGO.AddComponent<Outline>();
        contOL.effectColor = cfg.continueOutlineColor;
        contOL.effectDistance = new Vector2(2f, 2f);
        Button contBtn = contGO.AddComponent<Button>();
        ColorBlock ccb = contBtn.colors;
        ccb.normalColor = cfg.continueBtnNormal;
        ccb.highlightedColor = cfg.continueBtnHighlighted;
        ccb.pressedColor = cfg.continueBtnPressed;
        contBtn.colors = ccb;

        var contTxtGO = MakeUI("ContText", contGO.transform);
        Stretch(contTxtGO.GetComponent<RectTransform>());
        var contTxt = contTxtGO.AddComponent<TextMeshProUGUI>();
        contTxt.text = cfg.continueText;
        contTxt.fontSize = cfg.continueFontSize;
        contTxt.alignment = TextAlignmentOptions.Center;
        contTxt.color = cfg.continueTextColor;
        contTxt.fontStyle = FontStyles.Bold;
        if (font != null) contTxt.font = font;

        // ─── Auto-wire Shopmanager ───
        Shopmanager sm = Object.FindFirstObjectByType<Shopmanager>();
        if (sm != null)
        {
            Undo.RecordObject(sm, "Wire Shop Canvas");
            sm.shopCanvasObject = canvasGO;
            sm.shopCanvasGroupRef = canvasGroup;
            sm.shopTitleTextRef = titleTxt;
            sm.goldTextRef = goldText;
            sm.rerollButtonRef = rerollBtn;
            sm.rerollPriceTextRef = rerollTxt;
            sm.continueButtonRef = contBtn;
            sm.cardSlots = slots;
            sm.shopSlotCount = cfg.slotCount;
            EditorUtility.SetDirty(sm);
            Debug.Log($"[ShopCanvas] Shopmanager auto-wired — {slots.Count} slot");
        }
        else
        {
            Debug.LogWarning("[ShopCanvas] Sahnede Shopmanager bulunamadı — referansları manuel bağla.");
        }

        EditorUtility.SetDirty(canvasGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;
        Debug.Log("[ShopCanvas] Setup tamamlandı.");
    }

    private static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
