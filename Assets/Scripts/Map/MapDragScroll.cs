using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Haritayı sürükle: sağ tık / orta tuş ile pan (X+Y).
/// Mouse wheel ile dikey hareket.
/// Sol tık node butonlarına gider.
/// Sınırlar sabit pixel — MapManager'dan ayarlanır.
/// </summary>
public class MapDragScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [HideInInspector] public RectTransform content;
    [HideInInspector] public RectTransform viewport;

    [Header("Scroll Ayarları")]
    public float scrollSpeed = 60f;
    public float dragSpeed = 1f;

    [Header("Pan Sınırları (pixel)")]
    public float minX = -600f;
    public float maxX = 600f;
    public float minY = -300f;
    public float maxY = 2000f;

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

            Vector2 newPos = content.anchoredPosition + delta * dragSpeed;
            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
            content.anchoredPosition = newPos;
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
        Vector2 pos = content.anchoredPosition;
        pos.y += eventData.scrollDelta.y * scrollSpeed;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// Belirli bir Y pozisyonunu ekran ortasına getir.
    /// </summary>
    public void CenterOnY(float nodeY)
    {
        if (content == null || viewport == null) return;
        float viewportH = viewport.rect.height;
        if (viewportH <= 0) viewportH = Screen.height * 0.8f;

        // Node'u ekranın alt kısmına getir (alttan %20 yukarıda)
        // Oyuncu aşağıdan yukarı ilerliyor, seçenekler altta görünsün
        float targetY = -nodeY - viewportH * 0.8f;
        targetY = Mathf.Clamp(targetY, minY, maxY);

        Debug.Log($"[MAP SCROLL] CenterOnY: nodeY={nodeY}, viewportH={viewportH}, targetY={targetY}, content.pos={content.anchoredPosition}");

        content.anchoredPosition = new Vector2(0f, targetY);
    }
}
