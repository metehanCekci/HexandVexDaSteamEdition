using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SyncPerkDescriptions
{
    [MenuItem("HexAndVex/Sync Perk Descriptions to Prefabs")]
    public static void Sync()
    {
        // Add your new descriptions here: prefab name -> description
        Dictionary<string, string> descMap = new Dictionary<string, string>
        {
            // === COMMON ===
            { "LootGland",          "Gain +1 bonus gold per kill for each level." },
            { "ChitinArmor",        "+15% armor chance per level. Armor blocks all damage from one hit." },
            { "RegenTissue",        "Heal 1 HP per level at the start of each level." },
            { "PassiveEnzyme",      "Earn gold when you skip your turn with more than 1 enemy. +2 gold per level." },
            { "DormantSpore",       "Each skipped turn stores +1 bonus die for your next attack." },
            { "ToxinEdge",          "Add +1 damage to each die. +1 per level." },
            { "NeuroAim",           "+25% critical hit chance per level." },
            { "AcidBlood",          "Pushing an enemy into spikes heals you. +1 HP healed per level." },
            { "BioBarrier",         "Gain a shield at the start of each level. Blocks one lethal hit." },
            { "RetributionSplicer", "Consecutive hits on the same target deal +2 bonus damage. +1 more per level." },
            { "HypertrophicShell",  "Permanently increase max HP by +1 per level. (Max: 10 HP)" },
            { "PressurePoint",      "Deal more damage to healthier enemies. 100% HP: 2x, 99-50%: 1.75x, below 50%: 1.5x." },
            { "GravitonCore",       "When an enemy is knocked back, pull adjacent enemies 1 hex toward the impact." },
            { "AlphaOmegaStrand",   "First and last die get +2 per level." },
            { "NeuroStasisMist",    "Stuns you apply last +1 extra turn. +1 per level." },
            { "SlipperySecretion",  "Leave a mucus trail behind you. Enemies that step on it slide 1 extra hex." },
            { "SporeCloud",         "When you take damage, stun nearby enemies. Radius grows with level." },
            { "RiggedDice",         "All dice become equal to the highest rolled value." },
            { "HyperCortex",        "+0.5x critical damage multiplier per level." },
            { "NeuralReboot",       "Any die showing 3 or below is automatically rerolled until it lands above 3." },
            { "OrganPouch",         "+1 inventory slot per level. (Max: 5 slots)" },
            { "OverkillProtocol",   "Excess damage from a kill transfers to a random living enemy." },

            // === RARE ===
            { "VolatileCells",      "Attacks trigger an explosion. Explosion damage 25% per level of your attack." },
            { "GeneSplice",         "Upgrades a random perk by 1 level. If none can upgrade, grants a new random perk." },
            { "MutantSwarm",       "Each die adds +0.5x damage multiplier per level." },
            { "DoubleOrNothing",    "If total dice sum is even, damage is doubled." },
            { "GlassCanon",         "Your max HP becomes 3, but your damage is doubled." },
            { "VolatileRoll",       "Each die has a 50% chance to become 1 or 6." },
            { "CarrionFeeder",      "Killing an enemy heals 1 HP and grants +2 flat damage to your next attack." },
            { "CatalyticEnzyme",    "Each skip grants +30% damage to your next attack. Stacks, consumed on attack." },
            { "NecroticTouch",      "Enemies below 50% HP take 2x damage from all sources." },
            { "MomentumEngine",     "Each hex you walk before attacking adds +1 to all dice." },
            { "PhantomLimb",        "Place invisible mines when you move. Enemies that step on them take damage." },
            { "RecoilSpring",       "After attacking, bounce backward. If you land next to another enemy, attack again." },
            { "SynapticAnchor",     "First Skip places an anchor. Second Skip teleports you to it." },
            { "PerkLeech",          "Killing an elite drops a fragment. Collect 3 fragments for a random perk." },

            // === EPIC ===
            { "ReflexFiber",        "+1 extra move per turn per level." },
            { "BioMagnetism",       "Before attacking, pull all adjacent enemies 1 hex closer to you." },
            { "AdrenalSurge",       "Your total damage is multiplied by 2x." },
            { "HydraulicImpact",    "Enemies knocked into walls take 25%/50% of their max HP as bonus damage." },
            { "ViralCysts",         "Attacks plant cysts. +1 die per marked enemy. Skip to attack all marked enemies." },
            { "PyrogenicGlands",    "Attacks set enemies on fire for 5 turns. Burn deals 5/10/15% max HP per tick." },
            { "EchoStrike",         "Attacks have a chance to strike again. Lv1: 15%, Lv2: 30%, Lv3: 45%." },
            { "ExtraDendrite",      "+1 extra die per level. Can be taken multiple times." },
            { "Bribe",              "Lose all coins and rebirth." },

            // === LEGENDARY ===
            { "ApexPredator",       "5x damage multiplier, but lose 1x for each die you roll." },
            { "VoodooParasite",     "Damage dealt to one enemy is mirrored to all linked targets." },
            { "FatalSightProtocol", "All attacks are critical hits. Crit chance converts to bonus crit damage." },
            { "TerminalFuryGland",  "Always deal double damage, get +1x mult per missing health." },
            { "HostSyndrome",       "Roll +1 extra die for every enemy adjacent to you." },
            { "CapitalistPunch",    "Every 10 gold you carry adds +1 to all dice." },
            { "GeneticCartel",      "Each shop reroll permanently adds +1 to all dice." },

            // === NEW ===
            { "CondensedFury",      "Roll 1 fewer die, but add +3 flat damage to each remaining die. +1 per level." },
            { "SymbioticArsenal",   "Gain +0.5x damage multiplier per filled item slot. +0.25x per level." },
            { "IronWill",           "Complete a level without taking damage to gain +2x multiplier next combat. +1x per level." },
            { "NeuralHijack",       "Knock an enemy into another to convert it. Ally has 3 HP, deals your last attack's damage to adjacent enemies each turn." },
            { "SeismicStep",        "Skipping makes your tile unstable. When you leave, it collapses. Enemies on it take damage." },
            { "CascadeProtocol",    "Each attack's dice total carries over to the next as bonus damage. Resets each room." },
            { "DiceHoarder",        "Gain +1 permanent die for every perk selection and shop you visit." },
            { "PentUpStrike",       "Attacks deal 0 damage but still knockback. Dice values are stored. Skip to unleash all stored damage at once." },
            { "VoidHunger",         "Gain +0.5x damage multiplier for every tile that collapses. Permanent." },
            { "Deadweight",         "Stunned enemies take 2x damage. +1x per level." },
            { "KillChain",          "Killing an enemy grants +1 extra move this turn. +1 per level." },

            // === SECRET ===
            { "SymbioticFury",      "Dice values are multiplied together instead of added." },
            { "LetsGoAgain",        "After all perks trigger, they all trigger once more." },
            { "Ouroboros",          "Cheat death. All perks lose 1 level. Lv1 perks are destroyed. No perks left = true death." },
        };

        int prefabUpdated = 0;

        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Perks" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            if (!descMap.TryGetValue(prefab.name, out string newDesc)) continue;

            if (perk.description != newDesc)
            {
                SerializedObject so = new SerializedObject(perk);
                SerializedProperty descProp = so.FindProperty("description");
                if (descProp != null)
                {
                    Debug.Log($"[DESC] {prefab.name}: \"{perk.description}\" -> \"{newDesc}\"");
                    descProp.stringValue = newDesc;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                    prefabUpdated++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Description sync complete: {prefabUpdated} prefab(s) updated.");
    }
}
