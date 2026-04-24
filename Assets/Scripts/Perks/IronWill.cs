using UnityEngine;
using System.Collections.Generic;

public class IronWillPerk : BasePerk
{
    private int cleanLevelStreak = 0;
    private bool tookDamageThisLevel = false;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Each level cleared without taking damage grants {per} damage multiplier. Resets on damage.\nStreak: {streak} ({bonus})";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "per",    "X1" },
        { "streak", cleanLevelStreak },
        { "bonus",  $"X{cleanLevelStreak}" }
    };

    public override void OnAcquire()
    {
        Subscribe();
        RebuildDescription();
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        RebuildDescription();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        tookDamageThisLevel = false;
        RebuildDescription();
    }

    public override void OnLevelClear()
    {
        if (RunManager.instance != null)
        {
            var nodeType = RunManager.instance.currentNodeType;
            if (nodeType != MapNodeType.Combat && nodeType != MapNodeType.EliteCombat && nodeType != MapNodeType.Boss)
            {
                RebuildDescription();
                return;
            }
        }

        if (!tookDamageThisLevel)
        {
            cleanLevelStreak++;
        }
        RebuildDescription();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.health.OnDamaged += OnPlayerDamaged;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null
            && TurnManager.instance.player.health != null)
        {
            TurnManager.instance.player.health.OnDamaged -= OnPlayerDamaged;
        }
        subscribed = false;
    }

    private void OnPlayerDamaged(long remainingHP)
    {
        tookDamageThisLevel = true;
        cleanLevelStreak = 0;
        RebuildDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (cleanLevelStreak <= 0) return;

        // Eski model: multiplier += X -> efektif (1+X). Balatro modelinde ApplyMult(1+X).
        payload.ApplyMult(1f + cleanLevelStreak);
        TriggerVisualPop();
    }
}
