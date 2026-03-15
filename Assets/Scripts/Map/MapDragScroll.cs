using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Haritayı özgürce sürükle: sağ tık / orta tuş ile pan (X+Y).
/// Mouse wheel ile zoom (opsiyonel, şimdilik sadece dikey scroll).
/// Sol tık node butonlarına gider.
/// </summary>
public class MapDragScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [HideInInspector] public RectTransform content;
    [HideInInspector] public RectTransform viewport;

    public float scrollSpeed = 60f;
    public float dragSpeed = 1f;

    private bool isDragging = false;
    private Vector2 lastMousePos;

    void Update()
    {
        if (content == null) return;

        if (isDragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
        {
            Vector2 currentMousePos = (Vector2)Input.mousePosition;
            Vector2 delta = currentMousePos - lastMousePos;
            lastMousePos = currentMousePos;

            // Serbest hareket — X ve Y
            content.anchoredPosition += delta * dragSpeed;
        }
        else
        {
            isDragging = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle ||
            eventData.button == PointerEventData.InputButton.Right)
        {
            isDragging = true;
            lastMousePos = eventData.position;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle ||
            eventData.button == PointerEventData.InputButton.Right)
        {
            isDragging = false;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null) return;
        // Wheel ile dikey hareket
        Vector2 pos = content.anchoredPosition;
        pos.y += eventData.scrollDelta.y * scrollSpeed;
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// Start row'u ortala (oyuncu alttan başlıyor)
    /// </summary>
    public void ScrollToBottom()
    {
        if (content == null || viewport == null) return;
        float contentH = content.rect.height;
        float viewportH = viewport.rect.height;
        // Content en altını göster
        content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, contentH - viewportH));
    }

    /// <summary>
    /// Boss row'u göster (en üst)
    /// </summary>
    public void ScrollToTop()
    {
        if (content != null)
            content.anchoredPosition = new Vector2(0f, 0f);
    }
}
