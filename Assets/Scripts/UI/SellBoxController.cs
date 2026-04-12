using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// SellBox — perk veya item'a basılı tutulduğunda sağdan kayarak gelen satış kutusu.
/// Drag sırasında drop zone'a bırakılırsa satış gerçekleşir.
/// Fiyat = Ceil(buyPrice / 2).
/// PerkInventoryUI'daki drag sistemi ile entegre çalışır.
/// </summary>
public class SellBoxController : MonoBehaviour
{
    public static SellBoxController instance;

    [Header("Panel Referansları (Inspector'dan ata)")]
    public GameObject sellBoxPanel;
    public CanvasGroup panelCanvasGroup;
    public RectTransform panelRect;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI sellTitleText;

    [Header("Animasyon Ayarları")]
    public float slideDistance = 250f;   // sağdan ne kadar kayarak gelecek
    public float animDuration = 0.3f;    // açılma/kapanma süresi

    // State
    private bool isVisible;
    private Coroutine animCoroutine;
    private Vector2 finalPosition;   // Inspector'da ayarlanan hedef konum
    private Vector2 hiddenPosition;  // sağa kaydırılmış gizli konum

    // Şu an gösterilen satış bilgisi
    private int currentSellPrice;
    private BasePerk pendingPerk;
    private bool pendingPerkIsActive;
    private int pendingPerkIndex;
    private BaseItem pendingItem;
    private int pendingItemSlot;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        // Sprite'sız Image'lar render edilmez — built-in beyaz pixel ata
        if (sellBoxPanel != null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            Sprite whiteTex = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            foreach (var img in sellBoxPanel.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null)
                    img.sprite = whiteTex;
            }
        }

        // Inspector'daki mevcut pozisyon = final (görünür) konum
        if (panelRect != null)
        {
            finalPosition = panelRect.anchoredPosition;
            hiddenPosition = finalPosition + new Vector2(slideDistance, 0f);
            panelRect.anchoredPosition = hiddenPosition;
        }

        // Başlangıçta gizle
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }
        isVisible = false;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ════════════════════════════════════════════
    // PERK SATIŞ — PerkInventoryUI'dan çağrılır
    // ════════════════════════════════════════════

    /// <summary>Perk drag başladığında çağır — SellBox açılır, fiyat gösterilir.</summary>
    public void ShowForPerk(BasePerk perk, bool isActiveSlot, int index)
    {
        if (perk == null) return;

        int buyPrice = MergedShopPerkSlot.GetRarityPrice(perk.rarity);
        currentSellPrice = Mathf.CeilToInt(buyPrice / 2f);

        pendingPerk = perk;
        pendingPerkIsActive = isActiveSlot;
        pendingPerkIndex = index;
        pendingItem = null;

        UpdatePriceDisplay();
        SlideIn();
    }

    /// <summary>Item drag başladığında çağır.</summary>
    public void ShowForItem(BaseItem item, int slotIndex)
    {
        if (item == null) return;

        currentSellPrice = Mathf.CeilToInt(item.price / 2f);

        pendingItem = item;
        pendingItemSlot = slotIndex;
        pendingPerk = null;

        UpdatePriceDisplay();
        SlideIn();
    }

    /// <summary>Drag iptal edildiğinde veya bırakma SellBox dışına yapıldığında çağır.</summary>
    public void HideSellBox()
    {
        pendingPerk = null;
        pendingItem = null;
        SlideOut();
    }

    /// <summary>SellBox drop zone'una bırakıldığında çağır — satışı gerçekleştirir.</summary>
    public bool TrySell()
    {
        if (pendingPerk != null)
            return SellPerk();
        if (pendingItem != null)
            return SellItem();
        return false;
    }

    // ════════════════════════════════════════════
    // SATIŞ İŞLEMLERİ
    // ════════════════════════════════════════════

    private bool SellPerk()
    {
        if (pendingPerk == null || RunManager.instance == null) return false;

        var rm = RunManager.instance;

        // Perk'i listeden çıkar
        if (pendingPerkIsActive)
        {
            if (pendingPerkIndex >= rm.activePerks.Count) return false;
            BasePerk perk = rm.activePerks[pendingPerkIndex];
            if (!perk.CanUnequip()) return false;
            perk.OnUnequip();
            rm.activePerks.RemoveAt(pendingPerkIndex);
            Destroy(perk.gameObject);
        }
        else
        {
            if (pendingPerkIndex >= rm.inventoryPerks.Count) return false;
            BasePerk perk = rm.inventoryPerks[pendingPerkIndex];
            rm.inventoryPerks.RemoveAt(pendingPerkIndex);
            Destroy(perk.gameObject);
        }

        // Para ekle
        AddGold(currentSellPrice);

        // UI güncelle
        rm.RefreshPerkUI();

        pendingPerk = null;
        SlideOut();
        return true;
    }

    private bool SellItem()
    {
        if (pendingItem == null || InventoryManager.instance == null) return false;

        // Item'ı slottan çıkar
        InventoryManager.instance.RemoveItem(pendingItemSlot);

        // Para ekle
        AddGold(currentSellPrice);

        pendingItem = null;
        SlideOut();
        return true;
    }

    private void AddGold(int amount)
    {
        if (RunManager.instance == null) return;
        RunManager.instance.currentGold += amount;
        RunManager.instance.totalGoldEarned += amount;
        GameEvents.GoldChanged(RunManager.instance.currentGold);

        // Coin SFX
        if (AudioManager.instance != null)
            AudioManager.instance.PlayCoin();
    }

    // ════════════════════════════════════════════
    // UI GÜNCELLEME
    // ════════════════════════════════════════════

    private void UpdatePriceDisplay()
    {
        if (priceText != null)
            priceText.text = $"+{currentSellPrice}";
    }

    // ════════════════════════════════════════════
    // ANİMASYON — Sağdan kayma + fade
    // ════════════════════════════════════════════

    private void SlideIn()
    {
        if (isVisible) return;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateSlide(true));
    }

    private void SlideOut()
    {
        if (!isVisible) return;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateSlide(false));
    }

    private IEnumerator AnimateSlide(bool opening)
    {
        if (panelCanvasGroup == null || panelRect == null) yield break;

        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = opening ? 1f : 0f;
        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 targetPos = opening ? finalPosition : hiddenPosition;

        if (opening)
        {
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;
        }

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);

            if (opening)
            {
                // EaseOutCubic — fıyuv diye gelsin
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, ease);
                panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            }
            else
            {
                // EaseInQuad — kapanırken hızlanan
                float ease = t * t;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, ease);
                panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            }

            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        panelRect.anchoredPosition = targetPos;

        if (!opening)
        {
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }

        isVisible = opening;
        animCoroutine = null;
    }

    // ════════════════════════════════════════════
    // HOVER EFEKTI — mouse SellBox üstündeyken parla
    // ════════════════════════════════════════════

    /// <summary>Drop zone hover'da mı? PerkInventoryUI drag sırasında kontrol eder.</summary>
    public bool IsMouseOverSellBox()
    {
        if (!isVisible || panelRect == null) return false;

        // ScreenSpaceOverlay canvas'lar arası çalışması için
        // doğrudan screen-space bounds kontrolü yap
        Canvas rootCanvas = panelRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = rootCanvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, cam);
    }

    /// <summary>Şu anda görünür ve aktif mi?</summary>
    public bool IsActive => isVisible;
}
