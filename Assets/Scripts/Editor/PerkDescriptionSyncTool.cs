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
///   Inline highlight tag'leri (sabit kelimeler):
///     [a]burn[/a]       TURUNCU+bold (SADECE burn/fire icin rezerve)
///     [s]skip[/s]       beyaz+bold (skip/kill/attack/push/level/dodge/shield/stun/spike/etc)
///     [r]retriggers[/r] mor+bold (sadece "retrigger" kelimesi)
///     [c]5/30[/c]       beyaz+bold counter (sayaclar)
///     [m]X4[/m]         kirmizi sabit mult
///     [p]+5[/p]         mavi sabit damage ekleme
///     [g]5 gold[/g]     sari gold
///     [h]5 HP[/h]       yesil HP
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
        { "AcidBlood",        "[s]Push[/s] an enemy into [s]spikes[/s] to {heal}." },
        { "BioBarrier",       "Start each [s]level[/s] with a [s]shield[/s] that blocks one hit." },
        { "BioMagnetism",     "[s]Pull[/s] all enemies toward you at [s]level start[/s]." },
        { "Bribe",            "[s]Shop reroll[/s] is [g]free[/g]." },
        { "ChitinArmor",      "Gain [m]+30%[/m] [s]dodge[/s] chance." },
        { "DiceHoarder",      "Each [s]campfire[/s] visited grants a permanent {perFire}.\n[c]Campfires:[/c] {count} ({total})" },
        { "DormantSpore",     "Each [s]skip[/s] stores {die} for your next [s]attack[/s].\n[c]Stored dice:[/c] {stored}" },
        { "ExtraDendrite",    "Roll {extraDice} extra die." },
        { "GravitonCore",     "On [s]skip[/s], [s]pull[/s] enemies 2-3 hexes closer." },
        { "HangingNerve",     "The [s]first die[/s] {retrigger}." },
        { "HypertrophicShell","Gain {bonusHP} max [h]HP[/h] per [s]level[/s] (cap [c]10[/c])." },
        { "MomentumEngine",   "Each die gains {bonus} per hex walked this turn." },
        { "NeuroStasisMist",  "[s]{stun}[/s] you apply last {turns} longer. {per} per [s]level[/s]." },
        { "NeuralReboot",     "Any die rolling {low} or lower is [s]rerolled[/s] until above." },
        { "OrganPouch",       "Gain {slots} item slot per [s]level[/s] (cap {cap})." },
        { "PassiveEnzyme",    "[s]Skip[/s] grants {reward} per [s]level[/s]. No effect on bosses or last enemy." },
        { "RegenTissue",      "{heal} after clearing a [s]level[/s]." },
        { "SeismicStep",      "[s]Skip[/s] makes your tile collapse after you leave it. Enemies on it fall and take {dmg}." },
        { "SporeCloud",       "When damaged, [s]stun[/s] enemies within {radius} hex radius." },
        { "StelzerGambit",    "Each die {extra}.\nDecays in {rolls} rolls." },
        { "ToxinEdge",        "Each die gains {bonus}." },
        { "VoidHunger",       "Each {collapse} grants a permanent {per}.\n[c]Collapsed:[/c] {count} ({total})" },
        { "VolatileRoll",     "Dice only roll [s]1[/s] or [s]6[/s]. Every {six} rerolls a chain die." },

        // ── Rare ──
        { "AdrenalSurge",     "Every [s]attack[/s] deals {mult}." },
        { "AlphaOmegaStrand", "[s]First[/s] and [s]last[/s] die gain {bonus}." },
        { "CapitalistPunch",  "Every {cost} you have grants {bonus} to all dice." },
        { "CatalyticEnzyme",  "Each [s]skip[/s] grants {per} (stacks). Consumed on [s]attack[/s].\n[c]Stacks:[/c] {count} ({bonus})" },
        { "CondensedFury",    "Roll {diceDelta} die but each die value is {mult}." },
        { "DoubleOrNothing",  "If dice sum is even, deal {mult}." },
        { "EchoStrike",       "{chance}% chance to [s]echo attack[/s]." },
        { "ExtraAmmo",        "Use your items {extra}." },
        { "FatalSightProtocol","All attacks are [m]Critical Hits[/m]. Each {chance} converts to {dmg}." },
        { "GlassCanon",       "Max [h]HP[/h] drops to {hp}, but deal {mult}." },
        { "HostSyndrome",     "Roll {bonus} for every enemy adjacent to you." },
        { "HydraulicImpact",  "[s]Pushing[/s] an enemy into a wall deals {wallDmg} of their max [h]HP[/h]." },
        { "HyperCortex",      "Gain {crit}." },
        { "InsurancePolicy",  "Gain [g]gold[/g] when you take damage. {amount} per missing [h]HP[/h]." },
        { "IronWill",         "Each [s]level cleared[/s] without damage grants {per}. Resets on damage.\n[c]Streak:[/c] {streak} ({bonus})" },
        { "KillChain",        "[s]Killing[/s] an enemy grants {moves} extra move." },
        { "LootGland",        "Gain {bonus} per [s]kill[/s] for each [s]level[/s]." },
        { "Lucky Clover",     "[s]Reroll[/s] {rerolls} low dice per combat." },
        { "LuckyClover",      "[s]Reroll[/s] {rerolls} low dice per combat." },
        { "MutantSwarm",      "Each die rolled adds {bonus}." },
        { "NecroticTouch",    "Enemies below half [h]HP[/h] take {mult}." },
        { "NeuralHijack",     "[s]Push[/s] an enemy into another to convert it to your side." },
        { "NeuroAim",         "Gain {crit}." },
        { "PhantomLimb",      "Leave a [s]proximity mine[/s] on tiles you leave." },
        { "PhotovoltaicPulse","Multiplies damage by the [s]first die[/s] value. Re-applies each time the first die {retrig}." },
        { "PressurePoint",    "[h]Full HP[/h]: {mult1}. Above half: {mult2}. Below half: {mult3}." },
        { "PyrogenicGlands",  "[s]Attacks[/s] [a]burn[/a] enemies: {dmg} of max [h]HP[/h] per turn for [c]5 turns[/c]." },
        { "RecoilSpring",     "After [s]attacking[/s], bounce back and [s]attack[/s] again if an enemy is adjacent." },
        { "ReflexFiber",      "Gain {moves} extra move per turn." },
        { "RetributionSplicer","Each hit on the same target grants {bonus} against them.\n[c]Targets:[/c] {targets} | [c]Total hits:[/c] {hits}" },
        { "RiggedDice",       "All dice are [s]rerolled[/s] to match the highest rolled value." },
        { "SensoryOverload",  "Every {five} and {six} {extra}." },
        { "ShopRerollStackPerk","Each [s]shop reroll[/s] grants a permanent {bonus} to all your dice. [c]Stack:[/c] {stack}" },
        { "SlipperySecretion","Leave a [s]slime trail[/s]. Enemies stepping on it slide forward." },
        { "SymbioticArsenal", "Each filled item slot adds {bonus}." },
        { "SymbioticFury",    "All die bonuses [m]multiply[/m] damage instead of adding to it." },
        { "SynapticAnchor",   "First [s]skip[/s] drops an [s]anchor[/s]. Next [s]skip[/s] teleports you back." },
        { "ViralCysts",       "[s]Attacks[/s] plant [s]cysts[/s]. [s]Skip[/s] to detonate: {perMark} per mark, damage split among marked.\n[c]Marked:[/c] {count}" },
        { "VoodooParasite",   "Damage dealt to enemies is also dealt to nearby enemies (voodoo curse)." },

        // ── Epic ──
        { "Deadweight",       "[s]Stunned[/s] enemies take {mult}." },
        { "GeneSplice",       "[s]Upgrade[/s] a random perk, then consume itself." },
        { "MimeticGrowth",    "Copies the implant to its right, {trigger}." },
        { "NewParasite",      "" },
        { "Ouroboros",        "On death, revive at full [h]HP[/h]. A random perk loses 1 level." },
        { "OverkillProtocol", "[s]Overkill[/s] damage carries to a random living enemy." },
        { "PentUpStrike",     "[s]Attacks[/s] deal {zero} but still [s]knockback[/s]. Dice values are stored. [s]Skip[/s] to unleash all stored damage at {percent}.\n[c]Stored:[/c] {stored} ({stacks} stacks)" },
        { "PhantomAssault",   "[s]Knockback[/s] leaves a [s]ghost[/s] where the enemy stood. [s]Skip[/s] to teleport through all ghosts, {attack} at each.\n[c]Ghosts:[/c] {count}" },
        { "VolatileCells",    "Killed enemies explode for {dmg} of max [h]HP[/h] to adjacent enemies." },

        // ── Legendary ──
        { "ApexPredator",     "Deal {mult}, but lose {penalty} per die rolled." },
        { "CarrionFeeder",    "Each consecutive [s]kill[/s] doubles damage (max {max}). Resets on failed [s]kill[/s].\n[c]Streak:[/c] {streak} ({current})" },
        { "CascadeProtocol",  "Each [s]attack[/s]'s dice sum carries to the next as flat bonus ({percent}). Resets on damage taken or [s]level cleared[/s].\n[c]Accumulated:[/c] {accumulated}" },
        { "ItemEater",        "Feed items to grow. Base {base}, each fed item adds {bonus}.\n[c]Fed:[/c] {fed} ({current})" },
        { "LeftmostResonance","The [s]leftmost[/s] implant {again}." },
        { "ParasiticChorus",  "For each {common} implant, the right neighbor {retrig}." },
        { "PerkLeech",        "Earn an [s]implant fragment[/s] when you [s]kill[/s] an elite enemy. {needed} fragments = random implant.\n[c]Fragments:[/c] {current}" },
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
        { "CleaveAxe",          "Next [s]attack[/s] deals [s]full damage[/s] to all adjacent enemies without splitting." },
        { "FragMine",           "Place a [s]bomb[/s] on any hex. Rolls dice and deals damage to all enemies within [c]1[/c] hex radius." },
        { "GoldLeech",          "Next enemy drops [m]X2[/m] [g]gold[/g]." },
        { "HexThorn",           "Place a [s]spike trap[/s] on any empty hex. Breaks after [c]3 turns[/c] (blinks on turn [c]2[/c])." },
        { "LuckyClover",        "[s](Disabled)[/s] This item has been removed from the game." },
        { "MutaGen",            "Restore [h]2 HP[/h]." },
        { "MutationCatalyst",   "Sets the next [s]shop reroll[/s] cost to [g]0 gold[/g]." },
        { "NecroShot",          "[s]Instantly kill[/s] any non-boss enemy on the map." },
        { "OverClok",           "Deal [m]X2[/m] damage on your next dice roll." },
        { "PhaseShift",         "Select an enemy and [s]swap positions[/s] with it." },
        { "SurgeBoot",          "Next turn you can move up to [c]2[/c] hexes instead of [c]1[/c]." },
        { "SynthStim",          "Roll [p]+1[/p] extra die in the next [s]combat[/s]." },
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
