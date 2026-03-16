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

        Time.timeScale = 0f;

        // Heal miktarını hesapla
        int healAmount = 1;
        if (RunManager.instance != null)
            healAmount = Mathf.Max(1, RunManager.instance.playerMaxHealth / 3);

        if (titleText != null) titleText.text = "Campfire";
        if (restButtonText != null) restButtonText.text = $"Rest\n<size=70%>+{healAmount} HP</size>";
        if (trainButtonText != null) trainButtonText.text = "Train\n<size=70%>Upgrade Perk</size>";

        bool canTrain = RunManager.instance != null && RunManager.instance.activePerks.Count > 0;

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
        }
        else
        {
            if (infoText != null) infoText.text = "All perks maxed!";
        }

        DisableButtons();
        StartCoroutine(CloseAfterDelay());
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
        Debug.Log("[REST] Close called — setting timeScale=1, hiding panel, calling OnNodeComplete");
        Time.timeScale = 1f;

        // Panel'i ve parent canvas'ı kapat — yoksa map'in üstünde kalır
        if (restPanel != null)
        {
            restPanel.SetActive(false);
            // Canvas'ı da gizle ki map'in üstünde raycast bloklama kalmasın
            if (restPanel.transform.parent != null)
                restPanel.transform.parent.gameObject.SetActive(false);
        }

        if (MapManager.instance != null)
        {
            Debug.Log("[REST] MapManager.OnNodeComplete() calling...");
            MapManager.instance.OnNodeComplete();
        }
        else
        {
            Debug.LogWarning("[REST] MapManager.instance is NULL — cannot return to map!");
        }
    }
}
