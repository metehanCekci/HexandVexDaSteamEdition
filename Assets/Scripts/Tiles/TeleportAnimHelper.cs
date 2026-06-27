using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Teleport animasyon yardımcısı.
/// Hem SynapticAnchorPerk hem de TeleportTile tarafından kullanılır.
/// </summary>
public static class TeleportAnimHelper
{
    /// <summary>
    /// Entity'yi fade-out → teleport → fade-in animasyonu ile ışınlar.
    /// </summary>
    /// <param name="entityGO">Işınlanacak GameObject</param>
    /// <param name="teleportAction">Işınlama anında çağrılacak Action (ForceSetPosition vb.)</param>
    /// <param name="fadeOutDuration">Kaybolma süresi</param>
    /// <param name="fadeInDuration">Belirme süresi</param>
    public static IEnumerator TeleportEntity(
        MonoBehaviour runner,
        GameObject entityGO,
        Action teleportAction,
        float fadeOutDuration = 0.15f,
        float fadeInDuration = 0.2f)
    {
        SpriteRenderer sr = entityGO.GetComponentInChildren<SpriteRenderer>();

        // Fade out
        if (sr != null)
        {
            float elapsed = 0f;
            Color startColor = sr.color;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }
        }

        // Işınlama
        teleportAction?.Invoke();

        // Fade in (scale pop)
        if (sr != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                float scale = 1f + (1f - t) * 0.3f;
                entityGO.transform.localScale = Vector3.one * scale;
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            entityGO.transform.localScale = Vector3.one;
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }
    }
}
