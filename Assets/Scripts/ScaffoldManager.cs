using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Scaffold (İskele) Sistemi — ITrapTile uyumlu, event-driven, state-machine tabanlı.
///
/// Mekanik:
///   1. Entity (oyuncu/düşman) scaffold üzerine basınca → State: Triggered → Titreşim başlar.
///   2. Entity scaffold'dan ayrılınca → State: Collapsing → Çökme animasyonu oynar.
///   3. Çökme bitince → State: Destroyed → Tile kaldırılır, boşluk kalır.
///
/// Evrensel: Player/Enemy fark etmez, aynı OnEntityEnter/OnEntityLeave çağrıları ile çalışır.
/// </summary>
public class ScaffoldManager : MonoBehaviour
{
    public static ScaffoldManager instance;

    [Header("Scaffold Ayarları")]
    [Tooltip("Çökme animasyonu süresi (saniye).")]
    public float collapseDuration = 0.35f;

    // ──────── State Machine: Her scaffold hücresinin durumu ────────
    private Dictionary<Vector3Int, TrapTileState> scaffoldStates = new Dictionary<Vector3Int, TrapTileState>();

    // Aktif scaffold'lar: hücre -> titreşim coroutine
    private Dictionary<Vector3Int, Coroutine> shakeCoroutines = new Dictionary<Vector3Int, Coroutine>();

    void Awake()
    {
        if (instance == null) instance = this;
    }

    // ──────── Public Sorgular ────────

    public bool IsScaffoldCell(Vector3Int cell)
    {
        return LevelGenerator.instance != null
            && LevelGenerator.instance.scaffoldCells != null
            && LevelGenerator.instance.scaffoldCells.Contains(cell);
    }

    public bool IsCollapsing(Vector3Int cell)
    {
        return scaffoldStates.ContainsKey(cell) && scaffoldStates[cell] == TrapTileState.Collapsing;
    }

    public bool IsDestroyed(Vector3Int cell)
    {
        return scaffoldStates.ContainsKey(cell) && scaffoldStates[cell] == TrapTileState.Destroyed;
    }

    public TrapTileState GetState(Vector3Int cell)
    {
        if (scaffoldStates.ContainsKey(cell)) return scaffoldStates[cell];
        if (IsScaffoldCell(cell)) return TrapTileState.Idle;
        return TrapTileState.Destroyed;
    }

    // ──────── Entity Etkileşimleri ────────

    /// <summary>
    /// Bir varlık (oyuncu veya düşman) scaffold üzerine bastığında çağrılır.
    /// State: Idle → Triggered. Titreşim başlar.
    /// </summary>
    public void OnEntityEnter(Vector3Int cell)
    {
        if (!IsScaffoldCell(cell)) return;

        TrapTileState currentState = GetState(cell);
        if (currentState != TrapTileState.Idle) return;

        // State geçişi: Idle → Triggered
        SetState(cell, TrapTileState.Triggered);

        // Titreşim başlat
        if (!shakeCoroutines.ContainsKey(cell))
        {
            Coroutine shake = StartCoroutine(ShakeCoroutine(cell));
            shakeCoroutines[cell] = shake;
        }

        // Event yayınla
        TrapTileEvents.FireTileTriggered(cell, TrapTileState.Triggered);
        TrapTileEvents.FireTileShakeStarted(cell);
    }

    /// <summary>
    /// Bir varlık scaffold'dan ayrıldığında çağrılır.
    /// State: Triggered → Collapsing → Destroyed.
    /// </summary>
    public void OnEntityLeave(Vector3Int cell)
    {
        TrapTileState currentState = GetState(cell);

        // Sadece Triggered state'den çökme başlatılabilir
        if (currentState != TrapTileState.Triggered) return;

        // Titreşimi durdur
        StopShakeCoroutine(cell);
        ResetTileTransform(cell);
        TrapTileEvents.FireTileShakeStopped(cell);

        // Oyuncu hâlâ scaffold üstünde mi kontrol et
        HexMovement playerMovement = TurnManager.instance?.player;
        bool playerStillOnScaffold = playerMovement != null && playerMovement.GetCurrentCellPosition() == cell;

        if (!playerStillOnScaffold)
        {
            // State geçişi: Triggered → Collapsing
            StartCoroutine(CollapseCoroutine(cell));
        }
    }

    // ──────── State Yönetimi ────────

    private void SetState(Vector3Int cell, TrapTileState newState)
    {
        scaffoldStates[cell] = newState;
    }

    // ──────── Titreşim (Shake) Coroutine ────────

    /// <summary>
    /// Süresiz tile titreşimi. State Triggered olduğu sürece devam eder.
    /// Scaffold ve background tilemap'lerini titretir.
    /// Oyuncunun transform.position'ına dokunmaz — hareket sistemini bloklamaz.
    /// </summary>
    private IEnumerator ShakeCoroutine(Vector3Int cell)
    {
        Tilemap scaffoldMap = LevelGenerator.instance.scaffoldMap;
        Tilemap backgroundMap = LevelGenerator.instance.backgroundMap;
        if (scaffoldMap == null) yield break;

        float elapsed = 0f;
        float intensity = 0.005f;
        float speed = 45f;

        while (true)
        {
            // State artık Triggered değilse (çökme başladı vs.) durdur
            if (GetState(cell) != TrapTileState.Triggered)
            {
                break;
            }

            elapsed += Time.deltaTime;

            float ox = Mathf.Sin(elapsed * speed) * intensity;
            float oy = Mathf.Cos(elapsed * speed * 1.3f) * intensity * 0.5f;

            Matrix4x4 shakeMatrix = Matrix4x4.TRS(
                new Vector3(ox, oy, 0f), Quaternion.identity, Vector3.one);

            // Scaffold tile'ı titret
            if (scaffoldMap.HasTile(cell))
            {
                scaffoldMap.SetTransformMatrix(cell, shakeMatrix);
            }

            // Alt katmanı (background) da aynı şekilde titret
            if (backgroundMap != null && backgroundMap.HasTile(cell))
            {
                backgroundMap.SetTransformMatrix(cell, shakeMatrix);
            }

            yield return null;
        }
    }

    // ──────── Çökme (Collapse) Coroutine ────────

    /// <summary>
    /// Scaffold çökme animasyonu. Tile'ları kaldırır, hücreyi boşluk yapar.
    /// State: Collapsing → Destroyed.
    /// </summary>
    private IEnumerator CollapseCoroutine(Vector3Int cell)
    {
        TrapTileState currentState = GetState(cell);
        if (currentState == TrapTileState.Collapsing || currentState == TrapTileState.Destroyed)
            yield break;

        // State geçişi: → Collapsing
        SetState(cell, TrapTileState.Collapsing);
        TrapTileEvents.FireTileCollapsing(cell);

        Tilemap scaffoldMap = LevelGenerator.instance.scaffoldMap;
        Tilemap backgroundMap = LevelGenerator.instance.backgroundMap;

        // Ses efekti — event üzerinden de dinlenebilir ama mevcut entegrasyon korunuyor
        if (AudioManager.instance != null) AudioManager.instance.PlayWall();

        float elapsed = 0f;
        while (elapsed < collapseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / collapseDuration;
            float scale = Mathf.Lerp(1f, 0f, t);
            float yOff = Mathf.Lerp(0f, -0.5f, t * t);
            Color fadeColor = new Color(1f, 1f, 1f, 1f - t);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(0f, yOff, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            if (scaffoldMap != null && scaffoldMap.HasTile(cell))
            {
                scaffoldMap.SetTransformMatrix(cell, matrix);
                scaffoldMap.SetColor(cell, fadeColor);
            }
            if (backgroundMap != null && backgroundMap.HasTile(cell))
            {
                backgroundMap.SetTransformMatrix(cell, matrix);
                backgroundMap.SetColor(cell, fadeColor);
            }

            yield return null;
        }

        // Tile'ları tamamen kaldır (scaffold + background)
        // groundMap'te tile yok — scaffold hücreleri groundTile almadan spawn oluyor
        RemoveTile(scaffoldMap, cell);
        RemoveTile(backgroundMap, cell);

        // LevelGenerator'dan scaffold kaydını sil
        if (LevelGenerator.instance != null)
            LevelGenerator.instance.scaffoldCells.Remove(cell);

        // State geçişi: Collapsing → Destroyed
        SetState(cell, TrapTileState.Destroyed);
        TrapTileEvents.FireTileDestroyed(cell);
    }

    // ──────── Yardımcı Metotlar ────────

    private void StopShakeCoroutine(Vector3Int cell)
    {
        if (shakeCoroutines.ContainsKey(cell))
        {
            if (shakeCoroutines[cell] != null)
                StopCoroutine(shakeCoroutines[cell]);
            shakeCoroutines.Remove(cell);
        }
    }

    private void ResetTileTransform(Vector3Int cell)
    {
        Tilemap scaffoldMap = LevelGenerator.instance.scaffoldMap;
        if (scaffoldMap != null && scaffoldMap.HasTile(cell))
            scaffoldMap.SetTransformMatrix(cell, Matrix4x4.identity);

        // Alt katmanın (background) da pozisyonunu sıfırla
        Tilemap backgroundMap = LevelGenerator.instance.backgroundMap;
        if (backgroundMap != null && backgroundMap.HasTile(cell))
            backgroundMap.SetTransformMatrix(cell, Matrix4x4.identity);
    }

    private void RemoveTile(Tilemap map, Vector3Int cell)
    {
        if (map != null && map.HasTile(cell))
        {
            map.SetTransformMatrix(cell, Matrix4x4.identity);
            map.SetColor(cell, Color.white);
            map.SetTile(cell, null);
        }
    }

    /// <summary>
    /// Tüm scaffold state'lerini ve coroutine'leri temizler. Level geçişlerinde çağrılır.
    /// </summary>
    public void ClearAll()
    {
        foreach (var coroutine in shakeCoroutines.Values)
            if (coroutine != null) StopCoroutine(coroutine);
        shakeCoroutines.Clear();
        scaffoldStates.Clear();
        StopAllCoroutines();
    }
}
