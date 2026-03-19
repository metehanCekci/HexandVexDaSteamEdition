using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Herhangi bir UI objesine ekle — hover'da smooth scale-up + bounce efekti yapar.
/// Collection butonu, Skip butonu, checkbox gibi elemanlar için.
/// </summary>
public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Hover")]
    [Tooltip("Hover'daki hedef scale çarpanı")]
    public float hoverScale = 1.12f;
    [Tooltip("Scale geçiş hızı")]
    public float scaleSpeed = 10f;

    [Header("Click Punch")]
    [Tooltip("Tıklama anında küçülme miktarı")]
    public float clickShrink = 0.9f;
    [Tooltip("Click punch süresi")]
    public float clickDuration = 0.1f;

    private Vector3 baseScale;
    private Coroutine scaleCo;
    private bool isHovered;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        // Reset — sahne geçişlerinde bozulmasın
        isHovered = false;
        transform.localScale = baseScale;
    }

    void OnDisable()
    {
        if (scaleCo != null) { StopCoroutine(scaleCo); scaleCo = null; }
        transform.localScale = baseScale;
        isHovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        AnimateTo(baseScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        AnimateTo(baseScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (scaleCo != null) { StopCoroutine(scaleCo); scaleCo = null; }
        scaleCo = StartCoroutine(ClickPunch());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(isHovered ? baseScale * hoverScale : baseScale);
    }

    private void AnimateTo(Vector3 target)
    {
        if (scaleCo != null) { StopCoroutine(scaleCo); scaleCo = null; }
        if (gameObject.activeInHierarchy)
            scaleCo = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.005f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * scaleSpeed);
            yield return null;
        }
        transform.localScale = target;
        scaleCo = null;
    }

    private IEnumerator ClickPunch()
    {
        // Shrink
        Vector3 shrinkTarget = baseScale * clickShrink;
        float elapsed = 0f;
        while (elapsed < clickDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / clickDuration;
            transform.localScale = Vector3.Lerp(transform.localScale, shrinkTarget, t);
            yield return null;
        }

        // Bounce back — overshoot then settle
        Vector3 overshoot = baseScale * hoverScale * 1.08f;
        Vector3 settle = isHovered ? baseScale * hoverScale : baseScale;
        elapsed = 0f;
        float bounceDur = 0.12f;
        while (elapsed < bounceDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / bounceDur;
            // ease out back
            float easeT = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = Vector3.LerpUnclamped(shrinkTarget, settle, easeT);
            yield return null;
        }
        transform.localScale = settle;
        scaleCo = null;
    }
}
