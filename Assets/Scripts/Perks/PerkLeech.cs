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
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Earn a implant fragment when you kill an elite enemy. {needed} fragments = random implant.\nFragments: {current}";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "needed",  $"+{FRAGMENTS_NEEDED}" },
        { "current", $"+{fragments}/{FRAGMENTS_NEEDED}" }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.isElite || isMerging) return;

        fragments++;
        RebuildDescription();

        TriggerVisualPop();

        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.TriggerPopForPerk(this);

        if (fragments >= FRAGMENTS_NEEDED)
        {
            fragments = 0;
            RebuildDescription();
            StartCoroutine(MergeFragmentsAndGrantPerk());
        }
    }

    private IEnumerator MergeFragmentsAndGrantPerk()
    {
        isMerging = true;

        GameObject perkPrefab = null;
        if (LevelUpManager.instance != null)
            perkPrefab = LevelUpManager.instance.GetRandomPerkByRarity(false);

        if (perkPrefab == null) { isMerging = false; yield break; }

        Camera cam = Camera.main;
        if (cam == null) { GrantPerkDirect(perkPrefab); isMerging = false; yield break; }

        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));
        center.z = -5f;

        // Dimmer
        GameObject dimmer = new GameObject("PerkLeechDimmer");
        SpriteRenderer dimmerSR = dimmer.AddComponent<SpriteRenderer>();
        dimmerSR.sprite = CreateSquareSprite();
        dimmerSR.color = new Color(0f, 0f, 0f, 0f);
        dimmerSR.sortingOrder = 200;
        dimmer.transform.position = center;
        dimmer.transform.localScale = Vector3.one * 50f;

        // Dimmer fade in
        float dur = 0.2f;
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            dimmerSR.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.5f, e / dur));
            yield return null;
        }

        // 3 fragment — ucgen formasyonda
        float radius = 0.5f;
        float fragScale = 0.2f;
        List<Transform> frags = new List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f + 90f) * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            GameObject frag = new GameObject($"Fragment_{i}");
            SpriteRenderer sr = frag.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDiamondSprite();
            sr.color = new Color(0.5f, 1f, 0.5f, 0.9f);
            sr.sortingOrder = 201;
            frag.transform.position = pos;
            frag.transform.localScale = Vector3.zero;
            frags.Add(frag.transform);
        }

        // Pop in
        dur = 0.3f; e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float s = EaseOutBack(e / dur) * fragScale;
            foreach (var f in frags) f.localScale = Vector3.one * s;
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        // Orbit + converge
        dur = 0.6f; e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float t = e / dur;
            float eased = t * t;
            float spin = t * 360f * 2f;
            float curR = Mathf.Lerp(radius, 0f, eased);

            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 90f + spin) * Mathf.Deg2Rad;
                frags[i].position = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * curR;
                frags[i].position = new Vector3(frags[i].position.x, frags[i].position.y, -5f);
                float s = Mathf.Lerp(fragScale, fragScale * 0.3f, eased);
                frags[i].localScale = Vector3.one * s;

                SpriteRenderer sr = frags[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.Lerp(new Color(0.5f, 1f, 0.5f, 0.9f), new Color(1f, 1f, 1f, 1f), t);
            }
            yield return null;
        }

        foreach (var f in frags) Object.Destroy(f.gameObject);

        // Flash
        GameObject flash = new GameObject("MergeFlash");
        SpriteRenderer flashSR = flash.AddComponent<SpriteRenderer>();
        flashSR.sprite = CreateCircleSprite();
        flashSR.color = Color.white;
        flashSR.sortingOrder = 202;
        flash.transform.position = center;

        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();
        CameraController.ShakeLight();

        dur = 0.25f; e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float t = e / dur;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0f, 1.5f, t);
            flashSR.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        Object.Destroy(flash);

        yield return new WaitForSeconds(0.15f);

        dur = 0.2f; e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            dimmerSR.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.5f, 0f, e / dur));
            yield return null;
        }

        Object.Destroy(dimmer);

        GrantPerkDirect(perkPrefab);
        isMerging = false;
    }

    private void GrantPerkDirect(GameObject perkPrefab)
    {
        if (RunManager.instance == null) return;
        RunManager.instance.AddPerk(perkPrefab);

        BasePerk prefabScript = perkPrefab.GetComponent<BasePerk>();
        if (prefabScript != null && ActivePerkBar.instance != null)
        {
            BasePerk active = RunManager.instance.activePerks.Find(p => p.GetType() == prefabScript.GetType());
            if (active != null)
                ActivePerkBar.instance.TriggerPopForPerk(active);
        }
    }

    private float EaseOutBack(float t)
    {
        float overshoot = 1.4f;
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }

    // --- Sprite helpers ---

    private static Sprite _circle;
    private static Sprite CreateCircleSprite()
    {
        if (_circle != null) return _circle;
        int r = 64;
        Texture2D tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        float c = r / 2f;
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, r, r), Vector2.one * 0.5f, r);
        return _circle;
    }

    private static Sprite _diamond;
    private static Sprite CreateDiamondSprite()
    {
        if (_diamond != null) return _diamond;
        int r = 32;
        Texture2D tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        float c = r / 2f;
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) / c;
                tex.SetPixel(x, y, d < 0.9f ? Color.white : Color.clear);
            }
        tex.Apply();
        _diamond = Sprite.Create(tex, new Rect(0, 0, r, r), Vector2.one * 0.5f, r);
        return _diamond;
    }

    private static Sprite _square;
    private static Sprite CreateSquareSprite()
    {
        if (_square != null) return _square;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4);
        return _square;
    }
}
