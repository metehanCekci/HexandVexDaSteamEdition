using UnityEngine;
using System.Collections.Generic;

public class ViralCystsPerk : BasePerk
{
    private HashSet<int> markedEnemyIDs = new HashSet<int>();

    void OnEnable()
    {
        maxLevel = 3;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    /// <summary>
    /// Called by TurnManager after hitting an enemy to plant a cyst.
    /// </summary>
    public void PlantCyst(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health.currentHP <= 0) return;
        int id = enemy.GetInstanceID();
        if (markedEnemyIDs.Contains(id)) return; // Already marked

        markedEnemyIDs.Add(id);
        description = GetDescription();

        // Visual: green tint to show cyst
        ShowCystVisual(enemy);
    }

    public override void OnSkip()
    {
        if (TurnManager.instance == null) return;

        // Clean dead/null enemies
        CleanDeadMarks();

        int markedCount = markedEnemyIDs.Count;
        if (markedCount == 0) return;

        TriggerVisualPop();

        float damagePercent = GetDamagePercent();

        // Detonate all cysts
        List<EnemyMovement> markedEnemies = new List<EnemyMovement>();
        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy != null && enemy.health.currentHP > 0 && markedEnemyIDs.Contains(enemy.GetInstanceID()))
                markedEnemies.Add(enemy);
        }

        // Damage scales with total marked count
        foreach (var enemy in markedEnemies)
        {
            // Base damage = damagePercent of max HP * number of marked enemies
            int damage = Mathf.CeilToInt(enemy.health.maxHP * damagePercent * markedCount);
            damage = Mathf.Max(1, damage);
            enemy.health.TakeDamage(damage);
            ShowDetonateVFX(enemy);
        }

        markedEnemyIDs.Clear();
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        markedEnemyIDs.Clear();
        description = GetDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy != null)
            markedEnemyIDs.Remove(enemy.GetInstanceID());
        description = GetDescription();
    }

    private float GetDamagePercent()
    {
        // Lv1=10%, Lv2=15%, Lv3=20%
        switch (currentLevel)
        {
            case 1: return 0.10f;
            case 2: return 0.15f;
            default: return 0.20f;
        }
    }

    private void CleanDeadMarks()
    {
        if (TurnManager.instance == null) { markedEnemyIDs.Clear(); return; }

        HashSet<int> aliveIDs = new HashSet<int>();
        foreach (var e in TurnManager.instance.enemies)
        {
            if (e != null && e.health.currentHP > 0)
                aliveIDs.Add(e.GetInstanceID());
        }
        markedEnemyIDs.RemoveWhere(id => !aliveIDs.Contains(id));
    }

    private string GetDescription()
    {
        int percent = currentLevel == 1 ? 10 : currentLevel == 2 ? 15 : 20;
        CleanDeadMarks();
        return $"Saldırınca düşmana cyst yerleştir. Skip ile patlat.\nDüşman başına %{percent} hasar × işaretli düşman sayısı.\nİşaretli: {markedEnemyIDs.Count}";
    }

    private void ShowCystVisual(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            enemy.StartCoroutine(CystFlash(sr));
    }

    private System.Collections.IEnumerator CystFlash(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        Color cystColor = new Color(0.2f, 0.8f, 0.2f, original.a); // Green
        sr.color = cystColor;
        float dur = 0.3f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (sr == null) yield break;
            sr.color = Color.Lerp(cystColor, original, elapsed / dur);
            yield return null;
        }
        if (sr != null) sr.color = original;
    }

    private void ShowDetonateVFX(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            enemy.StartCoroutine(DetonateFlash(sr));
        CameraController.ShakeLighter();
    }

    private System.Collections.IEnumerator DetonateFlash(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        Color blastColor = new Color(0.8f, 1f, 0.2f, original.a); // Yellow-green
        sr.color = blastColor;
        float dur = 0.4f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (sr == null) yield break;
            sr.color = Color.Lerp(blastColor, original, elapsed / dur);
            yield return null;
        }
        if (sr != null) sr.color = original;
    }
}
