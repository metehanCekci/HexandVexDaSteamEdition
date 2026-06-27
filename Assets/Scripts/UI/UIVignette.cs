using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Her zaman ekranda görünen UI-tabanlı vignette efekti.
/// ScreenSpaceOverlay canvas'ların üstünde çalışır — post-processing'den bağımsız.
/// </summary>
public class UIVignette : MonoBehaviour
{
    public static UIVignette instance;

    [Header("Vignette Settings")]
    [Range(0f, 1f)] public float intensity = 0.55f;
    [Range(0.1f, 1f)] public float smoothness = 0.7f;

    private RawImage vignetteImage;
    private Canvas vignetteCanvas;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BuildVignetteUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void BuildVignetteUI()
    {
        // Canvas — tüm overlay UI'ların üstünde
        GameObject canvasGO = new GameObject("UIVignetteCanvas");
        canvasGO.transform.SetParent(transform);
        vignetteCanvas = canvasGO.AddComponent<Canvas>();
        vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        vignetteCanvas.sortingOrder = 9999; // En üstte

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Raycast almayacak — tıklamaları engellemesin
        // GraphicRaycaster eklemeyin!

        // Vignette image — tam ekran
        GameObject imgGO = new GameObject("VignetteImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = imgGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        vignetteImage = imgGO.AddComponent<RawImage>();
        vignetteImage.raycastTarget = false;
        vignetteImage.texture = GenerateVignetteTexture(512, intensity, smoothness);
    }

    /// <summary>
    /// Radial gradient vignette texture'u procedural olarak oluşturur.
    /// </summary>
    private static Texture2D GenerateVignetteTexture(int size, float vignetteIntensity, float vignetteSmoothness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float halfSize = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Normalize to -1..1
                float nx = (x - halfSize) / halfSize;
                float ny = (y - halfSize) / halfSize;

                // Eliptik mesafe (ekran oranını hesaba katmak için hafif genişlet)
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                // Vignette falloff — smoothness ile kenar yumuşaklığı
                float innerRadius = 1f - vignetteSmoothness;
                float alpha = Mathf.SmoothStep(0f, vignetteIntensity, (dist - innerRadius) / (1f - innerRadius));
                alpha = Mathf.Clamp01(alpha);

                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Runtime'da intensity'yi değiştirmek için.
    /// </summary>
    public void SetIntensity(float newIntensity)
    {
        intensity = newIntensity;
        if (vignetteImage != null)
            vignetteImage.texture = GenerateVignetteTexture(512, intensity, smoothness);
    }
}
