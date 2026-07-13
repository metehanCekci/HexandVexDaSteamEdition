using UnityEngine;
using System.Collections;

/// <summary>
/// Düşman görsel sistemi: intent arrow, stun efekti, fade animasyonları, sprite flip, sorting order.
/// EnemyMovement ile aynı GameObject'te bulunur.
/// </summary>
public class EnemyVisuals : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject intentArrow;
    public Vector2 arrowOffset = Vector2.zero;
    public float arrowAngleOffset = 0f;

    public GameObject stunEffectPrefab;
    private GameObject spawnedStunEffect;
    private SpriteRenderer stunRenderer;
    private Coroutine stunFadeCoroutine;
    private bool isStunVisualActive = false;

    private SpriteRenderer arrowRenderer;
    private Coroutine arrowFadeCoroutine;

    [HideInInspector] public SpriteRenderer visualRenderer;
    [HideInInspector] public bool isFading = false;

    private EnemyMovement movement; // cached reference

    void Start()
    {
        movement = GetComponent<EnemyMovement>();

        if (visualRenderer == null)
        {
            visualRenderer = GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
            {
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (intentArrow != null && sr.transform.IsChildOf(intentArrow.transform)) continue;
                    if (spawnedStunEffect != null && sr.gameObject == spawnedStunEffect) continue;
                    visualRenderer = sr;
                    break;
                }
            }
        }

        if (stunEffectPrefab != null)
        {
            spawnedStunEffect = Instantiate(stunEffectPrefab, this.transform);
            spawnedStunEffect.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            stunRenderer = spawnedStunEffect.GetComponent<SpriteRenderer>();

            if (stunRenderer != null)
            {
                Color c = stunRenderer.color; c.a = 0f; stunRenderer.color = c;
            }
            spawnedStunEffect.SetActive(false);
        }

        if (intentArrow != null)
        {
            arrowRenderer = intentArrow.GetComponentInChildren<SpriteRenderer>();
            if (arrowRenderer != null)
            {
                Color c = arrowRenderer.color; c.a = 0f; arrowRenderer.color = c;
            }
            intentArrow.SetActive(false);
        }
    }

    void Update()
    {
        if (movement == null) return;

        // Stun alpha
        if (movement.health != null && movement.health.currentHP > 0 && !isFading)
        {
            movement.health.SetStunnedAlpha(movement.skipTurns > 0);
        }

        // Sprite flip: allied düşmanlar en yakın hostile düşmana, normal düşmanlar player'a bakar
        if (visualRenderer != null && movement != null)
        {
            if (movement.isAllied && TurnManager.instance != null)
            {
                // En yakın hostile düşmana doğru bak
                Transform closestEnemy = null;
                float closestDist = float.MaxValue;
                foreach (var e in TurnManager.instance.enemies)
                {
                    if (e == null || e.isAllied || e.health.currentHP <= 0) continue;
                    float dist = Vector3.Distance(transform.position, e.transform.position);
                    if (dist < closestDist) { closestDist = dist; closestEnemy = e.transform; }
                }
                if (closestEnemy != null)
                {
                    float dx = closestEnemy.position.x - transform.position.x;
                    visualRenderer.flipX = dx < 0;
                }
            }
            else if (TurnManager.instance != null && TurnManager.instance.player != null)
            {
                Vector3 dirToPlayer = TurnManager.instance.player.transform.position - transform.position;
                visualRenderer.flipX = dirToPlayer.x < 0;
            }
        }

        UpdateSortingOrder();
    }

    private void UpdateSortingOrder()
    {
        int order = 100 + Mathf.RoundToInt(-transform.position.y * 10f);

        SpriteRenderer target = visualRenderer ?? GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (target != null) target.sortingOrder = order;

        if (arrowRenderer != null) arrowRenderer.sortingOrder = order + 1;
        if (stunRenderer != null) stunRenderer.sortingOrder = order + 2;

        if (movement != null && movement.health != null && movement.health.hptext != null)
        {
            Canvas hpCanvas = movement.health.hptext.GetComponentInParent<Canvas>();
            if (hpCanvas != null) hpCanvas.sortingOrder = order + 3;
        }

        if (movement != null && movement.health != null)
        {
            var healthBar = movement.health.GetComponent<EnemyHealthBar>();
            if (healthBar != null) healthBar.SetSortingOrder(order + 4);
        }

        // BruiserEnemyAI transparency logic
        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null && TurnManager.instance != null && movement != null)
        {
            bool coveredByEnemy = false;
            Vector3Int cell = movement.GetCurrentCellPosition();
            Vector3Int[] offsets = (cell.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;
            Vector3Int directlyAbove = cell;
            float minXDiff = float.MaxValue;
            Vector3 myWorldPos = movement.groundMap.GetCellCenterWorld(cell);
            foreach (var off in offsets)
            {
                Vector3Int neighbor = cell + off;
                if (neighbor.y <= cell.y) continue;
                float xDiff = Mathf.Abs(movement.groundMap.GetCellCenterWorld(neighbor).x - myWorldPos.x);
                if (xDiff < minXDiff) { minXDiff = xDiff; directlyAbove = neighbor; }
            }

            foreach (var e in TurnManager.instance.enemies)
            {
                if (e == null || e == movement) continue;
                var eVisuals = e.GetComponent<EnemyVisuals>();
                if (eVisuals != null && eVisuals.isFading) continue;
                if (e.GetCurrentCellPosition() == directlyAbove) { coveredByEnemy = true; break; }
            }

            float targetAlpha = (movement.skipTurns > 0 || coveredByEnemy) ? 0.45f : 1f;
            if (target != null)
            {
                Color c = target.color;
                c.a = Mathf.MoveTowards(c.a, targetAlpha, 4f * Time.deltaTime);
                target.color = c;
            }
        }
    }

    // ─── Intent Arrow ───

    public void SetArrowVisibility(bool show)
    {
        if (intentArrow == null || arrowRenderer == null) return;
        if (show && movement != null && !movement.hasLockedTarget) return;

        if (arrowFadeCoroutine != null) StopCoroutine(arrowFadeCoroutine);
        arrowFadeCoroutine = StartCoroutine(FadeArrowCoroutine(show));
    }

    private IEnumerator FadeArrowCoroutine(bool show)
    {
        if (show) intentArrow.SetActive(true);

        float targetAlpha = show ? 0.5f : 0f;
        float startAlpha = arrowRenderer.color.a;
        float elapsed = 0f; Color c = arrowRenderer.color;

        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / 0.2f);
            arrowRenderer.color = c;
            yield return null;
        }
        c.a = targetAlpha; arrowRenderer.color = c;
        if (!show) intentArrow.SetActive(false);
    }

    public void PointArrowAt(Vector3Int fromCell, Vector3Int toCell)
    {
        if (intentArrow == null || movement == null) return;
        Vector3 currentWorldPos = movement.groundMap.GetCellCenterWorld(fromCell);
        Vector3 nextWorldPos = movement.groundMap.GetCellCenterWorld(toCell);
        currentWorldPos.z = 0; nextWorldPos.z = 0;

        intentArrow.transform.position = currentWorldPos + (Vector3)arrowOffset;
        Vector3 dir = nextWorldPos - currentWorldPos;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        intentArrow.transform.rotation = Quaternion.AngleAxis(angle + arrowAngleOffset, Vector3.forward);
    }

    // ─── Stun Visual ───

    public void ShowStunEffect()
    {
        if (isStunVisualActive) return;
        isStunVisualActive = true;
        if (spawnedStunEffect != null)
        {
            spawnedStunEffect.SetActive(true);
            if (stunFadeCoroutine != null) StopCoroutine(stunFadeCoroutine);
            stunFadeCoroutine = StartCoroutine(FadeStunEffect(1f));
        }
    }

    public void HideStunEffect()
    {
        if (!isStunVisualActive) return;
        isStunVisualActive = false;
        if (stunFadeCoroutine != null) StopCoroutine(stunFadeCoroutine);
        if (spawnedStunEffect != null && stunRenderer != null)
            stunFadeCoroutine = StartCoroutine(FadeStunEffect(0f));
    }

    public void ForceHideStunEffect()
    {
        if (!isStunVisualActive) return;
        isStunVisualActive = false;
        if (spawnedStunEffect != null) spawnedStunEffect.SetActive(false);
    }

    private IEnumerator FadeStunEffect(float targetAlpha)
    {
        if (stunRenderer == null || spawnedStunEffect == null) yield break;

        float startAlpha = stunRenderer.color.a;
        float elapsed = 0f;
        float duration = 0.25f;
        Color c = stunRenderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            stunRenderer.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        stunRenderer.color = c;

        if (targetAlpha <= 0.01f)
        {
            spawnedStunEffect.SetActive(false);
        }
    }

    // ─── Fade Animations ───

    public IEnumerator FadeSpawnCoroutine()
    {
        isFading = true;
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers) { Color c = sr.color; c.a = 0f; sr.color = c; }

        float duration = 0.5f; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            foreach (var sr in allRenderers) { Color c = sr.color; c.a = alpha; sr.color = c; }
            yield return null;
        }

        foreach (var sr in allRenderers) { Color c = sr.color; c.a = 1f; sr.color = c; }
        isFading = false;
    }

    public IEnumerator FadeOutWithoutDestroy()
    {
        isFading = true;
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();

        float duration = 0.3f; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            foreach (var sr in allRenderers) { Color c = sr.color; c.a = alpha; sr.color = c; }
            yield return null;
        }
        isFading = false;
    }

    [HideInInspector] public bool fadeDieStarted = false;

    public IEnumerator FadeDieCoroutine()
    {
        // Zaten ölüm animasyonu başladıysa tekrar çalıştırma (race condition koruması)
        if (fadeDieStarted) yield break;
        fadeDieStarted = true;
        isFading = true;

        if (movement != null)
        {
            movement.ClearCombatState();

            // Scaffold leave
            if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(movement.GetCurrentCellPosition()))
            {
                ScaffoldManager.instance.OnEntityLeave(movement.GetCurrentCellPosition());
            }
        }

        SetArrowVisibility(false);
        ForceHideStunEffect();

        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();

        float duration = 0.4f; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            foreach (var sr in allRenderers) { Color c = sr.color; c.a = alpha; sr.color = c; }
            yield return null;
        }

        if (TurnManager.instance != null) TurnManager.instance.enemies.Remove(movement);
        Destroy(gameObject);
    }
}
