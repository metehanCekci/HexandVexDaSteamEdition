using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SacrificeTubeDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image highlightImage;
    private Color normalColor;
    private Color hoverColor = new Color(0.2f, 0.8f, 0.3f, 0.2f);

    void Start()
    {
        if (highlightImage != null)
            normalColor = highlightImage.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightImage != null)
            highlightImage.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (SacrificeNodeManager.instance != null)
            SacrificeNodeManager.instance.RemoveLastPerkFromTube();
    }
}
