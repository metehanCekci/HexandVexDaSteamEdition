using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PerkLeechPerk : BasePerk
{
    [HideInInspector] public int fragments = 0;
    private const int FRAGMENTS_NEEDED = 3;
    private bool isMerging = false;

    void OnEnable()
    {
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.isElite || isMerging) return;

        fragments++;
        description = GetDescription();

        TriggerVisualPop();

        // Fragment kazanıldı popup
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.TriggerPopForPerk(this);

        if (fragments >= FRAGMENTS_NEEDED)
        {
            fragments = 0;
            description = GetDescription();
            StartCoroutine(MergeFragmentsAndGrantPerk());
        }
    }

    private string GetDescription()
    {
        return $"Elite düşman öldürdüğünde bir perk parçası kazan. 3 parça = rastgele perk.\nParçalar: {fragments}/{FRAGMENTS_NEEDED}";
    }

    private IEnumerator MergeFragmentsAndGrantPerk()
    {
        isMerging = true;

        // Get random perk prefab
        GameObject perkPrefab = null;
        if (LevelUpManager.instance != null)
            perkPrefab = LevelUpManager.instance.GetRandomPerkByRarity(false);

        if (perkPrefab == null) { isMerging = false; yield break; }

        // --- MERGE ANIMATION ---
        // Create 3 fragment objects that fly toward center and merge
        Canvas canvas = GetMergeCanvas();
        if (canvas == null) { GrantPerkDirect(perkPrefab); isMerging = false; yield break; }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect.sizeDelta;
        Vector2 center = canvasSize * 0.5f;

        // Dimmer background
        GameObject dimmer = CreateDimmer(canvas.transform);

        // Create 3 fragment sprites at triangle positions around center
        float radius = 200f;
        List<RectTransform> fragmentObjects = new List<RectTransform>();
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f + 90f) * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            GameObject frag = CreateFragmentUI(canvas.transform, pos, i);
            fragmentObjects.Add(frag.GetComponent<RectTransform>());
        }

        // Phase 1: Fragments appear with pop (0.3s)
        float popDur = 0.3f;
        float popElapsed = 0f;
        while (popElapsed < popDur)
        {
            popElapsed += Time.unscaledDeltaTime;
            float t = popElapsed / popDur;
            float scale = EaseOutBack(t);
            foreach (var frag in fragmentObjects)
                frag.localScale = Vector3.one * scale * 0.8f;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        // Phase 2: Fragments orbit and converge toward center (0.8s)
        float mergeDur = 0.8f;
        float mergeElapsed = 0f;
        List<Vector2> startPositions = new List<Vector2>();
        foreach (var frag in fragmentObjects)
            startPositions.Add(frag.anchoredPosition);

        while (mergeElapsed < mergeDur)
        {
            mergeElapsed += Time.unscaledDeltaTime;
            float t = mergeElapsed / mergeDur;
            float eased = t * t; // EaseIn — slow start, fast end

            // Spin while converging
            float spinAngle = t * 360f * 2f; // 2 full rotations

            for (int i = 0; i < 3; i++)
            {
                float baseAngle = (i * 120f + 90f + spinAngle) * Mathf.Deg2Rad;
                float currentRadius = Mathf.Lerp(radius, 0f, eased);
                Vector2 pos = center + new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * currentRadius;
                fragmentObjects[i].anchoredPosition = pos;

                // Scale down as they approach center
                float scale = Mathf.Lerp(0.8f, 0.3f, eased);
                fragmentObjects[i].localScale = Vector3.one * scale;

                // Glow brighter
                var img = fragmentObjects[i].GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    float glow = Mathf.Lerp(0.7f, 1f, t);
                    img.color = new Color(glow, 1f, glow, 1f);
                }
            }
            yield return null;
        }

        // Destroy fragments
        foreach (var frag in fragmentObjects)
            Object.Destroy(frag.gameObject);

        // Phase 3: Flash + Perk reveal (0.5s)
        // Flash
        GameObject flash = CreateFlash(canvas.transform, center);
        float flashDur = 0.15f;
        float flashElapsed = 0f;
        RectTransform flashRT = flash.GetComponent<RectTransform>();
        while (flashElapsed < flashDur)
        {
            flashElapsed += Time.unscaledDeltaTime;
            float t = flashElapsed / flashDur;
            flashRT.localScale = Vector3.one * Mathf.Lerp(0f, 3f, t);
            var img = flash.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        Object.Destroy(flash);

        // Show resulting perk icon + name
        BasePerk perkScript = perkPrefab.GetComponent<BasePerk>();
        GameObject reveal = CreatePerkReveal(canvas.transform, center, perkScript);

        // Pop in
        RectTransform revealRT = reveal.GetComponent<RectTransform>();
        float revealDur = 0.4f;
        float revealElapsed = 0f;
        while (revealElapsed < revealDur)
        {
            revealElapsed += Time.unscaledDeltaTime;
            float t = revealElapsed / revealDur;
            revealRT.localScale = Vector3.one * EaseOutBack(t);
            yield return null;
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();
        CameraController.ShakeLight();

        yield return new WaitForSecondsRealtime(1.2f);

        // Fade out reveal + dimmer
        float fadeDur = 0.3f;
        float fadeElapsed = 0f;
        var revealGroup = reveal.AddComponent<CanvasGroup>();
        var dimmerGroup = dimmer.GetComponent<CanvasGroup>();
        while (fadeElapsed < fadeDur)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeElapsed / fadeDur;
            if (revealGroup != null) revealGroup.alpha = 1f - t;
            if (dimmerGroup != null) dimmerGroup.alpha = Mathf.Lerp(0.5f, 0f, t);
            yield return null;
        }

        Object.Destroy(reveal);
        Object.Destroy(dimmer);

        // Actually grant the perk
        GrantPerkDirect(perkPrefab);
        isMerging = false;
    }

    private void GrantPerkDirect(GameObject perkPrefab)
    {
        if (RunManager.instance != null)
            RunManager.instance.AddPerk(perkPrefab);
    }

    // --- UI Helpers ---

    private Canvas GetMergeCanvas()
    {
        // Use existing high-sort canvas or find one
        GameObject canvasObj = GameObject.Find("MainCanvas");
        if (canvasObj != null) return canvasObj.GetComponent<Canvas>();

        // Fallback
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        int bestOrder = int.MinValue;
        foreach (var c in allCanvases)
        {
            if (c.sortingOrder > bestOrder)
            {
                bestOrder = c.sortingOrder;
                best = c;
            }
        }
        return best;
    }

    private GameObject CreateDimmer(Transform parent)
    {
        GameObject obj = new GameObject("PerkLeechDimmer");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f);
        img.raycastTarget = false;
        var cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0.5f;
        cg.blocksRaycasts = false;
        return obj;
    }

    private GameObject CreateFragmentUI(Transform parent, Vector2 position, int index)
    {
        GameObject obj = new GameObject($"Fragment_{index}");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(60f, 60f);
        rt.localScale = Vector3.zero;

        var img = obj.AddComponent<UnityEngine.UI.Image>();
        // Diamond/crystal shape - use a rotated square as fragment
        img.color = new Color(0.5f, 1f, 0.5f, 0.9f);
        img.raycastTarget = false;
        rt.localRotation = Quaternion.Euler(0, 0, 45f);

        return obj;
    }

    private GameObject CreateFlash(Transform parent, Vector2 position)
    {
        GameObject obj = new GameObject("MergeFlash");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.localScale = Vector3.zero;

        var img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.white;
        img.raycastTarget = false;

        return obj;
    }

    private GameObject CreatePerkReveal(Transform parent, Vector2 position, BasePerk perkData)
    {
        GameObject obj = new GameObject("PerkReveal");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(180f, 220f);
        rt.localScale = Vector3.zero;

        // Background
        var bg = obj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.02f, 0.02f, 0.05f, 0.9f);
        bg.raycastTarget = false;

        // Perk icon
        if (perkData != null && perkData.icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(obj.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchoredPosition = new Vector2(0, 25f);
            iconRT.sizeDelta = new Vector2(80f, 80f);
            var iconImg = iconObj.AddComponent<UnityEngine.UI.Image>();
            iconImg.sprite = perkData.icon;
            iconImg.type = UnityEngine.UI.Image.Type.Simple;
            iconImg.raycastTarget = false;
        }

        // Perk name
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(obj.transform, false);
        RectTransform nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.anchoredPosition = new Vector2(0, -45f);
        nameRT.sizeDelta = new Vector2(170f, 40f);
        var nameText = nameObj.AddComponent<TMPro.TMP_Text>();
        // Use TextMeshProUGUI for canvas
        Object.Destroy(nameText);
        var nameTMP = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
        nameTMP.text = perkData != null ? perkData.perkName : "???";
        nameTMP.fontSize = 18f;
        nameTMP.alignment = TMPro.TextAlignmentOptions.Center;
        nameTMP.color = GetRarityColor(perkData);
        nameTMP.raycastTarget = false;

        // FontOverrider will handle the font
        return obj;
    }

    private Color GetRarityColor(BasePerk perk)
    {
        if (perk == null) return Color.white;
        switch (perk.rarity)
        {
            case PerkRarity.Common: return Color.white;
            case PerkRarity.Rare: return new Color(0.3f, 0.6f, 1f);
            case PerkRarity.Epic: return new Color(0.7f, 0.3f, 1f);
            case PerkRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
            default: return Color.white;
        }
    }

    private float EaseOutBack(float t)
    {
        float overshoot = 1.4f;
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}
