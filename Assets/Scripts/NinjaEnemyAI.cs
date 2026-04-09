using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ninja düşman AI'ı.
///
/// Döngü: Bekle → Hazırlan (warning 7 hücre) → Saldır (ışınlan + hasar)
///   - Saldırı isabet ederse: uzak hücreye kaç, döngü başa döner.
///   - Saldırı ıskalanırsa: stun yer, stun biter → uzak hücreye kaç, döngü başa döner.
///
/// Kaçınma: Oyuncu 2 hex içine girerse anında rastgele uzak hücreye ışınlanır.
/// </summary>
public class NinjaEnemyAI : MonoBehaviour
{
    [Header("Davranış")]
    public int fleeDistanceThreshold = 2;   // Oyuncu bu kadar yaklaşınca kaç
    public int stunTurnsOnMiss = 2;          // Iskalayınca stun süresi
    public int attackDamage = 2;

    [Header("Warning Tile'ları")]
    public TileBase centerWarningTile;   // Ortadaki hücre (ışınlanacak nokta) — Inspector'dan bağla
    // Dış 6 hücre için normal warningTile kullanılır (TurnManager.warningTile)

    [Header("Warning Rengi")]
    public Color centerWarningColor = new Color(0.2f, 0.8f, 1f, 1f);  // Merkez rengi (cyan)
    public Color outerWarningColor  = new Color(1f, 0.3f, 0.3f, 1f);  // Dış halka rengi (kırmızı)

    [Header("Animasyon")]
    public Animator animator;   // Inspector'dan bağla; boşsa movement.animator kullanılır

    // ─── Döngü Fazları ───
    // 0 = Bekle, 1 = Hazırlan (warning koy), 2 = Saldır (ışınlan + hasar)
    private int cyclePhase = 0;

    [HideInInspector] public bool readyToStrike = false;   // Faz 2'de TurnManager'a sinyal
    private List<Vector3Int> warningCells = new List<Vector3Int>();

    private bool isTransitioning = false;

    // ─── Personal Tilemap (Warlock gibi) ───
    private Tilemap myPersonalMap;

    // ─── Bileşenler ───
    private EnemyMovement movement;
    private Tilemap groundMap;
    private Animator _anim;   // çözülmüş animator referansı (Start'ta set edilir)

    private static readonly Vector3Int[] oddOffsets  = { new Vector3Int(+1,  0, 0), new Vector3Int( 0, +1, 0), new Vector3Int(-1, +1, 0), new Vector3Int(-1,  0, 0), new Vector3Int(-1, -1, 0), new Vector3Int( 0, -1, 0) };
    private static readonly Vector3Int[] evenOffsets = { new Vector3Int(+1,  0, 0), new Vector3Int(+1, +1, 0), new Vector3Int( 0, +1, 0), new Vector3Int(-1,  0, 0), new Vector3Int( 0, -1, 0), new Vector3Int(+1, -1, 0) };

    // ─── Unity Lifecycle ───

    void Start()
    {
        movement  = GetComponent<EnemyMovement>();
        groundMap = movement.groundMap;
        _anim     = animator != null ? animator : movement.animator;

        // Personal tilemap (Warlock pattern'i)
        if (TurnManager.instance != null && TurnManager.instance.warningMap != null)
        {
            NinjaEnemyAI[] allNinjas = FindObjectsByType<NinjaEnemyAI>(FindObjectsSortMode.None);
            int myIndex = System.Array.IndexOf(allNinjas, this);

            GameObject clone = Instantiate(
                TurnManager.instance.warningMap.gameObject,
                TurnManager.instance.warningMap.transform.parent);
            clone.name = "Ninja_PersonalMap_" + gameObject.GetInstanceID();
            clone.transform.localPosition = TurnManager.instance.warningMap.transform.localPosition;
            clone.transform.localRotation = TurnManager.instance.warningMap.transform.localRotation;
            clone.transform.localScale    = TurnManager.instance.warningMap.transform.localScale;

            myPersonalMap = clone.GetComponent<Tilemap>();
            myPersonalMap.ClearAllTiles();

            TilemapRenderer tr     = clone.GetComponent<TilemapRenderer>();
            TilemapRenderer baseTr = TurnManager.instance.warningMap.GetComponent<TilemapRenderer>();
            tr.sortingLayerID = baseTr.sortingLayerID;
            tr.sortingOrder   = baseTr.sortingOrder + myIndex + 10;  // +10: warlock'larla çakışmasın
        }
    }

    void OnDestroy()
    {
        if (myPersonalMap != null) Destroy(myPersonalMap.gameObject);
    }

    // ─── Ana Döngü (TurnManager ExecuteLockedMove'dan çağırır) ───

    public IEnumerator ExecuteNinjaTurn()
    {
        if (isTransitioning) yield break;
        if (movement.skipTurns > 0) yield break;

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        Vector3Int myCell     = movement.GetCurrentCellPosition();

        // Oyuncu çok yakınsa ve sadece bekleme fazındaysak kaç
        // (hazırlanma veya saldırı fazındaysa kaçma — warning tile'lar zaten konulmuş)
        if (cyclePhase == 0 && movement.Distance(myCell, playerCell) <= fleeDistanceThreshold)
        {
            yield return StartCoroutine(TeleportToFarCell(playerCell));
            yield break;
        }

        switch (cyclePhase)
        {
            case 0: // ─── BEKLE ───
                // Sadece oyuncuya doğru bak, hiçbir şey yapma
                FlipTowardPlayer(playerCell);
                cyclePhase = 1;
                break;

            case 1: // ─── HAZIRLAN: warning tile'ları koy, BU TUR SADECE BEKLE ───
                // Başka bir ninja zaten saldırı hazırlığındaysa bu tur bekle
                if (AnotherNinjaIsAttacking())
                {
                    FlipTowardPlayer(playerCell);
                    break;  // cyclePhase değişmez, bir tur daha bekler
                }

                FlipTowardPlayer(playerCell);
                AnimSetBool("IsCharging", true);
                if (AudioManager.instance != null) AudioManager.instance.PlayCharge();

                warningCells = GetStrikeCells(myCell, playerCell);
                ShowWarningCells(warningCells);

                cyclePhase = 2;  // readyToStrike YOK — bir sonraki tura kadar bekle
                break;

            case 2: // ─── SALDIRI: bu tur TurnManager ExecuteStrike() çağırır ───
                readyToStrike = true;  // TurnManager bu turu execute etsin
                cyclePhase = 0;
                break;
        }
    }

    // ─── Saldırı Execution (TurnManager çağırır) ───

    public IEnumerator ExecuteStrike()
    {
        readyToStrike = false;

        List<Vector3Int> strikeTargets = new List<Vector3Int>(warningCells);
        ClearWarnings();

        if (strikeTargets.Count == 0) yield break;

        // Merkezi bul (liste index 0 = merkez)
        Vector3Int strikeCenter = strikeTargets[0];

        // Ninja ışınlanıyor: fade out → konuma git → fade in
        yield return StartCoroutine(NinjaTeleportFade(1f, 0f, 0.2f));
        movement.TeleportTo(strikeCenter);
        yield return StartCoroutine(NinjaTeleportFade(0f, 1f, 0.2f));

        // Animator
        AnimSetBool("IsCharging", false);
        AnimTrigger("Attack");

        // Flash & VFX
        FlashWarningCells(strikeTargets);
        if (AudioManager.instance != null) AudioManager.instance.PlayLightning();
        if (HitstopManager.instance != null) HitstopManager.instance.TriggerHitstop();
        CameraController.ShakeLighter();
        SpawnImpactEffects(strikeTargets);

        yield return new WaitForSeconds(0.1f);

        // Hasar kontrolü
        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        bool playerHit = strikeTargets.Contains(playerCell);

        if (playerHit)
        {
            bool dodged = RunManager.instance != null && Random.value < RunManager.instance.dodgeChance;

            if (dodged)
            {
                if (TurnManager.instance != null)
                    TurnManager.instance.PlayShieldBreakFX(TurnManager.instance.player.transform.position);
            }
            else if (RunManager.instance != null && RunManager.instance.hasBioBarrier)
            {
                foreach (var perk in RunManager.instance.activePerks)
                    if (perk is BioBarrierPerk aegis) { aegis.BreakShield(); break; }
                RunManager.instance.hasBioBarrier = false;
            }
            else
            {
                TurnManager.instance.PlayerTakeDamage(attackDamage);
            }

            Vector3Int pushTarget = TurnManager.instance.GetOppositeCell(playerCell, movement.GetCurrentCellPosition());
            TurnManager.instance.player.StartKnockbackMovement(pushTarget);
            yield return new WaitUntil(() => !TurnManager.instance.player.IsMoving());
        }

        yield return new WaitForSeconds(0.15f);

        // Sonuç: isabet ettiyse kaç; ıskaladıysa stun ye
        if (playerHit)
        {
            // Kaç
            yield return StartCoroutine(TeleportToFarCell(TurnManager.instance.player.GetCurrentCellPosition()));
            cyclePhase = 0;
        }
        else
        {
            // Stun — biz vurursak OnHit() kaçışı halleder, vurmassak stun bitince döngü başa döner
            movement.ApplyStun(stunTurnsOnMiss, true);
            cyclePhase = 0;
        }
    }

    // ─── Yakınlaşma Kaçışı: LockNextMove'dan çağrılır ───

    public bool ShouldFlee(Vector3Int playerCell)
    {
        if (isTransitioning) return false;
        if (cyclePhase != 0) return false;  // hazırlanma/saldırı fazında kaçma
        return movement.Distance(movement.GetCurrentCellPosition(), playerCell) <= fleeDistanceThreshold;
    }

    // ─── Warning Hücrelerini Hesapla ───

    /// <summary>
    /// Ninja'nın baktığı yönün tersindeki (oyuncunun "arkası") hücreyi merkez alır.
    /// Merkez + çevresindeki 6 komşu = 7 hücre.
    /// </summary>
    private List<Vector3Int> GetStrikeCells(Vector3Int ninjaCell, Vector3Int playerCell)
    {
        // Ninja oyuncuya bakıyor. Oyuncunun "ninja tarafındaki" komşusunu bul.
        // Yani: ninjaCell → playerCell yönü. playerCell'in o yöndeki komşusu.
        Vector3Int direction = GetHexDirectionIndex(ninjaCell, playerCell);  // ninja→player yönü
        Vector3Int strikeCenter = playerCell + direction;  // oyuncunun "ninja tarafındaki" komşusu

        // Strike center geçersizse (hazard, explosion, ninja'nın kendi hücresi) fallback
        if (!HasWalkableTile(strikeCenter) || strikeCenter == ninjaCell || IsDangerousCell(strikeCenter))
        {
            strikeCenter = GetClosestWalkableNeighborToward(playerCell, ninjaCell);
        }

        List<Vector3Int> cells = new List<Vector3Int>();
        cells.Add(strikeCenter);

        Vector3Int[] offsets = (strikeCenter.y % 2 != 0) ? evenOffsets : oddOffsets;
        foreach (var off in offsets)
        {
            Vector3Int neighbor = strikeCenter + off;
            if (HasWalkableTile(neighbor))
                cells.Add(neighbor);
        }

        return cells;
    }

    /// <summary>
    /// ninjaCell'den playerCell'e doğru 1 adımlık offset döndürür.
    /// </summary>
    private Vector3Int GetHexDirectionIndex(Vector3Int from, Vector3Int toward)
    {
        Vector3Int[] offsets = (from.y % 2 != 0) ? evenOffsets : oddOffsets;
        Vector3Int best = Vector3Int.zero;
        float bestDist = float.MaxValue;

        foreach (var off in offsets)
        {
            Vector3Int candidate = from + off;
            float d = movement.Distance(candidate, toward);
            if (d < bestDist)
            {
                bestDist = d;
                best = off;
            }
        }
        return best;
    }

    private Vector3Int GetClosestWalkableNeighborToward(Vector3Int center, Vector3Int toward)
    {
        Vector3Int[] offsets = (center.y % 2 != 0) ? evenOffsets : oddOffsets;
        Vector3Int best = center;
        float bestDist = float.MaxValue;

        foreach (var off in offsets)
        {
            Vector3Int n = center + off;
            if (!HasWalkableTile(n)) continue;
            if (IsDangerousCell(n)) continue;
            float d = movement.Distance(n, toward);
            if (d < bestDist)
            {
                bestDist = d;
                best = n;
            }
        }
        return best;
    }

    private bool IsDangerousCell(Vector3Int c)
    {
        if (LevelGenerator.instance == null) return false;
        return LevelGenerator.instance.hazardCells.Contains(c)
            || LevelGenerator.instance.explosionCells.Contains(c);
    }

    // ─── Uzak Hücreye Işınlan ───

    private IEnumerator TeleportToFarCell(Vector3Int playerCell)
    {
        isTransitioning = true;
        ClearWarnings();

        yield return StartCoroutine(NinjaTeleportFade(1f, 0f, 0.18f));

        Vector3Int target = FindFarCell(playerCell, minDist: 4f);
        if (target != movement.GetCurrentCellPosition())
            movement.TeleportTo(target);

        yield return StartCoroutine(NinjaTeleportFade(0f, 1f, 0.18f));
        isTransitioning = false;
    }

    private Vector3Int FindFarCell(Vector3Int playerCell, float minDist)
    {
        List<Vector3Int> candidates = new List<Vector3Int>();
        int radius = LevelGenerator.instance != null
            ? LevelGenerator.instance.baseMapRadius + (RunManager.instance.currentLevel / 6) + 2
            : 8;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int c = new Vector3Int(x, y, 0);
                if (!HasWalkableTile(c)) continue;
                if (TurnManager.instance != null && TurnManager.instance.IsEnemyAtCell(c)) continue;
                if (LevelGenerator.instance == null) continue;
                if (LevelGenerator.instance.hazardCells.Contains(c)) continue;
                if (LevelGenerator.instance.explosionCells.Contains(c)) continue;
                if (movement.Distance(c, playerCell) >= minDist)
                    candidates.Add(c);
            }
        }

        // Fallback: mesafe eşiğini düşür, en az 0'a kadar
        if (candidates.Count == 0 && minDist > 0f)
            return FindFarCell(playerCell, minDist - 1f);

        // Son çare: mevcut hücrede kal
        if (candidates.Count == 0)
            return movement.GetCurrentCellPosition();

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ─── Visual Helpers ───

    private void ShowWarningCells(List<Vector3Int> cells)
    {
        if (myPersonalMap == null) return;

        TileBase outerTile = movement.warningTile;
        if (outerTile == null && TurnManager.instance != null) outerTile = TurnManager.instance.warningTile;

        TileBase innerTile = centerWarningTile != null ? centerWarningTile : outerTile;

        for (int i = 0; i < cells.Count; i++)
        {
            bool isCenter = (i == 0);
            TileBase tile  = isCenter ? innerTile  : outerTile;
            Color    color = isCenter ? centerWarningColor : outerWarningColor;
            if (tile != null)
                StartCoroutine(FadeInWarningCell(cells[i], tile, color));
        }
    }

    private IEnumerator FadeInWarningCell(Vector3Int cell, TileBase tile, Color targetColor)
    {
        myPersonalMap.SetTile(cell, tile);
        myPersonalMap.SetTileFlags(cell, TileFlags.None);

        Color start = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        myPersonalMap.SetColor(cell, start);

        float duration = 0.3f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            myPersonalMap.SetColor(cell, Color.Lerp(start, targetColor, t));
            yield return null;
        }
        myPersonalMap.SetColor(cell, targetColor);
    }

    private void FlashWarningCells(List<Vector3Int> cells)
    {
        if (myPersonalMap == null) return;
        Color flash = new Color(1f, 1f, 1f, 1f);
        foreach (var c in cells)
            if (myPersonalMap.HasTile(c)) myPersonalMap.SetColor(c, flash);
        StartCoroutine(FadeOutWarningCells(cells, 0.35f));
    }

    private IEnumerator FadeOutWarningCells(List<Vector3Int> cells, float duration)
    {
        float elapsed = 0f;
        Color startColor = new Color(1f, 1f, 1f, 1f);
        Color endColor   = new Color(1f, 1f, 1f, 0f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            Color c = Color.Lerp(startColor, endColor, t);
            foreach (var cell in cells)
                if (myPersonalMap != null && myPersonalMap.HasTile(cell)) myPersonalMap.SetColor(cell, c);
            yield return null;
        }
        foreach (var cell in cells)
            if (myPersonalMap != null && myPersonalMap.HasTile(cell)) myPersonalMap.SetTile(cell, null);
    }

    private void ClearWarnings()
    {
        if (myPersonalMap != null) myPersonalMap.ClearAllTiles();
        warningCells.Clear();
        readyToStrike = false;
    }

    private IEnumerator NinjaTeleportFade(float startAlpha, float endAlpha, float duration)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;

        Color col = sr.color;
        float elapsed = 0f;

        // Fade out: biraz sıkıştır; fade in: normalden başla
        Vector3 normalScale    = Vector3.one;
        Vector3 stretchedScale = new Vector3(0.5f, 1.5f, 1f);
        Vector3 startScale = startAlpha > endAlpha ? normalScale : stretchedScale;
        Vector3 endScale   = startAlpha > endAlpha ? stretchedScale : normalScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            col.a = Mathf.Lerp(startAlpha, endAlpha, t);
            sr.color = col;
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }
        col.a = endAlpha;
        sr.color = col;
        transform.localScale = normalScale;
    }

    private void FlipTowardPlayer(Vector3Int playerCell)
    {
        var visuals = GetComponent<EnemyVisuals>();
        if (visuals == null || visuals.visualRenderer == null) return;
        Vector3 playerPos = groundMap.GetCellCenterWorld(playerCell);
        float dx = playerPos.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
            visuals.visualRenderer.flipX = (dx < 0);
    }

    private void SpawnImpactEffects(List<Vector3Int> cells)
    {
        // Ninja'nın kendi animasyonu hasar görselini üstleniyor.
        // İleride özel bir VFX prefab eklemek istersen buraya koy.
    }

    private bool HasWalkableTile(Vector3Int c)
    {
        if (groundMap.HasTile(c)) return true;
        return ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(c);
    }

    // ─── Senkron Kontrol ───

    private bool AnotherNinjaIsAttacking()
    {
        foreach (var ninja in FindObjectsByType<NinjaEnemyAI>(FindObjectsSortMode.None))
        {
            if (ninja == this) continue;
            if (ninja.movement == null || ninja.movement.health.currentHP <= 0) continue;
            // cyclePhase 2 = warning koyulmuş, saldırı bekleniyor
            if (ninja.cyclePhase == 2 || ninja.readyToStrike) return true;
        }
        return false;
    }

    // ─── Animator Helpers (parametre yoksa sessizce geçer) ───

    private void AnimSetBool(string param, bool value)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
            if (p.name == param && p.type == AnimatorControllerParameterType.Bool)
            { _anim.SetBool(param, value); return; }
    }

    private void AnimTrigger(string param)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
            if (p.name == param && p.type == AnimatorControllerParameterType.Trigger)
            { _anim.SetTrigger(param); return; }
    }

    // ─── Public API (TurnManager & EnemyMovement için) ───

    public bool IsReadyToStrike()     => readyToStrike;
    public bool IsTransitioning()     => isTransitioning;

    public void OnNinjaDied()
    {
        ClearWarnings();
        cyclePhase = 0;
        StopAllCoroutines();
        if (myPersonalMap != null) myPersonalMap.ClearAllTiles();
    }

    /// <summary>
    /// Vurulunca çağrılır. Warning'leri temizler, döngüyü sıfırlar, uzağa kaçar.
    /// </summary>
    public void OnHit()
    {
        if (isTransitioning) return;
        if (movement.health.currentHP <= 0) return;

        isTransitioning = true;  // anında set et — aynı frame'de gelen ikinci çağrıyı engelle

        ClearWarnings();
        cyclePhase = 0;

        Vector3Int playerCell = TurnManager.instance != null
            ? TurnManager.instance.player.GetCurrentCellPosition()
            : Vector3Int.zero;

        StartCoroutine(TeleportToFarCell(playerCell));
    }
}
