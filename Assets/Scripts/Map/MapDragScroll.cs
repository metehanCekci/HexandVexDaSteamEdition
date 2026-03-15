using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect'in sol tık sürüklemesini engeller — sadece sağ tık ve orta tuşla pan.
/// Mouse wheel desteği ScrollRect'ten gelir.
/// Bu component ScrollRect'in yanına eklenir.
/// </summary>
public class MapDragScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scrollRect;
    private bool isDraggingWithRightOrMiddle = false;

    // Sürükleme sırasında sol tık event'lerini yutmak için
    private bool blockLeftDrag = false;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    // Sol tık drag'ı engelle — ScrollRect'in OnBeginDrag'ını çağırma
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            blockLeftDrag = true;
            eventData.pointerDrag = null; // ScrollRect'e iletme
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (blockLeftDrag) return;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        blockLeftDrag = false;
    }

    void Update()
    {
        if (scrollRect == null) return;

        // Sağ tık veya orta tuş ile sürükleme
        bool rightDown = Input.GetMouseButton(1);
        bool middleDown = Input.GetMouseButton(2);

        if ((rightDown || middleDown) && !isDraggingWithRightOrMiddle)
        {
            isDraggingWithRightOrMiddle = true;
        }

        if (isDraggingWithRightOrMiddle)
        {
            if (!rightDown && !middleDown)
            {
                isDraggingWithRightOrMiddle = false;
            }
            else
            {
                // Mouse delta ile content'i hareket ettir
                float dx = Input.GetAxis("Mouse X") * 15f;
                float dy = Input.GetAxis("Mouse Y") * 15f;

                var content = scrollRect.content;
                if (content != null)
                {
                    Vector2 pos = content.anchoredPosition;
                    pos.x += dx;
                    pos.y += dy;
                    content.anchoredPosition = pos;
                }
            }
        }
    }

    /// <summary>
    /// Belirli bir node Y pozisyonunu ekranın ortasına getir.
    /// Container pivot=bottom olduğu için nodeY pozitif.
    /// ScrollRect normalizedPosition kullanarak scroll eder.
    /// </summary>
    public void ScrollToNodeY(float nodeY, bool center)
    {
        if (scrollRect == null || scrollRect.content == null) return;

        float contentH = scrollRect.content.rect.height;
        float viewportH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : Screen.height;
        if (viewportH <= 0) viewportH = Screen.height * 0.8f;

        float scrollableH = contentH - viewportH;
        if (scrollableH <= 0)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        // normalizedPosition: 0 = en alt (row 0), 1 = en üst (boss)
        // nodeY viewport'un alt kenarından bu kadar yukarıda olsun:
        float offset = center ? viewportH * 0.5f : viewportH * 0.2f;
        float targetScrollY = (nodeY - offset) / scrollableH;
        targetScrollY = Mathf.Clamp01(targetScrollY);

        scrollRect.verticalNormalizedPosition = targetScrollY;
        Debug.Log($"[SCROLL] ScrollToNodeY: nodeY={nodeY} contentH={contentH} viewportH={viewportH} normalized={targetScrollY}");
    }

    /// <summary>En alta scroll et (row 0 görünsün).</summary>
    public void ScrollToBottom()
    {
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>En üste scroll et (boss görünsün).</summary>
    public void ScrollToTop()
    {
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
