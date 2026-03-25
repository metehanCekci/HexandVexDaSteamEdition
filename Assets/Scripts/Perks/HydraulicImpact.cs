using UnityEngine;
using System.Collections;

public class HydraulicImpactPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Epic;
    }

    public float GetWallDamagePercent()
    {
        switch (currentLevel)
        {
            case 1: return 0.25f;
            case 2: return 0.50f;
            default: return 0.50f;
        }
    }

    public void ApplyWallDamage(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null || enemy.health.currentHP <= 0) return;
        int wallDamage = Mathf.CeilToInt(enemy.health.maxHP * GetWallDamagePercent());
        wallDamage = Mathf.Max(1, wallDamage);
        enemy.health.TakeDamage(wallDamage);
        ShowWallImpactVFX(enemy);
        TriggerVisualPop();
    }

    public void ShowWallImpactVFX(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
        enemy.StartCoroutine(WallImpactFlash(enemy));
    }

    private IEnumerator WallImpactFlash(EnemyMovement enemy)
    {
        if (enemy == null) yield break;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;

        CameraController.ShakeLight();
        if (AudioManager.instance != null) AudioManager.instance.PlayTakeDamage();

        Color original = sr.color;
        Color impactColor = new Color(0.6f, 0.6f, 1f, original.a);

        sr.color = impactColor;
        float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (sr == null) yield break;
            sr.color = Color.Lerp(impactColor, original, elapsed / dur);
            yield return null;
        }
        if (sr != null) sr.color = original;
    }
}
