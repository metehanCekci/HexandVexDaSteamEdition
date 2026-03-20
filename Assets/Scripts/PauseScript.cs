using UnityEngine;
using UnityEngine.SceneManagement; // Sahne yönetimi için gerekli
using TMPro;

public class PauseManager : MonoBehaviour
{
    // Durdurma menüsü panelini (Canvas) buraya sürükleyeceğiz
    public GameObject pauseMenuUI;
    public GameObject deathMenuUI; // Müfettiş (Inspector) panelinden ölme ekranını buraya sürükle
    // Oyunun durup durmadığını takip eden değişken
    public static bool isPaused = false;

    [Header("Stats UI")]
    public TMP_Text pauseStatsText; // Duraklatma ekranındaki Text (eski, opsiyonel)
    public TMP_Text deathStatsText; // Ölme ekranındaki Text (eski, opsiyonel)
    public StatsPanelUI statsPanelUI;       // Pause menüsündeki stats paneli
    public StatsPanelUI deathStatsPanelUI;  // Ölüm ekranındaki stats paneli

    private bool deathStatsRefreshed = false;

    void Update()
    {
        // EĞER ÖLME EKRANI AÇIKSA, ESC TUŞUNU HİÇ DİNLEME!
        if (deathMenuUI != null && deathMenuUI.activeSelf)
        {
            if (!deathStatsRefreshed)
            {
                if (deathStatsPanelUI != null) deathStatsPanelUI.Refresh();
                else if (deathStatsText != null) deathStatsText.text = RunManager.instance.GetStatsSummary();
                deathStatsRefreshed = true;
            }
            return;
        }

        // Normal ESC basma kodun...
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    // Oyuna devam etme fonksiyonu
    public void Resume()
    {
        pauseMenuUI.SetActive(false); // Menüyü kapat
        Time.timeScale = 1f;          // Zamanı normal hızına döndür
        isPaused = false;
    }

    // Oyunu durdurma fonksiyonu
    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        EnsureCanvasSortingOrder(pauseMenuUI, 500);
        if (statsPanelUI != null) statsPanelUI.Refresh();
        else if (pauseStatsText != null) pauseStatsText.text = RunManager.instance.GetStatsSummary();
        Time.timeScale = 0f;
        isPaused = true;
    }

    // Bölümü baştan başlatma fonksiyonu
    public void Restart()
    {
        Time.timeScale = 1f; // Önemli: Sahne yüklenmeden zamanı açmalıyız!
                             // Mevcut sahnenin indeksini alıp tekrar yüklüyoruz
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Ana menüye veya başka bir sahneye gitme (Index ile çalışır)
    public void LoadSceneByIndex(int sceneIndex)
    {
        Time.timeScale = 1f; // Zamanı açmayı unutma!
        // Ölüm menüsünü kapat
        if (deathMenuUI != null) deathMenuUI.SetActive(false);
        SceneManager.LoadScene(sceneIndex);
    }

    // PauseManager.cs içindeki LoadMainMenu fonksiyonu
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        if (ScreenFader.instance != null)
        {
            ScreenFader.instance.FadeAndLoad(() =>
            {
                if (deathMenuUI != null) deathMenuUI.SetActive(false);
                // Singleton'ları fade-out bittikten sonra yok et
                if (TurnManager.instance != null)
                    TurnManager.instance.ResetGame();
                if (HotbarUI.instance != null)
                    Destroy(HotbarUI.instance.gameObject);
                if (ActivePerkBar.instance != null)
                    Destroy(ActivePerkBar.instance.gameObject);
                if (MapManager.instance != null)
                { Destroy(MapManager.instance.gameObject); MapManager.instance = null; }
                if (RunManager.instance != null)
                    Destroy(RunManager.instance.gameObject);
                if (InventoryManager.instance != null)
                    Destroy(InventoryManager.instance.gameObject);
                if (PerkInventoryUI.instance != null)
                    Destroy(PerkInventoryUI.instance.gameObject);

                SceneManager.LoadScene(1);
            });
        }
        else
        {
            if (deathMenuUI != null) deathMenuUI.SetActive(false);
            if (TurnManager.instance != null)
                TurnManager.instance.ResetGame();
            if (HotbarUI.instance != null)
                Destroy(HotbarUI.instance.gameObject);
            if (ActivePerkBar.instance != null)
                Destroy(ActivePerkBar.instance.gameObject);
            if (MapManager.instance != null)
            { Destroy(MapManager.instance.gameObject); MapManager.instance = null; }
            if (RunManager.instance != null)
                Destroy(RunManager.instance.gameObject);
            if (InventoryManager.instance != null)
                Destroy(InventoryManager.instance.gameObject);
            if (PerkInventoryUI.instance != null)
                Destroy(PerkInventoryUI.instance.gameObject);

            SceneManager.LoadScene(1);
        }
    }

    // KAYBOLAN FONKSİYON GERİ GELDİ
    public void PlayButton(int sceneIndex)
    {
        Time.timeScale = 1f;

        if (ScreenFader.instance != null)
        {
            ScreenFader.instance.FadeAndLoad(() =>
            {
                SceneManager.LoadScene(sceneIndex);
            });
        }
        else
        {
            SceneManager.LoadScene(sceneIndex);
        }
    }

    /// <summary>
    /// Ensures the given UI object's parent Canvas has the specified sorting order.
    /// </summary>
    private void EnsureCanvasSortingOrder(GameObject uiObj, int order)
    {
        if (uiObj == null) return;
        Canvas canvas = uiObj.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = uiObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
        }
    }

    // Oyundan tamamen çıkma (Sadece gerçek oyunda çalışır, Unity Editor'da çalışmaz)
    public void QuitGame()
    {
        Debug.Log("Oyundan çıkılıyor...");

        // Bu kısım sadece Unity Editor içindeyken çalışır
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Bu kısım ise oyun gerçekten yüklendiğinde (Build alındığında) çalışır
        Application.Quit();
#endif
    }
}