#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Perk prefab'larinin Inspector'daki "description" field'ini yonetir.
///
/// MENU:
///   Tools / Perk Descriptions / Apply Default Templates To All Prefabs
///     -> Asagidaki DefaultTemplates dictionary'sindeki sablonu her prefab'a yazar.
///
///   Tools / Perk Descriptions / Clear All Perk Description Fields
///     -> Tum prefab description field'larini bosaltir.
///
///   Tools / Perk Descriptions / Show Current Description Of All Perks
///     -> Console'a tum perklerin mevcut description'larini doker.
///
/// TEMPLATE YAZIM REHBERI:
///   Inline highlight tag'leri (renk adi ile, kolay okunur):
///     [white]skip[/white]      -> beyaz+bold (skip/kill/attack/push/level/dodge/shield/stun/spike vb.)
///     [orange]burn[/orange]    -> turuncu+bold (SADECE burn/fire)
///     [purple]retriggers[/purple] -> mor+bold (sadece "retriggers" kelimesi)
///     [red]X4[/red]            -> kirmizi (mult, X carpan)
///     [blue]+5 damage[/blue]   -> mavi (chips, +damage)
///     [yellow]5 gold[/yellow]  -> sari (gold)
///     [green]5 HP[/green]      -> yesil (HP/heal)
///
///   {token_adi}  -> dinamik deger, perkin GetDescValues() metodu doldurur.
/// </summary>
public static class PerkDescriptionSyncTool
{
    private const string PerkPrefabFolder = "Assets/Prefabs/Perks";
    private const string ItemAssetFolder  = "Assets/ScriptableObjects/Items";

    // =============================================================================
    // PERK TEMPLATE'LERI
    // =============================================================================
    // Key: prefab dosya adi (uzantisiz). Value: Inspector'a yazilacak template.
    // Yeni perk eklersen buraya entry ekle, sonra "Apply Default Templates" calistir.
    // Mevcut template'leri Inspector'da elle de degistirebilirsin — Apply tekrar calistirinca
    // bu listede ne varsa o gecerli olur.
    // =============================================================================
    // NOT: [a] tag'i SADECE 'burn'/'fire' icin turuncu rezerve edildi.
    // Diger highlight'lar (skip/kill/attack/push/level/shop/reroll/campfire/etc) [s] BEYAZ+BOLD.
    private static readonly Dictionary<string, string> DefaultTemplates = new Dictionary<string, string>
    {
        // ── Common ──
        { "AcidBlood",        "[white]Push[/white] an enemy into [white]spikes[/white] to {heal}." },
        { "BioBarrier",       "Start each [white]level[/white] with a [white]shield[/white] that blocks one hit." },
        { "BioMagnetism",     "[white]Pull[/white] all enemies toward you at [white]level start[/white]." },
        { "Bribe",            "[white]Shop reroll[/white] is [yellow]free[/yellow]." },
        { "ChitinArmor",      "Gain [red]+30%[/red] [white]dodge[/white] chance." },
        { "DiceHoarder",      "Each [white]campfire[/white] visited grants a permanent {perFire}.\n[white]Campfires:[/white] {count} ({total})" },
        { "DormantSpore",     "Each [white]skip[/white] stores {die} for your next [white]attack[/white].\n[white]Stored dice:[/white] {stored}" },
        { "ExtraDendrite",    "Roll {extraDice} extra die." },
        { "GravitonCore",     "On [white]skip[/white], [white]pull[/white] enemies 2-3 hexes closer." },
        { "HangingNerve",     "The [white]first die[/white] {retrigger}." },
        { "HypertrophicShell","Gain {bonusHP} max [green]HP[/green] per [white]level[/white] (cap [white]10[/white])." },
        { "MomentumEngine",   "Each die gains {bonus} per hex walked this turn." },
        { "NeuroStasisMist",  "{stun} you apply last {turns} longer. {per} per [white]level[/white]." },
        { "NeuralReboot",     "Any die rolling {low} or lower is [white]rerolled[/white] until above." },
        { "OrganPouch",       "Gain {slots} item slot per [white]level[/white] (cap {cap})." },
        { "PassiveEnzyme",    "[white]Skip[/white] grants {reward} per [white]level[/white]. No effect on bosses or last enemy." },
        { "RegenTissue",      "{heal} after clearing a [white]level[/white]." },
        { "SeismicStep",      "[white]Skip[/white] makes your tile collapse after you leave it. Enemies on it fall and take {dmg}." },
        { "SporeCloud",       "When damaged, [white]stun[/white] enemies within {radius} hex radius." },
        { "StelzerGambit",    "Each die {extra}.\nDecays in {rolls} rolls." },
        { "ToxinEdge",        "Each die gains {bonus}." },
        { "VoidHunger",       "Each {collapse} grants a permanent {per}.\n[white]Collapsed:[/white] {count} ({total})" },
        { "VolatileRoll",     "Dice only roll [white]1[/white] or [white]6[/white]. Every {six} rerolls a chain die." },

        // ── Rare ──
        { "AdrenalSurge",     "Every [white]attack[/white] deals {mult}." },
        { "AlphaOmegaStrand", "[white]First[/white] and [white]last[/white] die gain {bonus}." },
        { "CapitalistPunch",  "Every {cost} you have grants {bonus} to all dice." },
        { "CatalyticEnzyme",  "Each [white]skip[/white] grants {per} (stacks). Consumed on [white]attack[/white].\n[white]Stacks:[/white] {count} ({bonus})" },
        { "CondensedFury",    "Roll {diceDelta} die but each die value is {mult}." },
        { "DoubleOrNothing",  "If dice sum is even, deal {mult}." },
        { "EchoStrike",       "{chance}% chance to [white]echo attack[/white]." },
        { "ExtraAmmo",        "Use your items {extra}." },
        { "FatalSightProtocol","All attacks are [red]Critical Hits[/red]. Each {chance} converts to {dmg}." },
        { "GlassCanon",       "Max [green]HP[/green] drops to {hp}, but deal {mult}." },
        { "HostSyndrome",     "Roll {bonus} for every enemy adjacent to you." },
        { "HydraulicImpact",  "[white]Pushing[/white] an enemy into a wall deals {wallDmg} of their max [green]HP[/green]." },
        { "HyperCortex",      "Gain {crit}." },
        { "InsurancePolicy",  "Gain [yellow]gold[/yellow] when you take damage. {amount} per missing [green]HP[/green]." },
        { "IronWill",         "Each [white]level cleared[/white] without damage grants {per}. Resets on damage.\n[white]Streak:[/white] {streak} ({bonus})" },
        { "KillChain",        "[white]Killing[/white] an enemy grants {moves} extra move." },
        { "LootGland",        "Gain {bonus} per [white]kill[/white] for each [white]level[/white]." },
        { "Lucky Clover",     "[white]Reroll[/white] {rerolls} low dice per combat." },
        { "LuckyClover",      "[white]Reroll[/white] {rerolls} low dice per combat." },
        { "MutantSwarm",      "Each die rolled adds {bonus}." },
        { "NecroticTouch",    "Enemies below half [green]HP[/green] take {mult}." },
        { "NeuralHijack",     "[white]Push[/white] an enemy into another to convert it to your side." },
        { "NeuroAim",         "Gain {crit}." },
        { "PhantomLimb",      "Leave a [white]proximity mine[/white] on tiles you leave." },
        { "PhotovoltaicPulse","Multiplies damage by the [white]first die[/white] value. Re-applies each time the first die {retrig}." },
        { "PressurePoint",    "[green]Full HP[/green]: {mult1}. Above half: {mult2}. Below half: {mult3}." },
        { "PyrogenicGlands",  "[white]Attacks[/white] [orange]burn[/orange] enemies: {dmg} of max [green]HP[/green] per turn for [white]5 turns[/white]." },
        { "RecoilSpring",     "After [white]attacking[/white], bounce back and [white]attack[/white] again if an enemy is adjacent." },
        { "ReflexFiber",      "Gain {moves} extra move per turn." },
        { "RetributionSplicer","Each hit on the same target grants {bonus} against them.\n[white]Targets:[/white] {targets} | [white]Total hits:[/white] {hits}" },
        { "RiggedDice",       "All dice are [white]rerolled[/white] to match the highest rolled value." },
        { "SensoryOverload",  "Every {five} and {six} {extra}." },
        { "ShopRerollStackPerk","Each [white]shop reroll[/white] grants a permanent {bonus} to all your dice. [white]Stack:[/white] {stack}" },
        { "SlipperySecretion","Leave a [white]slime trail[/white]. Enemies stepping on it slide forward." },
        { "SymbioticArsenal", "Each filled item slot adds {bonus}." },
        { "SymbioticFury",    "All die bonuses [red]multiply[/red] damage instead of adding to it." },
        { "SynapticAnchor",   "First [white]skip[/white] drops an [white]anchor[/white]. Next [white]skip[/white] teleports you back." },
        { "ViralCysts",       "[white]Attacks[/white] plant [white]cysts[/white]. [white]Skip[/white] to detonate: {perMark} per mark, damage split among marked.\n[white]Marked:[/white] {count}" },
        { "VoodooParasite",   "Damage dealt to enemies is also dealt to nearby enemies (voodoo curse)." },

        // ── Epic ──
        { "Deadweight",       "[white]Stunned[/white] enemies take {mult}." },
        { "GeneSplice",       "[white]Upgrade[/white] a random perk, then consume itself." },
        { "MimeticGrowth",    "Copies the implant to its right, {trigger}." },
        { "NewParasite",      "" },
        { "Ouroboros",        "On death, revive at full [green]HP[/green]. A random perk loses 1 level." },
        { "OverkillProtocol", "[white]Overkill[/white] damage carries to a random living enemy." },
        { "PentUpStrike",     "[white]Attacks[/white] deal {zero} but still [white]knockback[/white]. Dice values are stored. [white]Skip[/white] to unleash all stored damage at {percent}.\n[white]Stored:[/white] {stored} ({stacks} stacks)" },
        { "PhantomAssault",   "[white]Knockback[/white] leaves a [white]ghost[/white] where the enemy stood. [white]Skip[/white] to teleport through all ghosts, {attack} at each.\n[white]Ghosts:[/white] {count}" },
        { "VolatileCells",    "Killed enemies explode for {dmg} of max [green]HP[/green] to adjacent enemies." },

        // ── Legendary ──
        { "ApexPredator",     "Deal {mult}, but lose {penalty} per die rolled." },
        { "CarrionFeeder",    "Each consecutive [white]kill[/white] doubles damage (max {max}). Resets on failed [white]kill[/white].\n[white]Streak:[/white] {streak} ({current})" },
        { "CascadeProtocol",  "Each [white]attack[/white]'s dice sum carries to the next as flat bonus ({percent}). Resets on damage taken or [white]level cleared[/white].\n[white]Accumulated:[/white] {accumulated}" },
        { "ItemEater",        "Feed items to grow. Base {base}, each fed item adds {bonus}.\n[white]Fed:[/white] {fed} ({current})" },
        { "LeftmostResonance","The [white]leftmost[/white] implant {again}." },
        { "ParasiticChorus",  "For each {common} implant, the right neighbor {retrig}." },
        { "PerkLeech",        "Earn an [white]implant fragment[/white] when you [white]kill[/white] an elite enemy. {needed} fragments = random implant.\n[white]Fragments:[/white] {current}" },
        { "TerminalFuryGland","Always {base} damage, get {per} mult per {missing}." },

        // ── Secret ──
        { "InvisibleJoker",   "After {wait}, sell another implant to {effect}." },
        { "Showman",          "You can stack multiple copies of the same {target}." },
        { "LetsGoAgain",      "After all implants trigger, they all {again}." },
    };

    // =============================================================================
    // ITEM TEMPLATE'LERI
    // =============================================================================
    // Key: item .asset dosya adi (uzantisiz). Value: Inspector'a yazilacak template.
    // =============================================================================
    private static readonly Dictionary<string, string> ItemTemplates = new Dictionary<string, string>
    {
        { "CleaveAxe",          "Next [white]attack[/white] deals [white]full damage[/white] to all adjacent enemies without splitting." },
        { "FragMine",           "Place a [white]bomb[/white] on any hex. Rolls dice and deals damage to all enemies within [white]1[/white] hex radius." },
        { "GoldLeech",          "Next enemy drops [red]X2[/red] [yellow]gold[/yellow]." },
        { "HexThorn",           "Place a [white]spike trap[/white] on any empty hex. Breaks after [white]3 turns[/white] (blinks on turn [white]2[/white])." },
        { "LuckyClover",        "[white](Disabled)[/white] This item has been removed from the game." },
        { "MutaGen",            "Restore [green]2 HP[/green]." },
        { "MutationCatalyst",   "Sets the next [white]shop reroll[/white] cost to [yellow]0 gold[/yellow]." },
        { "NecroShot",          "[white]Instantly kill[/white] any non-boss enemy on the map." },
        { "OverClok",           "Deal [red]X2[/red] damage on your next dice roll." },
        { "PhaseShift",         "Select an enemy and [white]swap positions[/white] with it." },
        { "SurgeBoot",          "Next turn you can move up to [white]2[/white] hexes instead of [white]1[/white]." },
        { "SynthStim",          "Roll [blue]+1[/blue] extra die in the next [white]combat[/white]." },
    };

    [MenuItem("Tools/Perk Descriptions/Apply Default Templates To All Prefabs")]
    public static void ApplyDefaultTemplates()
    {
        if (!EditorUtility.DisplayDialog(
            "Apply Default Templates",
            "Bu islem tum perk prefab'larina yukaridaki DefaultTemplates listesindeki sablonlari yazar.\n\n" +
            "Mevcut Inspector description'lari ezilir.\n\nDevam?",
            "Evet, uygula",
            "Iptal"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PerkPrefabFolder });
        int applied = 0, missing = 0, skipped = 0;
        var report = new List<string>();
        var notInList = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) { skipped++; continue; }

            if (!DefaultTemplates.TryGetValue(fileName, out string template))
            {
                notInList.Add(fileName);
                missing++;
                continue;
            }

            perk.description = template;
            EditorUtility.SetDirty(prefab);
            applied++;
            report.Add($"  • {fileName}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"<b>[PerkDescriptionSync] Applied {applied} templates.</b>\n{string.Join("\n", report)}";
        if (notInList.Count > 0)
            msg += $"\n\n<b>NOT IN DefaultTemplates:</b>\n  {string.Join("\n  ", notInList)}";
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Done",
            $"Applied: {applied}\nNot in list: {missing}\nSkipped (no perk component): {skipped}\n\nDetay icin Console'a bak.",
            "OK");
    }

    [MenuItem("Tools/Perk Descriptions/Clear All Perk Description Fields")]
    public static void ClearAllPerkDescriptions()
    {
        if (!EditorUtility.DisplayDialog(
            "Clear All Perk Descriptions",
            "Bu islem TUM perk prefab'larinin Inspector'daki 'description' field'ini BOSALTIR.\n\nDevam?",
            "Evet, hepsini bosalt",
            "Iptal"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PerkPrefabFolder });
        int cleared = 0, skipped = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) { skipped++; continue; }

            if (string.IsNullOrEmpty(perk.description)) { skipped++; continue; }

            perk.description = "";
            EditorUtility.SetDirty(prefab);
            cleared++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<b>[PerkDescriptionSync] Cleared {cleared} perk descriptions. Skipped: {skipped}.</b>");
        EditorUtility.DisplayDialog("Done", $"{cleared} perk prefab description'i bosaltildi.", "OK");
    }

    [MenuItem("Tools/Perk Descriptions/Show Current Description Of All Perks")]
    public static void ShowAllDescriptions()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PerkPrefabFolder });
        var lines = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string desc = string.IsNullOrEmpty(perk.description) ? "<empty>" : perk.description;
            lines.Add($"  • {name}: \"{desc}\"");
        }

        Debug.Log($"<b>[PerkDescriptionSync] All perk descriptions:</b>\n{string.Join("\n", lines)}");
    }

    // =============================================================================
    // ITEM MENULERI
    // =============================================================================

    [MenuItem("Tools/Item Descriptions/Apply Default Templates To All Items")]
    public static void ApplyDefaultItemTemplates()
    {
        if (!EditorUtility.DisplayDialog(
            "Apply Default Item Templates",
            "Bu islem tum item .asset dosyalarinin Inspector'daki 'description' field'ini ItemTemplates listesinden yazar.\n\n" +
            "Mevcut Inspector description'lari ezilir.\n\nDevam?",
            "Evet, uygula",
            "Iptal"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemAssetFolder });
        int applied = 0, missing = 0, skipped = 0;
        var report = new List<string>();
        var notInList = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            BaseItem item = AssetDatabase.LoadAssetAtPath<BaseItem>(path);
            if (item == null) { skipped++; continue; }

            if (!ItemTemplates.TryGetValue(fileName, out string template))
            {
                notInList.Add(fileName);
                missing++;
                continue;
            }

            item.description = template;
            EditorUtility.SetDirty(item);
            applied++;
            report.Add($"  • {fileName}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"<b>[ItemDescriptionSync] Applied {applied} templates.</b>\n{string.Join("\n", report)}";
        if (notInList.Count > 0)
            msg += $"\n\n<b>NOT IN ItemTemplates:</b>\n  {string.Join("\n  ", notInList)}";
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Done",
            $"Applied: {applied}\nNot in list: {missing}\nSkipped (no item asset): {skipped}\n\nDetay icin Console'a bak.",
            "OK");
    }

    [MenuItem("Tools/Item Descriptions/Clear All Item Description Fields")]
    public static void ClearAllItemDescriptions()
    {
        if (!EditorUtility.DisplayDialog(
            "Clear All Item Descriptions",
            "Bu islem TUM item .asset dosyalarinin Inspector'daki 'description' field'ini BOSALTIR.\n\nDevam?",
            "Evet, hepsini bosalt",
            "Iptal"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemAssetFolder });
        int cleared = 0, skipped = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaseItem item = AssetDatabase.LoadAssetAtPath<BaseItem>(path);
            if (item == null) { skipped++; continue; }
            if (string.IsNullOrEmpty(item.description)) { skipped++; continue; }

            item.description = "";
            EditorUtility.SetDirty(item);
            cleared++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<b>[ItemDescriptionSync] Cleared {cleared} item descriptions. Skipped: {skipped}.</b>");
        EditorUtility.DisplayDialog("Done", $"{cleared} item description'i bosaltildi.", "OK");
    }

    // =============================================================================
    // PERK RARITY SYNC — prefab Inspector'daki rarity field'ini kod-tanimli degerle esitle
    // =============================================================================

    [MenuItem("Tools/Perk Descriptions/Sync All Perk Fields From Code (rarity/maxLevel/etc)")]
    public static void SyncAllPerkFields()
    {
        if (!EditorUtility.DisplayDialog(
            "Sync All Perk Fields",
            "Bu islem her perk prefab'inin Inspector'daki SU FIELD'lari kod tarafindan tanimli\n" +
            "degerlerle esitler:\n\n" +
            "  • rarity\n" +
            "  • maxLevel\n" +
            "  • processLast\n" +
            "  • isPerkRetrigger\n" +
            "  • isRerollPerk\n" +
            "  • priority\n\n" +
            "Bu islem sonrasi koddaki OnEnable atamalarini guvenle silebilirsin.\n\nDevam?",
            "Evet, esitle",
            "Iptal"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PerkPrefabFolder });
        int synced = 0, unchanged = 0, skipped = 0;
        var changes = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }
            BasePerk prefabPerk = prefab.GetComponent<BasePerk>();
            if (prefabPerk == null) { skipped++; continue; }

            // Geçici instance — OnEnable tetiklenir, runtime field degerlerini al
            GameObject temp = UnityEngine.Object.Instantiate(prefab);
            temp.SetActive(false);
            BasePerk inst = temp.GetComponent<BasePerk>();
            if (inst == null) { UnityEngine.Object.DestroyImmediate(temp); skipped++; continue; }

            PerkRarity codeRarity     = inst.rarity;
            int        codeMaxLevel   = inst.maxLevel;
            bool       codeProcLast   = inst.processLast;
            bool       codeIsPerkRT   = inst.isPerkRetrigger;
            bool       codeIsReroll   = inst.isRerollPerk;
            int        codePriority   = inst.priority;
            UnityEngine.Object.DestroyImmediate(temp);

            var diffs = new List<string>();
            if (prefabPerk.rarity          != codeRarity)   diffs.Add($"rarity {prefabPerk.rarity}→{codeRarity}");
            if (prefabPerk.maxLevel        != codeMaxLevel) diffs.Add($"maxLevel {prefabPerk.maxLevel}→{codeMaxLevel}");
            if (prefabPerk.processLast     != codeProcLast) diffs.Add($"processLast {prefabPerk.processLast}→{codeProcLast}");
            if (prefabPerk.isPerkRetrigger != codeIsPerkRT) diffs.Add($"isPerkRetrigger {prefabPerk.isPerkRetrigger}→{codeIsPerkRT}");
            if (prefabPerk.isRerollPerk    != codeIsReroll) diffs.Add($"isRerollPerk {prefabPerk.isRerollPerk}→{codeIsReroll}");
            if (prefabPerk.priority        != codePriority) diffs.Add($"priority {prefabPerk.priority}→{codePriority}");

            if (diffs.Count == 0) { unchanged++; continue; }

            prefabPerk.rarity          = codeRarity;
            prefabPerk.maxLevel        = codeMaxLevel;
            prefabPerk.processLast     = codeProcLast;
            prefabPerk.isPerkRetrigger = codeIsPerkRT;
            prefabPerk.isRerollPerk    = codeIsReroll;
            prefabPerk.priority        = codePriority;
            EditorUtility.SetDirty(prefab);
            synced++;
            changes.Add($"  • {fileName}: {string.Join(", ", diffs)}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"<b>[PerkFieldSync] Synced {synced} perks. Unchanged: {unchanged}. Skipped: {skipped}.</b>";
        if (changes.Count > 0) msg += "\n" + string.Join("\n", changes);
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Done",
            $"Synced: {synced}\nUnchanged: {unchanged}\nSkipped: {skipped}\n\nDetay icin Console'a bak.",
            "OK");
    }

    [MenuItem("Tools/Item Descriptions/Show Current Description Of All Items")]
    public static void ShowAllItemDescriptions()
    {
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemAssetFolder });
        var lines = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaseItem item = AssetDatabase.LoadAssetAtPath<BaseItem>(path);
            if (item == null) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string desc = string.IsNullOrEmpty(item.description) ? "<empty>" : item.description;
            lines.Add($"  • {name}: \"{desc}\"");
        }

        Debug.Log($"<b>[ItemDescriptionSync] All item descriptions:</b>\n{string.Join("\n", lines)}");
    }
}
#endif
