using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Haritayı sürükle: sağ tık / orta tuş ile pan (X+Y).
/// Mouse wheel ile dikey hareket.
/// Sol tık node butonlarına gider.
///
/// Container pivot=top, anchor=top demek:
///   content.y = 0  → container üstü = viewport üstü → boss görünür
///   content.y < 0  → container yukarı kayar → alt kısım (row 0) görünür
///   content.y > 0  → container aşağı kayar → üstün üstü (boşluk) görünür
/// Yani row 0'ı görmek için content.y NEGATİF olmalı.
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
    public float minY = -1500f;  // Negatif = aşağıyı gösterebilir
    public float maxY = 200f;    // Pozitif = üstün biraz üstü

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
    /// Scroll sınırlarını harita boyutuna göre otomatik ayarla.
    /// Container height bilindiğinde çağrılmalı.
    /// </summary>
    public void UpdateLimitsForMapHeight(float containerHeight)
    {
        float viewportH = viewport != null ? viewport.rect.height : Screen.height * 0.8f;
        if (viewportH <= 0) viewportH = Screen.height * 0.8f;

        // content.y = 0 → boss (üst) görünür
        // content.y = -(containerHeight - viewportH) → row 0 (alt) görünür
        maxY = 100f; // Boss'un biraz üstüne kadar scroll edilebilsin
        minY = -(containerHeight - viewportH + 100f); // Row 0'ın biraz altına kadar
        if (minY > maxY) minY = maxY - 100f; // Güvenlik

        Debug.Log($"[SCROLL] UpdateLimits: containerH={containerHeight} viewportH={viewportH} minY={minY} maxY={maxY}");
    }

    /// <summary>
    /// Node'u ekranın ortasına getir (bölüm bittikten sonra).
    /// Container pivot=top: nodeY negatif. Ekran ortasında görmek için:
    /// content.y = nodeY + viewportH/2
    /// </summary>
    public void CenterOnY(float nodeY)
    {
        if (content == null || viewport == null) return;
        float viewportH = viewport.rect.height;
        if (viewportH <= 0) viewportH = Screen.height * 0.8f;

        // nodeY negatif (ör: -800). Ortada görmek için container'ı yukarı çek.
        // content.y = nodeY + viewportH/2 → negatif + pozitif
        float targetY = nodeY + viewportH / 2f;
        targetY = Mathf.Clamp(targetY, minY, maxY);
        Debug.Log($"[SCROLL] CenterOnY nodeY={nodeY} viewportH={viewportH} targetY={targetY}");
        content.anchoredPosition = new Vector2(0f, targetY);
    }

    /// <summary>
    /// Node'u ekranın alt kısmına getir (ilk açılış — row 0 altta görünmeli).
    /// content.y = nodeY + viewportH * 0.8  (node'u ekranın %80'ine koy, altta)
    /// </summary>
    public void ShowAtBottom(float nodeY)
    {
        if (content == null || viewport == null) return;
        float viewportH = viewport.rect.height;
        if (viewportH <= 0) viewportH = Screen.height * 0.8f;

        // Row 0 ekranın altında olsun — %80 aşağıda
        float targetY = nodeY + viewportH * 0.8f;
        targetY = Mathf.Clamp(targetY, minY, maxY);
        Debug.Log($"[SCROLL] ShowAtBottom nodeY={nodeY} viewportH={viewportH} targetY={targetY}");
        content.anchoredPosition = new Vector2(0f, targetY);
    }
}
