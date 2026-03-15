using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ScrollRect'i tamamen devre dışı bırakır.
/// Kendi scroll mantığımız: orta tuş / sağ tık drag + mouse wheel.
/// Sol tık node butonlarına gider, scroll'a karışmaz.
/// </summary>
public class MapDragScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    private RectTransform content;
    private RectTransform viewport;
    private float scrollSpeed = 30f;
    private float dragSpeed = 1f;

    private bool isDragging = false;
    private Vector2 lastMousePos;

    void Start()
    {
        // ScrollRect'ten referansları al ve sonra tamamen kapat
        ScrollRect sr = GetComponent<ScrollRect>();
        if (sr != null)
        {
            content = sr.content;
            viewport = sr.viewport;
            scrollSpeed = sr.scrollSensitivity;
            sr.enabled = false;
        }
    }

    void Update()
    {
        if (content == null) return;

        // Orta tuş (2) veya sağ tuş (1) basılıyken drag
        if (isDragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
        {
            Vector2 currentMousePos = Input.mousePosition;
            float deltaY = currentMousePos.y - lastMousePos.y;
            lastMousePos = currentMousePos;

            MoveContent(-deltaY * dragSpeed);
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
        // Mouse wheel — her zaman çalışsın
        if (content == null) return;
        MoveContent(-eventData.scrollDelta.y * scrollSpeed);
    }

    private void MoveContent(float deltaY)
    {
        if (content == null) return;

        Vector2 pos = content.anchoredPosition;
        pos.y += deltaY;

        // Clamp: content pivot=bottom, anchor=bottom
        // pos.y = 0 → en alt görünür, pos.y = maxScroll → en üst görünür
        float contentH = content.rect.height;
        float viewportH = viewport != null ? viewport.rect.height : Screen.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);

        pos.y = Mathf.Clamp(pos.y, 0f, maxScroll);
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// Scroll'u en alta getir (row 0 görünsün)
    /// </summary>
    public void ScrollToBottom()
    {
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    }

    /// <summary>
    /// Scroll'u en yukarı getir (boss row görünsün)
    /// </summary>
    public void ScrollToTop()
    {
        if (content == null || viewport == null) return;
        float maxScroll = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, maxScroll);
    }
}
