using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RestNodeUI : MonoBehaviour
{
    public static RestNodeUI instance;

    [Header("Panel")]
    public GameObject restPanel;

    [Header("Butonlar")]
    public Button restButton;       // Dinlen: HP iyileştir
    public Button trainButton;      // Antreman: Rastgele perk upgrade

    [Header("UI Text")]
    public TMP_Text titleText;
    public TMP_Text restButtonText;
    public TMP_Text trainButtonText;
    public TMP_Text infoText;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    public void Show()
    {
        Debug.Log($"[REST] Show() called — restPanel={restPanel != null}, restButton={restButton != null}, trainButton={trainButton != null}");
        if (restPanel != null)
        {
            // Parent canvas'ı da aktif et (Close'da kapatılmış olabilir)
            if (restPanel.transform.parent != null)
                restPanel.transform.parent.gameObject.SetActive(true);
            restPanel.SetActive(true);
        }

        // Butonlara animatör ekle (yoksa)
        EnsureButtonAnimator(restButton);
        EnsureButtonAnimator(trainButton);

        Time.timeScale = 0f;

        // Heal miktarını hesapla — canı tamamen doldur
        int healAmount = 999;
        if (RunManager.instance != null)
            healAmount = RunManager.instance.playerMaxHealth - RunManager.instance.playerCurrentHealth;

        if (titleText != null) titleText.text = "Campfire";
        if (restButtonText != null) restButtonText.text = "Rest\n(Heal up)\n<size=70%>Full HP</size>";
        if (trainButtonText != null) trainButtonText.text = "Train\n(Upgrade Perk)";

        bool canTrain = false;
        if (RunManager.instance != null)
        {
            canTrain = RunManager.instance.activePerks.Exists(
                p => p != null && p.currentLevel < p.maxLevel
            );
        }

        if (restButton != null)
        {
            restButton.onClick.RemoveAllListeners();
            restButton.onClick.AddListener(() =>
            {
                Debug.Log($"[REST] Rest button onClick FIRED — healAmount={healAmount}");
                OnRest(healAmount);
            });
            restButton.interactable = true;
            Debug.Log($"[REST] Rest button listener attached. interactable={restButton.interactable}");
        }
        else
        {
            Debug.LogError("[REST] restButton is NULL — cannot attach listener!");
        }

        if (trainButton != null)
        {
            trainButton.onClick.RemoveAllListeners();
            trainButton.onClick.AddListener(OnTrain);
            trainButton.interactable = canTrain;
        }

        if (infoText != null) infoText.text = "";
    }

    private void OnRest(int amount)
    {
        Debug.Log($"[REST] OnRest called — amount={amount}");

        if (RunManager.instance != null)
        {
            RunManager.instance.playerCurrentHealth = Mathf.Min(
                RunManager.instance.playerCurrentHealth + amount,
                RunManager.instance.playerMaxHealth
            );
        }

        // HealthScript'i de senkronize et (yoksa savaşta eski can değeri kalır)
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = RunManager.instance.playerMaxHealth;
            h.currentHP = RunManager.instance.playerCurrentHealth;
            h.updateHealth();
        }

        if (infoText != null) infoText.text = $"+{amount} HP";

        DisableButtons();
        StartCoroutine(CloseAfterDelay());
    }

    private void OnTrain()
    {
        if (RunManager.instance == null || RunManager.instance.activePerks.Count == 0) return;

        var upgradeablePerks = RunManager.instance.activePerks.FindAll(
            p => p != null && p.currentLevel < p.maxLevel
        );

        if (upgradeablePerks.Count > 0)
        {
            BasePerk chosen = upgradeablePerks[Random.Range(0, upgradeablePerks.Count)];
            chosen.Upgrade();
            if (infoText != null) infoText.text = $"{chosen.perkName} upgraded!";

            // Upgrade olan perkte pop animasyonu
            if (PerkInventoryUI.instance != null)
                PerkInventoryUI.instance.TriggerPopForPerk(chosen);
            if (ActivePerkBar.instance != null)
                ActivePerkBar.instance.TriggerPopForPerk(chosen);
        }
        else
        {
            if (infoText != null) infoText.text = "All perks maxed!";
        }

        DisableButtons();
        StartCoroutine(CloseAfterDelay());
    }

    private void EnsureButtonAnimator(Button btn)
    {
        if (btn != null && btn.GetComponent<RestButtonAnimator>() == null)
            btn.gameObject.AddComponent<RestButtonAnimator>();
    }

    private void DisableButtons()
    {
        if (restButton != null) restButton.interactable = false;
        if (trainButton != null) trainButton.interactable = false;
    }

    private System.Collections.IEnumerator CloseAfterDelay()
    {
        Debug.Log("[REST] CloseAfterDelay started — waiting 0.8s realtime");
        // Time.timeScale=0 iken Invoke çalışmaz, WaitForSecondsRealtime kullan
        yield return new WaitForSecondsRealtime(0.8f);
        Debug.Log("[REST] CloseAfterDelay wait done — calling Close()");
        Close();
    }

    private void Close()
    {
        Time.timeScale = 1f;
        StartCoroutine(CloseWithFade());
    }

    private System.Collections.IEnumerator CloseWithFade()
    {
        // Rest panelini smooth fade out
        CanvasGroup panelCG = restPanel != null ? restPanel.GetComponent<CanvasGroup>() : null;
        if (panelCG == null && restPanel != null)
            panelCG = restPanel.AddComponent<CanvasGroup>();

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

        // Paneli kapat
        if (restPanel != null)
        {
            restPanel.SetActive(false);
            if (restPanel.transform.parent != null)
                restPanel.transform.parent.gameObject.SetActive(false);
        }

        // Alpha'yı resetle (sonraki açılış için)
        if (panelCG != null) panelCG.alpha = 1f;

        // Map'e smooth dönüş
        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }
}
