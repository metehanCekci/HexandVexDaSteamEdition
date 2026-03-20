using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using UnityEngine.SceneManagement;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader instance;
    public CanvasGroup faderGroup;
    public float fadeDuration = 1.0f;

    private bool pendingFadeIn = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureFaderExists();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void EnsureFaderExists()
    {
        if (faderGroup != null) return;

        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasObj.AddComponent<CanvasScaler>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        Image img = imageObj.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        faderGroup = canvasObj.AddComponent<CanvasGroup>();
        faderGroup.alpha = 0f;
        faderGroup.blocksRaycasts = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFaderExists();

        // Map sistemi aktifse fade'i MapManager kontrol ediyor
        if (MapManager.instance != null) return;

        if (pendingFadeIn)
        {
            pendingFadeIn = false;
            faderGroup.alpha = 1f;
            StartCoroutine(FadeInAndFinish());
            return;
        }

        // FadeAndLoad kullanılmadan direkt sahne yüklendiyse fade-in yap
        faderGroup.alpha = 1f;
        StartCoroutine(Fade(0f));
    }

    private IEnumerator FadeInAndFinish()
    {
        yield return StartCoroutine(Fade(0f));
    }

    public void FadeAndLoad(Action loadAction)
    {
        StopAllCoroutines();
        pendingFadeIn = false;
        StartCoroutine(FadeOutThenLoad(loadAction));
    }

    private IEnumerator FadeOutThenLoad(Action loadAction)
    {
        yield return StartCoroutine(Fade(1f));
        pendingFadeIn = true;
        loadAction?.Invoke();
    }

    IEnumerator Fade(float targetAlpha)
    {
        if (faderGroup == null) yield break;

        yield return null;

        float startAlpha = faderGroup.alpha;
        float elapsed = 0f;
        faderGroup.blocksRaycasts = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            faderGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        faderGroup.alpha = targetAlpha;
        if (targetAlpha <= 0.05f) faderGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
