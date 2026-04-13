using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Balatro tarzı 3D kart tilt efekti.
/// Mouse kartın üzerinde gezdirildiğinde kart, mouse yönüne doğru 3D döner.
/// NOT: Scale artık ShopCardHover tarafından yönetiliyor.
/// Bu script sadece rotation + shine ile ilgilenir (çakışma olmasın diye).
/// </summary>
public class CardTilt3D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tilt Ayarları")]
    public float maxTiltAngle = 15f;
    public float tiltSpeed = 12f;
    public float returnSpeed = 10f;

    [Header("Shine Efekti (opsiyonel)")]
    [Tooltip("Kartın üzerinde gezen parlama için bir Image atayın (null ise devre dışı)")]
    public RectTransform shineOverlay;
    public float shineRange = 120f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Camera canvasCamera;
    private bool isHovered;

    /// <summary>Scale dışarıdan kontrol ediliyorsa (pop-in animasyonu vb.) true yap.</summary>
    [System.NonSerialized] public bool scaleOverridden;

    private Quaternion targetRotation;
    private Vector2 targetShinePos;

    // Smooth için mevcut değerler
    private float currentNx;
    private float currentNy;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetRotation = Quaternion.identity;
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
                    // Pivot merkez değilse localPoint kayar — rect.center ile düzelt
                    Vector2 center = rectTransform.rect.center;
                    float nx = Mathf.Clamp((localPoint.x - center.x) / halfW, -1f, 1f);
                    float ny = Mathf.Clamp((localPoint.y - center.y) / halfH, -1f, 1f);

                    // Smooth normalized values
                    float lerpT = Time.unscaledDeltaTime * tiltSpeed;
                    currentNx = Mathf.Lerp(currentNx, nx, lerpT);
                    currentNy = Mathf.Lerp(currentNy, ny, lerpT);

                    // 3D rotation
                    float rotX = -currentNy * maxTiltAngle;
                    float rotY = -currentNx * maxTiltAngle;
                    targetRotation = Quaternion.Euler(rotX, rotY, 0f);

                    // Shine overlay
                    if (shineOverlay != null)
                    {
                        targetShinePos = new Vector2(currentNx * shineRange, currentNy * shineRange);
                    }
                }
            }

            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation, targetRotation, Time.unscaledDeltaTime * tiltSpeed);

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
    }

    void OnDisable()
    {
        isHovered = false;
        if (rectTransform != null)
            rectTransform.localRotation = Quaternion.identity;
        currentNx = 0f;
        currentNy = 0f;
    }

    /// <summary>Pop-in animasyonu bittikten sonra çağır.</summary>
    public void RefreshBaseScale()
    {
        scaleOverridden = false;
    }
}
