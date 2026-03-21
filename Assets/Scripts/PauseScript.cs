using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenuUI;
    public GameObject deathMenuUI;
    public static bool isPaused = false;

    [Header("Stats UI")]
    public TMP_Text pauseStatsText;
    public TMP_Text deathStatsText;
    public StatsPanelUI statsPanelUI;
    public StatsPanelUI deathStatsPanelUI;

    private bool deathStatsRefreshed = false;
    private CanvasGroup pauseCanvasGroup;
    private Coroutine animCoroutine;

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

    public void Resume()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(PauseCloseAnim());
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        EnsureCanvasSortingOrder(pauseMenuUI, 500);
        EnsurePauseCanvasGroup();

        if (statsPanelUI != null) statsPanelUI.Refresh();
        else if (pauseStatsText != null) pauseStatsText.text = RunManager.instance.GetStatsSummary();
        if (NodeMinimapUI.instance != null) NodeMinimapUI.instance.Refresh();

        Time.timeScale = 0f;
        isPaused = true;

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(PauseOpenAnim());
    }

    // ═══════════════════════════════════════════
    // ANIMATIONS
    // ═══════════════════════════════════════════

    private void EnsurePauseCanvasGroup()
    {
        if (pauseCanvasGroup != null) return;
        pauseCanvasGroup = pauseMenuUI.GetComponent<CanvasGroup>();
        if (pauseCanvasGroup == null)
            pauseCanvasGroup = pauseMenuUI.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Bouncy açılma: scale overshoot + fade in.
    /// Elastic ease — hızlı açılır, hafif geri sekip yerine oturur.
    /// </summary>
    private IEnumerator PauseOpenAnim()
    {
        EnsurePauseCanvasGroup();
        float duration = 0.4f;
        float elapsed = 0f;

        pauseCanvasGroup.alpha = 0f;
        pauseMenuUI.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Elastic ease out — overshoot then settle
            float s = EaseOutBack(t, 1.4f);
            pauseMenuUI.transform.localScale = new Vector3(s, s, 1f);

            // Fade in — faster than scale (ilk %60'ta tamamlanır)
            pauseCanvasGroup.alpha = Mathf.Clamp01(t * 2.5f);

            yield return null;
        }

        pauseMenuUI.transform.localScale = Vector3.one;
        pauseCanvasGroup.alpha = 1f;
        animCoroutine = null;
    }

    /// <summary>
    /// Kapanma: hızlı shrink + fade out.
    /// </summary>
    private IEnumerator PauseCloseAnim()
    {
        EnsurePauseCanvasGroup();
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float s = Mathf.Lerp(1f, 0.85f, t);
            pauseMenuUI.transform.localScale = new Vector3(s, s, 1f);
            pauseCanvasGroup.alpha = 1f - t;

            yield return null;
        }

        pauseMenuUI.SetActive(false);
        pauseMenuUI.transform.localScale = Vector3.one;
        pauseCanvasGroup.alpha = 1f;
        Time.timeScale = 1f;
        isPaused = false;
        animCoroutine = null;
    }

    /// <summary>
    /// Ease Out Back — overshoot parametresiyle.
    /// s=1.0 hafif bounce, s=1.7 belirgin bounce.
    /// </summary>
    private static float EaseOutBack(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
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