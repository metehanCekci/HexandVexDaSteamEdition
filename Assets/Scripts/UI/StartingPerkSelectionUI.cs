using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class StartingPerkSelectionUI : MonoBehaviour
{
    public static StartingPerkSelectionUI instance;

    [Header("UI References")]
    public GameObject panel;
    public CanvasGroup canvasGroup;
    public TMP_Text titleText;
    public TMP_Text counterText;
    public Button confirmButton;
    public TMP_Text confirmButtonText;

    [Header("Perk Cards")]
    public List<StartingPerkCard> perkCards = new List<StartingPerkCard>();

    // State
    private List<GameObject> offeredPerks = new List<GameObject>();
    private HashSet<int> selectedIndices = new HashSet<int>();
    private const int MAX_SELECTION = 3;
    private const int OFFER_COUNT = 6;
    private bool isActive = false;
    private System.Action onComplete;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        if (panel != null) panel.SetActive(false);

        // Wire confirm button listener at runtime (Editor-added listeners don't persist)
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);

            // Add UIHoverEffect to confirm button
            if (confirmButton.GetComponent<UIHoverEffect>() == null)
                confirmButton.gameObject.AddComponent<UIHoverEffect>();
        }
    }

    /// <summary>
    /// Show the starting perk selection screen.
    /// Calls onComplete when the player confirms their 3 picks.
    /// </summary>
    public void Show(System.Action onComplete)
    {
        this.onComplete = onComplete;
        isActive = true;
        selectedIndices.Clear();

        Debug.Log($"[StartingPerkSelection] Show() called. panel={panel != null}, cards={perkCards.Count}");

        // Get 6 random common perks
        offeredPerks = GetRandomCommonPerks(OFFER_COUNT);

        Debug.Log($"[StartingPerkSelection] offeredPerks={offeredPerks.Count}");

        if (offeredPerks.Count == 0)
        {
            Debug.LogWarning("[StartingPerkSelection] No common perks found! Skipping selection.");
            isActive = false;
            onComplete?.Invoke();
            return;
        }

        // Populate cards
        for (int i = 0; i < perkCards.Count; i++)
        {
            if (i < offeredPerks.Count)
            {
                BasePerk bp = offeredPerks[i].GetComponent<BasePerk>();
                perkCards[i].Setup(bp, i, this);
                perkCards[i].gameObject.SetActive(true);
            }
            else
            {
                perkCards[i].gameObject.SetActive(false);
            }
        }

        UpdateUI();

        if (panel != null) panel.SetActive(true);
        if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; }

        // Show inventory panel so player can see their perks
        if (PerkInventoryUI.instance != null)
        {
            PerkInventoryUI.instance.Show();
            PerkInventoryUI.instance.RefreshUI();
        }
    }

    public void ToggleSelection(int index)
    {
        if (!isActive) return;

        if (selectedIndices.Contains(index))
        {
            selectedIndices.Remove(index);
            perkCards[index].SetSelected(false);
        }
        else
        {
            if (selectedIndices.Count >= MAX_SELECTION) return;
            selectedIndices.Add(index);
            perkCards[index].SetSelected(true);
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        int count = selectedIndices.Count;

        if (counterText != null)
            counterText.text = $"{count} / {MAX_SELECTION}";

        if (confirmButton != null)
            confirmButton.interactable = (count == MAX_SELECTION);

        if (confirmButtonText != null)
            confirmButtonText.color = (count == MAX_SELECTION) ? Color.white : new Color(0.35f, 0.35f, 0.35f);
    }

    public void OnConfirmClicked()
    {
        if (selectedIndices.Count != MAX_SELECTION) return;
        if (!isActive) return;
        isActive = false;

        StartCoroutine(ConfirmSequence());
    }

    private IEnumerator ConfirmSequence()
    {
        if (confirmButton != null) confirmButton.interactable = false;

        // Get the canvas RectTransform for fly icon positioning
        Canvas canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
        RectTransform canvasRT = canvas != null ? canvas.GetComponent<RectTransform>() : null;

        // Animate each selected perk flying out, one by one
        List<int> sortedIndices = new List<int>(selectedIndices);
        sortedIndices.Sort();

        foreach (int idx in sortedIndices)
        {
            if (idx >= perkCards.Count || idx >= offeredPerks.Count) continue;

            StartingPerkCard card = perkCards[idx];

            // Fade card while icon flies
            if (card.canvasGroup == null)
            {
                CanvasGroup cg = card.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
                card.canvasGroup = cg;
            }

            // Start fly animation
            if (canvasRT != null && card.iconImage != null)
                yield return StartCoroutine(FlyPerkIcon(card, canvasRT));

            // Grant perk
            if (idx < offeredPerks.Count && RunManager.instance != null)
            {
                RunManager.instance.AddPerk(offeredPerks[idx]);

                // Refresh inventory so the new perk appears immediately
                if (PerkInventoryUI.instance != null)
                    PerkInventoryUI.instance.RefreshUI();
            }

            // Fade card out after granting
            if (card.canvasGroup != null)
                card.canvasGroup.alpha = 0.3f;

            yield return new WaitForSeconds(0.15f);
        }

        Debug.Log($"[StartingPerkSelection] Granted {selectedIndices.Count} starting perks");

        // Fade out entire panel
        float fadeDur = 0.35f;
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDur)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - (fadeElapsed / fadeDur);
            fadeElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Hide
        if (panel != null) panel.SetActive(false);
        if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = false; }

        onComplete?.Invoke();
        onComplete = null;
    }

    private IEnumerator FlyPerkIcon(StartingPerkCard card, RectTransform canvasRT)
    {
        // Create ghost icon
        GameObject flyGO = new GameObject("FlyPerkIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flyGO.transform.SetParent(canvasRT, false);
        flyGO.transform.SetAsLastSibling();

        RectTransform flyRT = flyGO.GetComponent<RectTransform>();
        flyRT.sizeDelta = new Vector2(80, 80);

        Image flyImg = flyGO.GetComponent<Image>();
        if (card.iconImage != null && card.iconImage.sprite != null)
        {
            flyImg.sprite = card.iconImage.sprite;
            flyImg.color = Color.white;
        }
        else
        {
            flyImg.color = new Color(0.35f, 0.35f, 0.4f, 0.8f);
        }
        flyImg.raycastTarget = false;
        flyImg.preserveAspect = true;

        // Rarity outline on ghost
        Outline outline = flyGO.AddComponent<Outline>();
        Color rarCol = card.nameText != null ? card.nameText.color : Color.white;
        outline.effectColor = rarCol;
        outline.effectDistance = new Vector2(2f, 2f);

        // Start: card icon world position → canvas local
        Vector3 startLocal = canvasRT.InverseTransformPoint(card.iconImage.transform.position);

        // End: top-right of canvas (where PerkInventoryUI panel lives)
        // PerkInventoryUI panel is anchored at (1,1) with offset (-10,-10), width ~280
        Vector3 endLocal;
        if (PerkInventoryUI.instance != null && PerkInventoryUI.instance.panelRoot != null)
        {
            endLocal = canvasRT.InverseTransformPoint(PerkInventoryUI.instance.panelRoot.transform.position);
        }
        else
        {
            endLocal = new Vector3(canvasRT.rect.width / 2f - 150f, canvasRT.rect.height / 2f - 40f, 0);
        }

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (flyGO == null) yield break;
            float t = elapsed / duration;
            float ease = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic

            flyRT.localPosition = Vector3.Lerp(startLocal, endLocal, ease);

            // Scale: shrink as it flies
            float scale = Mathf.Lerp(1.2f, 0.5f, ease);
            flyRT.localScale = Vector3.one * scale;

            // Fade out in last 30%
            if (t > 0.7f)
            {
                float alpha = 1f - ((t - 0.7f) / 0.3f);
                flyImg.color = new Color(flyImg.color.r, flyImg.color.g, flyImg.color.b, alpha);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (flyGO != null) Object.Destroy(flyGO);
    }

    private List<GameObject> GetRandomCommonPerks(int count)
    {
        List<GameObject> source = null;

        Debug.Log($"[StartingPerkSelection] GetRandomCommonPerks: MergedShop={MergedShopManager.instance != null}, LevelUp={LevelUpManager.instance != null}");

        // Try MergedShopManager first (most reliable at this point)
        if (MergedShopManager.instance != null && MergedShopManager.instance.commonPerks != null && MergedShopManager.instance.commonPerks.Count > 0)
        {
            source = MergedShopManager.instance.commonPerks;
            Debug.Log($"[StartingPerkSelection] Source: MergedShopManager.commonPerks = {source.Count}");
        }
        else if (LevelUpManager.instance != null && LevelUpManager.instance.commonPerks != null && LevelUpManager.instance.commonPerks.Count > 0)
        {
            source = LevelUpManager.instance.commonPerks;
            Debug.Log($"[StartingPerkSelection] Source: LevelUpManager.commonPerks = {source.Count}");
        }

        // Fallback: scan all BasePerk prefabs
        if (source == null || source.Count == 0)
        {
            Debug.Log("[StartingPerkSelection] Primary sources empty, scanning all BasePerk prefabs...");
            source = new List<GameObject>();
            var allPerks = Resources.FindObjectsOfTypeAll<BasePerk>();
            Debug.Log($"[StartingPerkSelection] FindObjectsOfTypeAll<BasePerk> found {allPerks.Length}");
            foreach (var perk in allPerks)
            {
                if (perk == null) continue;
                if (perk.gameObject.scene.name != null) continue; // skip scene instances
                if (perk.rarity == PerkRarity.Common)
                    source.Add(perk.gameObject);
            }
            Debug.Log($"[StartingPerkSelection] Fallback common perks found: {source.Count}");
        }

        if (source == null || source.Count == 0) return new List<GameObject>();

        // Shuffle and pick 'count'
        List<GameObject> shuffled = new List<GameObject>(source);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = tmp;
        }

        List<GameObject> result = new List<GameObject>();
        for (int i = 0; i < Mathf.Min(count, shuffled.Count); i++)
            result.Add(shuffled[i]);

        return result;
    }
}
