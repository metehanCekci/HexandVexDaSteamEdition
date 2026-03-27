using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Seismic Step (Legendary)
/// Skip yaptığında, skip yapılan tile titremeye başlar.
/// Oyuncu o tile'dan çıktığında tile çöker (scaffold mantığı).
/// Düşman çöken tile üzerindeyse düşer ve hasar alır.
/// Seviye başına ek etki: Lv2 çökme hasarı 2x, Lv3 çökme hasarı 3x + komşu tile'lar da titrer.
/// </summary>
public class SeismicStepPerk : BasePerk
{
    // Aktif titreyen tile'lar (skip yapılmış ama oyuncu henüz çıkmamış)
    private HashSet<Vector3Int> shakingCells = new HashSet<Vector3Int>();
    private Dictionary<Vector3Int, Coroutine> shakeCoroutines = new Dictionary<Vector3Int, Coroutine>();

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        maxLevel = 1;
    }

    /// <summary>
    /// Skip yapıldığında çağrılır. Oyuncunun bulunduğu tile'ı titretmeye başla.
    /// </summary>
    public override void OnSkip()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        // Zaten titriyor veya scaffold ise dokunma
        if (shakingCells.Contains(playerCell)) return;
        if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(playerCell)) return;

        // Ground tile olmayan hücrelere de dokunma
        if (LevelGenerator.instance == null || !LevelGenerator.instance.groundMap.HasTile(playerCell)) return;

        // Hazard tile'ına da dokunma — diken çökerse mantık bozulur
        if (LevelGenerator.instance.hazardCells.Contains(playerCell)) return;

        shakingCells.Add(playerCell);
        Coroutine shake = TurnManager.instance.StartCoroutine(ShakeCoroutine(playerCell));
        shakeCoroutines[playerCell] = shake;

        TriggerVisualPop();
    }

    /// <summary>
    /// TurnManager tarafından oyuncu her hareket ettiğinde çağrılır.
    /// Oyuncu eski tile'dan ayrıldıysa ve o tile titriyorsa çöker.
    /// </summary>
    public void OnPlayerLeftCell(Vector3Int oldCell)
    {
        if (!shakingCells.Contains(oldCell)) return;

        // Pre-collapse safety: don't collapse if it would disconnect player from all enemies
        if (TurnManager.instance != null && !TurnManager.instance.WouldRemainConnectedToEnemies(oldCell))
        {
            // Tile stabilizes — cancel shake, don't collapse
            StopShake(oldCell);
            shakingCells.Remove(oldCell);
            return;
        }

        // Titreşimi durdur
        StopShake(oldCell);
        shakingCells.Remove(oldCell);

        // Çökme başlat
        TurnManager.instance.StartCoroutine(CollapseCoroutine(oldCell));
    }

    /// <summary>
    /// Level geçişinde temizle.
    /// </summary>
    public override void OnLevelStart()
    {
        CleanupAll();
    }

    void OnDestroy()
    {
        CleanupAll();
    }

    private void CleanupAll()
    {
        foreach (var kvp in shakeCoroutines)
        {
            if (kvp.Value != null && TurnManager.instance != null)
                TurnManager.instance.StopCoroutine(kvp.Value);
        }
        shakeCoroutines.Clear();

        // Titreyen tile'ların transform'larını resetle
        if (LevelGenerator.instance != null)
        {
            Tilemap groundMap = LevelGenerator.instance.groundMap;
            Tilemap bgMap = LevelGenerator.instance.backgroundMap;
            foreach (var cell in shakingCells)
            {
                if (groundMap != null && groundMap.HasTile(cell))
                    groundMap.SetTransformMatrix(cell, Matrix4x4.identity);
                if (bgMap != null && bgMap.HasTile(cell))
                    bgMap.SetTransformMatrix(cell, Matrix4x4.identity);
            }
        }
        shakingCells.Clear();
    }

    public bool IsCellShaking(Vector3Int cell)
    {
        return shakingCells.Contains(cell);
    }

    // ──────── Titreşim (Scaffold ile aynı kalitede) ────────

    private IEnumerator ShakeCoroutine(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) yield break;
        Tilemap groundMap = LevelGenerator.instance.groundMap;
        Tilemap bgMap = LevelGenerator.instance.backgroundMap;
        if (groundMap == null) yield break;

        float elapsed = 0f;
        float intensity = 0.005f;
        float speed = 45f;

        while (shakingCells.Contains(cell))
        {
            elapsed += Time.deltaTime;

            float ox = Mathf.Sin(elapsed * speed) * intensity;
            float oy = Mathf.Cos(elapsed * speed * 1.3f) * intensity * 0.5f;

            Matrix4x4 shakeMatrix = Matrix4x4.TRS(
                new Vector3(ox, oy, 0f), Quaternion.identity, Vector3.one);

            if (groundMap.HasTile(cell))
                groundMap.SetTransformMatrix(cell, shakeMatrix);

            if (bgMap != null && bgMap.HasTile(cell))
                bgMap.SetTransformMatrix(cell, shakeMatrix);

            yield return null;
        }
    }

    // ──────── Çökme (Scaffold ile aynı polish seviyesinde) ────────

    private IEnumerator CollapseCoroutine(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) yield break;
        Tilemap groundMap = LevelGenerator.instance.groundMap;
        Tilemap bgMap = LevelGenerator.instance.backgroundMap;

        if (AudioManager.instance != null) AudioManager.instance.PlayWall();

        // Çökme sırasında düşman varsa hasar ver
        int collapseDamage = 1;
        if (TurnManager.instance != null)
        {
            EnemyMovement victim = TurnManager.instance.GetEnemyAtCell(cell);
            if (victim != null && victim.health.currentHP > 0)
            {
                victim.health.TakeDamage(collapseDamage);

                // Düşmanı güvenli komşu hücreye it
                if (victim.health.currentHP > 0)
                {
                    Vector3Int safeCell = TurnManager.instance.GetSafeNeighborForEnemy(cell);
                    if (safeCell != cell)
                        victim.StartKnockbackMovement(safeCell);
                }
            }
        }

        // Çökme animasyonu — scaffold ile birebir aynı kalite
        float collapseDuration = 0.35f;
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

            if (groundMap != null && groundMap.HasTile(cell))
            {
                groundMap.SetTransformMatrix(cell, matrix);
                groundMap.SetTileFlags(cell, TileFlags.None);
                groundMap.SetColor(cell, fadeColor);
            }
            if (bgMap != null && bgMap.HasTile(cell))
            {
                bgMap.SetTransformMatrix(cell, matrix);
                bgMap.SetTileFlags(cell, TileFlags.None);
                bgMap.SetColor(cell, fadeColor);
            }

            yield return null;
        }

        // Tile'ı kaldır
        RemoveTile(groundMap, cell);
        RemoveTile(bgMap, cell);

        // validCells'den kaldır — artık yürünebilir değil
        if (LevelGenerator.instance != null)
            LevelGenerator.instance.validCells.Remove(cell);

        // VoidHunger gibi dinleyiciler için event yayınla
        TrapTileEvents.FireTileDestroyed(cell);
    }

    private void StopShake(Vector3Int cell)
    {
        if (shakeCoroutines.ContainsKey(cell))
        {
            if (shakeCoroutines[cell] != null && TurnManager.instance != null)
                TurnManager.instance.StopCoroutine(shakeCoroutines[cell]);
            shakeCoroutines.Remove(cell);
        }

        // Transform'u resetle
        if (LevelGenerator.instance != null)
        {
            Tilemap groundMap = LevelGenerator.instance.groundMap;
            Tilemap bgMap = LevelGenerator.instance.backgroundMap;

            if (groundMap != null && groundMap.HasTile(cell))
                groundMap.SetTransformMatrix(cell, Matrix4x4.identity);
            if (bgMap != null && bgMap.HasTile(cell))
                bgMap.SetTransformMatrix(cell, Matrix4x4.identity);
        }
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
}
