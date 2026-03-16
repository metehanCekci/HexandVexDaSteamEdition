using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Map-node Shop Manager — Inspector-driven shop canvas.
/// Use Tools → Setup Shop Canvas to create the hierarchy, then wire references.
/// 4th slot = duplicate a card in hierarchy + drag into cardSlots list.
/// </summary>
public class Shopmanager : MonoBehaviour
{
    public static Shopmanager instance;

    [Header("Item Pool")]
    public List<BaseItem> itemPool = new List<BaseItem>();

    [Header("Secret Item")]
    public BaseItem secretItem;
    [Range(0f, 1f)] public float secretItemChance = 0.001f;

    [Header("Shop Config")]
    public int shopSlotCount = 3;

    [Header("Reroll Settings")]
    public float rerollBaseCost = 10f;
    public float rerollMultiplier = 1.5f;

    // ═══════════════════════════════════════════
    // SCENE CANVAS REFERENCES (wire via Inspector or Setup Tool)
    // ═══════════════════════════════════════════

    [Header("Shop Canvas")]
    public GameObject shopCanvasObject;
    public CanvasGroup shopCanvasGroupRef;

    [Header("Shop Texts")]
    public TMP_Text shopTitleTextRef;
    public TMP_Text goldTextRef;

    [Header("Shop Buttons")]
    public Button rerollButtonRef;
    public TMP_Text rerollPriceTextRef;
    public Button continueButtonRef;

    [Header("Shop Card Slots")]
    public List<ShopCardSlot> cardSlots = new List<ShopCardSlot>();

    // ─── Legacy Inspector References (kept so Inspector doesn't break) ───
    [Header("Legacy (no longer used)")]
    public Transform shopSlotContainer;
    public GameObject shopSlotPrefab;
    public TMP_Text coinDisplayText;
    public Button rerollButton;
    public TMP_Text rerollPriceText;
    public GameObject continueButton;
    [HideInInspector] public GameObject shopCanvas; // old field

    // ─── Internal State ───
    private List<BaseItem> currentItems = new List<BaseItem>();
    private List<bool> purchased = new List<bool>();
    private HashSet<string> shownItemNames = new HashSet<string>();

    private int rerollCount = 0;
    private int currentRerollCost;

    public static bool hasBoughtSecretItem = false;
    private bool isShopOpen = false;
    private int hoveredCardIndex = -1;

    // ═══════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);

        if (RunManager.instance != null && RunManager.instance.currentLevel <= 1)
            hasBoughtSecretItem = false;

        // HIDE legacy scene UI so it doesn't overlap
        HideLegacyUI();

        // Wire button listeners + Color Tint hover
        if (rerollButtonRef != null)
        {
            rerollButtonRef.onClick.RemoveAllListeners();
            rerollButtonRef.onClick.AddListener(TryReroll);
            MakeColorTintWork(rerollButtonRef);
        }
        if (continueButtonRef != null)
        {
            continueButtonRef.onClick.RemoveAllListeners();
            continueButtonRef.onClick.AddListener(CloseMapNodeShop);
            MakeColorTintWork(continueButtonRef);
        }

        // Wire card buy buttons + hover
        for (int i = 0; i < cardSlots.Count; i++)
        {
            int idx = i;
            var slot = cardSlots[i];
            if (slot.button != null)
            {
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => TryBuy(idx));
                MakeColorTintWork(slot.button);
            }
            // Hover index for idle bounce
            if (slot.root != null)
            {
                EventTrigger trigger = slot.root.GetComponent<EventTrigger>();
                if (trigger == null) trigger = slot.root.AddComponent<EventTrigger>();
                trigger.triggers.Clear();
                EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((_) => { hoveredCardIndex = idx; });
                trigger.triggers.Add(enterEntry);
                EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => { if (hoveredCardIndex == idx) hoveredCardIndex = -1; });
                trigger.triggers.Add(exitEntry);
            }
        }

        // Set coin sprites + fix gold layout order (text left, icon right)
        Sprite coinSprite = GetCoinSprite();
        if (goldTextRef != null && goldTextRef.transform.parent != null)
        {
            // Text first (left), coin icon second (right)
            goldTextRef.transform.SetAsFirstSibling();
            goldTextRef.alignment = TextAlignmentOptions.Right;
            HorizontalLayoutGroup hlg = goldTextRef.transform.parent.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) hlg.childAlignment = TextAnchor.MiddleRight;
        }
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i].coinImage != null && coinSprite != null)
                cardSlots[i].coinImage.sprite = coinSprite;
            // Price row: text left, coin icon right
            if (cardSlots[i].priceText != null && cardSlots[i].coinImage != null)
            {
                cardSlots[i].priceText.transform.SetAsFirstSibling();
                cardSlots[i].coinImage.transform.SetAsLastSibling();
            }
        }

        CloseShop();
    }

    private void HideLegacyUI()
    {
        if (shopSlotContainer != null && shopSlotContainer.parent != null)
            shopSlotContainer.parent.gameObject.SetActive(false);
        if (rerollButton != null)
            rerollButton.gameObject.SetActive(false);
        if (coinDisplayText != null)
            coinDisplayText.transform.parent?.gameObject.SetActive(false);
        if (rerollPriceText != null)
            rerollPriceText.gameObject.SetActive(false);
        if (continueButton != null)
            continueButton.SetActive(false);
    }

    private Sprite GetCoinSprite()
    {
        if (TurnManager.instance != null && TurnManager.instance.coinSprite != null)
            return TurnManager.instance.coinSprite;
        var vfx = Object.FindFirstObjectByType<CoinDropVFX>();
        if (vfx != null) return vfx.coinSprite;
        return null;
    }

    // ═══════════════════════════════════════════
    // SHOP OPEN / CLOSE
    // ═══════════════════════════════════════════

    public void OpenAsMapNode()
    {
        rerollCount = 0;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);
        shownItemNames.Clear();
        GenerateShopItems();
        RefreshCoinDisplay();

        if (shopCanvasObject != null)
            shopCanvasObject.SetActive(true);

        isShopOpen = true;
        hoveredCardIndex = -1;

        StopAllCoroutines();
        StartCoroutine(ShopOpenAnimation());
        GameEvents.ShopOpened();
    }

    private void CloseMapNodeShop()
    {
        StopAllCoroutines();
        StartCoroutine(ShopCloseAnimation());
    }

    public void CloseShop()
    {
        if (shopCanvasObject != null)
            shopCanvasObject.SetActive(false);

        if (isShopOpen)
        {
            isShopOpen = false;
            GameEvents.ShopClosed();
        }
    }

    public bool IsShopOpen => isShopOpen;

    // ═══════════════════════════════════════════
    // ANIMATIONS
    // ═══════════════════════════════════════════

    private IEnumerator ShopOpenAnimation()
    {
        if (shopCanvasGroupRef != null) shopCanvasGroupRef.alpha = 1f;

        for (int i = 0; i < cardSlots.Count; i++)
            if (i < currentItems.Count && cardSlots[i].root != null && cardSlots[i].root.activeSelf)
                cardSlots[i].root.transform.localScale = Vector3.zero;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i >= currentItems.Count || cardSlots[i].root == null || !cardSlots[i].root.activeSelf) continue;
            yield return StartCoroutine(CardPopIn(cardSlots[i].root.transform));
            if (i < cardSlots.Count - 1) yield return new WaitForSecondsRealtime(0.12f);
        }

        for (int i = 0; i < cardSlots.Count; i++)
            if (i < currentItems.Count && cardSlots[i].root != null && cardSlots[i].root.activeSelf)
                StartCoroutine(CardIdleBounce(cardSlots[i].root.transform, i));
    }

    private IEnumerator ShopCloseAnimation()
    {
        if (ScreenFader.instance != null && ScreenFader.instance.faderGroup != null)
        {
            CanvasGroup fader = ScreenFader.instance.faderGroup;
            fader.blocksRaycasts = true;
            float fadeDur = 0.2f;
            float fadeElapsed = 0f;
            float startAlpha = fader.alpha;
            while (fadeElapsed < fadeDur)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                fader.alpha = Mathf.Lerp(startAlpha, 1f, fadeElapsed / fadeDur);
                yield return null;
            }
            fader.alpha = 1f;
        }

        CloseShop();

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    private IEnumerator CardPopIn(Transform card)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayCard();
        float dur = 0.25f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float s = Mathf.Lerp(0f, 1.08f, 1f - (1f - t) * (1f - t));
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        dur = 0.1f; elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(1.08f, 1f, Mathf.Clamp01(elapsed / dur));
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private IEnumerator CardIdleBounce(Transform card, int cardIdx)
    {
        while (card != null && card.gameObject.activeSelf && isShopOpen)
        {
            float target = (hoveredCardIndex == cardIdx) ? 1.08f : 1f;
            float s = Mathf.MoveTowards(card.localScale.x, target, Time.unscaledDeltaTime * 3f);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════
    // LEGACY HOOKS
    // ═══════════════════════════════════════════

    public void OnDungeonCleared()
    {
        RefreshCoinDisplay();
        if (MapManager.instance != null) { MapManager.instance.OnNodeComplete(); return; }
        if (LevelUpManager.instance != null) LevelUpManager.instance.ShowLevelUpScreen();
        else { RunManager.instance.currentLevel++; LevelGenerator.instance.GenerateNextLevel(); }
    }

    public void OnBossCleared()
    {
        rerollCount = 0;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);
        RefreshCoinDisplay();
        if (MapManager.instance != null) { MapManager.instance.OnNodeComplete(); return; }
        if (LevelUpManager.instance != null) LevelUpManager.instance.ShowLevelUpScreen();
        else { RunManager.instance.currentLevel++; LevelGenerator.instance.GenerateNextLevel(); }
    }

    // ═══════════════════════════════════════════
    // REROLL
    // ═══════════════════════════════════════════

    public void TryReroll()
    {
        if (RunManager.instance == null) return;
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        if (RunManager.instance.currentGold < currentRerollCost)
        {
            StartCoroutine(FlashText(goldTextRef));
            return;
        }

        RunManager.instance.currentGold -= currentRerollCost;
        rerollCount++;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost * Mathf.Pow(rerollMultiplier, rerollCount));
        RunManager.instance.shopRerollStack++;

        GenerateShopItems();
        RefreshCoinDisplay();
        GameEvents.GoldChanged(RunManager.instance.currentGold);

        foreach (var perk in RunManager.instance.activePerks)
            if (perk != null) perk.OnShopReroll();

        hoveredCardIndex = -1;
        StopAllCoroutines();
        StartCoroutine(ShopOpenAnimation());
    }

    // ═══════════════════════════════════════════
    // ITEM GENERATION
    // ═══════════════════════════════════════════

    public void GenerateShopItems()
    {
        foreach (var item in currentItems)
            if (item != null) shownItemNames.Add(item.itemName);

        currentItems.Clear();
        purchased.Clear();
        if (itemPool.Count == 0) return;

        int secretSlotIndex = -1;
        bool guaranteeSecret = (RunManager.instance != null && RunManager.instance.currentLevel >= 6
                                && !hasBoughtSecretItem && rerollCount == 0);
        if (secretItem != null && !hasBoughtSecretItem && (guaranteeSecret || Random.value < secretItemChance))
            secretSlotIndex = Random.Range(0, shopSlotCount);

        int availableCount = 0;
        foreach (var item in itemPool)
            if (!shownItemNames.Contains(item.itemName)) availableCount++;
        if (availableCount < shopSlotCount)
            shownItemNames.Clear();

        List<int> usedIndices = new List<int>();
        for (int i = 0; i < shopSlotCount; i++)
        {
            BaseItem selectedItem = null;
            if (i == secretSlotIndex)
            {
                selectedItem = secretItem;
            }
            else
            {
                if (itemPool.Count <= usedIndices.Count) break;
                int poolIdx, safety = 0;
                do
                {
                    poolIdx = Random.Range(0, itemPool.Count);
                    selectedItem = itemPool[poolIdx];
                    if (++safety > 100) break;
                }
                while (usedIndices.Contains(poolIdx) ||
                       (selectedItem != null && shownItemNames.Contains(selectedItem.itemName)) ||
                       (selectedItem != null && secretItem != null && selectedItem.itemName == secretItem.itemName) ||
                       (RunManager.instance != null && RunManager.instance.hasPerkReroll && selectedItem is MutationCatalyst));
                usedIndices.Add(poolIdx);
            }
            if (selectedItem == null) continue;
            currentItems.Add(selectedItem);
            purchased.Add(false);
        }

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i < currentItems.Count)
            {
                PopulateCard(cardSlots[i], currentItems[i]);
                if (cardSlots[i].root != null) cardSlots[i].root.SetActive(true);
                if (cardSlots[i].soldOutOverlay != null) cardSlots[i].soldOutOverlay.SetActive(false);
                if (cardSlots[i].button != null) cardSlots[i].button.interactable = true;
            }
            else
            {
                if (cardSlots[i].root != null) cardSlots[i].root.SetActive(false);
            }
        }
        RefreshCoinDisplay();
    }

    private void PopulateCard(ShopCardSlot card, BaseItem item)
    {
        if (card.nameText != null) card.nameText.text = item.itemName.ToUpper();
        if (card.iconImage != null)
        {
            if (item.icon != null) { card.iconImage.sprite = item.icon; card.iconImage.color = Color.white; card.iconImage.enabled = true; }
            else { card.iconImage.sprite = null; card.iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
        }
        if (card.descriptionText != null) card.descriptionText.text = item.description;
        if (card.priceText != null) card.priceText.text = item.price.ToString();
    }

    // ═══════════════════════════════════════════
    // PURCHASING
    // ═══════════════════════════════════════════

    public void TryBuy(int index)
    {
        if (index >= purchased.Count || purchased[index]) return;
        if (RunManager.instance == null) return;
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        BaseItem item = currentItems[index];
        if (item == null) return;

        if (item.itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
        { StartCoroutine(FlashText(goldTextRef)); return; }

        if (RunManager.instance.currentGold < item.price)
        { StartCoroutine(FlashText(goldTextRef)); return; }

        RunManager.instance.currentGold -= item.price;
        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        if (item.itemType == ItemType.Instant)
        {
            if (!item.Use()) { RunManager.instance.currentGold += item.price; RefreshCoinDisplay(); return; }
        }
        else
        {
            GameEvents.ItemPurchased(item);
        }

        if (secretItem != null && item.itemName == secretItem.itemName)
            hasBoughtSecretItem = true;

        purchased[index] = true;
        if (index < cardSlots.Count)
        {
            if (cardSlots[index].button != null) cardSlots[index].button.interactable = false;
            if (cardSlots[index].soldOutOverlay != null) cardSlots[index].soldOutOverlay.SetActive(true);
        }

        RefreshCoinDisplay();
        GameEvents.GoldChanged(RunManager.instance.currentGold);
    }

    // ═══════════════════════════════════════════
    // UI REFRESH
    // ═══════════════════════════════════════════

    public void RefreshCoinDisplay()
    {
        if (goldTextRef != null && RunManager.instance != null)
            goldTextRef.text = RunManager.instance.currentGold.ToString();
        RefreshRerollButton();
        RefreshAffordability();
    }

    public void RefreshAffordability()
    {
        if (RunManager.instance == null) return;
        for (int i = 0; i < cardSlots.Count && i < currentItems.Count; i++)
        {
            if (purchased.Count > i && purchased[i]) continue;
            if (cardSlots[i].button == null) continue;
            bool canAfford = currentItems[i] != null && RunManager.instance.currentGold >= currentItems[i].price;
            if (canAfford && currentItems[i].itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
                canAfford = false;
            cardSlots[i].button.interactable = canAfford;
        }
    }

    private void RefreshRerollButton()
    {
        if (rerollPriceTextRef != null)
            rerollPriceTextRef.text = "REROLL  <color=#FFD933>" + currentRerollCost + "</color>";
        if (rerollButtonRef != null && RunManager.instance != null)
            rerollButtonRef.interactable = RunManager.instance.currentGold >= currentRerollCost;
    }

    // ═══════════════════════════════════════════
    // EXTRA SLOT (Perk-driven)
    // ═══════════════════════════════════════════

    public void AddSingleExtraSlot()
    {
        if (itemPool.Count == 0) return;
        List<int> usedIndices = new List<int>();
        foreach (var ci in currentItems)
            if (ci != null) { int p = itemPool.IndexOf(ci); if (p != -1) usedIndices.Add(p); }
        if (itemPool.Count <= usedIndices.Count) return;

        int idx, safety = 0;
        do { idx = Random.Range(0, itemPool.Count); if (++safety > 100) break; }
        while (usedIndices.Contains(idx));

        BaseItem newItem = itemPool[idx];
        currentItems.Add(newItem);
        purchased.Add(false);

        if (cardSlots.Count > currentItems.Count - 1)
        {
            int ni = currentItems.Count - 1;
            PopulateCard(cardSlots[ni], newItem);
            if (cardSlots[ni].root != null) cardSlots[ni].root.SetActive(true);
            if (cardSlots[ni].soldOutOverlay != null) cardSlots[ni].soldOutOverlay.SetActive(false);
            if (cardSlots[ni].button != null) cardSlots[ni].button.interactable = true;
        }
        RefreshAffordability();
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    /// <summary>
    /// Image.color'ı white yapar, asıl renkleri ColorBlock'a taşır.
    /// Böylece Unity Color Tint (white × ColorBlock) düzgün çalışır.
    /// </summary>
    private void MakeColorTintWork(Button btn)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img == null) return;

        // Image rengine DOKUNMA — ColorBlock çarpan olarak çalışır
        // normal: image × white = image (değişmez)
        // hover:  image × 2.5 = belirgin açık
        // press:  image × 0.5 = koyu
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(2.5f, 2f, 2.5f, 1f);
        cb.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        cb.fadeDuration = 0.1f;
        btn.colors = cb;
    }

    private IEnumerator FlashText(TMP_Text t)
    {
        if (t == null) yield break;
        Color orig = t.color;
        t.color = Color.red;
        yield return new WaitForSecondsRealtime(0.3f);
        t.color = orig;
    }
}

[System.Serializable]
public class ShopCardSlot
{
    public GameObject root;
    public Image background;
    public Button button;
    public TMP_Text nameText;
    public Image iconImage;
    public TMP_Text descriptionText;
    public TMP_Text priceText;
    public Image coinImage;
    public GameObject soldOutOverlay;
}
