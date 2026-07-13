using System.Collections.Generic;

public class ReflexFiberPerk : BasePerk
{
    // Tracks how many extra moves THIS perk instance is currently granting, so OnUnequip
    // (and Upgrade re-equipping) cleanly subtracts what we added — even across stash <-> active swaps.
    private int grantedMoves;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "moves", GameKeywords.Plus(currentLevel) }
    };

    public override void OnAcquire()
    {
        // No stat change here — OnEquip handles it. Stash perks should not grant moves.
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        if (RunManager.instance == null) return;
        // If we somehow re-equip without OnUnequip running, clean up first.
        if (grantedMoves > 0)
            RunManager.instance.extraMovesPerTurn -= grantedMoves;
        grantedMoves = currentLevel;
        RunManager.instance.extraMovesPerTurn += grantedMoves;
    }

    public override void OnUnequip()
    {
        if (RunManager.instance == null) { grantedMoves = 0; return; }
        if (grantedMoves > 0)
        {
            RunManager.instance.extraMovesPerTurn -= grantedMoves;
            grantedMoves = 0;
        }
    }

    public override void Upgrade()
    {
        base.Upgrade();
        // Only apply the level bump if we're currently equipped.
        if (RunManager.instance != null
            && RunManager.instance.activePerks.Contains(this))
        {
            RunManager.instance.extraMovesPerTurn += 1;
            grantedMoves += 1;
        }
    }
}
