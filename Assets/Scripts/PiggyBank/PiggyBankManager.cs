using UnityEngine;

/// <summary>
/// Owns the interest / piggy bank logic. Independent of UI - UI subscribes to GameEvents.
///
/// Flow on each level clear:
///   1. TurnManager calls PayInterest().
///   2. We snapshot currentGold, compute base interest (gold / ratio, capped).
///   3. Fire OnInterestCalculating so perks can mutate the payload.
///   4. Add final interest to currentGold, emit GoldChanged + InterestPaid.
///   5. Persist lifetime stats.
///
/// Live preview:
///   Whenever gold changes (or cap/ratio modifiers change), we recompute what the player *would* earn
///   and emit OnInterestPreview so HUD can show "n/cap".
///
/// Perks hook in via GameEvents.OnInterestCalculating - e.g. Fat Pig raises cap, Compound Interest
/// boosts ratio. That keeps the manager closed to extension: no manager edits per perk.
/// </summary>
public class PiggyBankManager : MonoBehaviour
{
    public static PiggyBankManager instance;

    [Header("Config (auto-loaded from Resources/PiggyBankConfig if null)")]
    public PiggyBankConfig config;

    // Cached last preview so we don't spam events
    private int lastPreviewAmount = -1;
    private int lastPreviewCap = -1;

    // Lifetime stats keys
    private const string LifetimeInterestKey = "lifetime_interest_earned";
    private const string RunInterestKey = "run_interest_earned";

    /// <summary>Total interest earned across all runs. Updated each payout.</summary>
    public static int LifetimeInterestEarned => PlayerPrefs.GetInt(LifetimeInterestKey, 0);

    /// <summary>Total interest earned this run. Reset when RunManager resets run state.</summary>
    public int runInterestEarned { get; private set; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null) config = PiggyBankConfig.LoadOrDefault();
    }

    /// <summary>
    /// Auto-spawn the singleton before the first scene loads so we never rely on scene placement
    /// or the editor setup tool. If one already exists in the scene, Awake's guard will collapse the duplicate.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("PiggyBankManager (auto)");
        go.AddComponent<PiggyBankManager>();
    }

    void OnEnable()
    {
        GameEvents.OnGoldChanged += HandleGoldChanged;
    }

    void OnDisable()
    {
        GameEvents.OnGoldChanged -= HandleGoldChanged;
    }

    void Start()
    {
        // Emit an initial preview so UI that spawns after us still sees a value.
        EmitPreview();
    }

    private void HandleGoldChanged(int _)
    {
        EmitPreview();
    }

    /// <summary>
    /// Call this when an external modifier (perk cap change, config swap) happens so UI can refresh.
    /// </summary>
    public void RefreshPreview()
    {
        lastPreviewAmount = -1;
        lastPreviewCap = -1;
        EmitPreview();
    }

    private void EmitPreview()
    {
        if (config == null) return;
        if (!config.showLivePreview) return;

        var payload = BuildPayload();
        GameEvents.InterestCalculating(payload);

        int finalAmount = Mathf.Clamp(payload.Amount, 0, payload.Cap);
        if (finalAmount != lastPreviewAmount || payload.Cap != lastPreviewCap)
        {
            lastPreviewAmount = finalAmount;
            lastPreviewCap = payload.Cap;
            GameEvents.InterestPreview(finalAmount, payload.Cap);
        }
    }

    private InterestPayload BuildPayload()
    {
        int gold = RunManager.instance != null ? RunManager.instance.currentGold : 0;
        int ratio = Mathf.Max(1, config.goldPerInterest);
        int rawInterest = gold / ratio;
        int cap = Mathf.Max(0, config.maxInterestPerWave);

        return new InterestPayload
        {
            SourceGold = gold,
            GoldPerInterest = ratio,
            Amount = Mathf.Min(rawInterest, cap),
            Cap = cap
        };
    }

    /// <summary>
    /// Pure calculation - no side effects. Useful for UI previews and tests.
    /// Applies perk modifiers via OnInterestCalculating, same as the real payout would.
    /// </summary>
    public InterestPayload PeekInterest()
    {
        var payload = BuildPayload();
        GameEvents.InterestCalculating(payload);
        payload.Amount = Mathf.Clamp(payload.Amount, 0, payload.Cap);
        return payload;
    }

    /// <summary>
    /// Announces interest payout. Gold is NOT added here immediately - UI emits one CreditOne()
    /// call per coin as it lands, which is where the gold actually ticks up. That keeps the
    /// gold counter in sync with the flying coin animation instead of jumping ahead.
    ///
    /// Returns the seconds the caller should wait before proceeding with the fade so the
    /// coin-fly animation has time to finish.
    /// </summary>
    public float PayInterest()
    {
        if (config == null) { Debug.LogWarning("[PiggyBankManager] PayInterest aborted: config null"); return 0f; }
        if (RunManager.instance == null) { Debug.LogWarning("[PiggyBankManager] PayInterest aborted: RunManager null"); return 0f; }

        var payload = BuildPayload();
        GameEvents.InterestCalculating(payload);

        int finalAmount = Mathf.Clamp(payload.Amount, 0, payload.Cap);
        Debug.Log($"[PiggyBankManager] PayInterest: gold={payload.SourceGold} ratio={payload.GoldPerInterest} cap={payload.Cap} -> pay {finalAmount}");

        if (finalAmount <= 0)
        {
            EmitPreview();
            return 0f;
        }

        // Fire InterestPaid so UI can stage the fly animation. Gold is credited coin-by-coin via CreditOne().
        GameEvents.InterestPaid(finalAmount);

        return EstimatePayoutDuration(finalAmount);
    }

    /// <summary>
    /// Called by PiggyBankUI each time a flying coin reaches the gold counter - adds 1 gold
    /// and refreshes run/lifetime stats. Keeps gold counter in sync with visual coin landings.
    /// </summary>
    public void CreditOne()
    {
        if (RunManager.instance == null) return;

        RunManager.instance.currentGold += 1;
        RunManager.instance.totalGoldEarned += 1;
        runInterestEarned += 1;

        int lifetime = PlayerPrefs.GetInt(LifetimeInterestKey, 0) + 1;
        PlayerPrefs.SetInt(LifetimeInterestKey, lifetime);
        PlayerPrefs.SetInt(RunInterestKey, runInterestEarned);

        GameEvents.GoldChanged(RunManager.instance.currentGold);
        // EmitPreview() fires automatically via the OnGoldChanged subscription.
    }

    /// <summary>How long the payout animation will hold up the level-clear transition.</summary>
    public float EstimatePayoutDuration(int amount)
    {
        if (amount <= 0 || config == null) return 0f;

        float raw;
        if (amount <= config.hybridOneByOneThreshold)
            raw = amount * config.perCoinDelay + 0.15f;
        else
            raw = 0.35f + Mathf.Clamp01(amount / 10f) * 0.2f;

        return Mathf.Min(raw, config.maxPayoutHoldSeconds);
    }

    /// <summary>Called when the player starts a new run so per-run counters reset.</summary>
    public void OnRunStart()
    {
        runInterestEarned = 0;
        PlayerPrefs.SetInt(RunInterestKey, 0);
        RefreshPreview();
    }
}
