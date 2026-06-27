using UnityEngine;
using System.Collections;

/// <summary>
/// Merged Shop kartlarina dramatik hover efekti verir.
/// EventTrigger (MergedShopManager veya MergedShopSetup tarafindan eklenir)
/// HoverIn / HoverOut metodlarini cagirir.
///
/// Efektler:
///  - Scale 1.0 -> 1.18 (snappy spring animasyon)
///  - Karti sibling order ile one cikarir (diger kartlarin ustunde)
/// </summary>
public class ShopCardHover : MonoBehaviour
{
    [Header("Hover Scale")]
    public float hoverScale = 1.18f;
    public float animSpeed = 18f;          // Lerp hizi — buyuk = daha snappy

    [Header("Spring Overshoot")]
    public float overshootAmount = 1.06f;  // Hedefin bu kati kadar asip geri doner
    public float overshootDuration = 0.06f;

    private Vector3 baseScale;
    private Coroutine scaleCo;
    private bool isHovered;
    private int homeSiblingIndex = -1;

    /// <summary>True iken hover scale'a dokunmaz (pop-in animasyonu sırasında).</summary>
    [System.NonSerialized] public bool scaleOverridden;

    void Awake()
    {
        baseScale = transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;
    }

    void Start()
    {
        homeSiblingIndex = transform.GetSiblingIndex();
    }

    void OnEnable()
    {
        isHovered = false;
        // homeSiblingIndex henüz atanmadıysa güncelle
        if (homeSiblingIndex < 0)
            homeSiblingIndex = transform.GetSiblingIndex();
    }

    void OnDisable()
    {
        if (scaleCo != null) { StopCoroutine(scaleCo); scaleCo = null; }
        isHovered = false;
    }

    /// <summary>
    /// MergedShopManager EventTrigger tarafindan cagirilir.
    /// </summary>
    public void HoverIn()
    {
        if (scaleOverridden) return;
        isHovered = true;
        transform.SetAsLastSibling();

        Vector3 target = baseScale * hoverScale;
        if (scaleCo != null) StopCoroutine(scaleCo);
        if (gameObject.activeInHierarchy)
            scaleCo = StartCoroutine(SpringScaleTo(target));
    }

    /// <summary>
    /// MergedShopManager EventTrigger tarafindan cagirilir.
    /// </summary>
    public void HoverOut()
    {
        if (scaleOverridden) return;
        isHovered = false;
        if (homeSiblingIndex >= 0)
            transform.SetSiblingIndex(homeSiblingIndex);

        if (scaleCo != null) StopCoroutine(scaleCo);
        if (gameObject.activeInHierarchy)
            scaleCo = StartCoroutine(SnapScaleTo(baseScale));
    }

    /// <summary>Pop-in animasyonu bittikten sonra çağır — baseScale güncellenir.</summary>
    public void RefreshBaseScale()
    {
        baseScale = transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;
        scaleOverridden = false;
    }

    /// <summary>Sibling index değiştiğinde (reroll vb.) güncelle.</summary>
    public void UpdateHomeIndex()
    {
        homeSiblingIndex = transform.GetSiblingIndex();
    }

    /// <summary>
    /// Hover-in: hedefe spring ile gider (overshoot + settle).
    /// </summary>
    private IEnumerator SpringScaleTo(Vector3 target)
    {
        Vector3 overshootTarget = baseScale + (target - baseScale) * overshootAmount;

        // Phase 1: hizla overshoot'a git
        while (Vector3.Distance(transform.localScale, overshootTarget) > 0.003f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, overshootTarget, Time.unscaledDeltaTime * animSpeed);
            yield return null;
        }

        // Phase 2: overshoot'tan hedefe geri don (daha hizli)
        float elapsed = 0f;
        Vector3 from = transform.localScale;
        while (elapsed < overshootDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / overshootDuration);
            // Ease-out quad
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(from, target, eased);
            yield return null;
        }
        transform.localScale = target;
        scaleCo = null;
    }

    /// <summary>
    /// Hover-out: hizli ve temiz geri don.
    /// </summary>
    private IEnumerator SnapScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.003f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, target, Time.unscaledDeltaTime * animSpeed * 1.2f);
            yield return null;
        }
        transform.localScale = target;
        scaleCo = null;
    }
}
