using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// World-space shop system (Cult of the Lamb style).
/// Dealer (capsule) sits at the back. 3 small item cards float above item hexes.
/// Player walks onto a card hex to buy. Exit hex (right edge) returns to map.
/// No full-screen canvas — everything happens in the game world.
/// </summary>
public class ShopDealer : MonoBehaviour
{
    public static ShopDealer instance;

    [Header("Dealer Cell")]
    public Vector3Int dealerCell;

    [Header("Idle Animation")]
    public float bounceAmplitude = 0.05f;
    public float bounceSpeed = 2f;

    [Header("Visual")]
    public Sprite dealerSprite;
    public Color dealerColor = new Color(1f, 0.85f, 0.2f, 1f);
    public int sortingOrder = 5;

    [Header("Card Visuals")]
    [Tooltip("How high above the hex the card floats.")]
    public float cardFloatHeight = 0.4f;
    [Tooltip("Card Y bob amplitude.")]
    public float cardBobAmplitude = 0.06f;
    [Tooltip("Card Y bob speed.")]
    public float cardBobSpeed = 1.5f;
    [Tooltip("Card spin speed (degrees per second).")]
    public float cardSpinSpeed = 60f;

    // ─── Runtime ───
    private Vector3Int[] itemCells;
    private Vector3Int exitCell;
    private List<BaseItem> shopItems = new List<BaseItem>();
    private List<bool> purchased = new List<bool>();
    private List<GameObject> cardObjects = new List<GameObject>();
    private List<GameObject> priceLabels = new List<GameObject>();

    // Gold HUD
    private GameObject goldHudGO;
    private TMP_Text goldHudText;
    private Image goldHudCoinIcon;

    // Exit hex visual
    private GameObject exitMarkerGO;

    private SpriteRenderer spriteRenderer;
    private bool isActive = false;

    // ═══════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        instance = this;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerMoved += OnPlayerMoved;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerMoved -= OnPlayerMoved;
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (dealerSprite != null && spriteRenderer.sprite == null)
                spriteRenderer.sprite = dealerSprite;
            spriteRenderer.color = dealerColor;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        StartCoroutine(IdleBounce());
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        CleanupShop();
    }

    // ═══════════════════════════════════════════
    // SETUP (called by LevelGenerator)
    // ═══════════════════════════════════════════

    public void SetupShopArena(Vector3Int[] itemHexes, Vector3Int exitHex)
    {
        itemCells = itemHexes;
        exitCell = exitHex;
        isActive = true;

        GenerateShopItems();
        SpawnItemCards();
        SpawnExitMarker();
        BuildGoldHUD();
        RefreshGoldHUD();
    }

    // ═══════════════════════════════════════════
    // ITEM GENERATION (reuses Shopmanager's pool)
    // ═══════════════════════════════════════════

    private void GenerateShopItems()
    {
        shopItems.Clear();
        purchased.Clear();

        if (Shopmanager.instance == null || Shopmanager.instance.itemPool.Count == 0) return;

        var pool = Shopmanager.instance.itemPool;
        BaseItem secretItem = Shopmanager.instance.secretItem;
        int slotCount = itemCells.Length;

        int secretSlotIndex = -1;
        bool secretAvailable = false;
        if (secretItem != null && secretItem is SecretPerkOrb orbCheck)
            secretAvailable = orbCheck.HasAvailableSecrets();
        bool guaranteeSecret = (RunManager.instance != null && RunManager.instance.currentLevel >= 6
                                && !Shopmanager.hasBoughtSecretItem);
        if (secretItem != null && !Shopmanager.hasBoughtSecretItem && secretAvailable &&
            (guaranteeSecret || Random.value < Shopmanager.instance.secretItemChance))
            secretSlotIndex = Random.Range(0, slotCount);

        List<int> usedIndices = new List<int>();
        for (int i = 0; i < slotCount; i++)
        {
            BaseItem selectedItem = null;
            if (i == secretSlotIndex)
            {
                selectedItem = secretItem;
            }
            else
            {
                if (pool.Count <= usedIndices.Count) break;
                int poolIdx, safety = 0;
                do
                {
                    poolIdx = Random.Range(0, pool.Count);
                    selectedItem = pool[poolIdx];
                    if (++safety > 100) break;
                }
                while (usedIndices.Contains(poolIdx) ||
                       (selectedItem != null && secretItem != null && selectedItem.itemName == secretItem.itemName) ||
                       (RunManager.instance != null && RunManager.instance.hasPerkReroll && selectedItem is MutationCatalyst));
                usedIndices.Add(poolIdx);
            }
            if (selectedItem == null) continue;
            shopItems.Add(selectedItem);
            purchased.Add(false);
        }
    }

    // ═══════════════════════════════════════════
    // WORLD-SPACE ITEM CARDS (small, side by side)
    // ═══════════════════════════════════════════

    private void SpawnItemCards()
    {
        foreach (var go in cardObjects) if (go != null) Destroy(go);
        foreach (var go in priceLabels) if (go != null) Destroy(go);
        cardObjects.Clear();
        priceLabels.Clear();

        Tilemap groundMap = LevelGenerator.instance != null ? LevelGenerator.instance.groundMap : null;
        if (groundMap == null) return;

        for (int i = 0; i < itemCells.Length && i < shopItems.Count; i++)
        {
            Vector3 hexWorldPos = groundMap.GetCellCenterWorld(itemCells[i]);
            BaseItem item = shopItems[i];

            // ─── Card root (floating above hex) ───
            GameObject cardRoot = new GameObject($"ShopCard_{i}");
            cardRoot.transform.position = hexWorldPos + new Vector3(0f, cardFloatHeight, 0f);

            // ─── Single HORIZONTAL world-space canvas: [Icon] [Name] [Coin Price] ───
            GameObject canvasGO = new GameObject("CardCanvas");
            canvasGO.transform.SetParent(cardRoot.transform, false);
            canvasGO.transform.localPosition = Vector3.zero;

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(320f, 44f);
            canvasGO.transform.localScale = new Vector3(0.005f, 0.005f, 1f);

            // Background panel
            Image bgImg = canvasGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.08f, 0.15f, 0.92f);
            bgImg.raycastTarget = false;

            // Horizontal layout: icon | name | coin+price
            HorizontalLayoutGroup hlg = canvasGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // [1] Item icon
            if (item.icon != null)
            {
                GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
                iconGO.transform.SetParent(canvasGO.transform, false);
                Image iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = item.icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 36f);
                LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 36f;
                iconLE.preferredHeight = 36f;
            }

            // [2] Item name
            GameObject nameGO = new GameObject("Name", typeof(RectTransform));
            nameGO.transform.SetParent(canvasGO.transform, false);
            TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text = item.itemName.ToUpperInvariant();
            nameTxt.fontSize = 16;
            nameTxt.alignment = TextAlignmentOptions.Left;
            nameTxt.color = Color.white;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.raycastTarget = false;
            nameGO.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 36f);
            LayoutElement nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 150f;
            nameLE.preferredHeight = 36f;

            // [3] Coin icon
            Sprite coinSprite = GetCoinSprite();
            if (coinSprite != null)
            {
                GameObject coinGO = new GameObject("Coin", typeof(RectTransform));
                coinGO.transform.SetParent(canvasGO.transform, false);
                Image coinImg = coinGO.AddComponent<Image>();
                coinImg.sprite = coinSprite;
                coinImg.preserveAspect = true;
                coinImg.raycastTarget = false;
                coinGO.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);
                LayoutElement coinLE = coinGO.AddComponent<LayoutElement>();
                coinLE.preferredWidth = 22f;
                coinLE.preferredHeight = 22f;
            }

            // [4] Price text
            GameObject priceGO = new GameObject("Price", typeof(RectTransform));
            priceGO.transform.SetParent(canvasGO.transform, false);
            TMP_Text priceTxt = priceGO.AddComponent<TextMeshProUGUI>();
            priceTxt.text = item.price.ToString();
            priceTxt.fontSize = 18;
            priceTxt.alignment = TextAlignmentOptions.Left;
            priceTxt.color = new Color(1f, 0.85f, 0.2f);
            priceTxt.fontStyle = FontStyles.Bold;
            priceTxt.raycastTarget = false;
            priceGO.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 36f);
            LayoutElement pLE = priceGO.AddComponent<LayoutElement>();
            pLE.preferredWidth = 50f;
            pLE.preferredHeight = 36f;

            // ─── Small glow under card ───
            GameObject glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(cardRoot.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, -cardFloatHeight + 0.05f, 0f);
            glowGO.transform.localScale = new Vector3(0.5f, 0.25f, 1f);
            SpriteRenderer glowSR = glowGO.AddComponent<SpriteRenderer>();
            glowSR.sprite = CreateGlowCircle();
            glowSR.color = new Color(1f, 0.85f, 0.3f, 0.4f);
            glowSR.sortingOrder = 3;

            cardObjects.Add(cardRoot);
            priceLabels.Add(null); // no separate price label needed

            // ─── Start card animation (bob only, no spin) ───
            StartCoroutine(AnimateCard(cardRoot.transform, null, i));
        }

        StartCoroutine(CardPopInSequence());
    }

    // ═══════════════════════════════════════════
    // EXIT HEX MARKER — VERY BRIGHT
    // ═══════════════════════════════════════════

    private void SpawnExitMarker()
    {
        if (exitMarkerGO != null) Destroy(exitMarkerGO);

        Tilemap groundMap = LevelGenerator.instance != null ? LevelGenerator.instance.groundMap : null;
        if (groundMap == null) return;

        Vector3 exitWorldPos = groundMap.GetCellCenterWorld(exitCell);

        exitMarkerGO = new GameObject("ExitMarker");

        // ─── Big bright glow (sprite, NOT parented to canvas scale) ───
        GameObject glowGO = new GameObject("ExitGlow");
        glowGO.transform.SetParent(exitMarkerGO.transform, false);
        glowGO.transform.position = exitWorldPos;
        glowGO.transform.localScale = new Vector3(1.2f, 0.6f, 1f);
        SpriteRenderer glowSR = glowGO.AddComponent<SpriteRenderer>();
        glowSR.sprite = CreateGlowCircle();
        glowSR.color = new Color(0.3f, 1f, 0.5f, 0.8f); // Bright green glow
        glowSR.sortingOrder = 4;

        // Second inner glow for extra brightness
        GameObject innerGlow = new GameObject("InnerGlow");
        innerGlow.transform.SetParent(exitMarkerGO.transform, false);
        innerGlow.transform.position = exitWorldPos;
        innerGlow.transform.localScale = new Vector3(0.7f, 0.35f, 1f);
        SpriteRenderer innerSR = innerGlow.AddComponent<SpriteRenderer>();
        innerSR.sprite = CreateGlowCircle();
        innerSR.color = new Color(0.5f, 1f, 0.7f, 0.9f); // Even brighter center
        innerSR.sortingOrder = 5;

        StartCoroutine(PulseExitGlow(glowSR, innerSR));

        // ─── Exit text (world-space canvas) ───
        GameObject textCanvasGO = new GameObject("ExitTextCanvas");
        textCanvasGO.transform.SetParent(exitMarkerGO.transform, false);
        textCanvasGO.transform.position = exitWorldPos + new Vector3(0f, 0.35f, 0f);

        Canvas canvas = textCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        textCanvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 50f);
        textCanvasGO.transform.localScale = new Vector3(0.008f, 0.008f, 1f);

        GameObject txtGO = new GameObject("ExitText", typeof(RectTransform));
        txtGO.transform.SetParent(textCanvasGO.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.sizeDelta = new Vector2(160f, 50f);
        TMP_Text txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text = "EXIT ►";
        txt.fontSize = 30;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.4f, 1f, 0.6f); // Bright green text
        txt.fontStyle = FontStyles.Bold;
        txt.raycastTarget = false;

        // Outline for visibility
        txt.outlineWidth = 0.3f;
        txt.outlineColor = new Color32(0, 80, 20, 255);
    }

    // ═══════════════════════════════════════════
    // GOLD HUD (small overlay at top-right)
    // ═══════════════════════════════════════════

    private void BuildGoldHUD()
    {
        if (goldHudGO != null) Destroy(goldHudGO);

        goldHudGO = new GameObject("ShopGoldHUD");
        Canvas canvas = goldHudGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = goldHudGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        goldHudGO.AddComponent<GraphicRaycaster>();

        GameObject rowGO = new GameObject("GoldRow", typeof(RectTransform));
        rowGO.transform.SetParent(goldHudGO.transform, false);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(1f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(1f, 1f);
        rowRT.anchoredPosition = new Vector2(-20f, -20f);
        rowRT.sizeDelta = new Vector2(180f, 50f);

        Image rowBG = rowGO.AddComponent<Image>();
        rowBG.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        rowBG.raycastTarget = false;

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Sprite coinSprite = GetCoinSprite();
        if (coinSprite != null)
        {
            GameObject iconGO = new GameObject("CoinIcon", typeof(RectTransform));
            iconGO.transform.SetParent(rowGO.transform, false);
            goldHudCoinIcon = iconGO.AddComponent<Image>();
            goldHudCoinIcon.sprite = coinSprite;
            goldHudCoinIcon.preserveAspect = true;
            goldHudCoinIcon.raycastTarget = false;
            iconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(32f, 32f);
            LayoutElement le = iconGO.AddComponent<LayoutElement>();
            le.preferredWidth = 32f;
            le.preferredHeight = 32f;
        }

        GameObject txtGO = new GameObject("GoldText", typeof(RectTransform));
        txtGO.transform.SetParent(rowGO.transform, false);
        goldHudText = txtGO.AddComponent<TextMeshProUGUI>();
        goldHudText.fontSize = 28;
        goldHudText.alignment = TextAlignmentOptions.Left;
        goldHudText.color = new Color(1f, 0.85f, 0.2f);
        goldHudText.fontStyle = FontStyles.Bold;
        goldHudText.raycastTarget = false;
        txtGO.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 36f);
        LayoutElement txtLE = txtGO.AddComponent<LayoutElement>();
        txtLE.preferredWidth = 100f;
        txtLE.preferredHeight = 36f;
    }

    private void RefreshGoldHUD()
    {
        if (goldHudText != null && RunManager.instance != null)
            goldHudText.text = RunManager.instance.currentGold.ToString();
    }

    // ═══════════════════════════════════════════
    // PLAYER MOVEMENT HANDLER
    // ═══════════════════════════════════════════

    private void OnPlayerMoved(Vector3Int playerCell)
    {
        if (!isActive) return;

        // Check item hexes
        for (int i = 0; i < itemCells.Length && i < shopItems.Count; i++)
        {
            if (playerCell == itemCells[i] && !purchased[i])
            {
                TryBuyItem(i);
                break;
            }
        }

        // Check exit hex
        if (playerCell == exitCell)
        {
            LeaveShop();
        }
    }

    private void TryBuyItem(int index)
    {
        if (index < 0 || index >= shopItems.Count) return;
        if (purchased[index]) return;
        if (RunManager.instance == null) return;

        BaseItem item = shopItems[index];
        if (item == null) return;

        if (item.itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
        {
            StartCoroutine(FlashGoldHUD());
            return;
        }

        if (RunManager.instance.currentGold < item.price)
        {
            StartCoroutine(FlashGoldHUD());
            return;
        }

        // Purchase!
        RunManager.instance.currentGold -= item.price;
        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        if (item.itemType == ItemType.Instant)
        {
            // Secret orb: satın alındığında her zaman bought flag'ini set et
            if (Shopmanager.instance != null && Shopmanager.instance.secretItem != null &&
                item.itemName == Shopmanager.instance.secretItem.itemName)
                Shopmanager.hasBoughtSecretItem = true;

            if (!item.Use())
            {
                RunManager.instance.currentGold += item.price;
                RefreshGoldHUD();
                return;
            }
        }
        else
        {
            GameEvents.ItemPurchased(item);
        }

        if (Shopmanager.instance != null && Shopmanager.instance.secretItem != null &&
            item.itemName == Shopmanager.instance.secretItem.itemName)
            Shopmanager.hasBoughtSecretItem = true;

        purchased[index] = true;
        RefreshGoldHUD();
        GameEvents.GoldChanged(RunManager.instance.currentGold);

        StartCoroutine(CardSoldAnimation(index));
    }

    private void LeaveShop()
    {
        if (!isActive) return;
        isActive = false;

        CleanupShop();

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    // ═══════════════════════════════════════════
    // ANIMATIONS
    // ═══════════════════════════════════════════

    private IEnumerator CardPopInSequence()
    {
        foreach (var card in cardObjects)
            if (card != null) card.transform.localScale = Vector3.zero;

        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            if (AudioManager.instance != null) AudioManager.instance.PlayCard();
            yield return StartCoroutine(PopIn(cardObjects[i].transform));
            if (i < cardObjects.Count - 1) yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator PopIn(Transform t)
    {
        float dur = 0.3f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / dur);
            float s = Mathf.Lerp(0f, 1.12f, 1f - (1f - p) * (1f - p));
            t.localScale = new Vector3(s, s, s);
            yield return null;
        }
        dur = 0.12f; elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(elapsed / dur));
            t.localScale = new Vector3(s, s, s);
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    private IEnumerator AnimateCard(Transform root, Transform face, int index)
    {
        Vector3 basePos = root.position;
        float phaseOffset = index * 1.2f;

        while (root != null)
        {
            float bob = Mathf.Sin((Time.time + phaseOffset) * cardBobSpeed) * cardBobAmplitude;
            root.position = basePos + new Vector3(0f, bob, 0f);

            if (face != null)
                face.Rotate(0f, cardSpinSpeed * Time.deltaTime, 0f);

            yield return null;
        }
    }

    private IEnumerator CardSoldAnimation(int index)
    {
        if (index >= cardObjects.Count || cardObjects[index] == null) yield break;

        Transform card = cardObjects[index].transform;

        float dur = 0.4f, elapsed = 0f;
        Vector3 startScale = card.localScale;

        // Gather all renderers (sprites + UI)
        SpriteRenderer[] srs = card.GetComponentsInChildren<SpriteRenderer>();
        Graphic[] graphics = card.GetComponentsInChildren<Graphic>();
        TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>();

        Color[] srColors = new Color[srs.Length];
        Color[] gfxColors = new Color[graphics.Length];
        Color[] txtColors = new Color[texts.Length];
        for (int i = 0; i < srs.Length; i++) srColors[i] = srs[i].color;
        for (int i = 0; i < graphics.Length; i++) gfxColors[i] = graphics[i].color;
        for (int i = 0; i < texts.Length; i++) txtColors[i] = texts[i].color;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            float s = t < 0.3f ? Mathf.Lerp(1f, 1.3f, t / 0.3f) : Mathf.Lerp(1.3f, 0f, (t - 0.3f) / 0.7f);
            card.localScale = startScale * s;

            float fade = 1f - t;
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                Color c = srColors[i]; c.a *= fade; srs[i].color = c;
            }
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                Color c = gfxColors[i]; c.a *= fade; graphics[i].color = c;
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                Color c = txtColors[i]; c.a *= fade; texts[i].color = c;
            }

            yield return null;
        }

        Destroy(cardObjects[index]);
        cardObjects[index] = null;
    }

    private IEnumerator PulseExitGlow(SpriteRenderer outerGlow, SpriteRenderer innerGlow)
    {
        Color outerBase = outerGlow != null ? outerGlow.color : Color.clear;
        Color innerBase = innerGlow != null ? innerGlow.color : Color.clear;

        while (outerGlow != null)
        {
            // Strong pulse between 0.5 and 1.0 alpha — very visible
            float pulse = 0.5f + Mathf.Sin(Time.time * 3f) * 0.5f;

            if (outerGlow != null)
            {
                Color c = outerBase;
                c.a = Mathf.Lerp(0.5f, 1f, pulse);
                outerGlow.color = c;
            }

            if (innerGlow != null)
            {
                Color c = innerBase;
                c.a = Mathf.Lerp(0.6f, 1f, pulse);
                innerGlow.color = c;

                // Also pulse scale slightly
                float s = 1f + pulse * 0.15f;
                innerGlow.transform.localScale = new Vector3(0.7f * s, 0.35f * s, 1f);
            }

            yield return null;
        }
    }

    private IEnumerator IdleBounce()
    {
        Vector3 basePos = transform.position;
        while (true)
        {
            float offset = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
            transform.position = basePos + new Vector3(0f, offset, 0f);
            yield return null;
        }
    }

    private IEnumerator FlashGoldHUD()
    {
        if (goldHudText == null) yield break;
        Color orig = goldHudText.color;
        goldHudText.color = Color.red;
        yield return new WaitForSeconds(0.3f);
        goldHudText.color = orig;
    }

    // ═══════════════════════════════════════════
    // CLEANUP
    // ═══════════════════════════════════════════

    private void CleanupShop()
    {
        foreach (var go in cardObjects) if (go != null) Destroy(go);
        foreach (var go in priceLabels) if (go != null) Destroy(go);
        cardObjects.Clear();
        priceLabels.Clear();

        if (exitMarkerGO != null) Destroy(exitMarkerGO);
        if (goldHudGO != null) Destroy(goldHudGO);
    }

    // ═══════════════════════════════════════════
    // SPRITE HELPERS
    // ═══════════════════════════════════════════

    private Sprite GetCoinSprite()
    {
        if (TurnManager.instance != null && TurnManager.instance.coinSprite != null)
            return TurnManager.instance.coinSprite;
        var vfx = Object.FindFirstObjectByType<CoinDropVFX>();
        if (vfx != null) return vfx.coinSprite;
        return null;
    }

    /// <summary>Creates a rounded rectangle sprite for card backgrounds (horizontal).</summary>
    private Sprite CreateCardSprite()
    {
        int w = 64, h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[w * h];
        float radius = 6f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inside = true;
                if (x < radius && y < radius)
                    inside = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) <= radius;
                else if (x >= w - radius && y < radius)
                    inside = Vector2.Distance(new Vector2(x, y), new Vector2(w - radius - 1, radius)) <= radius;
                else if (x < radius && y >= h - radius)
                    inside = Vector2.Distance(new Vector2(x, y), new Vector2(radius, h - radius - 1)) <= radius;
                else if (x >= w - radius && y >= h - radius)
                    inside = Vector2.Distance(new Vector2(x, y), new Vector2(w - radius - 1, h - radius - 1)) <= radius;

                pixels[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
    }

    /// <summary>Creates a soft circle sprite for glow effects.</summary>
    private Sprite CreateGlowCircle()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) / 2f;
        float maxDist = size / 2f;

        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size;
            int y = i / size;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            float a = Mathf.Clamp01(1f - dist / maxDist);
            a = a * a; // Quadratic falloff for softer edge
            pixels[i] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(pixels);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }
}
