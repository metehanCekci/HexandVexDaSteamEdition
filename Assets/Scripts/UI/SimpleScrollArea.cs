using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Basit scroll alani — RectMask2D ile kirpma, mouse wheel ile kaydirma.
/// ScrollRect kullanmadan calisir, layout sistemiyle catismaz.
/// </summary>
public class SimpleScrollArea : MonoBehaviour, IScrollHandler
{
    public RectTransform content;
    public float scrollSpeed = 30f;

    private RectTransform viewportRT;
    private float scrollOffset;

    void Awake()
    {
        viewportRT = GetComponent<RectTransform>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null || viewportRT == null) return;

        float contentH = content.rect.height;
        float viewportH = viewportRT.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);

        if (maxScroll <= 0f) return;

        scrollOffset -= eventData.scrollDelta.y * scrollSpeed;
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, scrollOffset);
    }

    public void ResetScroll()
    {
        scrollOffset = 0f;
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    }
}
