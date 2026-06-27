using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LogoSceneScript : MonoBehaviour
{
    [Header("Fade")]
    public float fadeDuration = 1.0f;

    [Header("Canvas")]
    public Canvas mainCanvas;

    [Header("Müzik")]
    public AudioSource musicSource;

    void Awake()
    {
        // Logo başta gizli
        if (mainCanvas != null) mainCanvas.enabled = false;
    }

    IEnumerator Start()
    {
        // ScreenFader'ın hazır olmasını bekle
        while (ScreenFader.instance == null)
            yield return null;

        // Ekranı siyah yap
        CanvasGroup fader = ScreenFader.instance.faderGroup;
        if (fader != null)
        {
            fader.alpha = 1f;
            fader.blocksRaycasts = true;
        }

        // 1 saniye siyah ekran
        yield return new WaitForSeconds(1f);

        // Logo göster
        if (mainCanvas != null) mainCanvas.enabled = true;

        // Fade in
        if (fader != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fader.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            fader.alpha = 0f;
            fader.blocksRaycasts = false;
        }

        // Müzik
        if (musicSource != null) musicSource.Play();

        // Müziğin bitmesini bekle
        if (musicSource != null && musicSource.clip != null)
            yield return new WaitWhile(() => musicSource.isPlaying);
        else
            yield return new WaitForSeconds(3f);

        // Fade out + sahne geçişi
        ScreenFader.instance.FadeAndLoad(() =>
        {
            SceneManager.LoadScene(1);
        });
    }
}
