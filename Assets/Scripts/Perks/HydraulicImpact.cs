using UnityEngine;
using System.Collections;

public class HydraulicImpactPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
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
        Color impactColor = new Color(0.6f, 0.6f, 1f, original.a); // Blue-ish impact

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
