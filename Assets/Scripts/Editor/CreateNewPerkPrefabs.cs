using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CreateNewPerkPrefabs
{
    [MenuItem("HexAndVex/Create New Perk Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePerk<VolatileRollPerk>("VolatileRoll", "Volatile Roll",
            "Each die has a 50% chance to become 1 or 6. High risk, high reward.");

        CreatePerk<SporeCloudPerk>("SporeCloud", "Spore Cloud",
            "When you take damage, stun nearby enemies. Cloud radius grows with level.");

        CreatePerk<PerkLeechPerk>("PerkLeech", "Perk Leech",
            "Killing an elite enemy drops a perk fragment. Collect 3 fragments for a random perk.");

        CreatePerk<PyrogenicGlandsPerk>("PyrogenicGlands", "Pyrogenic Glands",
            "Attacks set enemies on fire for 5 turns. Burns deal 5%/10%/15% of max HP per turn.");

        CreatePerk<HydraulicImpactPerk>("HydraulicImpact", "Hydraulic Impact",
            "Enemies knocked into walls take bonus damage. Lv1: 25%, Lv2: 40%, Lv3: 50% of max HP.");

        CreatePerk<SlipperySecretionPerk>("SlipperySecretion", "Slippery Secretion",
            "Leave a mucus trail on tiles you walk over. Enemies slide an extra hex in their movement direction.");

        CreatePerk<ViralCystsPerk>("ViralCysts", "Viral Cysts",
            "Attacks plant cysts on enemies. Skip to detonate. Damage scales with marked enemy count.");

        CreatePerk<HypertrophicShellPerk>("HypertrophicShell", "Hypertrophic Shell",
            "Permanently increase max HP by +1. (Max: 10 HP)");

        CreatePerk<SynapticAnchorPerk>("SynapticAnchor", "Synaptic Anchor",
            "First Skip places an anchor. Second Skip teleports you to it.");

        CreatePerk<PressurePointPerk>("PressurePoint", "Pressure Point",
            "Deal more damage to healthier enemies. 100% HP: 2x, 99-50%: 1.75x, below 50%: 1.5x.");

        CreatePerk<OverkillProtocolPerk>("OverkillProtocol", "Overkill Protocol",
            "Excess damage from a kill transfers to a random living enemy.");

        CreatePerk<CarrionFeederPerk>("CarrionFeeder", "Carrion Feeder",
            "Each consecutive kill doubles your total damage. Resets when an attack fails to kill. Max stacks per level.");

        CreatePerk<EchoStrikePerk>("EchoStrike", "Echo Strike",
            "Attacks have a chance to strike again. Lv1: 15%, Lv2: 30%, Lv3: 45%.");

        CreatePerk<CatalyticEnzymePerk>("CatalyticEnzyme", "Catalytic Enzyme",
            "Each skip grants +30% damage to your next attack. Stacks multiply, consumed on attack.");

        CreatePerk<GravitonCorePerk>("GravitonCore", "Graviton Core",
            "When you skip, pull enemies within 2-3 hex range 1 hex closer to you.");

        CreatePerk<NecroticTouchPerk>("NecroticTouch", "Necrotic Touch",
            "Enemies at or below 50% HP take 2X damage.");

        CreatePerk<CondensedFuryPerk>("CondensedFury", "Condensed Fury",
            "Roll 1 fewer die, but each remaining die deals double its rolled value.");

        CreatePerk<SymbioticArsenalPerk>("SymbioticArsenal", "Symbiotic Arsenal",
            "Gain damage multiplier per filled item slot. Lv1: +0.5X, Lv2: +0.75X, Lv3: +1.0X per slot.");

        CreatePerk<IronWillPerk>("IronWill", "Iron Will",
            "Each combat level cleared without taking damage grants +1X damage multiplier. Resets on damage.");

        CreatePerk<NeuralHijackPerk>("NeuralHijack", "Neural Hijack",
            "Knocking an enemy into another enemy converts the hit enemy to your side. Allies attack adjacent enemies each turn.");

        CreatePerk<SeismicStepPerk>("SeismicStep", "Seismic Step",
            "Skipping makes your tile unstable. When you leave, it collapses. Enemies on it take damage.");

        CreatePerk<CascadeProtocolPerk>("CascadeProtocol", "Cascade Protocol",
            "Each attack's dice total carries over to the next as bonus damage. Resets each room.");

        CreatePerk<DiceHoarderPerk>("DiceHoarder", "Dice Hoarder",
            "Each campfire visited grants a permanent +1 die.");

        CreatePerk<PentUpStrikePerk>("PentUpStrike", "Pent-Up Strike",
            "Attacks deal 0 damage but still knockback. Dice values are stored. Skip to unleash all stored damage at once.");

        CreatePerk<VoidHungerPerk>("VoidHunger", "Void Hunger",
            "Each collapsed tile grants permanent +0.25X damage multiplier.");

        CreatePerk<DeadweightPerk>("Deadweight", "Deadweight",
            "Stunned enemies take 2X damage. +1X per level.");

        CreatePerk<KillChainPerk>("KillChain", "Kill Chain",
            "Killing an enemy grants +1 extra move this turn. +1 per level.");

        CreatePerk<OuroborosPerk>("Ouroboros", "Ouroboros",
            "Cheat death. All perks lose 1 level. Lv1 perks are destroyed. No perks left = true death.");

        CreatePerk<InsurancePolicyPerk>("InsurancePolicy", "Insurance Policy",
            "Gain gold when you take damage. +4 gold per missing HP at Lv1, +6 at Lv2, +8 at Lv3.");

        CreatePerk<PhantomAssaultPerk>("PhantomAssault", "Phantom Assault",
            "Knockback leaves a ghost where the enemy stood. Skip to teleport through all ghosts, attacking at each.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Perk prefab'ları oluşturuldu: Assets/Prefabs/Perks/");
    }

    [MenuItem("HexAndVex/Create New Perk Collection Entries")]
    public static void CreateCollectionEntries()
    {
        // Load database
        PerkCollectionDatabase db = AssetDatabase.LoadAssetAtPath<PerkCollectionDatabase>(
            "Assets/Resources/PerkCollectionDatabase.asset");
        if (db == null)
        {
            Debug.LogError("PerkCollectionDatabase bulunamadı!");
            return;
        }

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources/CollectionEntries"))
            AssetDatabase.CreateFolder("Assets/Resources", "CollectionEntries");

        // Get existing perkIds to avoid duplicates
        HashSet<string> existingIds = new HashSet<string>();
        foreach (var entry in db.entries)
        {
            if (entry != null) existingIds.Add(entry.perkId);
        }

        int added = 0;

        added += CreateEntry(db, existingIds, "VolatileRoll", "Volatile Roll", PerkRarity.Legendary,
            "Dice that defy probability — each face is either salvation or doom. The organism's neural pathways have been rewired to embrace chaos.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "SporeCloud", "Spore Cloud", PerkRarity.Rare,
            "A defensive fungal colony embedded in the host's epidermis. When threatened, it releases paralytic spores in a dense cloud.",
            UnlockCondition.KillEnemies, 100, "", "Kill 100 enemies in total.");

        added += CreateEntry(db, existingIds, "PerkLeech", "Perk Leech", PerkRarity.Legendary,
            "A parasitic organism that feeds on the essence of powerful foes. Each elite kill leaves behind a crystallized fragment of their strength.",
            UnlockCondition.KillEnemiesSingleRun, 20, "", "Kill 20 enemies in a single run.");

        added += CreateEntry(db, existingIds, "PyrogenicGlands", "Pyrogenic Glands", PerkRarity.Common,
            "Bio-engineered glands that coat weapons in an exothermic compound. The flames linger, consuming flesh over time.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "HydraulicImpact", "Hydraulic Impact", PerkRarity.Epic,
            "Hydraulic augmentation that amplifies knockback force exponentially. When a target has nowhere to go, the impact is devastating.",
            UnlockCondition.PushEnemyIntoSpike, 10, "", "Push 10 enemies into spikes.");

        added += CreateEntry(db, existingIds, "SlipperySecretion", "Slippery Secretion", PerkRarity.Rare,
            "A mucus-producing organ that leaves a slick, frictionless trail. Enemies who step on it lose their footing and slide uncontrollably.",
            UnlockCondition.MoveTiles, 500, "", "Walk 500 tiles in total.");

        added += CreateEntry(db, existingIds, "ViralCysts", "Viral Cysts", PerkRarity.Epic,
            "Weaponized bio-cysts that embed themselves in enemy tissue. When detonated, the chain reaction scales with the number of infected hosts.",
            UnlockCondition.SkipTurns, 50, "", "Skip 50 turns in total.");

        added += CreateEntry(db, existingIds, "HypertrophicShell", "Hypertrophic Shell", PerkRarity.Common,
            "A calcified growth that reinforces the host's vital organs. Each layer adds another wall between you and death.",
            UnlockCondition.CompleteRuns, 3, "", "Complete 3 runs.");

        added += CreateEntry(db, existingIds, "SynapticAnchor", "Synaptic Anchor", PerkRarity.Legendary,
            "A neural implant that creates a quantum entanglement between two points in space. The anchor remembers, and the body follows.",
            UnlockCondition.ClearLevels, 30, "", "Clear 30 levels in total.");

        added += CreateEntry(db, existingIds, "PressurePoint", "Pressure Point", PerkRarity.Common,
            "A bio-sensor that detects structural weaknesses in undamaged tissue. The first strike is always the deadliest.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "OverkillProtocol", "Overkill Protocol", PerkRarity.Legendary,
            "A predatory reflex that redirects lethal force. When prey falls too easily, the excess energy seeks new targets.",
            UnlockCondition.KillEnemies, 200, "", "Kill 200 enemies in total.");

        added += CreateEntry(db, existingIds, "CarrionFeeder", "Carrion Feeder", PerkRarity.Rare,
            "A scavenger organ that metabolizes fallen enemies into raw energy. Each kill feeds the hunger, each feast sharpens the claws.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "EchoStrike", "Echo Strike", PerkRarity.Epic,
            "Phantom muscle memory that replays lethal movements. The body remembers the kill, and sometimes acts on instinct alone.",
            UnlockCondition.ClearLevels, 15, "", "Clear 15 levels in total.");

        added += CreateEntry(db, existingIds, "CatalyticEnzyme", "Catalytic Enzyme", PerkRarity.Rare,
            "A dormant enzyme that builds pressure with each moment of stillness. When finally unleashed, the reaction is explosive.",
            UnlockCondition.SkipTurns, 30, "", "Skip 30 turns in total.");

        added += CreateEntry(db, existingIds, "GravitonCore", "Graviton Core", PerkRarity.Common,
            "A dense gravitational implant that warps local space during impacts. Nearby bodies are pulled toward the point of collision.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "NecroticTouch", "Necrotic Touch", PerkRarity.Rare,
            "A necrotizing agent that accelerates cellular decay in weakened organisms. The closer to death, the faster the rot spreads.",
            UnlockCondition.KillEnemiesSingleRun, 15, "", "Kill 15 enemies in a single run.");

        added += CreateEntry(db, existingIds, "CondensedFury", "Condensed Fury", PerkRarity.Epic,
            "A volatile compression organ that sacrifices quantity for devastating potency. Fewer strikes, but each one hits like a freight train.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "SymbioticArsenal", "Symbiotic Arsenal", PerkRarity.Rare,
            "A symbiotic organism that feeds on equipped tools, converting their presence into raw combat power.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "IronWill", "Iron Will", PerkRarity.Rare,
            "An indomitable survival instinct. Each flawless victory fuels a devastating surge of power in the next encounter.",
            UnlockCondition.ClearLevels, 10, "", "Clear 10 levels in total.");

        added += CreateEntry(db, existingIds, "NeuralHijack", "Neural Hijack", PerkRarity.Legendary,
            "A parasitic neural implant that hijacks enemy nervous systems on impact. The converted host fights alongside you until the implant burns out.",
            UnlockCondition.PushEnemyIntoSpike, 20, "", "Push 20 enemies into spikes.");

        added += CreateEntry(db, existingIds, "SeismicStep", "Seismic Step", PerkRarity.Legendary,
            "Your very presence destabilizes the ground beneath you. Each moment of stillness sends tremors through the earth, and when you leave, the ground crumbles.",
            UnlockCondition.SkipTurns, 100, "", "Skip 100 turns in total.");

        added += CreateEntry(db, existingIds, "CascadeProtocol", "Cascade Protocol", PerkRarity.Legendary,
            "A neural feedback loop that amplifies combat data with each strike. Every attack feeds the next, building toward an unstoppable crescendo of destruction.",
            UnlockCondition.ClearLevels, 50, "", "Clear 50 levels in total.");

        added += CreateEntry(db, existingIds, "DiceHoarder", "Dice Hoarder", PerkRarity.Legendary,
            "A parasitic growth that feeds on choice itself. Every crossroads strengthens it, every decision adds another weapon to the arsenal.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "PentUpStrike", "Pent-Up Strike", PerkRarity.Legendary,
            "A kinetic absorption organ that converts failed impacts into stored potential energy. The longer you wait, the more devastating the release.",
            UnlockCondition.SkipTurns, 75, "", "Skip 75 turns in total.");

        added += CreateEntry(db, existingIds, "VoidHunger", "Void Hunger", PerkRarity.Common,
            "A parasitic void that feeds on structural collapse. Each crumbling tile strengthens its hunger, converting destruction into raw power.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "Deadweight", "Deadweight", PerkRarity.Rare,
            "A neurotoxin that exploits paralyzed nervous systems. Immobilized prey becomes fragile, their frozen muscles offering no resistance to the killing blow.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "KillChain", "Kill Chain", PerkRarity.Epic,
            "A predatory adrenaline response that accelerates with each kill. The hunt feeds itself — every death fuels the next strike.",
            UnlockCondition.KillEnemiesSingleRun, 10, "", "Kill 10 enemies in a single run.");

        added += CreateEntry(db, existingIds, "Ouroboros", "Ouroboros", PerkRarity.Secret,
            "The serpent devours its own tail. Death is not the end — it is merely a shedding of skin. Each rebirth costs a piece of what you were, until nothing remains.",
            UnlockCondition.CompleteRuns, 5, "", "Complete 5 runs.");

        added += CreateEntry(db, existingIds, "InsurancePolicy", "Insurance Policy", PerkRarity.Rare,
            "A parasitic actuary that monetizes suffering. Every wound becomes a transaction, every scar a deposit. Pain is just another currency.",
            UnlockCondition.Default, 1, "", "Unlocked by default.");

        added += CreateEntry(db, existingIds, "PhantomAssault", "Phantom Assault", PerkRarity.Legendary,
            "Quantum echoes of your killing intent linger where you struck. When you pause, every phantom remembers, and every phantom strikes as one.",
            UnlockCondition.KillEnemies, 300, "", "Kill 300 enemies in total.");

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{added} yeni collection entry oluşturuldu ve database'e eklendi.");
    }

    private static int CreateEntry(PerkCollectionDatabase db, HashSet<string> existingIds,
        string perkId, string perkName, PerkRarity rarity, string loreText,
        UnlockCondition condition, int requiredAmount, string conditionParam, string unlockHint)
    {
        if (existingIds.Contains(perkId))
        {
            Debug.Log($"[SKIP] {perkId} collection entry zaten mevcut.");
            return 0;
        }

        // Load perk prefab
        string prefabPath = $"Assets/Prefabs/Perks/{perkId}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WARN] {perkId} prefab bulunamadı: {prefabPath}");
            return 0;
        }

        // Create ScriptableObject
        PerkCollectionData data = ScriptableObject.CreateInstance<PerkCollectionData>();
        data.perkPrefab = prefab;
        data.perkId = perkId;
        data.loreText = loreText;
        data.rarity = rarity;
        data.unlockCondition = condition;
        data.requiredAmount = requiredAmount;
        data.conditionParam = conditionParam;
        data.unlockHint = unlockHint;

        string assetPath = $"Assets/Resources/CollectionEntries/{perkId}_CollectionEntry.asset";
        AssetDatabase.CreateAsset(data, assetPath);

        // Add to database
        db.entries.Add(data);
        existingIds.Add(perkId);

        Debug.Log($"[OK] {perkId} collection entry oluşturuldu.");
        return 1;
    }

    private static void CreatePerk<T>(string fileName, string perkName, string description) where T : BasePerk
    {
        string path = $"Assets/Prefabs/Perks/{fileName}.prefab";

        // Zaten varsa atla
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"[SKIP] {fileName} zaten mevcut.");
            return;
        }

        GameObject obj = new GameObject(fileName);
        T perk = obj.AddComponent<T>();
        perk.perkName = perkName;
        perk.description = description;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);

        Debug.Log($"[OK] {fileName} prefab oluşturuldu.");
    }
}
