using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ScrollRect'i sadece orta tuş / sağ tık ile drag'a izin verecek şekilde override eder.
/// Sol tık drag'ı devre dışı bırakır (buton tıklamaları çalışsın diye).
/// Mouse wheel scroll da çalışır.
/// </summary>
public class MapDragScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private ScrollRect scrollRect;
    private bool isDragging = false;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        // ScrollRect'in kendi drag handling'ini devre dışı bırak
        if (scrollRect != null)
            scrollRect.enabled = false;
    }

    void Update()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        // Orta tuş veya sağ tuş basılıyken drag
        bool middleHeld = Input.GetMouseButton(2);
        bool rightHeld = Input.GetMouseButton(1);

        if ((middleHeld || rightHeld) && isDragging)
        {
            float deltaY = Input.GetAxis("Mouse Y") * scrollRect.scrollSensitivity * 10f;
            Vector2 pos = scrollRect.content.anchoredPosition;
            pos.y -= deltaY;

            // Clamp
            float contentH = scrollRect.content.rect.height;
            float viewportH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
            float maxY = Mathf.Max(0f, contentH - viewportH);
            pos.y = Mathf.Clamp(pos.y, 0f, maxY);

            scrollRect.content.anchoredPosition = pos;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Sadece orta/sağ tuşa izin ver
        if (eventData.button == PointerEventData.InputButton.Middle ||
            eventData.button == PointerEventData.InputButton.Right)
        {
            isDragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (scrollRect == null || scrollRect.content == null) return;

        float deltaY = eventData.delta.y;
        Vector2 pos = scrollRect.content.anchoredPosition;
        pos.y -= deltaY;

        float contentH = scrollRect.content.rect.height;
        float viewportH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        float maxY = Mathf.Max(0f, contentH - viewportH);
        pos.y = Mathf.Clamp(pos.y, 0f, maxY);

        scrollRect.content.anchoredPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        // Mouse wheel scroll — her zaman çalışsın
        if (scrollRect == null || scrollRect.content == null) return;

        float scrollDelta = eventData.scrollDelta.y * scrollRect.scrollSensitivity;
        Vector2 pos = scrollRect.content.anchoredPosition;
        pos.y -= scrollDelta;

        float contentH = scrollRect.content.rect.height;
        float viewportH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        float maxY = Mathf.Max(0f, contentH - viewportH);
        pos.y = Mathf.Clamp(pos.y, 0f, maxY);

        scrollRect.content.anchoredPosition = pos;
    }
}
