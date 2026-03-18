using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Skip butonuna ekle. Basılı tutunca sürekli skip atar.
/// </summary>
public class SkipButtonHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (TurnManager.instance != null)
            TurnManager.instance.holdingSkip = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (TurnManager.instance != null)
            TurnManager.instance.holdingSkip = false;
    }

    void OnDisable()
    {
        if (TurnManager.instance != null)
            TurnManager.instance.holdingSkip = false;
    }
}
