using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ViralCystsPerk : BasePerk
{
    [Header("VFX")]
    public GameObject markEffectPrefab;
    public Vector3 markOffset = new Vector3(0f, 0.35f, -0.05f);

    private Dictionary<int, GameObject> markedEnemies = new Dictionary<int, GameObject>();

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Attacks plant cysts. Skip to detonate: {perMark} per mark, damage split among marked.\nMarked: {count}";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues()
    {
        CleanDeadMarks();
        return new Dictionary<string, object>
        {
            { "perMark", "+1 die" },
            { "count",   $"+{markedEnemies.Count}" }
        };
    }

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public void PlantCyst(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health.currentHP <= 0) return;
        int id = enemy.GetInstanceID();
        if (markedEnemies.ContainsKey(id)) return;

        GameObject marker = CreateCystMarker(enemy);
        markedEnemies.Add(id, marker);
        RebuildDescription();
    }

    public List<EnemyMovement> ConsumeMarkedTargets()
    {
        CleanDeadMarks();
        if (markedEnemies.Count == 0) return new List<EnemyMovement>();

        TriggerVisualPop();

        List<EnemyMovement> targets = new List<EnemyMovement>();
        if (TurnManager.instance != null)
        {
            foreach (var enemy in TurnManager.instance.enemies)
            {
                if (enemy != null && enemy.health.currentHP > 0 && markedEnemies.ContainsKey(enemy.GetInstanceID()))
                    targets.Add(enemy);
            }
        }

        foreach (var kvp in markedEnemies)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                MonoBehaviour host = kvp.Value.GetComponentInParent<MonoBehaviour>();
                if (host != null && host.gameObject.activeInHierarchy)
                    host.StartCoroutine(PopAndDestroy(kvp.Value));
                else
                    Object.Destroy(kvp.Value);
            }
        }

        markedEnemies.Clear();
        RebuildDescription();
        return targets;
    }

    public int GetMarkedCount()
    {
        CleanDeadMarks();
        return markedEnemies.Count;
    }

    public override void OnLevelStart()
    {
        ClearAllMarkers();
        RebuildDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy != null) RemoveMarker(enemy.GetInstanceID());
        RebuildDescription();
    }

    private void CleanDeadMarks()
    {
        if (TurnManager.instance == null) { ClearAllMarkers(); return; }

        HashSet<int> aliveIDs = new HashSet<int>();
        foreach (var e in TurnManager.instance.enemies)
        {
            if (e != null && e.health.currentHP > 0)
                aliveIDs.Add(e.GetInstanceID());
        }

        List<int> toRemove = new List<int>();
        foreach (var id in markedEnemies.Keys)
        {
            if (!aliveIDs.Contains(id)) toRemove.Add(id);
        }
        foreach (var id in toRemove) RemoveMarker(id);
    }

    private GameObject CreateCystMarker(EnemyMovement enemy)
    {
        GameObject marker;
        if (markEffectPrefab != null)
        {
            marker = Object.Instantiate(markEffectPrefab, enemy.transform);
        }
        else
        {
            marker = new GameObject("CystMarker");
            marker.transform.SetParent(enemy.transform, false);
            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(0.2f, 0.9f, 0.2f, 0.7f);
            sr.sortingOrder = 10;
        }

        marker.transform.localPosition = markOffset;

        if (markEffectPrefab == null)
            marker.transform.localScale = Vector3.one * 0.3f;

        CystBreatheAnim breathe = marker.AddComponent<CystBreatheAnim>();
        breathe.baseScale = marker.transform.localScale.x;

        return marker;
    }

    private IEnumerator PopAndDestroy(GameObject marker)
    {
        if (marker == null) yield break;
        Transform t = marker.transform;
        Vector3 startScale = t.localScale;

        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(1f, 1.2f, elapsed / dur);
            t.localScale = startScale * s;
            yield return null;
        }

        SpriteRenderer sr = marker.GetComponentInChildren<SpriteRenderer>();
        Color origColor = sr != null ? sr.color : Color.white;
        dur = 0.1f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float tt = elapsed / dur;
            t.localScale = startScale * Mathf.Lerp(1.2f, 0f, tt);
            if (sr != null) sr.color = new Color(origColor.r, origColor.g, origColor.b, 1f - tt);
            yield return null;
        }

        if (marker != null) Object.Destroy(marker);
    }

    private void RemoveMarker(int id)
    {
        if (markedEnemies.TryGetValue(id, out GameObject marker))
        {
            if (marker != null) Object.Destroy(marker);
            markedEnemies.Remove(id);
        }
    }

    private void ClearAllMarkers()
    {
        foreach (var kvp in markedEnemies)
        {
            if (kvp.Value != null) Object.Destroy(kvp.Value);
        }
        markedEnemies.Clear();
    }

    void OnDestroy()
    {
        ClearAllMarkers();
    }

    private static Sprite cachedCircle;
    private static Sprite CreateCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                tex.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircle;
    }
}
