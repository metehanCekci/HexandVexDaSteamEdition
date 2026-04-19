public class SymbioticFuryPerk : BasePerk
{
    private void Awake()
    {
        priority = 99;
        rarity = PerkRarity.Secret;
    }

    void OnEnable()
    {
        if (string.IsNullOrEmpty(description))
            description = "All die bonuses multiply damage instead of adding to it.";
        RebuildDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.multiplyInsteadOfAdd = true;
    }
}
