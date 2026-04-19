using UnityEngine;
using System.Collections.Generic;

public class PyrogenicGlandsPerk : BasePerk
{
    [Header("VFX")]
    public GameObject fireEffectPrefab;

    // Track burn state per enemy instance ID
    private Dictionary<int, int> burnTurnsRemaining = new Dictionary<int, int>();
    // Persistent fire orbit objects per enemy
    private Dictionary<int, GameObject> burnOrbitObjects = new Dictionary<int, GameObject>();

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Attacks burn enemies: {dmg} of max HP per turn for 5 turns.";
        RebuildDescription();
    }

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "dmg", $"{currentLevel * 5}%" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        // Burn is applied after attack in TurnManager via ApplyBurn
    }

    public void ApplyBurn(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health.currentHP <= 0) return;
        int id = enemy.GetInstanceID();
        burnTurnsRemaining[id] = 5; // 5 tur yanar
        TriggerVisualPop();
        ShowBurnVFX(enemy);
        CreateBurnOrbit(enemy, 5);
    }

    public void TickBurns()
    {
        if (TurnManager.instance == null) return;

        float damagePercent = currentLevel * 0.05f; // Lv1=%5, Lv2=%10, Lv3=%15
        List<int> toRemove = new List<int>();

        var burnEntries = new List<KeyValuePair<int, int>>(burnTurnsRemaining);
        foreach (var kvp in burnEntries)
        {
            int enemyId = kvp.Key;
            int turnsLeft = kvp.Value;

            EnemyMovement enemy = null;
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e != null && e.GetInstanceID() == enemyId)
                { enemy = e; break; }
            }

            if (enemy == null || enemy.health.currentHP <= 0)
            {
                toRemove.Add(enemyId);
                continue;
            }

            long damage = System.Math.Max(1L, (long)System.Math.Round(enemy.health.maxHP * (double)damagePercent));
            // Warlock'un burn hasarında ışınlanmaması için flag set et
            var warlock = enemy.GetComponent<WarlockEnemyAI>();
            if (warlock != null) warlock.isBurnDamage = true;
            enemy.health.TakeDamage(damage, false, false);
            ShowBurnVFX(enemy);

            // Yanarak öldüyse kill reward ver ve perk callback'lerini tetikle
            if (enemy.health.currentHP <= 0)
            {
                toRemove.Add(enemyId);
                if (TurnManager.instance.coinService != null)
                    TurnManager.instance.coinService.ProcessKillRewards(enemy);

                // Perk OnEnemyKilled callback'leri
                if (RunManager.instance != null)
                {
                    foreach (var p in RunManager.instance.activePerks)
                        if (p != null) p.OnEnemyKilled(enemy);
                }
                continue;
            }

            if (turnsLeft <= 1)
            {
                toRemove.Add(enemyId);
            }
            else
            {
                int newTurns = turnsLeft - 1;
                burnTurnsRemaining[enemyId] = newTurns;
                UpdateBurnOrbitCount(enemyId, newTurns);
            }
        }

        foreach (int id in toRemove)
        {
            burnTurnsRemaining.Remove(id);
            RemoveBurnOrbit(id);
        }
    }

    public override void OnLevelStart()
    {
        burnTurnsRemaining.Clear();
        ClearAllOrbits();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy != null)
        {
            int id = enemy.GetInstanceID();
            burnTurnsRemaining.Remove(id);
            RemoveBurnOrbit(id);
        }
    }

    void OnDestroy()
    {
        ClearAllOrbits();
    }

    // --- Persistent 3-fireball orbit ---

    private void CreateBurnOrbit(EnemyMovement enemy, int fireballCount)
    {
        int id = enemy.GetInstanceID();
        // Zaten varsa guncelle
        if (burnOrbitObjects.ContainsKey(id))
        {
            UpdateBurnOrbitCount(id, fireballCount);
            return;
        }

        GameObject orbit = new GameObject("BurnOrbit");
        orbit.transform.SetParent(enemy.transform, false);
        orbit.transform.localPosition = Vector3.zero;

        SpawnFireballs(orbit.transform, fireballCount);

        BurnOrbitAnim anim = orbit.AddComponent<BurnOrbitAnim>();
        anim.orbitRadius = 0.22f;
        anim.orbitSpeed = 180f;

        burnOrbitObjects[id] = orbit;
    }

    private void SpawnFireballs(Transform parent, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject fireball;
            if (fireEffectPrefab != null)
            {
                fireball = Object.Instantiate(fireEffectPrefab, parent);
            }
            else
            {
                fireball = new GameObject($"Fireball_{i}");
                fireball.transform.SetParent(parent, false);
                SpriteRenderer sr = fireball.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(1f, 0.5f, 0f, 0.8f);
                sr.sortingOrder = 15;
            }
            // Prefab varsa kendi scale'ini koru, yoksa fallback
            if (fireEffectPrefab == null)
                fireball.transform.localScale = Vector3.one * 0.5f;
        }
    }

    /// <summary>Alev sayisini guncelle — fazla olanlari sil, eksikleri ekle.</summary>
    private void UpdateBurnOrbitCount(int enemyId, int newCount)
    {
        if (!burnOrbitObjects.TryGetValue(enemyId, out GameObject orbit)) return;
        if (orbit == null) return;

        int current = orbit.transform.childCount;

        // Fazla alevleri sil (sondan)
        while (current > newCount && current > 0)
        {
            Transform child = orbit.transform.GetChild(current - 1);
            Object.Destroy(child.gameObject);
            current--;
        }

        // Eksik alevleri ekle
        if (current < newCount)
            SpawnFireballs(orbit.transform, newCount - current);
    }

    private void RemoveBurnOrbit(int enemyId)
    {
        if (burnOrbitObjects.TryGetValue(enemyId, out GameObject orbit))
        {
            if (orbit != null) Object.Destroy(orbit);
            burnOrbitObjects.Remove(enemyId);
        }
    }

    private void ClearAllOrbits()
    {
        foreach (var kvp in burnOrbitObjects)
        {
            if (kvp.Value != null) Object.Destroy(kvp.Value);
        }
        burnOrbitObjects.Clear();
    }

    // --- VFX ---

    private void ShowBurnVFX(EnemyMovement enemy)
    {
        if (enemy == null) return;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && enemy.gameObject.activeInHierarchy)
            enemy.StartCoroutine(BurnFlash(sr));
    }

    private System.Collections.IEnumerator BurnFlash(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        Color burnColor = new Color(1f, 0.5f, 0f, original.a);
        sr.color = burnColor;

        float dur = 0.3f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (sr == null) yield break;
            sr.color = Color.Lerp(burnColor, original, elapsed / dur);
            yield return null;
        }
        if (sr != null) sr.color = original;
    }

    // --- Placeholder sprite ---

    private static Sprite cachedCircle;
    private static Sprite CreateCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - dist)));
            }
        tex.Apply();
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        return cachedCircle;
    }
}
