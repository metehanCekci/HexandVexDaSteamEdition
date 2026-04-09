using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.EventSystems;

public static class MergedShopSetup
{
    [MenuItem("Tools/Hex and Vex/Rebuild MergedShop UI")]
    public static void Rebuild()
    {
        // 1. MergedShopCanvas'ı bul (inactive olabilir)
        GameObject canvasGO = null;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas.gameObject.name == "MergedShopCanvas" && canvas.gameObject.scene.IsValid())
            {
                canvasGO = canvas.gameObject;
                break;
            }
        }
        if (canvasGO == null)
        {
            Debug.LogError("MergedShopCanvas not found in scene!");
            return;
        }

        // 2. MergedShopManager'ı bul
        var manager = Object.FindObjectOfType<MergedShopManager>(true);
        if (manager == null)
        {
            Debug.LogError("MergedShopManager not found in scene!");
            return;
        }

        // 3. Canvas altındaki eski çocukları sil
        for (int i = canvasGO.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(canvasGO.transform.GetChild(i).gameObject);

        // 4. Font + Coin sprite
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("alagard SDF");
        Sprite coinSprite = LoadCoinSprite();

        // ═══════════════════════════════════
        // PANEL (fullscreen overlay)
        // ═══════════════════════════════════
        GameObject panel = CreateUI("ShopPanel", canvasGO.transform);
        Stretch(panel);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.01f, 0.01f, 0.03f, 0.92f);
        CanvasGroup cg = panel.AddComponent<CanvasGroup>();

        // ═══════════════════════════════════
        // IMPLANTS TITLE (üst)
        // ═══════════════════════════════════
        GameObject implantTitle = CreateUI("ImplantTitle", panel.transform);
        SetAnchors(implantTitle, 0.25f, 0.92f, 0.75f, 0.99f);
        AddText(implantTitle, "IMPLANTS", 42, font, new Color(0.9f, 0.6f, 1f), TextAlignmentOptions.Center);

        // IMPLANTS Subtitle
        GameObject implantSub = CreateUI("ImplantSubtitle", panel.transform);
        SetAnchors(implantSub, 0.25f, 0.89f, 0.75f, 0.93f);
        AddText(implantSub, "Get stronger", 16, font, new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.Center);

        // ═══════════════════════════════════
        // PERK SECTION (üst — 3 kart)
        // ═══════════════════════════════════
        GameObject perkSection = CreateUI("PerkSection", panel.transform);
        SetAnchors(perkSection, 0.15f, 0.55f, 0.85f, 0.88f);

        manager.perkSlots.Clear();
        for (int i = 0; i < 3; i++)
            manager.perkSlots.Add(CreatePerkCard(perkSection.transform, i, font));

        // ═══════════════════════════════════
        // ORTA BAR: Reroll (sol) — Gold (orta) — Leave (sağ)
        // ═══════════════════════════════════

        // Reroll Button (sol)
        GameObject rerollGO = CreateUI("RerollButton", panel.transform);
        SetAnchors(rerollGO, 0.04f, 0.46f, 0.22f, 0.52f);
        Image rerollBg = rerollGO.AddComponent<Image>();
        rerollBg.color = new Color(0.12f, 0.08f, 0.18f, 0.95f);
        Button rerollBtn = rerollGO.AddComponent<Button>();
        rerollBtn.targetGraphic = rerollBg;

        GameObject rerollTextGO = CreateUI("Text", rerollGO.transform);
        Stretch(rerollTextGO);
        TMP_Text rerollText = AddText(rerollTextGO, "REROLL  <color=#FFD933>15</color>", 20, font, Color.white, TextAlignmentOptions.Center);

        // ITEMS Title (orta üst)
        GameObject itemsTitle = CreateUI("ItemsTitle", panel.transform);
        SetAnchors(itemsTitle, 0.30f, 0.46f, 0.70f, 0.52f);
        AddText(itemsTitle, "ITEMS", 28, font, new Color(0.6f, 0.8f, 1f), TextAlignmentOptions.Center);

        // Gold Text (orta, ITEMS altı)
        GameObject goldGO = CreateUI("GoldText", panel.transform);
        SetAnchors(goldGO, 0.35f, 0.42f, 0.65f, 0.46f);
        TMP_Text goldText = AddText(goldGO, "0", 24, font, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);

        // Leave/Continue Button (sağ)
        GameObject contGO = CreateUI("ContinueButton", panel.transform);
        SetAnchors(contGO, 0.78f, 0.46f, 0.96f, 0.52f);
        Image contBg = contGO.AddComponent<Image>();
        contBg.color = new Color(0f, 5f / 255f, 12f / 255f, 0.95f);
        Button contBtn = contGO.AddComponent<Button>();
        contBtn.targetGraphic = contBg;

        GameObject contTextGO = CreateUI("Text", contGO.transform);
        Stretch(contTextGO);
        AddText(contTextGO, "LEAVE", 20, font, new Color(0.7f, 1f, 0.7f), TextAlignmentOptions.Center);

        // ITEMS Subtitle
        GameObject itemsSub = CreateUI("ItemsSubtitle", panel.transform);
        SetAnchors(itemsSub, 0.15f, 0.38f, 0.85f, 0.42f);
        AddText(itemsSub, "Boost your chance of survival", 14, font, new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.Center);

        // ═══════════════════════════════════
        // ITEM SECTION (alt — 3 kart)
        // ═══════════════════════════════════
        GameObject itemSection = CreateUI("ItemSection", panel.transform);
        SetAnchors(itemSection, 0.15f, 0.12f, 0.85f, 0.37f);

        manager.itemSlots.Clear();
        for (int i = 0; i < 3; i++)
            manager.itemSlots.Add(CreateItemCard(itemSection.transform, i, font, coinSprite));

        // ═══════════════════════════════════
        // WIRE REFERENCES
        // ═══════════════════════════════════
        manager.panel = panel;
        manager.canvasGroup = cg;
        manager.goldText = goldText;
        manager.perkSection = perkSection;
        manager.itemSection = itemSection;
        manager.perkRerollButton = rerollBtn;
        manager.perkRerollPriceText = rerollText;
        manager.continueButton = contBtn;

        panel.SetActive(false);
        canvasGO.SetActive(false);

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(canvasGO);
        Debug.Log("[MergedShop] UI rebuilt with new layout. Adjust positions in Scene view, then Ctrl+S.");
    }

    // ═══════════════════════════════════════════
    // PERK CARD (Implant)
    // ═══════════════════════════════════════════
    static MergedShopPerkSlot CreatePerkCard(Transform parent, int index, TMP_FontAsset font)
    {
        MergedShopPerkSlot slot = new MergedShopPerkSlot();

        float gap = 0.015f;
        float cardW = (1f - gap * 4) / 3f;
        float x = gap + (cardW + gap) * index;

        GameObject card = CreateUI($"PerkCard_{index}", parent);
        SetAnchors(card, x, 0.02f, x + cardW, 0.98f);

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0f, 5f / 255f, 12f / 255f, 0.95f);
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = bg;
        card.AddComponent<CardTilt3D>();
        AddHoverScale(card);

        // Icon
        GameObject iconGO = CreateUI("Icon", card.transform);
        SetAnchors(iconGO, 0.12f, 0.55f, 0.88f, 0.92f);
        Image icon = iconGO.AddComponent<Image>();
        icon.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Name
        GameObject nameGO = CreateUI("Name", card.transform);
        SetAnchors(nameGO, 0.04f, 0.42f, 0.96f, 0.54f);
        TMP_Text nameText = AddText(nameGO, "IMPLANT", 20, font, Color.white, TextAlignmentOptions.Center);

        // Rarity
        GameObject rarityGO = CreateUI("Rarity", card.transform);
        SetAnchors(rarityGO, 0.04f, 0.35f, 0.96f, 0.43f);
        TMP_Text rarityText = AddText(rarityGO, "COMMON", 13, font, Color.gray, TextAlignmentOptions.Center);

        // Level
        GameObject levelGO = CreateUI("Level", card.transform);
        SetAnchors(levelGO, 0.04f, 0.28f, 0.96f, 0.36f);
        TMP_Text levelText = AddText(levelGO, "Lv 0 → Lv 1", 13, font, Color.white, TextAlignmentOptions.Center);

        // Description
        GameObject descGO = CreateUI("Description", card.transform);
        SetAnchors(descGO, 0.06f, 0.03f, 0.94f, 0.28f);
        TMP_Text descText = AddText(descGO, "Description", 11, font, new Color(0.65f, 0.65f, 0.65f), TextAlignmentOptions.Top);
        descText.enableWordWrapping = true;
        descText.overflowMode = TextOverflowModes.Ellipsis;

        // Sold out overlay
        GameObject soldOut = CreateUI("SoldOutOverlay", card.transform);
        Stretch(soldOut);
        Image soldBg = soldOut.AddComponent<Image>();
        soldBg.color = new Color(0, 0, 0, 0.75f);
        soldBg.raycastTarget = false;
        GameObject soldTextGO = CreateUI("SoldText", soldOut.transform);
        Stretch(soldTextGO);
        AddText(soldTextGO, "SELECTED", 18, font, new Color(0.4f, 0.4f, 0.4f), TextAlignmentOptions.Center);
        soldOut.SetActive(false);

        slot.root = card;
        slot.background = bg;
        slot.button = btn;
        slot.nameText = nameText;
        slot.rarityText = rarityText;
        slot.levelText = levelText;
        slot.descriptionText = descText;
        slot.iconImage = icon;
        slot.soldOutOverlay = soldOut;
        return slot;
    }

    // ═══════════════════════════════════════════
    // ITEM CARD
    // ═══════════════════════════════════════════
    static MergedShopItemSlot CreateItemCard(Transform parent, int index, TMP_FontAsset font, Sprite coinSprite)
    {
        MergedShopItemSlot slot = new MergedShopItemSlot();

        float gap = 0.015f;
        float cardW = (1f - gap * 4) / 3f;
        float x = gap + (cardW + gap) * index;

        GameObject card = CreateUI($"ItemCard_{index}", parent);
        SetAnchors(card, x, 0.02f, x + cardW, 0.98f);

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.07f, 0.95f);
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = bg;
        card.AddComponent<CardTilt3D>();
        AddHoverScale(card);

        // Icon
        GameObject iconGO = CreateUI("Icon", card.transform);
        SetAnchors(iconGO, 0.12f, 0.55f, 0.88f, 0.92f);
        Image icon = iconGO.AddComponent<Image>();
        icon.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Name
        GameObject nameGO = CreateUI("Name", card.transform);
        SetAnchors(nameGO, 0.04f, 0.42f, 0.96f, 0.54f);
        TMP_Text nameText = AddText(nameGO, "ITEM", 20, font, Color.white, TextAlignmentOptions.Center);

        // Description
        GameObject descGO = CreateUI("Description", card.transform);
        SetAnchors(descGO, 0.06f, 0.18f, 0.94f, 0.42f);
        TMP_Text descText = AddText(descGO, "desc", 11, font, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Top);
        descText.enableWordWrapping = true;

        // Price text
        GameObject priceGO = CreateUI("PriceText", card.transform);
        SetAnchors(priceGO, 0.15f, 0.04f, 0.60f, 0.16f);
        TMP_Text priceText = AddText(priceGO, "10", 22, font, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);

        // Coin icon
        GameObject coinGO = CreateUI("CoinIcon", card.transform);
        SetAnchors(coinGO, 0.60f, 0.05f, 0.78f, 0.15f);
        Image coinImg = coinGO.AddComponent<Image>();
        if (coinSprite != null) { coinImg.sprite = coinSprite; coinImg.color = Color.white; }
        else coinImg.color = new Color(1f, 0.85f, 0.2f);
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;

        // Sold out overlay
        GameObject soldOut = CreateUI("SoldOut", card.transform);
        Stretch(soldOut);
        Image soldBg = soldOut.AddComponent<Image>();
        soldBg.color = new Color(0, 0, 0, 0.75f);
        soldBg.raycastTarget = false;
        GameObject soldTextGO = CreateUI("SoldText", soldOut.transform);
        Stretch(soldTextGO);
        AddText(soldTextGO, "SOLD", 18, font, new Color(0.4f, 0.4f, 0.4f), TextAlignmentOptions.Center);
        soldOut.SetActive(false);

        slot.root = card;
        slot.background = bg;
        slot.button = btn;
        slot.nameText = nameText;
        slot.descriptionText = descText;
        slot.priceText = priceText;
        slot.iconImage = icon;
        slot.soldOutOverlay = soldOut;
        return slot;
    }

    // ─── Hover Scale ───
    static void AddHoverScale(GameObject card)
    {
        EventTrigger trigger = card.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => card.transform.localScale = new Vector3(1.05f, 1.05f, 1f));
        trigger.triggers.Add(enter);
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => card.transform.localScale = Vector3.one);
        trigger.triggers.Add(exit);
    }

    // ─── Coin Sprite ───
    static Sprite LoadCoinSprite()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/VFX/Coin.aseprite");
        foreach (var a in assets)
            if (a is Sprite s) return s;
        return null;
    }

    // ─── UI Helpers ───
    static GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TMP_Text AddText(GameObject go, string text, float size, TMP_FontAsset font, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        if (font != null) tmp.font = font;
        tmp.raycastTarget = false;
        return tmp;
    }
}
