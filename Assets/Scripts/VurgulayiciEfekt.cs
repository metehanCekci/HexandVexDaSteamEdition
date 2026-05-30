using UnityEngine;

public class VurgulayiciEfekt : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Ne kadar yukarı aşağı gidecek?")]
    public float dalgalanmaMiktari = 0.1f; 
    
    [Tooltip("Ne kadar hızlı dalgalanacak?")]
    public float hiz = 5f; 
    
    [Tooltip("Ne kadar büyüyüp küçülecek?")]
    public float buyumeMiktari = 0.05f; 

    private Vector3 gercekPozisyon; // Objenin ışınlandığı merkez nokta
    private Vector3 normalBoyut;    // Objenin ilk baştaki kendi boyutu

    void Awake()
    {
        // Oyun başlarken objenin orijinal boyutunu aklında tutar
        normalBoyut = transform.localScale;
    }

    void Update()
    {
        // Zamanla akıp giden bir dalga matematiği oluştururuz (Deniz dalgası gibi)
        float dalga = Mathf.Sin(Time.time * hiz);

        // 1. ADIM: Y ekseninde (yukarı/aşağı) dalgalanma
        // Gerçek pozisyonun üzerine dalgamızı ekliyoruz
        transform.position = gercekPozisyon + new Vector3(0, dalga * dalgalanmaMiktari, 0);

        // 2. ADIM: Büyüyüp Küçülme
        // Orijinal boyutun üzerine ufak bir dalga ekleyip çıkarıyoruz
        float yeniBuyukluk = 1f + (dalga * buyumeMiktari);
        transform.localScale = normalBoyut * yeniBuyukluk;
    }

    // FareTakip kodu bu fonksiyona "Şuraya git" diyecek
    public void YeniAltigeneIsinlan(Vector3 yeniYer)
    {
        gercekPozisyon = yeniYer;
    }
}
