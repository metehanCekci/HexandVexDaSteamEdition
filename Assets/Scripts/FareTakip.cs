using UnityEngine;
using UnityEngine.Tilemaps;

public class FareTakip : MonoBehaviour
{
    [Header("Bağlantılar")]
    [Tooltip("Karakterin hareket edebileceği yerleri gösteren Highlight Tilemap'i")]
    public Tilemap highlightTilemap; 
    
    [Tooltip("Zıplayıp dalgalanan sahte altıgen objemiz")]
    public VurgulayiciEfekt sahteAltigenScripti; 
    
    private Camera anaKamera;

    void Start()
    {
        anaKamera = Camera.main;
    }

    void Update()
    {
        // 1. Farenin oyun dünyasındaki yerini nokta atışı bul
        Vector3 mauseEkrandakiYeri = Input.mousePosition;
        mauseEkrandakiYeri.z = Mathf.Abs(anaKamera.transform.position.z); 
        Vector3 fareninDunyaYeri = anaKamera.ScreenToWorldPoint(mauseEkrandakiYeri);
        fareninDunyaYeri.z = 0; 

        // 2. Farenin bu konumu, Highlight Tilemap'inin hangi hücresine denk geliyor?
        Vector3Int hucreKordinati = highlightTilemap.WorldToCell(fareninDunyaYeri);
        
        // 3. KESİN KONTROL: Highlight Tilemap bu hücreye bir "hareket edilebilir" işareti çizmiş mi?
        if (highlightTilemap.HasTile(hucreKordinati))
        {
            // Eğer çizmişse, demek ki burası mesafenin yettiği, dikensiz ve temiz bir kare!
            sahteAltigenScripti.gameObject.SetActive(true);
            
            // Highlight hücresinin tam merkezini al ve efektimizi oraya yerleştir
            Vector3 hucreninTamMerkezi = highlightTilemap.GetCellCenterWorld(hucreKordinati);
            sahteAltigenScripti.YeniAltigeneIsinlan(hucreninTamMerkezi);
        }
        else
        {
            // Eğer orada bir highlight (vurgu) yoksa, yani karakter gidemiyorsa efekti gizle
            sahteAltigenScripti.gameObject.SetActive(false);
        }
    }
}
