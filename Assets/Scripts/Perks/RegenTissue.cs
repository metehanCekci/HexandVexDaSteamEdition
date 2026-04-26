using UnityEngine;

public class RegenTissuePerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "heal",  GameKeywords.Heal(currentLevel) },
        { "clear", GameKeywords.Action("clearing") }
    };

    public override void OnAcquire()
    {
        ApplyHealthBoost();
    }

    // YENÄ°: Kart tekrar seÃ§ildiÄŸinde (Upgrade) HEMEN ekstra canÄ± bassÄ±n!
    public override void Upgrade()
    {
        base.Upgrade(); // Seviyeyi artÄ±r
        ApplyHealthBoost(); // Seviye atladÄ±ÄŸÄ± an canÄ± ver!
    }

    public override void OnLevelClear()
    {
        ApplyHealthBoost();
    }

    public override void OnLetsGoAgain()
    {
        ApplyHealthBoost();
    }

    private void ApplyHealthBoost()
    {
        if (RunManager.instance == null) return;

        // Seviyesi ne kadarsa o kadar can versin (Lv 1 = 1 Can, Lv 3 = 3 Can)
        int healAmount = currentLevel;

        RunManager.instance.playerCurrentHealth = System.Math.Min(
            RunManager.instance.playerCurrentHealth + healAmount,
            RunManager.instance.playerMaxHealth
        );

        HexMovement player = TurnManager.instance != null ? TurnManager.instance.player : null;
        if (player != null && player.health != null)
            player.health.Heal(healAmount);

        TriggerVisualPop();
    }
}
