using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Haritayı sürükle: sağ tık / orta tuş ile pan (X+Y) — sınırlı.
/// Mouse wheel ile dikey hareket.
/// Sol tık node butonlarına gider.
/// </summary>
public class MapDragScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [HideInInspector] public RectTransform content;
    [HideInInspector] public RectTransform viewport;

    [Header("Scroll Ayarları — Inspector'dan değiştir")]
    public float scrollSpeed = 60f;
    public float dragSpeed = 1f;

    [Header("Sınırlar (padding ekstra boşluk)")]
    public float paddingX = 200f;  // Yatay sınır: node'ların ötesinde ne kadar boşluk
    public float paddingY = 150f;  // Dikey sınır: node'ların ötesinde ne kadar boşluk

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
            content.anchoredPosition = ClampPosition(newPos);
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
        content.anchoredPosition = ClampPosition(pos);
    }

    private Vector2 ClampPosition(Vector2 pos)
    {
        if (content == null || viewport == null) return pos;

        float contentH = content.rect.height;
        float contentW = content.rect.width;
        float viewportH = viewport.rect.height;
        float viewportW = viewport.rect.width;

        // Content pivot=top: y=0 → üst kenar viewport üstünde
        // y pozitif → content aşağı kayar → alt node'lar görünür
        float minY = -paddingY;
        float maxY = Mathf.Max(0f, contentH - viewportH) + paddingY;

        // X: content merkezi viewport merkezinde, sağa/sola kayma sınırı
        float maxX = contentW / 2f + paddingX;
        float minX = -contentW / 2f - paddingX;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
    }

    /// <summary>
    /// Belirli bir Y pozisyonunu viewport ortasına getir.
    /// nodeY: container içindeki node'un anchoredPosition.y değeri (negatif).
    /// </summary>
    public void CenterOnY(float nodeY)
    {
        if (content == null || viewport == null) return;
        // nodeY negatif (üstten aşağı). Viewport ortasına getirmek için:
        // content.y = -nodeY - viewportH/2
        float viewportH = viewport.rect.height;
        float targetY = -nodeY - viewportH / 2f;
        Vector2 pos = new Vector2(0f, targetY);
        content.anchoredPosition = ClampPosition(pos);
    }
}
