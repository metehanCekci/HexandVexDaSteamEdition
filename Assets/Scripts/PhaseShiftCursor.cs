using UnityEngine;

/// <summary>
/// PhaseShift hedefleme modunda özel cursor gösterir.
/// cursorTexture alanına istediğiniz PNG'yi atın, hotspot otomatik ortalanır.
/// </summary>
public class PhaseShiftCursor : MonoBehaviour
{
    public static PhaseShiftCursor instance;

    [Header("Cursor Görseli (PNG atın)")]
    [Tooltip("PhaseShift hedefleme sırasında görünecek ikon. İstediğiniz zaman değiştirin.")]
    public Texture2D cursorTexture;

    [Header("Hotspot")]
    [Tooltip("Cursor'un tıklama noktası. (0,0) sol üst köşe. Boş bırakırsanız otomatik ortalanır.")]
    public Vector2 hotspotOverride = Vector2.zero;
    public bool autoCenter = true;

    private bool isCustomCursorActive = false;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        bool shouldBeActive = TurnManager.instance != null && TurnManager.instance.isPhaseShiftTargeting;

        if (shouldBeActive && !isCustomCursorActive)
            ActivateCursor();
        else if (!shouldBeActive && isCustomCursorActive)
            DeactivateCursor();
    }

    private void ActivateCursor()
    {
        Vector2 hotspot = (cursorTexture != null && autoCenter)
            ? new Vector2(cursorTexture.width / 2f, cursorTexture.height / 2f)
            : hotspotOverride;
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
        isCustomCursorActive = true;
    }

    private void DeactivateCursor()
    {
        // Hand control back to CursorManager (project-wide custom cursor) instead of the OS default.
        if (CursorManager.instance != null)
            CursorManager.instance.SetDefault();
        else
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        isCustomCursorActive = false;
    }

    void OnDisable()
    {
        if (isCustomCursorActive)
            DeactivateCursor();
    }
}
