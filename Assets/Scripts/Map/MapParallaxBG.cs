using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Map scroll'una bağlı parallax arkaplan.
/// ScrollRect hareket ettikçe arkaplanın UV offset'i daha yavaş kayar.
/// Tiling ile tekrarlanan bir doku kullanır.
/// </summary>
public class MapParallaxBG : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RawImage bgImage;

    [Header("Parallax")]
    public float parallaxFactor = 0.3f; // 0 = sabit, 1 = scroll ile aynı hız
    public float tileScaleX = 16f;      // Yatay tekrar sayısı
    public float tileScaleY = 9f;       // Dikey tekrar sayısı (16:9 oran)

    private float contentHeight;
    private float viewportHeight;

    void Start()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScroll);

        UpdateSizes();
        // İlk frame'de canvas/RectTransform henüz layout edilmemiş olabilir;
        // doğru tile boyutu için bir frame bekleyip uvRect'i zorla uygula.
        StartCoroutine(ApplyInitialTilingNextFrame());
    }

    private IEnumerator ApplyInitialTilingNextFrame()
    {
        // Canvas layout'u ve RectTransform boyutları güncellensin
        yield return null;
        yield return new WaitForEndOfFrame();

        UpdateSizes();
        ApplyCurrentTiling();
    }

    /// <summary>
    /// OnScroll ile aynı uvRect hesabını, mevcut scroll pozisyonu için uygular.
    /// Kullanıcı etkileşimi olmadan (ilk açılışta) doğru tile boyutunu garanti eder.
    /// </summary>
    private void ApplyCurrentTiling()
    {
        if (bgImage == null) return;

        float normY = 0f;
        if (scrollRect != null)
            normY = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);

        float yOffset = normY * parallaxFactor;
        bgImage.uvRect = new Rect(0f, yOffset, tileScaleX, tileScaleY);
    }

    void OnDestroy()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScroll);
    }

    private void UpdateSizes()
    {
        if (scrollRect == null || scrollRect.content == null) return;
        contentHeight = scrollRect.content.rect.height;
        viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
    }

    private void OnScroll(Vector2 normalizedPos)
    {
        if (bgImage == null) return;

        // normalizedPos.y: 0 = en alt, 1 = en üst
        // UV offset'i parallax faktörü ile kaydır
        float scrollRange = contentHeight - viewportHeight;
        if (scrollRange <= 0f) return;

        float yOffset = normalizedPos.y * parallaxFactor;
        bgImage.uvRect = new Rect(0f, yOffset, tileScaleX, tileScaleY);
    }

    /// <summary>
    /// Content boyutu değiştiğinde çağır (harita yeniden oluşturulduğunda).
    /// </summary>
    public void Refresh()
    {
        UpdateSizes();
        ApplyCurrentTiling();
    }
}
