using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    public float panSpeed = 15f; 

    [Header("Zoom Ayarları (Perspektif)")]
    public float zoomSpeed = 25f;
    public float minZoomZ = -5f;
    public float maxZoomZ = -25f;

    [Header("Kamera Sınırları")]
    public Vector2 minBounds = new Vector2(-60f, -60f); 
    public Vector2 maxBounds = new Vector2(60f, 60f);   

    [Header("Mobil Hassasiyet Ayarları")]
    public float touchPanSpeed = 0.05f;
    public float touchZoomSpeed = 0.02f;

    private Camera cam;
    private Vector3 dragOrigin;
    
    // Shake offset
    private Vector3 shakeOffset = Vector3.zero;
    private float shakeTimer = 0f;
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0f;
    private Vector3 targetPosition = Vector3.zero;

    void Start()
    {
        cam = Camera.main;
        targetPosition = transform.position;
        
        if (cam.orthographic) 
        {
            cam.orthographic = false;
        }
    }

    void LateUpdate()
    {
        // Map açıkken kamera sabit kalsın
        if (MapManager.instance != null && MapManager.instance.mapUI != null && MapManager.instance.mapUI.IsMapVisible())
            return;

        // Pause sırasında kamera girişi ve shake dursun
        if (PauseManager.isPaused)
        {
            shakeTimer = 0f;
            shakeOffset = Vector3.zero;
            transform.position = targetPosition;
            return;
        }

        // Cihaz kontrolü: PC mi Mobil mi?
        if (!Application.isMobilePlatform)
        {
            HandleKeyboardPan();
            HandleMouseDragPan();
            HandleZoom();
        }
        else
        {
            HandleMobileInput();
        }

        ClampCameraPosition();
        UpdateShake();
    }

    private void HandleKeyboardPan()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(x, y, 0).normalized * panSpeed * Time.deltaTime;
        targetPosition += move;
    }

    private void HandleMouseDragPan()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) 
        {
            dragOrigin = GetMouseWorldPosition();
        }

        if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            Vector3 currentMousePos = GetMouseWorldPosition();
            Vector3 difference = dragOrigin - currentMousePos;
            targetPosition += difference;
        }
    }

    private void HandleMobileInput()
    {
        // Tek Parmak (Kaydırma)
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                targetPosition.x -= touch.deltaPosition.x * touchPanSpeed;
                targetPosition.y -= touch.deltaPosition.y * touchPanSpeed;
            }
        }
        // İki Parmak (Yakınlaştırma / Uzaklaştırma)
        else if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0.position - touch1.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            targetPosition.z += difference * touchZoomSpeed;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(cam.transform.position.z); 
        return cam.ScreenToWorldPoint(mousePos);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0.0f)
        {
            Vector3 pos = targetPosition;
            pos.z += scroll * zoomSpeed;
            targetPosition = pos;
        }
    }

    private void UpdateShake()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float progress = 1f - (shakeTimer / shakeDuration);
            float falloff = 1f - (progress * progress);
            shakeOffset = Random.insideUnitSphere * shakeMagnitude * falloff;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        transform.position = targetPosition + shakeOffset;
    }

    private void ClampCameraPosition()
    {
        float clampedX = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
        float clampedY = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        float clampedZ = Mathf.Clamp(targetPosition.z, maxZoomZ, minZoomZ);
        
        targetPosition = new Vector3(clampedX, clampedY, clampedZ);
    }

    public void Shake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    public static void ShakeLight()
    {
        CameraController controller = FindFirstObjectByType<CameraController>();
        if (controller != null)
            controller.Shake(0.1f, 0.075f);
    }

    public static void ShakeLighter()
    {
        CameraController controller = FindFirstObjectByType<CameraController>();
        if (controller != null)
            controller.Shake(0.1f, 0.0375f);
    }
}
