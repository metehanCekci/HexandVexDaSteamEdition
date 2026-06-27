using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BioBarrierPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "level",  GameKeywords.Action("level") },
        { "shield", GameKeywords.Status("shield") }
    };

    [Header("Obje AyarlarÄ±")]
    public GameObject shieldPrefab; // Kalkan objen
    
    // ========================================================
    // YENÄ°: KalkanÄ±n Y eksenindeki yÃ¼ksekliÄŸi (Inspector'dan ayarla)
    // ========================================================
    public float shieldOffsetY = 0.07f; 
    
    private GameObject currentShieldInstance;

    public override void OnAcquire()
    {
        RunManager.instance.hasBioBarrier = true;
        SpawnShield();
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        RunManager.instance.hasBioBarrier = true;
        SpawnShield();
        TriggerVisualPop();
    }

    public override void OnUnequip()
    {
        RunManager.instance.hasBioBarrier = false;
        if (currentShieldInstance != null)
        {
            Destroy(currentShieldInstance);
            currentShieldInstance = null;
        }
    }

    public override void OnLevelStart()
    {
        RunManager.instance.hasBioBarrier = true;
        if (currentShieldInstance == null) SpawnShield();
        TriggerVisualPop();
    }

    private void SpawnShield()
    {
        if (currentShieldInstance != null) Destroy(currentShieldInstance);
        
        // KalkanÄ± OYUNCUNUN Ã¼stÃ¼ne ekliyoruz
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            Transform playerTransform = TurnManager.instance.player.transform;
            currentShieldInstance = Instantiate(shieldPrefab, playerTransform.position, Quaternion.identity, playerTransform);
            
            // ========================================================
            // DÃœZELTME: KalkanÄ± karakterin merkezinden offset deÄŸeri kadar yukarÄ± taÅŸÄ±yoruz
            // ========================================================
            currentShieldInstance.transform.localPosition = new Vector3(0f, shieldOffsetY, 0f); 
            
            // PrefabÄ±n alpha deÄŸerini koru, hard code etme
        }
    }

    public void BreakShield()
    {
        if (currentShieldInstance != null)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayShieldBreak();
            StartCoroutine(AnimateShieldBreak());
        }
    }

    private IEnumerator AnimateShieldBreak()
    {
        SpriteRenderer[] renderers = currentShieldInstance.GetComponentsInChildren<SpriteRenderer>();
        float startAlpha = renderers.Length > 0 ? renderers[0].color.a : 1f;

        Vector3 startScale = currentShieldInstance.transform.localScale;
        Vector3 endScale = startScale * 2.5f;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            currentShieldInstance.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            foreach (var sr in renderers)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(startAlpha, 0f, t);
                sr.color = c;
            }
            
            yield return null;
        }
        
        // Animasyon bitince kalkanÄ± tamamen sil
        Destroy(currentShieldInstance);
        currentShieldInstance = null;
    }
}