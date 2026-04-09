using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Balatro tarzı 3D kart tilt efekti.
/// Mouse kartın üzerinde gezdirildiğinde kart, mouse yönüne doğru 3D döner.
/// Perspektif illüzyonu için asimetrik scale + hafif shine offset kullanır.
/// </summary>
public class CardTilt3D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tilt Ayarları")]
    public float maxTiltAngle = 15f;
    public float tiltSpeed = 10f;
    public float returnSpeed = 8f;

    [Header("Perspektif Skew (sahte 3D derinlik)")]
    [Tooltip("Mouse yönündeki kenar küçülür, karşı kenar büyür")]
    public float maxSkewAmount = 0.06f;

    [Header("Shine Efekti (opsiyonel)")]
    [Tooltip("Kartın üzerinde gezen parlama için bir Image atayın (null ise devre dışı)")]
    public RectTransform shineOverlay;
    public float shineRange = 120f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Camera canvasCamera;
    private bool isHovered;

    /// <summary>Scale dışarıdan kontrol ediliyorsa (pop-in animasyonu vb.) true yap.
    /// CardTilt3D scale'a dokunmaz.</summary>
    [System.NonSerialized] public bool scaleOverridden;

    private Quaternion targetRotation;
    private Vector3 targetScale;
    private Vector3 baseScale;
    private Vector2 targetShinePos;

    // Smooth için mevcut değerler
    private float currentNx;
    private float currentNy;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetRotation = Quaternion.identity;
        baseScale = rectTransform.localScale;
        targetScale = baseScale;
    }

    void OnEnable()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            Canvas rootCanvas = parentCanvas.rootCanvas;
            canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }
    }

    void Update()
    {
        if (isHovered)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Input.mousePosition, canvasCamera, out localPoint))
            {
                float halfW = rectTransform.rect.width * 0.5f;
                float halfH = rectTransform.rect.height * 0.5f;

                if (halfW > 0 && halfH > 0)
                {
                    float nx = Mathf.Clamp(localPoint.x / halfW, -1f, 1f);
                    float ny = Mathf.Clamp(localPoint.y / halfH, -1f, 1f);

                    // Smooth normalized values
                    float lerpT = Time.unscaledDeltaTime * tiltSpeed;
                    currentNx = Mathf.Lerp(currentNx, nx, lerpT);
                    currentNy = Mathf.Lerp(currentNy, ny, lerpT);

                    // 3D rotation
                    float rotX = -currentNy * maxTiltAngle;
                    float rotY = currentNx * maxTiltAngle;
                    targetRotation = Quaternion.Euler(rotX, rotY, 0f);

                    // Perspektif skew: mouse'a yakın kenar küçülür (uzaklaşır gibi)
                    // Bu sağ üst vs sol alt arasındaki farkı görünür kılar
                    float scaleX = baseScale.x * (1f - Mathf.Abs(currentNx) * maxSkewAmount);
                    float scaleY = baseScale.y * (1f - Mathf.Abs(currentNy) * maxSkewAmount);
                    targetScale = new Vector3(scaleX, scaleY, baseScale.z);

                    // Shine overlay
                    if (shineOverlay != null)
                    {
                        targetShinePos = new Vector2(currentNx * shineRange, currentNy * shineRange);
                    }
                }
            }

            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation, targetRotation, Time.unscaledDeltaTime * tiltSpeed);
            if (!scaleOverridden)
                rectTransform.localScale = Vector3.Lerp(
                    rectTransform.localScale, targetScale, Time.unscaledDeltaTime * tiltSpeed);

            if (shineOverlay != null)
            {
                shineOverlay.anchoredPosition = Vector2.Lerp(
                    shineOverlay.anchoredPosition, targetShinePos, Time.unscaledDeltaTime * tiltSpeed);
            }
        }
        else
        {
            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation, Quaternion.identity, Time.unscaledDeltaTime * returnSpeed);
            if (!scaleOverridden)
                rectTransform.localScale = Vector3.Lerp(
                    rectTransform.localScale, baseScale, Time.unscaledDeltaTime * returnSpeed);

            currentNx = Mathf.Lerp(currentNx, 0f, Time.unscaledDeltaTime * returnSpeed);
            currentNy = Mathf.Lerp(currentNy, 0f, Time.unscaledDeltaTime * returnSpeed);

            if (shineOverlay != null)
            {
                shineOverlay.anchoredPosition = Vector2.Lerp(
                    shineOverlay.anchoredPosition, Vector2.zero, Time.unscaledDeltaTime * returnSpeed);
            }

            if (Quaternion.Angle(rectTransform.localRotation, Quaternion.identity) < 0.1f)
            {
                rectTransform.localRotation = Quaternion.identity;
                if (!scaleOverridden) rectTransform.localScale = baseScale;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetRotation = Quaternion.identity;
        targetScale = baseScale;
    }

    void OnDisable()
    {
        isHovered = false;
        rectTransform.localRotation = Quaternion.identity;
        if (!scaleOverridden) rectTransform.localScale = baseScale;
        currentNx = 0f;
        currentNy = 0f;
    }

    /// <summary>Pop-in animasyonu bittikten sonra çağır — baseScale'i günceller.</summary>
    public void RefreshBaseScale()
    {
        baseScale = rectTransform.localScale;
        targetScale = baseScale;
        scaleOverridden = false;
    }
}
