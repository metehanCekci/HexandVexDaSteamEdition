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
        if (restPanel != null) restPanel.SetActive(true);

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
            restButton.onClick.AddListener(() => OnRest(healAmount));
            restButton.interactable = true;
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
        if (RunManager.instance != null)
        {
            RunManager.instance.playerCurrentHealth = Mathf.Min(
                RunManager.instance.playerCurrentHealth + amount,
                RunManager.instance.playerMaxHealth
            );
        }

        if (infoText != null) infoText.text = $"+{amount} HP";

        DisableButtons();
        Invoke(nameof(Close), 0.8f);
    }

    private void OnTrain()
    {
        if (RunManager.instance == null || RunManager.instance.activePerks.Count == 0) return;

        // Upgrade edilebilir perklerden rastgele birini seç
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
        Invoke(nameof(Close), 0.8f);
    }

    private void DisableButtons()
    {
        if (restButton != null) restButton.interactable = false;
        if (trainButton != null) trainButton.interactable = false;
    }

    private void Close()
    {
        Time.timeScale = 1f;
        if (restPanel != null) restPanel.SetActive(false);

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }
}
