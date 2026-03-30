using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Balatro tarzı 3D kart tilt efekti.
/// Mouse kartın üzerinde gezdirildiğinde kart, mouse yönüne doğru 3D döner.
/// </summary>
public class CardTilt3D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Ayarlar")]
    public float maxTiltAngle = 17.5f;
    public float tiltSpeed = 8f;
    public float returnSpeed = 6f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Camera canvasCamera;
    private bool isHovered;
    private Quaternion targetRotation;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetRotation = Quaternion.identity;
    }

    void OnEnable()
    {
        // Find parent canvas and its camera
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
                    // -1 to 1 normalized position
                    float nx = Mathf.Clamp(localPoint.x / halfW, -1f, 1f);
                    float ny = Mathf.Clamp(localPoint.y / halfH, -1f, 1f);

                    // Mouse sağda → kart sağa döner (Y ekseni), mouse üstte → kart yukarı döner (X ekseni)
                    float rotX = -ny * maxTiltAngle; // mouse üstte → alt kalkar
                    float rotY = nx * maxTiltAngle;  // mouse sağda → sağ tarafa döner

                    targetRotation = Quaternion.Euler(rotX, rotY, 0f);
                }
            }

            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation, targetRotation, Time.unscaledDeltaTime * tiltSpeed);
        }
        else
        {
            // Smooth return to flat
            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation, Quaternion.identity, Time.unscaledDeltaTime * returnSpeed);

            // Snap when close enough
            if (Quaternion.Angle(rectTransform.localRotation, Quaternion.identity) < 0.1f)
                rectTransform.localRotation = Quaternion.identity;
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
        rectTransform.localRotation = Quaternion.identity;
    }
}
