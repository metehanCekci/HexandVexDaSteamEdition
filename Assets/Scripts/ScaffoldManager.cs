using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Scaffold (İskele) Sistemi — event-driven.
///
/// Mekanik (basit ve kesin):
///   1. Entity (oyuncu/düşman fark etmez) scaffold üzerine basınca → Titreşim başlar.
///   2. Entity scaffold'dan ayrılınca → Scaffold anında çökmeye başlar.
///   3. Çökme bitince → Tile kaldırılır, boşluk kalır. Geri dönüş yok.
///
/// Kurallar:
///   - Kim girerse girsin, kim çıkarsa çıksın, scaffold çöker.
///   - Zaten çökmekte olan veya çökmüş scaffold'a bir şey olmaz.
/// </summary>
public class ScaffoldManager : MonoBehaviour
{
    public static ScaffoldManager instance;

    [Header("Scaffold Ayarları")]
    [Tooltip("Çökme animasyonu süresi (saniye).")]
    public float collapseDuration = 0.35f;

    // Çökmekte veya çökmüş scaffold'lar — tekrar tetiklemeyi önler
    private HashSet<Vector3Int> collapsingOrDestroyed = new HashSet<Vector3Int>();

    // Aktif titreşim coroutine'leri
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
        return collapsingOrDestroyed.Contains(cell);
    }

    // ──────── Entity Etkileşimleri ────────

    /// <summary>
    /// Herhangi bir varlık scaffold üzerine bastığında çağrılır.
    /// Titreşim başlar. Zaten çökmekte/çökmüşse hiçbir şey olmaz.
    /// </summary>
    public void OnEntityEnter(Vector3Int cell)
    {
        if (!IsScaffoldCell(cell)) return;
        if (collapsingOrDestroyed.Contains(cell)) return;

        // Titreşim başlat (zaten titreşiyorsa tekrar başlatma)
        if (!shakeCoroutines.ContainsKey(cell))
        {
            Coroutine shake = StartCoroutine(ShakeCoroutine(cell));
            shakeCoroutines[cell] = shake;
        }

        TrapTileEvents.FireTileTriggered(cell, TrapTileState.Triggered);
        TrapTileEvents.FireTileShakeStarted(cell);
    }

    /// <summary>
    /// Herhangi bir varlık scaffold'dan ayrıldığında çağrılır.
    /// Scaffold anında çökmeye başlar. Kim çıkarsa çıksın.
    /// </summary>
    public void OnEntityLeave(Vector3Int cell)
    {
        if (!IsScaffoldCell(cell)) return;
        if (collapsingOrDestroyed.Contains(cell)) return;

        // Titreşimi durdur
        StopShakeCoroutine(cell);
        ResetTileTransform(cell);
        TrapTileEvents.FireTileShakeStopped(cell);

        // Çökmeyi başlat
        StartCoroutine(CollapseCoroutine(cell));
    }

    // ──────── Titreşim (Shake) Coroutine ────────

    /// <summary>
    /// Süresiz tile titreşimi. Scaffold ve background tilemap'lerini titretir.
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
            // Çökme başladıysa durdur
            if (collapsingOrDestroyed.Contains(cell)) break;

            elapsed += Time.deltaTime;

            float ox = Mathf.Sin(elapsed * speed) * intensity;
            float oy = Mathf.Cos(elapsed * speed * 1.3f) * intensity * 0.5f;

            Matrix4x4 shakeMatrix = Matrix4x4.TRS(
                new Vector3(ox, oy, 0f), Quaternion.identity, Vector3.one);

            if (scaffoldMap.HasTile(cell))
                scaffoldMap.SetTransformMatrix(cell, shakeMatrix);

            if (backgroundMap != null && backgroundMap.HasTile(cell))
                backgroundMap.SetTransformMatrix(cell, shakeMatrix);

            yield return null;
        }
    }

    // ──────── Çökme (Collapse) Coroutine ────────

    /// <summary>
    /// Scaffold çökme animasyonu. Tile'ları kaldırır, hücreyi boşluk yapar.
    /// </summary>
    private IEnumerator CollapseCoroutine(Vector3Int cell)
    {
        if (collapsingOrDestroyed.Contains(cell)) yield break;
        collapsingOrDestroyed.Add(cell);

        TrapTileEvents.FireTileCollapsing(cell);

        Tilemap scaffoldMap = LevelGenerator.instance.scaffoldMap;
        Tilemap backgroundMap = LevelGenerator.instance.backgroundMap;

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

        RemoveTile(scaffoldMap, cell);
        RemoveTile(backgroundMap, cell);

        if (LevelGenerator.instance != null)
            LevelGenerator.instance.scaffoldCells.Remove(cell);

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
        collapsingOrDestroyed.Clear();
        StopAllCoroutines();
    }
}
