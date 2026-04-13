using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager instance;

    [Header("Cursor Textures")]
    public Texture2D defaultCursor;
    public Texture2D clickCursor;

    [Header("Hotspot (imlecin tıklama noktası)")]
    public Vector2 hotspot = Vector2.zero;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        SetDefault();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            SetClick();
        else if (Input.GetMouseButtonUp(0))
            SetDefault();
    }

    public void SetDefault()
    {
        if (defaultCursor != null)
            Cursor.SetCursor(defaultCursor, hotspot, CursorMode.Auto);
    }

    public void SetClick()
    {
        if (clickCursor != null)
            Cursor.SetCursor(clickCursor, hotspot, CursorMode.Auto);
        else
            SetDefault();
    }
}
