using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect kullanmadan kendi scroll mantığımız.
/// Container pivot=top, anchor=top: pos.y=0 → boss görünür, pos.y negatif → start görünür.
/// Orta tuş / sağ tık drag + mouse wheel ile scroll.
/// </summary>
public class MapDragScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [HideInInspector] public RectTransform content;
    [HideInInspector] public RectTransform viewport;

    public float scrollSpeed = 40f;
    public float dragSpeed = 1.5f;

    private bool isDragging = false;
    private Vector2 lastMousePos;

    void Update()
    {
        if (content == null || viewport == null) return;

        if (isDragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
        {
            Vector2 currentMousePos = (Vector2)Input.mousePosition;
            float deltaY = currentMousePos.y - lastMousePos.y;
            lastMousePos = currentMousePos;

            // Mouse yukarı → content yukarı kayar → alttaki node'lar görünür
            MoveContent(deltaY * dragSpeed);
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
        // Wheel yukarı → yukarı scroll (boss'a doğru)
        MoveContent(eventData.scrollDelta.y * scrollSpeed);
    }

    private void MoveContent(float deltaY)
    {
        if (content == null || viewport == null) return;

        Vector2 pos = content.anchoredPosition;
        pos.y += deltaY;

        // Container pivot=top, anchor=top
        // pos.y = 0 → en üst (boss) görünür
        // pos.y = pozitif → content aşağı kayar → start row görünür
        float contentH = content.rect.height;
        float viewportH = viewport.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);

        pos.y = Mathf.Clamp(pos.y, 0f, maxScroll);
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// Start row'u göster (oyuncu alttan başlıyor)
    /// </summary>
    public void ScrollToBottom()
    {
        if (content == null || viewport == null) return;
        float contentH = content.rect.height;
        float viewportH = viewport.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, maxScroll);
    }

    /// <summary>
    /// Boss row'u göster (en üst)
    /// </summary>
    public void ScrollToTop()
    {
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    }
}
