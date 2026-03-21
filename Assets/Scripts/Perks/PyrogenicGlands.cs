using UnityEngine;
using System.Collections.Generic;

public class PyrogenicGlandsPerk : BasePerk
{
    // Track burn state per enemy instance ID
    private Dictionary<int, int> burnTurnsRemaining = new Dictionary<int, int>();

    void OnEnable()
    {
        maxLevel = 3;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        // Burn is applied after attack in TurnManager via ApplyBurn
        // This perk just marks itself as active — actual ignite happens in OnAttackHit
    }

    /// <summary>
    /// Called by TurnManager after a successful attack on an enemy.
    /// </summary>
    public void ApplyBurn(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health.currentHP <= 0) return;
        int id = enemy.GetInstanceID();
        burnTurnsRemaining[id] = 2; // Always 2 turns of burn
        TriggerVisualPop();

        // Visual: tint enemy orange briefly
        ShowBurnVFX(enemy);
    }

    /// <summary>
    /// Called at the start of each player turn to tick burn damage on all burning enemies.
    /// </summary>
    public void TickBurns()
    {
        if (TurnManager.instance == null) return;

        int damage = currentLevel; // Lv1=1, Lv2=2, Lv3=3
        List<int> toRemove = new List<int>();

        foreach (var kvp in burnTurnsRemaining)
        {
            int enemyId = kvp.Key;
            int turnsLeft = kvp.Value;

            // Find the enemy
            EnemyMovement enemy = null;
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e != null && e.GetInstanceID() == enemyId)
                {
                    enemy = e;
                    break;
                }
            }

            if (enemy == null || enemy.health.currentHP <= 0)
            {
                toRemove.Add(enemyId);
                continue;
            }

            // Deal burn damage
            enemy.health.TakeDamage(damage);
            ShowBurnVFX(enemy);

            if (turnsLeft <= 1)
                toRemove.Add(enemyId);
            else
                burnTurnsRemaining[enemyId] = turnsLeft - 1;
        }

        foreach (int id in toRemove)
            burnTurnsRemaining.Remove(id);
    }

    public override void OnLevelStart()
    {
        burnTurnsRemaining.Clear();
    }

    private void ShowBurnVFX(EnemyMovement enemy)
    {
        if (enemy == null) return;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && enemy.gameObject.activeInHierarchy)
        {
            enemy.StartCoroutine(BurnFlash(sr));
        }
    }

    private System.Collections.IEnumerator BurnFlash(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        Color burnColor = new Color(1f, 0.5f, 0f, original.a); // Orange
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
}
