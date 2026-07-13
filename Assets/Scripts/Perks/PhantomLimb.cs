using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

public class PhantomLimbPerk : BasePerk
{
    private Vector3 mineOffset = new Vector3(0f, -0.07f, 0f);

    // Lvl1 = 25%, Lvl2 = 50%, Lvl3 = 75%. Joker perkler scale edebilir.
    private ScaledValue _mineDamagePercent = new ScaledValue(baseValue: 0.25f, perLevel: 0.25f);
    public ScaledValue MineDamagePercentValue => _mineDamagePercent;

    public float GetMineDamagePercent() => _mineDamagePercent.Get(currentLevel);

    public override Dictionary<string, object> GetDescValues()
    {
        int pct = Mathf.RoundToInt(GetMineDamagePercent() * 100f);
        return new Dictionary<string, object>
        {
            { "mine",   GameKeywords.Status("proximity mine") },
            { "damage", GameKeywords.Status("%" + pct.ToString(CultureInfo.InvariantCulture) + " damage") }
        };
    }

    public override void OnAcquire()
    {
        TriggerVisualPop();
    }

    public Vector3 GetMineOffset()
    {
        return mineOffset;
    }
}
