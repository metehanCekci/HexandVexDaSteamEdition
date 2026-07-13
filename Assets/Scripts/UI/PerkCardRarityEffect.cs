using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Perk kartlarına enderlik bazlı görsel efekt ekler.
/// Common: efekt yok.
/// Rare: glow pulse + frost vein pulse + mist drift.
/// Epic: glow + purple sweep + arcane pulse ring + void spiral particles + edge sparks.
/// Legendary: soft amber glow + toned light rays + icon float + ember rise + heat wave.
/// </summary>
public class PerkCardRarityEffect : MonoBehaviour
{
    private PerkRarity rarity;
    private RectTransform cardRect;
    private RectTransform iconRT; // Legendary icon float

    // ── Glow ──
    private Image glowImage;
    private Color glowColorA, glowColorB;
    private float pulseTimer;

    // ── Frost Veins (Rare) ──
    private const int VEIN_COUNT = 8;
    private RectTransform[] veinRTs;
    private Image[] veinImgs;
    private float veinTimer;

    // ── Mist Drift (Rare) ──
    private const int MIST_COUNT = 3;
    private RectTransform[] mistRTs;
    private Image[] mistImgs;
    private float[] mistSeeds;
    private bool mistInit;

    // ── Purple Sweep (Epic) ──
    private RectTransform sweepRT;
    private Image sweepImg;
    private float sweepTimer;

    // ── Arcane Pulse Ring (Epic) ──
    private RectTransform ringRT;
    private Image ringImg;
    private float ringTimer;

    // ── Void Spiral Particles (Epic) ──
    private const int VOID_COUNT = 10;
    private RectTransform[] voidRTs;
    private Image[] voidImgs;
    private float[] voidPhases;
    private float voidTimer;

    // ── Edge Sparks (Epic) ──
    private const int SPARK_COUNT = 6;
    private RectTransform[] sparkRTs;
    private Image[] sparkImgs;
    private float[] sparkTimers;
    private float[] sparkCooldowns;
    private bool sparksPositioned;

    // ── Light Rays (Legendary) ──
    private RectTransform raysHub;
    private float raysAngle;

    // ── Ember Rise (Legendary) ──
    private const int EMBER_COUNT = 12;
    private RectTransform[] emberRTs;
    private Image[] emberImgs;
    private float[] emberSeeds;
    private bool embersInit;

    // ── Heat Wave (Legendary) ──
    private RectTransform heatRingRT;
    private Image heatRingImg;
    private float heatTimer;

    // ── Icon Float (Legendary) ──
    private Vector2 iconOrigPos;
    private float iconFloatTimer;
    private bool iconFloatActive;

    public void Setup(PerkRarity r, RectTransform iconTransform = null)
    {
        rarity = r;
        cardRect = GetComponent<RectTransform>();
        iconRT = iconTransform;
        Cleanup();

        if (rarity == PerkRarity.Common || rarity == PerkRarity.Secret) return;

        Color baseCol = RarityBaseColor(rarity);
        glowColorA = baseCol;
        glowColorB = HueShift(baseCol, rarity == PerkRarity.Legendary ? 0.03f : 0.07f);
        pulseTimer = Random.Range(0f, Mathf.PI * 2f);

        // ── ALL NON-COMMON: Glow ──
        BuildGlow(baseCol);

        // ── RARE ──
        if (rarity == PerkRarity.Rare)
        {
            BuildFrostVeins();
            BuildMistDrift();
        }

        // ── EPIC ──
        if (rarity == PerkRarity.Epic)
        {
            BuildPurpleSweep();
            BuildArcanePulseRing();
            BuildVoidSpiral();
            BuildEdgeSparks();
        }

        // ── LEGENDARY ──
        if (rarity == PerkRarity.Legendary)
        {
            BuildLightRays();
            BuildEmberRise();
            BuildHeatWave();
            SetupIconFloat();
        }
    }

    // ═══════════════════════ BUILD ═══════════════════════

    private void BuildGlow(Color col)
    {
        var go = MakeChild("RarityGlow");
        go.transform.SetAsFirstSibling();
        float extend = rarity == PerkRarity.Legendary ? 6f : 5f;
        Stretch(go, extend);

        glowImage = go.AddComponent<Image>();
        float startAlpha = rarity == PerkRarity.Legendary ? 0.3f : 0.45f;
        glowImage.color = WithAlpha(col, startAlpha);
        glowImage.raycastTarget = false;
    }

    // ─── RARE: Frost Veins ───
    private void BuildFrostVeins()
    {
        var mask = MakeChild("FrostVeins");
        Stretch(mask, 0f);
        AddMask(mask);

        veinRTs = new RectTransform[VEIN_COUNT];
        veinImgs = new Image[VEIN_COUNT];

        for (int i = 0; i < VEIN_COUNT; i++)
        {
            var v = MakeChild("FV_" + i, mask.transform);
            veinRTs[i] = v.GetComponent<RectTransform>();
            veinRTs[i].anchorMin = veinRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            // Thin line shapes
            float w = Random.Range(2f, 4f);
            float h = Random.Range(20f, 50f);
            veinRTs[i].sizeDelta = new Vector2(w, h);
            // Random rotation for branch-like look
            veinRTs[i].localRotation = Quaternion.Euler(0, 0, Random.Range(-80f, 80f));

            veinImgs[i] = v.AddComponent<Image>();
            veinImgs[i].color = new Color(0.6f, 0.85f, 1f, 0f);
            veinImgs[i].raycastTarget = false;
        }
        veinTimer = 0f;
    }

    // ─── RARE: Mist Drift ───
    private void BuildMistDrift()
    {
        var mask = MakeChild("MistDrift");
        Stretch(mask, 0f);
        AddMask(mask);

        mistRTs = new RectTransform[MIST_COUNT];
        mistImgs = new Image[MIST_COUNT];
        mistSeeds = new float[MIST_COUNT];

        Sprite radial = MakeRadialSprite();
        for (int i = 0; i < MIST_COUNT; i++)
        {
            var m = MakeChild("Mist_" + i, mask.transform);
            mistRTs[i] = m.GetComponent<RectTransform>();
            mistRTs[i].anchorMin = mistRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            float sz = Random.Range(60f, 100f);
            mistRTs[i].sizeDelta = new Vector2(sz, sz * 0.5f);

            mistImgs[i] = m.AddComponent<Image>();
            mistImgs[i].sprite = radial;
            mistImgs[i].color = new Color(0.5f, 0.75f, 1f, 0f);
            mistImgs[i].raycastTarget = false;

            mistSeeds[i] = Random.Range(0f, 100f);
        }
        mistInit = false;
    }

    // ─── EPIC: Purple Sweep ───
    private void BuildPurpleSweep()
    {
        var mask = MakeChild("SweepMask");
        mask.transform.SetAsLastSibling();
        Stretch(mask, 0f);
        AddMask(mask);

        var bar = MakeChild("SweepBar", mask.transform);
        sweepRT = bar.GetComponent<RectTransform>();
        sweepRT.sizeDelta = new Vector2(50f, 500f);
        sweepRT.localRotation = Quaternion.Euler(0, 0, 22f);

        sweepImg = bar.AddComponent<Image>();
        sweepImg.color = new Color(0.6f, 0.3f, 1f, 0f);
        sweepImg.raycastTarget = false;
        sweepTimer = Random.Range(0f, 3f);
    }

    // ─── EPIC: Arcane Pulse Ring ───
    private void BuildArcanePulseRing()
    {
        var mask = MakeChild("ArcaneRing");
        Stretch(mask, 0f);
        AddMask(mask);

        var ring = MakeChild("Ring", mask.transform);
        ringRT = ring.GetComponent<RectTransform>();
        ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f, 0.5f);
        ringRT.sizeDelta = new Vector2(20f, 20f);

        ringImg = ring.AddComponent<Image>();
        ringImg.sprite = MakeRingSprite();
        ringImg.color = new Color(0.65f, 0.3f, 1f, 0f);
        ringImg.raycastTarget = false;
        ringTimer = 0f;
    }

    // ─── EPIC: Void Spiral Particles ───
    private void BuildVoidSpiral()
    {
        var mask = MakeChild("VoidSpiral");
        Stretch(mask, 0f);
        AddMask(mask);

        voidRTs = new RectTransform[VOID_COUNT];
        voidImgs = new Image[VOID_COUNT];
        voidPhases = new float[VOID_COUNT];

        for (int i = 0; i < VOID_COUNT; i++)
        {
            var p = MakeChild("VP_" + i, mask.transform);
            voidRTs[i] = p.GetComponent<RectTransform>();
            voidRTs[i].anchorMin = voidRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            float sz = Random.Range(3f, 6f);
            voidRTs[i].sizeDelta = new Vector2(sz, sz);

            voidImgs[i] = p.AddComponent<Image>();
            voidImgs[i].color = new Color(0.7f, 0.4f, 1f, 0f);
            voidImgs[i].raycastTarget = false;

            voidPhases[i] = (i / (float)VOID_COUNT) * Mathf.PI * 2f;
        }
        voidTimer = 0f;
    }

    // ─── EPIC: Edge Sparks ───
    private void BuildEdgeSparks()
    {
        var container = MakeChild("EdgeSparks");
        Stretch(container, 0f);

        sparkRTs = new RectTransform[SPARK_COUNT];
        sparkImgs = new Image[SPARK_COUNT];
        sparkTimers = new float[SPARK_COUNT];
        sparkCooldowns = new float[SPARK_COUNT];

        for (int i = 0; i < SPARK_COUNT; i++)
        {
            var s = MakeChild("ES_" + i, container.transform);
            sparkRTs[i] = s.GetComponent<RectTransform>();
            sparkRTs[i].anchorMin = sparkRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            sparkRTs[i].sizeDelta = new Vector2(8f, 8f);

            sparkImgs[i] = s.AddComponent<Image>();
            sparkImgs[i].color = new Color(0.8f, 0.5f, 1f, 0f);
            sparkImgs[i].raycastTarget = false;

            sparkTimers[i] = 0f;
            sparkCooldowns[i] = Random.Range(0.3f, 1.5f);
        }
        sparksPositioned = false;
    }

    // ─── LEGENDARY: Light Rays (toned down) ───
    private void BuildLightRays()
    {
        var container = MakeChild("LightRays");
        container.transform.SetSiblingIndex(1);
        Stretch(container, 0f);
        AddMask(container);

        var pivot = MakeChild("RayPivot", container.transform);
        var pivRT = pivot.GetComponent<RectTransform>();
        pivRT.anchorMin = pivRT.anchorMax = new Vector2(0.5f, 0.5f);
        pivRT.sizeDelta = Vector2.zero;
        raysHub = pivRT;

        for (int i = 0; i < 5; i++)
        {
            var ray = MakeChild("R_" + i, pivot.transform);
            var rrt = ray.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            float width = Random.Range(12f, 22f);
            rrt.sizeDelta = new Vector2(width, 500f);
            rrt.localRotation = Quaternion.Euler(0, 0, (i / 5f) * 360f + Random.Range(-10f, 10f));

            var img = ray.AddComponent<Image>();
            // Toned down: lower alpha
            img.color = new Color(0.95f, 0.6f, 0.2f, Random.Range(0.025f, 0.05f));
            img.raycastTarget = false;
        }
        raysAngle = Random.Range(0f, 360f);
    }

    // ─── LEGENDARY: Ember Rise ───
    private void BuildEmberRise()
    {
        var mask = MakeChild("EmberRise");
        mask.transform.SetAsLastSibling();
        Stretch(mask, 0f);
        AddMask(mask);

        emberRTs = new RectTransform[EMBER_COUNT];
        emberImgs = new Image[EMBER_COUNT];
        emberSeeds = new float[EMBER_COUNT];

        for (int i = 0; i < EMBER_COUNT; i++)
        {
            var e = MakeChild("EM_" + i, mask.transform);
            emberRTs[i] = e.GetComponent<RectTransform>();
            emberRTs[i].anchorMin = emberRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            float sz = Random.Range(2f, 5f);
            emberRTs[i].sizeDelta = new Vector2(sz, sz);

            emberImgs[i] = e.AddComponent<Image>();
            emberImgs[i].color = new Color(1f, 0.6f, 0.2f, 0f);
            emberImgs[i].raycastTarget = false;

            emberSeeds[i] = Random.Range(0f, 100f);
        }
        embersInit = false;
    }

    // ─── LEGENDARY: Heat Wave ───
    private void BuildHeatWave()
    {
        var mask = MakeChild("HeatWave");
        Stretch(mask, 0f);
        AddMask(mask);

        var ring = MakeChild("HRing", mask.transform);
        heatRingRT = ring.GetComponent<RectTransform>();
        heatRingRT.anchorMin = heatRingRT.anchorMax = new Vector2(0.5f, 0.5f);
        heatRingRT.sizeDelta = new Vector2(10f, 10f);

        heatRingImg = ring.AddComponent<Image>();
        heatRingImg.sprite = MakeRingSprite();
        heatRingImg.color = new Color(1f, 0.55f, 0.15f, 0f);
        heatRingImg.raycastTarget = false;
        heatTimer = 0f;
    }

    // ─── LEGENDARY: Icon Float ───
    private void SetupIconFloat()
    {
        if (iconRT == null) return;
        iconOrigPos = iconRT.anchoredPosition;
        iconFloatTimer = Random.Range(0f, Mathf.PI * 2f);
        iconFloatActive = true;
    }

    // ═══════════════════════ UPDATE ═══════════════════════

    void Update()
    {
        if (rarity == PerkRarity.Common || rarity == PerkRarity.Secret) return;
        float dt = Time.unscaledDeltaTime;

        TickGlow(dt);

        if (rarity == PerkRarity.Rare)
        {
            TickFrostVeins(dt);
            TickMistDrift(dt);
        }

        if (rarity == PerkRarity.Epic)
        {
            TickPurpleSweep(dt);
            TickArcanePulseRing(dt);
            TickVoidSpiral(dt);
            TickEdgeSparks(dt);
        }

        if (rarity == PerkRarity.Legendary)
        {
            TickLightRays(dt);
            TickEmberRise(dt);
            TickHeatWave(dt);
            TickIconFloat(dt);
        }
    }

    // ═══════════════════════ TICK ═══════════════════════

    private void TickGlow(float dt)
    {
        if (glowImage == null) return;
        pulseTimer += dt * PulseSpeed();
        float blend = (Mathf.Sin(pulseTimer) + 1f) * 0.5f;
        Color c = Color.Lerp(glowColorA, glowColorB, blend);
        float a = BaseAlpha() + Mathf.Sin(pulseTimer * 1.3f) * PulseAmp();
        glowImage.color = new Color(c.r, c.g, c.b, a);
    }

    // ─── RARE ───

    private void TickFrostVeins(float dt)
    {
        if (veinRTs == null || cardRect == null) return;
        if (cardRect.rect.width <= 0) return;

        veinTimer += dt;
        float hw = cardRect.rect.width * 0.45f;
        float hh = cardRect.rect.height * 0.45f;

        // Cycle: veins appear in a wave, hold briefly, then fade
        float cycleDur = 4f;
        float t = Mathf.Repeat(veinTimer, cycleDur);

        for (int i = 0; i < VEIN_COUNT; i++)
        {
            if (veinRTs[i] == null) continue;

            // Stagger each vein's appearance
            float stagger = (i / (float)VEIN_COUNT) * 1.5f;
            float localT = t - stagger;

            float alpha = 0f;
            if (localT > 0f && localT < 2f)
            {
                if (localT < 0.4f)
                    alpha = localT / 0.4f; // fade in
                else if (localT < 1.2f)
                    alpha = 1f; // hold
                else
                    alpha = 1f - (localT - 1.2f) / 0.8f; // fade out
            }
            alpha = Mathf.Clamp01(alpha) * 0.5f;

            veinImgs[i].color = new Color(0.6f, 0.85f, 1f, alpha);

            // Position veins along edges, pointing inward
            if (veinTimer < dt * 2f) // first frame positioning
            {
                int edge = i % 4; // 0=top, 1=right, 2=bottom, 3=left
                float edgeT = (i / 4f) % 1f;
                Vector2 pos = Vector2.zero;
                switch (edge)
                {
                    case 0: pos = new Vector2(Mathf.Lerp(-hw, hw, edgeT), hh); break;
                    case 1: pos = new Vector2(hw, Mathf.Lerp(hh, -hh, edgeT)); break;
                    case 2: pos = new Vector2(Mathf.Lerp(hw, -hw, edgeT), -hh); break;
                    case 3: pos = new Vector2(-hw, Mathf.Lerp(-hh, hh, edgeT)); break;
                }
                veinRTs[i].anchoredPosition = pos;
            }
        }
    }

    private void TickMistDrift(float dt)
    {
        if (mistRTs == null || cardRect == null) return;
        float hw = cardRect.rect.width * 0.5f;
        float hh = cardRect.rect.height * 0.5f;
        if (hw <= 0) return;

        if (!mistInit)
        {
            for (int i = 0; i < MIST_COUNT; i++)
            {
                float x = Mathf.Lerp(-hw * 1.2f, hw * 0.5f, mistSeeds[i] % 1f);
                float y = -hh * 0.5f + i * hh * 0.3f; // lower half, spread out
                mistRTs[i].anchoredPosition = new Vector2(x, y);
            }
            mistInit = true;
        }

        for (int i = 0; i < MIST_COUNT; i++)
        {
            if (mistRTs[i] == null) continue;
            var pos = mistRTs[i].anchoredPosition;

            // Slow horizontal drift
            float speed = 12f + mistSeeds[i] % 8f;
            pos.x += dt * speed;
            // Gentle vertical bob
            pos.y += Mathf.Sin(Time.unscaledTime * 0.8f + mistSeeds[i]) * dt * 3f;

            // Wrap from right to left
            if (pos.x > hw * 1.3f)
            {
                pos.x = -hw * 1.3f;
                pos.y = -hh * 0.6f + Random.Range(0f, hh * 0.6f);
            }
            mistRTs[i].anchoredPosition = pos;

            // Alpha: fade at edges, subtle overall
            float nx = (pos.x + hw) / (hw * 2f);
            float edgeFade = Mathf.Clamp01(Mathf.Min(nx * 3f, (1f - nx) * 3f));
            float breath = 0.06f + 0.04f * Mathf.Sin(Time.unscaledTime * 1.2f + mistSeeds[i]);
            mistImgs[i].color = new Color(0.5f, 0.75f, 1f, edgeFade * breath);
        }
    }

    // ─── EPIC ───

    private void TickPurpleSweep(float dt)
    {
        if (sweepRT == null || sweepImg == null || cardRect == null) return;
        float cw = cardRect.rect.width;
        if (cw <= 0) return;

        sweepTimer += dt * 1f;
        float cycle = 3.5f;
        float t = Mathf.Repeat(sweepTimer, cycle);

        if (t < 1f)
        {
            float x = Mathf.Lerp(-cw * 0.7f, cw * 0.7f, t);
            sweepRT.anchoredPosition = new Vector2(x, 0);

            // Purple tone shift (not rainbow)
            float hue = Mathf.Lerp(0.75f, 0.85f, Mathf.Sin(t * Mathf.PI) * 0.5f + 0.5f);
            Color purple = Color.HSVToRGB(hue, 0.6f, 1f);
            float edge = Mathf.Clamp01(Mathf.Min(t, 1f - t) * 5f);
            sweepImg.color = new Color(purple.r, purple.g, purple.b, 0.2f * edge);
        }
        else
        {
            sweepRT.anchoredPosition = new Vector2(-cw, 0);
        }
    }

    private void TickArcanePulseRing(float dt)
    {
        if (ringRT == null || ringImg == null || cardRect == null) return;
        if (cardRect.rect.width <= 0) return;

        ringTimer += dt;
        float cycle = 2.5f;
        float t = Mathf.Repeat(ringTimer, cycle);

        float maxSize = Mathf.Max(cardRect.rect.width, cardRect.rect.height) * 1.2f;

        if (t < 1.2f)
        {
            // Expand from center
            float progress = t / 1.2f;
            float ease = 1f - (1f - progress) * (1f - progress); // ease out
            float size = Mathf.Lerp(20f, maxSize, ease);
            ringRT.sizeDelta = new Vector2(size, size);

            // Fade out as it expands
            float alpha = (1f - ease) * 0.35f;
            ringImg.color = new Color(0.65f, 0.3f, 1f, alpha);
        }
        else
        {
            ringImg.color = new Color(0.65f, 0.3f, 1f, 0f);
            ringRT.sizeDelta = new Vector2(20f, 20f);
        }
    }

    private void TickVoidSpiral(float dt)
    {
        if (voidRTs == null || cardRect == null) return;
        if (cardRect.rect.width <= 0) return;

        voidTimer += dt;
        float hw = cardRect.rect.width * 0.35f;
        float hh = cardRect.rect.height * 0.35f;

        for (int i = 0; i < VOID_COUNT; i++)
        {
            if (voidRTs[i] == null) continue;

            float angle = voidPhases[i] + voidTimer * (0.8f + i * 0.05f);
            // Spiral: radius oscillates
            float radius = 0.5f + 0.5f * Mathf.Sin(voidTimer * 0.4f + voidPhases[i]);
            float px = Mathf.Cos(angle) * hw * radius;
            float py = Mathf.Sin(angle) * hh * radius;
            voidRTs[i].anchoredPosition = new Vector2(px, py);

            // Twinkle alpha
            float twinkle = Mathf.Max(0f, Mathf.Sin(voidTimer * 3f + voidPhases[i] * 2f));
            twinkle *= twinkle;
            voidImgs[i].color = new Color(0.7f, 0.4f, 1f, twinkle * 0.6f);

            // Size pulse
            float sz = 3f + twinkle * 4f;
            voidRTs[i].sizeDelta = new Vector2(sz, sz);
        }
    }

    private void TickEdgeSparks(float dt)
    {
        if (sparkRTs == null || cardRect == null) return;
        float hw = cardRect.rect.width * 0.5f;
        float hh = cardRect.rect.height * 0.5f;
        if (hw <= 0) return;

        if (!sparksPositioned)
        {
            for (int i = 0; i < SPARK_COUNT; i++)
            {
                float t = Random.Range(0f, 1f);
                sparkRTs[i].anchoredPosition = PerimeterPos(t, hw, hh);
            }
            sparksPositioned = true;
        }

        for (int i = 0; i < SPARK_COUNT; i++)
        {
            if (sparkImgs[i] == null) continue;

            sparkTimers[i] += dt;

            if (sparkTimers[i] >= sparkCooldowns[i])
            {
                // Flash!
                float flashT = sparkTimers[i] - sparkCooldowns[i];
                float flashDur = 0.15f;

                if (flashT < flashDur)
                {
                    float bright = 1f - (flashT / flashDur);
                    bright *= bright;
                    sparkImgs[i].color = new Color(0.8f, 0.5f, 1f, bright * 0.8f);
                    float sz = 6f + bright * 6f;
                    sparkRTs[i].sizeDelta = new Vector2(sz, sz);
                }
                else
                {
                    // Reset: new position, new cooldown
                    sparkImgs[i].color = new Color(0.8f, 0.5f, 1f, 0f);
                    sparkRTs[i].sizeDelta = new Vector2(8f, 8f);
                    sparkRTs[i].anchoredPosition = PerimeterPos(Random.Range(0f, 1f), hw, hh);
                    sparkTimers[i] = 0f;
                    sparkCooldowns[i] = Random.Range(0.3f, 1.5f);
                }
            }
            else
            {
                sparkImgs[i].color = new Color(0.8f, 0.5f, 1f, 0f);
            }
        }
    }

    // ─── LEGENDARY ───

    private void TickLightRays(float dt)
    {
        if (raysHub == null) return;
        raysAngle += dt * 4f; // slower rotation
        raysHub.localRotation = Quaternion.Euler(0, 0, raysAngle);
    }

    private void TickEmberRise(float dt)
    {
        if (emberRTs == null || cardRect == null) return;
        float hw = cardRect.rect.width * 0.4f;
        float hh = cardRect.rect.height * 0.5f;
        if (hh <= 0) return;

        if (!embersInit)
        {
            for (int i = 0; i < EMBER_COUNT; i++)
            {
                float x = Mathf.Lerp(-hw, hw, (emberSeeds[i] * 0.97f) % 1f);
                float y = Mathf.Lerp(-hh, hh, (emberSeeds[i] * 3.71f) % 1f);
                emberRTs[i].anchoredPosition = new Vector2(x, y);
            }
            embersInit = true;
        }

        for (int i = 0; i < EMBER_COUNT; i++)
        {
            if (emberRTs[i] == null) continue;
            var pos = emberRTs[i].anchoredPosition;

            // Gentle rise + stronger sway
            pos.y += dt * 18f;
            pos.x += Mathf.Sin(Time.unscaledTime * 1.5f + emberSeeds[i] * 2f) * dt * 15f;

            if (pos.y > hh)
            {
                pos.y = -hh;
                pos.x = Mathf.Lerp(-hw, hw, Mathf.Repeat(emberSeeds[i] + Time.unscaledTime * 0.2f, 1f));
            }
            emberRTs[i].anchoredPosition = pos;

            // Alpha with twinkle
            float ny = (pos.y + hh) / (hh * 2f);
            float a = Mathf.Clamp01(Mathf.Min(ny * 3f, (1f - ny) * 3f));
            a *= 0.55f;
            a *= 0.4f + 0.6f * Mathf.Sin(Time.unscaledTime * 2.5f + emberSeeds[i] * 5f);

            // Orange-amber color with slight variation
            float hue = 0.07f + 0.03f * Mathf.Sin(Time.unscaledTime + emberSeeds[i]);
            Color emberCol = Color.HSVToRGB(hue, 0.8f, 1f);
            emberImgs[i].color = new Color(emberCol.r, emberCol.g, emberCol.b, Mathf.Max(0f, a));

            float sz = 2f + a * 4f;
            emberRTs[i].sizeDelta = new Vector2(sz, sz);
        }
    }

    private void TickHeatWave(float dt)
    {
        if (heatRingRT == null || heatRingImg == null || cardRect == null) return;
        if (cardRect.rect.width <= 0) return;

        heatTimer += dt;
        float cycle = 3f;
        float t = Mathf.Repeat(heatTimer, cycle);
        float maxSize = Mathf.Max(cardRect.rect.width, cardRect.rect.height) * 1.1f;

        if (t < 1.5f)
        {
            float progress = t / 1.5f;
            float ease = 1f - (1f - progress) * (1f - progress);
            float size = Mathf.Lerp(15f, maxSize, ease);
            heatRingRT.sizeDelta = new Vector2(size, size);

            float alpha = (1f - ease) * 0.2f;
            heatRingImg.color = new Color(1f, 0.55f, 0.15f, alpha);
        }
        else
        {
            heatRingImg.color = new Color(1f, 0.55f, 0.15f, 0f);
            heatRingRT.sizeDelta = new Vector2(15f, 15f);
        }
    }

    private void TickIconFloat(float dt)
    {
        if (!iconFloatActive || iconRT == null) return;
        iconFloatTimer += dt * 1.8f;
        float yOff = Mathf.Sin(iconFloatTimer) * 4f;
        iconRT.anchoredPosition = iconOrigPos + new Vector2(0f, yOff);
    }

    // ═══════════════════════ HELPERS ═══════════════════════

    private Vector2 PerimeterPos(float t, float hw, float hh)
    {
        float fullW = hw * 2f, fullH = hh * 2f;
        float perim = 2f * (fullW + fullH);
        float d = t * perim;
        if (d < fullW) return new Vector2(-hw + d, hh);
        d -= fullW;
        if (d < fullH) return new Vector2(hw, hh - d);
        d -= fullH;
        if (d < fullW) return new Vector2(hw - d, -hh);
        d -= fullW;
        return new Vector2(-hw, -hh + d);
    }

    private GameObject MakeChild(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent ?? transform, false);
        return go;
    }

    private void Stretch(GameObject go, float extend)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-extend, -extend);
        rt.offsetMax = new Vector2(extend, extend);
    }

    private void AddMask(GameObject go)
    {
        var mask = go.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private Color RarityBaseColor(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Rare: return new Color(0.3f, 0.6f, 1f);
            case PerkRarity.Epic: return new Color(0.65f, 0.3f, 1f);
            case PerkRarity.Legendary: return new Color(0.95f, 0.6f, 0.2f); // turuncu amber
            default: return Color.white;
        }
    }

    private Color HueShift(Color c, float shift)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        return Color.HSVToRGB(Mathf.Repeat(h + shift, 1f), s, v);
    }

    private Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private float PulseSpeed()
    {
        switch (rarity)
        {
            case PerkRarity.Rare: return 2f;
            case PerkRarity.Epic: return 2.5f;
            case PerkRarity.Legendary: return 2f;
            default: return 2f;
        }
    }

    private float BaseAlpha()
    {
        switch (rarity)
        {
            case PerkRarity.Rare: return 0.25f;
            case PerkRarity.Epic: return 0.35f;
            case PerkRarity.Legendary: return 0.25f; // daha az parlak
            default: return 0.2f;
        }
    }

    private float PulseAmp()
    {
        switch (rarity)
        {
            case PerkRarity.Rare: return 0.1f;
            case PerkRarity.Epic: return 0.15f;
            case PerkRarity.Legendary: return 0.1f;
            default: return 0.1f;
        }
    }

    private static Sprite _radialSprite;
    private static Sprite MakeRadialSprite()
    {
        if (_radialSprite != null) return _radialSprite;
        int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _radialSprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _radialSprite;
    }

    private static Sprite _ringSprite;
    private static Sprite MakeRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Ring: visible between 0.7 and 1.0, fading
                float a = 0f;
                if (d > 0.6f && d < 1f)
                {
                    float ring = 1f - Mathf.Abs(d - 0.8f) / 0.2f;
                    a = Mathf.Clamp01(ring);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _ringSprite;
    }

    public void Cleanup()
    {
        // Restore icon position
        if (iconFloatActive && iconRT != null)
        {
            iconRT.anchoredPosition = iconOrigPos;
            iconFloatActive = false;
        }

        var toKill = new List<GameObject>();
        foreach (Transform ch in transform)
        {
            string n = ch.name;
            if (n == "RarityGlow" || n == "FrostVeins" || n == "MistDrift"
                || n == "SweepMask" || n == "ArcaneRing" || n == "VoidSpiral"
                || n == "EdgeSparks" || n == "LightRays" || n == "EmberRise"
                || n == "HeatWave"
                // old names
                || n == "BorderTravel" || n == "HoloMask" || n == "ParallaxMask"
                || n == "ParticleMask" || n == "ShimmerMask"
                || n.StartsWith("Sparkle_") || n.StartsWith("BS_") || n.StartsWith("RP_"))
                toKill.Add(ch.gameObject);
        }
        foreach (var go in toKill) Destroy(go);

        glowImage = null;
        veinRTs = null; veinImgs = null;
        mistRTs = null; mistImgs = null; mistSeeds = null; mistInit = false;
        sweepRT = null; sweepImg = null;
        ringRT = null; ringImg = null;
        voidRTs = null; voidImgs = null; voidPhases = null;
        sparkRTs = null; sparkImgs = null; sparkTimers = null; sparkCooldowns = null;
        sparksPositioned = false;
        raysHub = null;
        emberRTs = null; emberImgs = null; emberSeeds = null; embersInit = false;
        heatRingRT = null; heatRingImg = null;
        iconRT = null; iconFloatActive = false;
    }

    void OnDestroy() => Cleanup();
}
