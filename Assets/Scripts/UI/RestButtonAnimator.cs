using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Rest menüsü butonlarına eklenir.
/// Hover: büyüyüp küçülme (pulse), basınca: haşin patlama animasyonu.
/// Time.timeScale=0 iken çalışır (unscaled time kullanır).
/// </summary>
public class RestButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rt;
    private Vector3 baseScale;
    private bool isHovered;
    private bool isPressed;
    private Coroutine pulseRoutine;

    // Hover ayarları
    private const float hoverScale = 1.12f;
    private const float hoverSpeed = 6f;
    // Pulse ayarları
    private const float pulseMin = 1.08f;
    private const float pulseMax = 1.15f;
    private const float pulseSpeed = 3f;
    // Press ayarları
    private const float pressScale = 0.85f;
    private const float pressBouncePeak = 1.35f;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(HoverPulse());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(SmoothReturn());
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PressAnimation());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    /// <summary>Hover sırasında nefes alıp veren pulse efekti</summary>
    private IEnumerator HoverPulse()
    {
        // Önce hover boyutuna smooth geçiş
        float current = rt.localScale.x;
        while (Mathf.Abs(current - hoverScale) > 0.01f && isHovered && !isPressed)
        {
            current = Mathf.Lerp(current, hoverScale, Time.unscaledDeltaTime * hoverSpeed);
            rt.localScale = new Vector3(current, current, 1f);
            yield return null;
        }

        // Pulse döngüsü
        float time = 0f;
        while (isHovered && !isPressed)
        {
            time += Time.unscaledDeltaTime * pulseSpeed;
            float s = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(time) + 1f) * 0.5f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    /// <summary>Hover'dan çıkınca smooth normal boyuta dön</summary>
    private IEnumerator SmoothReturn()
    {
        float current = rt.localScale.x;
        while (Mathf.Abs(current - 1f) > 0.005f)
        {
            current = Mathf.Lerp(current, 1f, Time.unscaledDeltaTime * hoverSpeed);
            rt.localScale = new Vector3(current, current, 1f);
            yield return null;
        }
        rt.localScale = baseScale;
    }

    /// <summary>
    /// Basınca haşin animasyon: hızlı küçülme → patlayarak büyüme → bounce ile geri dönme
    /// </summary>
    private IEnumerator PressAnimation()
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();

        // Faz 1: Hızlı ezilme (0.08s)
        float elapsed = 0f;
        float phase1 = 0.08f;
        float startScale = rt.localScale.x;
        while (elapsed < phase1)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / phase1;
            float s = Mathf.Lerp(startScale, pressScale, t * t); // easeIn
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // Faz 2: Patlayarak büyüme (0.12s)
        elapsed = 0f;
        float phase2 = 0.12f;
        while (elapsed < phase2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / phase2;
            // easeOutBack — hedeften biraz taşar
            float overshoot = 1.8f;
            float t1 = t - 1f;
            float eased = t1 * t1 * ((overshoot + 1f) * t1 + overshoot) + 1f;
            float s = Mathf.Lerp(pressScale, pressBouncePeak, eased);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        CameraController.ShakeLight();

        // Faz 3: Bounce geri dönme (0.2s)
        elapsed = 0f;
        float phase3 = 0.2f;
        float target = isHovered ? hoverScale : 1f;
        float peak = pressBouncePeak;
        while (elapsed < phase3)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / phase3;
            // Damped oscillation
            float decay = 1f - t;
            float bounce = Mathf.Sin(t * Mathf.PI * 2f) * decay * 0.15f;
            float s = Mathf.Lerp(peak, target, t) + bounce;
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        rt.localScale = new Vector3(target, target, 1f);

        // Hâlâ hover'daysa pulse'a devam
        if (isHovered && !isPressed)
            pulseRoutine = StartCoroutine(HoverPulse());
    }
}
