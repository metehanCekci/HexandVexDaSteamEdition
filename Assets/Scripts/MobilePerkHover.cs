using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Bu script mobildeki iki önemli sorunu çözer:
/// 1) Perklere basılı tutunca okuma (hover), kısa tıklayınca seçme yapar.
/// 2) Haritada kamerayı kaydırırken yanlışlıkla tıklanmasını engeller.
/// </summary>
public class MobilePerkHover : MonoBehaviour
{
    [Header("Dokunma Ayarları")]
    [Tooltip("Özelliğin okunması için kaç saniye basılı tutulmalı?")]
    public float basiliTutmaSuresi = 0.3f; 

    private float dokunmaZamanlayicisi = 0f;
    private bool ozellikOkunuyorMu = false;
    private GameObject dokunulanObje = null;

    void Start()
    {
        // 1. SORUNUN ÇÖZÜMÜ (HARİTADA KAYDIRIRKEN TIKLAMAYI ENGELLEME):
        // Oyunun parmak kaydırma toleransını ekranın hassasiyetine göre artırıyoruz.
        // Böylece parmağını kaydırdığında Unity bunu "tıklama" olarak algılamaz.
        if (EventSystem.current != null)
        {
            float ekranHassasiyeti = Screen.dpi;
            if (ekranHassasiyeti <= 0) ekranHassasiyeti = 160f; 

            int yeniSinir = (int)(ekranHassasiyeti * 0.15f); 
            EventSystem.current.pixelDragThreshold = Mathf.Max(EventSystem.current.pixelDragThreshold, yeniSinir);
        }
    }

    void Update()
    {
        // Ekrana ilk dokunulduğu an
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            dokunmaZamanlayicisi = 0f;
            ozellikOkunuyorMu = false;
            dokunulanObje = EkrandaDokunulanObjeyiBul();
        }

        // Ekrana basılı tutulmaya devam ediliyorsa
        if (Input.GetMouseButton(0) || (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary)))
        {
            if (dokunulanObje != null)
            {
                dokunmaZamanlayicisi += Time.deltaTime;

                // Eğer parmağımızı ayarladığımız süreden (0.3 sn) fazla tuttuysak ve özellik daha açılmadıysa
                if (dokunmaZamanlayicisi >= basiliTutmaSuresi && !ozellikOkunuyorMu)
                {
                    ozellikOkunuyorMu = true;
                    
                    // Bilgisayardaki fareyi üstüne getirme (Hover) olayını çalıştır
                    ExecuteEvents.Execute(dokunulanObje, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                }
            }
        }

        // Parmak ekrandan çekildiğinde
        if (Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(0).phase == TouchPhase.Canceled)))
        {
            if (dokunulanObje != null)
            {
                // Eğer uzun basıldıysa (özellik okunduysa)
                if (ozellikOkunuyorMu)
                {
                    // Parmağı çekince özelliği gizle
                    ExecuteEvents.Execute(dokunulanObje, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                    
                    // OYUNUN YANLIŞLIKLA SEÇMESİNİ ENGELLEMEK İÇİN HİLE:
                    // Parmağımızı çektiğimizde oyunun bunu "tıklama" sanmaması için butonu saniyenin binde biri kadar bir süreliğine etkisiz hale getiriyoruz.
                    UnityEngine.UI.Button buton = dokunulanObje.GetComponentInParent<UnityEngine.UI.Button>();
                    if (buton != null)
                    {
                        StartCoroutine(ButonuGeciciKapat(buton));
                    }
                }
                else
                {
                    // Kısa tıklandıysa: Önce hover kapat, sonra SEÇME işlemini gerçekleştir
                    ExecuteEvents.Execute(dokunulanObje, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                    ExecuteEvents.Execute(dokunulanObje, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                }
            }

            // Her şey bittiğinde değerleri sıfırla
            dokunulanObje = null;
            ozellikOkunuyorMu = false;
            dokunmaZamanlayicisi = 0f;
        }
    }

    // Butonu anlık kapatıp açan yardımcı komut
    private System.Collections.IEnumerator ButonuGeciciKapat(UnityEngine.UI.Button btn)
    {
        bool eskiDurum = btn.interactable;
        btn.interactable = false;
        yield return new WaitForEndOfFrame();
        btn.interactable = eskiDurum;
    }

    // Parmağın tam olarak hangi arayüz nesnesine dokunduğunu bulur
    private GameObject EkrandaDokunulanObjeyiBul()
    {
        PointerEventData isaretciVerisi = new PointerEventData(EventSystem.current);
        isaretciVerisi.position = Input.mousePosition; 

        List<RaycastResult> sonuclar = new List<RaycastResult>();
        EventSystem.current.RaycastAll(isaretciVerisi, sonuclar);

        if (sonuclar.Count > 0)
        {
            return sonuclar[0].gameObject;
        }
        return null;
    }
}
