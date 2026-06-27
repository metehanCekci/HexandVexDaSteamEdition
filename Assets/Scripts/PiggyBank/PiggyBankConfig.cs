using UnityEngine;

/// <summary>
/// Tunable values for the piggy bank / interest system.
/// Create one instance at Resources/PiggyBankConfig.asset so PiggyBankManager can auto-load it.
/// Perks / items can layer on top at runtime via GameEvents.OnInterestCalculating.
/// </summary>
[CreateAssetMenu(fileName = "PiggyBankConfig", menuName = "HexAndVex/Piggy Bank Config", order = 0)]
public class PiggyBankConfig : ScriptableObject
{
    [Header("Interest Formula")]
    [Tooltip("Amount of gold needed to earn 1 interest. Default: 10 -> every 10 gold earns 1 bonus gold.")]
    [Min(1)] public int goldPerInterest = 10;

    [Tooltip("Maximum interest paid per wave, regardless of how much gold the player has.")]
    [Min(0)] public int maxInterestPerWave = 10;

    [Header("Preview")]
    [Tooltip("If true, the HUD continuously shows the interest the player would earn right now. If false, the piggy only shows after a wave is cleared.")]
    public bool showLivePreview = true;

    [Header("Animation Timings (Hybrid)")]
    [Tooltip("Per-coin delay when coins fly one at a time (small payouts).")]
    [Min(0f)] public float perCoinDelay = 0.08f;

    [Tooltip("Payout counts <= this number fly one-by-one; larger payouts are paid in a single burst.")]
    [Min(1)] public int hybridOneByOneThreshold = 3;

    [Tooltip("How long the level-clear flow should wait for the payout animation before handing off to fade. Clamp keeps things snappy.")]
    [Min(0f)] public float maxPayoutHoldSeconds = 1.2f;

    [Header("Art (optional - leave null to use the coin sprite as fallback)")]
    public Sprite piggyBankSprite;
    public Sprite piggyBankFullSprite;

    public Color piggyTintEmpty = Color.white;
    public Color piggyTintFull  = new Color(1f, 0.95f, 0.4f);

    /// <summary>
    /// Lazy-loads the config from Resources/PiggyBankConfig.asset, creating an in-memory fallback if none exists.
    /// Safe to call before any config asset exists - returns sensible defaults.
    /// </summary>
    public static PiggyBankConfig LoadOrDefault()
    {
        var cfg = Resources.Load<PiggyBankConfig>("PiggyBankConfig");
        if (cfg != null) return cfg;

        cfg = CreateInstance<PiggyBankConfig>();
        cfg.name = "PiggyBankConfig (runtime default)";
        return cfg;
    }
}
