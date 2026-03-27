using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager instance;

    public GameObject levelUpPanel;

    [Header("Perk Listeleri")]
    public List<GameObject> commonPerks;
    public List<GameObject> rarePerks;
    public List<GameObject> epicPerks;
    public List<GameObject> legendaryPerks;

    [Header("UI Elemanları (3 Buton)")]
    public Button[] choiceButtons;

    public TMP_Text[] choiceTitleTexts;
    public TMP_Text[] choiceLevelTexts;
    public TMP_Text[] choiceDescriptionTexts;
    public TMP_Text[] choiceRarityTexts;

    public Image[] choiceIcons;

    [Header("Reroll Butonu")]
    public Button rerollPerkButton; // Inspector'dan bağla

    [Header("Skip Butonu")]
    public Button skipButton; // Inspector'dan bağla
    public Image skipButtonImage; // Inspector'dan bağla
    private Color skipButtonOriginalColor;
    private ColorBlock skipButtonOriginalColorBlock;

    private List<GameObject> currentChoices = new List<GameObject>();

    [Header("Animasyon Ayarları")]
    public CanvasGroup levelUpCanvasGroup;

    [Header("Debug")]
    [HideInInspector] public GameObject forcedPerk;
    [HideInInspector] public GameObject forcedPerk2;
    [HideInInspector] public GameObject forcedPerk3;

    private int hoveredCardIndex = -1;
    private bool[] cardAnimDone;
    private GameObject perkBlackBackground;

    void Awake()
    {
        if (instance == null) instance = this;
        SetupCardHoverListeners();

        // Skip butonunun orijinal rengini ve color block'ı kaydet
        if (skipButtonImage != null)
            skipButtonOriginalColor = skipButtonImage.color;
        if (skipButton != null)
            skipButtonOriginalColorBlock = skipButton.colors;
    }

    void OnDestroy()
    {
        // Sahne değişirken perk panelini temizle
        ForceClose();
        if (instance == this) instance = null;
    }

    public void ShowLevelUpScreen()
    {
        // Tüm perkler max seviyeye ulaştıysa perk seçme ekranını atla
        if (AreAllPerksMaxed())
        {
            Debug.Log("Tüm perkler max seviyede — perk seçme ekranı atlanıyor.");
            SkipLevelUpScreen();
            return;
        }

        ShowBlackBackground(true);
        levelUpPanel.SetActive(true);
        if (levelUpCanvasGroup != null) levelUpCanvasGroup.gameObject.SetActive(true);

        // Perk stash panelini göster (yoksa oluştur)
        if (PerkInventoryUI.instance == null)
            PerkInventoryUI.CreateFromCode();
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Show();

        currentChoices.Clear();

        // Boss node'unda legendary garanti, eski % 5 mantığı kaldırıldı
        bool isBossReward = RunManager.instance.currentNodeType == MapNodeType.Boss;

        for (int i = 0; i < 3; i++)
        {
            GameObject randomPerk = null;
            int safetyBreak = 0;

            if (i == 0 && forcedPerk != null && forcedPerk != null)
            {
                randomPerk = forcedPerk;
            }
            else if (i == 1 && forcedPerk2 != null && forcedPerk2 != forcedPerk)
            {
                randomPerk = forcedPerk2;
            }
            else if (i == 2 && forcedPerk3 != null && forcedPerk3 != forcedPerk && forcedPerk3 != forcedPerk2)
            {
                randomPerk = forcedPerk3;
            }

            bool isForced = (i == 0 && forcedPerk != null && randomPerk == forcedPerk)
                          || (i == 1 && forcedPerk2 != null && randomPerk == forcedPerk2)
                          || (i == 2 && forcedPerk3 != null && randomPerk == forcedPerk3);

            if (!isForced)
            {
                while (randomPerk == null || currentChoices.Contains(randomPerk) || IsPerkMaxedOut(randomPerk))
                {
                    randomPerk = GetRandomPerkByRarity(isBossReward);
                    safetyBreak++;
                    if (safetyBreak > 50)
                    {
                        randomPerk = GetAnyValidFallback();
                        break;
                    }
                }
            }

            if (randomPerk != null)
            {
                currentChoices.Add(randomPerk);
                BasePerk perkScript = randomPerk.GetComponent<BasePerk>();

                BasePerk existing = RunManager.instance.activePerks.Find(p => p.GetType() == perkScript.GetType());
                if (existing == null)
                    existing = RunManager.instance.inventoryPerks.Find(p => p.GetType() == perkScript.GetType());
                int displayLevel = (existing != null) ? existing.currentLevel + 1 : 1;

                // ==========================================
                // YENİ: Yazıları kendi özel Text'lerine aktarıyoruz
                // Rarity'e göre isim ve açıklama renklendirmesi
                // ==========================================
                PerkRarity detectedRarity = GetRarityFromList(randomPerk);
                Color rarityColor = GetRarityColor(detectedRarity);

                if (choiceTitleTexts.Length > i && choiceTitleTexts[i] != null)
                {
                    choiceTitleTexts[i].text = perkScript.perkName;
                    choiceTitleTexts[i].color = rarityColor;
                }

                if (choiceLevelTexts.Length > i && choiceLevelTexts[i] != null)
                {
                    int fromLevel = (existing != null) ? existing.currentLevel : 0;
                    choiceLevelTexts[i].text = $"Lv {fromLevel} <color=#00FF00>→ Lv {displayLevel}</color>";
                }

                if (choiceDescriptionTexts.Length > i && choiceDescriptionTexts[i] != null)
                {
                    choiceDescriptionTexts[i].text = perkScript.description;
                    choiceDescriptionTexts[i].color = Color.Lerp(rarityColor, new Color(0.85f, 0.85f, 0.85f, 1f), 0.4f);
                }

                if (choiceRarityTexts != null && choiceRarityTexts.Length > i && choiceRarityTexts[i] != null)
                {
                    choiceRarityTexts[i].text = detectedRarity.ToString().ToUpperInvariant();
                    choiceRarityTexts[i].color = rarityColor;
                }

                if (choiceIcons != null && choiceIcons.Length > i && choiceIcons[i] != null)
                {
                    if (perkScript.icon != null)
                    {
                        choiceIcons[i].sprite = perkScript.icon;
                        choiceIcons[i].color = Color.white;
                    }
                    else
                    {
                        choiceIcons[i].sprite = null;
                        choiceIcons[i].color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    }
                }

                int index = i;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => SelectPerk(index));
                choiceButtons[i].gameObject.SetActive(true);
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }

        // Reroll butonu
        if (rerollPerkButton != null)
        {
            rerollPerkButton.gameObject.SetActive(RunManager.instance.hasPerkReroll);
            rerollPerkButton.onClick.RemoveAllListeners();
            rerollPerkButton.onClick.AddListener(RerollPerkChoices);

            // Navigation'ı kapat — yakın butonlara hover sızmasını engelle
            rerollPerkButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // Skip butonunu deaktif yap ve rengini orijinaline sıfırla
        if (skipButton != null)
        {
            skipButton.interactable = false;
            skipButton.colors = skipButtonOriginalColorBlock;
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        if (skipButtonImage != null)
            skipButtonImage.color = skipButtonOriginalColor;

        Time.timeScale = 0f;
        StopAllCoroutines();
        StartCoroutine(FadeInAndPopRoutine());
    }

    public void RerollPerkChoices()
    {
        if (RunManager.instance == null || !RunManager.instance.hasPerkReroll) return;
        RunManager.instance.hasPerkReroll = false;
        if (rerollPerkButton != null) rerollPerkButton.gameObject.SetActive(false);

        // Mevcut seçimleri temizleyip yeniden göster
        currentChoices.Clear();
        StopAllCoroutines();
        ShowLevelUpScreen();
    }

    /// <summary>
    /// Lucky Clover item perk ekranında kullanıldığında çağrılır.
    /// Eşit rarity şanslarıyla yeniden roll atar.
    /// </summary>
    public void LuckyCloverReroll()
    {
        currentChoices.Clear();
        StopAllCoroutines();
        ShowLevelUpScreen();
    }

    private bool IsPerkMaxedOut(GameObject perkPrefab)
    {
        if (perkPrefab == null || RunManager.instance == null) return true;

        // Koleksiyonda kilitli perkler havuzda görünmez
        if (!PerkCollectionManager.IsUnlocked(perkPrefab)) return true;

        BasePerk checkPerk = perkPrefab.GetComponent<BasePerk>();

        // CanBeOffered kontrolu -- kosullu perkler (GeneSplice vb.)
        if (!checkPerk.CanBeOffered()) return true;

        // Hem activePerks hem inventoryPerks'te kontrol et
        BasePerk existing = RunManager.instance.activePerks.Find(p => p.GetType() == checkPerk.GetType());
        if (existing == null)
            existing = RunManager.instance.inventoryPerks.Find(p => p.GetType() == checkPerk.GetType());

        if (existing != null)
        {
            return true;
        }
        return false;
    }

    public GameObject GetRandomPerkByRarity(bool isBossReward)
    {
        // Boss reward → legendary garanti
        if (isBossReward && legendaryPerks.Count > 0)
            return legendaryPerks[Random.Range(0, legendaryPerks.Count)];

        // Lucky Clover item aktifse: tüm rarityler eşit şans (%25)
        if (RunManager.instance != null && RunManager.instance.hasLuckyClover)
        {
            float eqRoll = Random.Range(0f, 100f);
            if (eqRoll < 25f && legendaryPerks.Count > 0)
                return legendaryPerks[Random.Range(0, legendaryPerks.Count)];
            if (eqRoll < 50f && epicPerks.Count > 0)
                return epicPerks[Random.Range(0, epicPerks.Count)];
            if (eqRoll < 75f && rarePerks.Count > 0)
                return rarePerks[Random.Range(0, rarePerks.Count)];
            if (commonPerks.Count > 0)
                return commonPerks[Random.Range(0, commonPerks.Count)];
            return null;
        }

        // Yüzdelik sistem: Legendary dahil
        // Level ilerledikçe legendary şansı artar
        int level = RunManager.instance != null ? RunManager.instance.currentLevel : 1;
        int cloverLv = RunManager.instance != null ? RunManager.instance.luckyCloverLevel : 0;

        // Legendary: her bölümde çıkabilir, level ilerledikçe şans artar
        //   Base: %4 (lv1-7), %6 (lv8-15), %8 (lv16+)
        //   Clover bonus: +2% per level
        float legendaryChance = 4f;
        if (level >= 16) legendaryChance = 8f;
        else if (level >= 8) legendaryChance = 6f;
        legendaryChance += cloverLv * 2f;

        // Epic / Rare / Common (legendary yüzdesi düşüldükten sonra kalan)
        //   Clover Lv0: Epic %10 / Rare %30 / Common %kalan
        //   Clover Lv1: Epic %17 / Rare %33 / Common %kalan
        //   Clover Lv2: Epic %25 / Rare %33 / Common %kalan
        //   Clover Lv3: Epic %33 / Rare %33 / Common %kalan
        float epicBase = cloverLv == 0 ? 10f : cloverLv == 1 ? 17f : cloverLv == 2 ? 25f : 33f;
        float rareBase = cloverLv == 0 ? 30f : cloverLv == 1 ? 33f : cloverLv == 2 ? 33f : 33f;

        float roll = Random.Range(0f, 100f);
        float cursor = 0f;

        // Legendary
        cursor += legendaryChance;
        if (roll < cursor && legendaryPerks.Count > 0)
            return legendaryPerks[Random.Range(0, legendaryPerks.Count)];

        // Epic
        cursor += epicBase;
        if (roll < cursor && epicPerks.Count > 0)
            return epicPerks[Random.Range(0, epicPerks.Count)];

        // Rare
        cursor += rareBase;
        if (roll < cursor && rarePerks.Count > 0)
            return rarePerks[Random.Range(0, rarePerks.Count)];

        // Common (kalan)
        if (commonPerks.Count > 0)
            return commonPerks[Random.Range(0, commonPerks.Count)];
        return null;
    }

    private PerkRarity GetRarityFromList(GameObject perk)
    {
        if (legendaryPerks.Contains(perk)) return PerkRarity.Legendary;
        if (epicPerks.Contains(perk)) return PerkRarity.Epic;
        if (rarePerks.Contains(perk)) return PerkRarity.Rare;
        // Secret perkler normal havuzda olmaz ama güvenlik için kontrol
        BasePerk bp = perk.GetComponent<BasePerk>();
        if (bp != null && bp.rarity == PerkRarity.Secret) return PerkRarity.Secret;
        return PerkRarity.Common;
    }

    private Color GetRarityColor(PerkRarity rarity)
    {
        switch (rarity)
        {
            case PerkRarity.Common: return new Color(0.8f, 0.8f, 0.8f); // Gri
            case PerkRarity.Rare: return new Color(0.2f, 0.5f, 1f);   // Mavi
            case PerkRarity.Epic: return new Color(0.6f, 0.2f, 1f);   // Mor
            case PerkRarity.Legendary: return new Color(1f, 0.6f, 0f);     // Turuncu/Altın
            case PerkRarity.Secret: return new Color(1f, 0.27f, 0.27f); // Kırmızı
            default: return Color.white;
        }
    }

    private GameObject GetAnyValidFallback()
    {
        List<GameObject> allAvailable = new List<GameObject>();
        allAvailable.AddRange(legendaryPerks.Where(p => !currentChoices.Contains(p) && !IsPerkMaxedOut(p)));
        allAvailable.AddRange(epicPerks.Where(p => !currentChoices.Contains(p) && !IsPerkMaxedOut(p)));
        allAvailable.AddRange(rarePerks.Where(p => !currentChoices.Contains(p) && !IsPerkMaxedOut(p)));
        allAvailable.AddRange(commonPerks.Where(p => !currentChoices.Contains(p) && !IsPerkMaxedOut(p)));

        if (allAvailable.Count > 0) return allAvailable[Random.Range(0, allAvailable.Count)];
        return null;
    }

    /// <summary>Tüm perkler max seviyeye ulaştıysa true döner.</summary>
    public bool AreAllPerksMaxed()
    {
        foreach (var p in commonPerks) if (!IsPerkMaxedOut(p)) return false;
        foreach (var p in rarePerks) if (!IsPerkMaxedOut(p)) return false;
        foreach (var p in epicPerks) if (!IsPerkMaxedOut(p)) return false;
        foreach (var p in legendaryPerks) if (!IsPerkMaxedOut(p)) return false;
        return true;
    }

    public void SelectPerk(int index)
    {
        foreach (var btn in choiceButtons) btn.interactable = false;

        GameObject chosenPerk = currentChoices[index];
        List<BasePerk> existingPerks = new List<BasePerk>(RunManager.instance.activePerks);

        // Fly animasyonu bilgilerini AddPerk'ten ÖNCE kaydet (prefab'dan icon al)
        BasePerk prefabScript = chosenPerk.GetComponent<BasePerk>();
        GameObject flySourceIcon = (choiceIcons != null && index < choiceIcons.Length && choiceIcons[index] != null)
            ? choiceIcons[index].gameObject : null;

        RunManager.instance.AddPerk(chosenPerk);

        // Lucky Clover etkisini tüket (bir perk seçimi boyunca geçerli)
        if (RunManager.instance.hasLuckyClover)
            RunManager.instance.hasLuckyClover = false;

        // Map sistemi aktif değilse eski davranış (level++)
        if (MapManager.instance == null)
            RunManager.instance.currentLevel++;

        BasePerk activeInstance = RunManager.instance.activePerks.Find(p => p.GetType() == prefabScript.GetType());
        if (activeInstance == null)
            activeInstance = RunManager.instance.inventoryPerks.Find(p => p.GetType() == prefabScript.GetType());

        if (activeInstance != null && activeInstance.currentLevel >= activeInstance.maxLevel)
        {
            if (commonPerks.Contains(chosenPerk)) commonPerks.Remove(chosenPerk);
            if (rarePerks.Contains(chosenPerk)) rarePerks.Remove(chosenPerk);
            if (epicPerks.Contains(chosenPerk)) epicPerks.Remove(chosenPerk);
            if (legendaryPerks.Contains(chosenPerk)) legendaryPerks.Remove(chosenPerk);
            Debug.Log($"🔥 {activeInstance.perkName} Max Seviyeye ulaştı! Havuzdan kalıcı olarak silindi.");
            if (forcedPerk == chosenPerk) forcedPerk = null;
            if (forcedPerk2 == chosenPerk) forcedPerk2 = null;
            if (forcedPerk3 == chosenPerk) forcedPerk3 = null;
        }

        foreach (var perk in existingPerks)
            if (perk != null) perk.OnLevelStart();

        // Kartın ikonundan perk inventory'ye uçuş animasyonu — AddPerk sonrası RefreshUI
        // zaten TryStartFlyAnim'i çağıracağı için burada sadece tetikle
        if (PerkInventoryUI.instance != null && activeInstance != null && flySourceIcon != null)
            PerkInventoryUI.instance.BeginFlyAnimExternal(activeInstance, flySourceIcon);

        // RefreshUI'ı tekrar çağır ki TryStartFlyAnim tetiklensin (fly bilgileri artık set)
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.RefreshUI();

        StartCoroutine(FadeOutAndShrinkRoutine(index));
    }

    private IEnumerator FadeInAndPopRoutine()
    {
        // Panel fade in
        if (levelUpCanvasGroup != null) levelUpCanvasGroup.alpha = 0f;
        levelUpPanel.transform.localScale = Vector3.one;

        // Hide all cards initially
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null && choiceButtons[i].gameObject.activeSelf)
                choiceButtons[i].transform.localScale = Vector3.zero;
        }

        // Fade in the panel background
        float fadeDur = 0.2f;
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            while (PauseManager.isPaused) yield return null;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeDur;
            if (levelUpCanvasGroup != null) levelUpCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }
        if (levelUpCanvasGroup != null) levelUpCanvasGroup.alpha = 1f;

        // Spawn cards one by one with delay
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null || !choiceButtons[i].gameObject.activeSelf) continue;
            yield return StartCoroutine(CardPopIn(choiceButtons[i].transform));
            if (i < choiceButtons.Length - 1)
            {
                float delay = 0f;
                while (delay < 0.15f)
                {
                    while (PauseManager.isPaused) yield return null;
                    delay += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        // Mark all cards as animation-done
        cardAnimDone = new bool[choiceButtons.Length];
        for (int i = 0; i < cardAnimDone.Length; i++) cardAnimDone[i] = true;

        // Start idle bounce on all visible cards
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null && choiceButtons[i].gameObject.activeSelf)
                StartCoroutine(CardIdleBounce(choiceButtons[i].transform));
        }
    }

    private IEnumerator CardPopIn(Transform card)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayCard();
        float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            while (PauseManager.isPaused) yield return null;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float ease = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(0f, 1.08f, ease);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        dur = 0.1f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            while (PauseManager.isPaused) yield return null;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float s = Mathf.Lerp(1.08f, 1f, t);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private IEnumerator CardIdleBounce(Transform card)
    {
        int cardIdx = -1;
        for (int i = 0; i < choiceButtons.Length; i++)
            if (choiceButtons[i] != null && choiceButtons[i].transform == card) { cardIdx = i; break; }

        while (card != null && card.gameObject.activeSelf && levelUpPanel.activeSelf)
        {
            if (PauseManager.isPaused) { yield return null; continue; }
            float target = (hoveredCardIndex == cardIdx) ? 1.1f : 1f;
            float cur = card.localScale.x;
            float s = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * 3f);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    private void SetupCardHoverListeners()
    {
        if (choiceButtons == null) return;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;
            EventTrigger trigger = choiceButtons[i].GetComponent<EventTrigger>();
            if (trigger == null) trigger = choiceButtons[i].gameObject.AddComponent<EventTrigger>();

            int idx = i;

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((_) => { hoveredCardIndex = idx; });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((_) => { if (hoveredCardIndex == idx) hoveredCardIndex = -1; });
            trigger.triggers.Add(exitEntry);
        }
    }

    private IEnumerator FadeOutAndShrinkRoutine(int chosenIndex = -1)
    {
        float duration = 0.35f;
        float elapsed = 0f;

        // Başlangıç scale'lerini kaydet
        Vector3[] startScales = new Vector3[choiceButtons.Length];
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                startScales[i] = choiceButtons[i].transform.localScale;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float easeIn = t * t;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null || !choiceButtons[i].gameObject.activeSelf) continue;

                CanvasGroup cardCG = choiceButtons[i].GetComponent<CanvasGroup>();
                if (cardCG == null) cardCG = choiceButtons[i].gameObject.AddComponent<CanvasGroup>();

                if (i == chosenIndex)
                {
                    // Seçilen kart: sadece fade out
                    cardCG.alpha = Mathf.Lerp(1f, 0f, t);
                }
                else
                {
                    // Diğerleri: küçülerek + fade out
                    float scale = Mathf.Lerp(1f, 0f, easeIn);
                    choiceButtons[i].transform.localScale = startScales[i] * scale;
                    cardCG.alpha = Mathf.Lerp(1f, 0f, t);
                }
            }
            yield return null;
        }

        // Kartları resetle
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].transform.localScale = startScales[i];
            CanvasGroup cardCG = choiceButtons[i].GetComponent<CanvasGroup>();
            if (cardCG != null) cardCG.alpha = 1f;
        }

        levelUpPanel.SetActive(false);
        ShowBlackBackground(false);
        if (levelUpCanvasGroup != null)
        {
            levelUpCanvasGroup.alpha = 1f; // resetle sonraki açılış için
            levelUpCanvasGroup.gameObject.SetActive(false);
        }

        // Perk inventory'yi burada kapatma — map'e dönünce kapanacak
        foreach (var btn in choiceButtons) btn.interactable = true;

        // Skip butonunu aktif yap ve görsel state'i zorla sıfırla
        if (skipButton != null)
        {
            // EventSystem'ın stale pointer verisini temizle
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            skipButton.interactable = true;
            skipButton.colors = skipButtonOriginalColorBlock;
            if (skipButtonImage != null)
                skipButtonImage.color = skipButtonOriginalColor;

            // Button'ın dahili state'ini zorla "Normal"e çek
            // GameObject toggle — Selectable.OnDisable+OnEnable full reset yapar
            skipButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(true);
        }

        Time.timeScale = 1f;

        // Gene Splice gibi upgrade sekanslarının görünmesi için kısa bekleme
        yield return new WaitForSeconds(0.5f);

        // Map sistemi aktifse haritaya dön
        if (MapManager.instance != null)
        {
            MapManager.instance.OnNodeComplete();
        }
        else if (ScreenFader.instance != null)
        {
            ScreenFader.instance.FadeAndLoad(() =>
            {
                LevelGenerator.instance.GenerateNextLevel();
            });
        }
        else
        {
            LevelGenerator.instance.GenerateNextLevel();
        }
    }

    private void ShowBlackBackground(bool show)
    {
        if (perkBlackBackground == null && show)
        {
            perkBlackBackground = new GameObject("PerkBlackBG");
            Canvas bgCanvas = perkBlackBackground.AddComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bgCanvas.sortingOrder = 14; // Below LevelUpCanvas (15)
            perkBlackBackground.AddComponent<CanvasScaler>();
            GameObject imgGO = new GameObject("BlackImage", typeof(RectTransform));
            imgGO.transform.SetParent(perkBlackBackground.transform, false);
            RectTransform rt = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image img = imgGO.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // saydam — map arkaplanı görünsün
            img.raycastTarget = false;
        }
        if (perkBlackBackground != null)
            perkBlackBackground.SetActive(show);
    }

    /// <summary>Perk menüsünü zorla kapat (R reset, sahne değişimi vs.).</summary>
    public void ForceClose()
    {
        StopAllCoroutines();
        if (levelUpPanel != null) levelUpPanel.SetActive(false);
        if (levelUpCanvasGroup != null)
        {
            levelUpCanvasGroup.alpha = 1f;
            levelUpCanvasGroup.gameObject.SetActive(false);
        }
        ShowBlackBackground(false);

        // Kartları resetle
        if (choiceButtons != null)
        {
            foreach (var btn in choiceButtons)
            {
                if (btn == null) continue;
                btn.transform.localScale = Vector3.one;
                btn.interactable = true;
                btn.gameObject.SetActive(false);
                CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        currentChoices.Clear();

        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Hide();
    }

    /// <summary>Perk seçme ekranını göstermeden sonraki levele geç.</summary>
    private void SkipLevelUpScreen()
    {
        // Map sistemi aktifse haritaya dön
        if (MapManager.instance != null)
        {
            MapManager.instance.OnNodeComplete();
            return;
        }

        RunManager.instance.currentLevel++;

        if (ScreenFader.instance != null)
        {
            ScreenFader.instance.FadeAndLoad(() =>
            {
                LevelGenerator.instance.GenerateNextLevel();
            });
        }
        else
        {
            LevelGenerator.instance.GenerateNextLevel();
        }
    }
}