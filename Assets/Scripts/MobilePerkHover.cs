using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Tüm perkleri, market eşyalarını ve butonları TEK BİR SCRIPT ile mobile uyumlu yapar.
/// Bunu sahnendeki 'EventSystem' objesine atman yeterli.
/// Mevcut ShopSlot, ActivePerkBar vb. kodlarına DOKUNMANA GEREK YOK.
/// </summary>
public class MobileHoverAutoFixer : MonoBehaviour
{
    private PointerEventData pointerData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    
    // Ekranda o an parmağın altında kalan ve bilgi ekranı açılan objelerin hafızası
    private HashSet<GameObject> currentlyHoveredObjects = new HashSet<GameObject>();

    void Start()
    {
        if (EventSystem.current != null)
        {
            pointerData = new PointerEventData(EventSystem.current);
        }
    }

    void Update()
    {
        // Eğer cihaz bilgisayarsa (fare kullanılıyorsa) bu kod oyununu hiç etkilemesin
        if (!Application.isMobilePlatform) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            if (pointerData == null && EventSystem.current != null) 
                pointerData = new PointerEventData(EventSystem.current);
                
            pointerData.position = touch.position;

            // DURUM 1: Parmak ekrana değdiğinde veya kaydırıldığında (Basılı tutma)
            if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                raycastResults.Clear();
                EventSystem.current.RaycastAll(pointerData, raycastResults);

                // Dokunduğumuz ve arayüzde altından geçtiğimiz her UI objesini hafızaya al
                foreach (RaycastResult result in raycastResults)
                {
                    if (result.gameObject != null)
                    {
                        currentlyHoveredObjects.Add(result.gameObject);
                    }
                }
            }
            // DURUM 2: Parmağı ekrandan tam çektiği an (Bilgi penceresini kapatma anı)
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                // Hafızadaki her şeye "Fare üzerinden çekildi (PointerExit)" sinyali yollayarak pencereleri kapattır
                foreach (GameObject obj in currentlyHoveredObjects)
                {
                    if (obj != null)
                    {
                        ExecuteEvents.ExecuteHierarchy(obj, pointerData, ExecuteEvents.pointerExitHandler);
                    }
                }
                
                // İşlem bitti, hafızayı temizle
                currentlyHoveredObjects.Clear();
                
                // Unity'nin mobil bug'ını (butonu seçili bırakmasını) engellemek için seçimi sıfırla
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }
    }
}
