using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SyncPerkRaritiesToPrefabs
{
    [MenuItem("HexAndVex/Sync Perk Rarities to Prefabs + Collection")]
    public static void Sync()
    {
        // Exact rarity mapping from LevelUpManager inspector
        Dictionary<string, PerkRarity> rarityMap = new Dictionary<string, PerkRarity>
        {
            // Common (21)
            { "LootGland",          PerkRarity.Common },
            { "ChitinArmor",        PerkRarity.Common },
            { "RegenTissue",        PerkRarity.Common },
            { "PassiveEnzyme",      PerkRarity.Common },
            { "DormantSpore",       PerkRarity.Common },
            { "ToxinEdge",          PerkRarity.Common },
            { "NeuroAim",           PerkRarity.Common },
            { "AcidBlood",          PerkRarity.Common },
            { "BioBarrier",         PerkRarity.Common },
            { "RetributionSplicer", PerkRarity.Common },
            { "HypertrophicShell",  PerkRarity.Common },
            { "GravitonCore",       PerkRarity.Common },
            { "AlphaOmegaStrand",   PerkRarity.Common },
            { "NeuroStasisMist",    PerkRarity.Common },
            { "SlipperySecretion",  PerkRarity.Common },
            { "SporeCloud",         PerkRarity.Common },
            { "RiggedDice",         PerkRarity.Common },
            { "HyperCortex",        PerkRarity.Common },
            { "NeuralReboot",       PerkRarity.Common },
            { "OrganPouch",         PerkRarity.Common },
            { "OverkillProtocol",   PerkRarity.Common },

            // Rare (15)
            { "VolatileCells",      PerkRarity.Rare },
            { "GeneSplice",         PerkRarity.Rare },
            { "MutantSwarm",        PerkRarity.Rare },
            { "DoubleOrNothing",    PerkRarity.Rare },
            { "GlassCanon",         PerkRarity.Rare },
            { "VolatileRoll",       PerkRarity.Rare },
            { "CarrionFeeder",      PerkRarity.Rare },
            { "CatalyticEnzyme",    PerkRarity.Rare },
            { "NecroticTouch",      PerkRarity.Rare },
            { "PressurePoint",      PerkRarity.Rare },
            { "MomentumEngine",     PerkRarity.Rare },
            { "PhantomLimb",        PerkRarity.Rare },
            { "RecoilSpring",       PerkRarity.Rare },
            { "SynapticAnchor",     PerkRarity.Rare },
            { "PerkLeech",          PerkRarity.Rare },

            // Epic (8)
            { "ReflexFiber",        PerkRarity.Epic },
            { "BioMagnetism",       PerkRarity.Epic },
            { "AdrenalSurge",       PerkRarity.Epic },
            { "HydraulicImpact",    PerkRarity.Epic },
            { "ViralCysts",         PerkRarity.Epic },
            { "PyrogenicGlands",    PerkRarity.Epic },
            { "EchoStrike",         PerkRarity.Epic },
            { "ExtraDendrite",      PerkRarity.Epic },
            { "Bribe",              PerkRarity.Epic },

            // Legendary
            { "ApexPredator",       PerkRarity.Legendary },
            { "VoodooParasite",     PerkRarity.Legendary },
            { "FatalSightProtocol", PerkRarity.Legendary },
            { "TerminalFuryGland",  PerkRarity.Legendary },
            { "HostSyndrome",       PerkRarity.Legendary },
            { "CapitalistPunch",    PerkRarity.Legendary },
            { "GeneticCartel",      PerkRarity.Legendary },

            // Secret
            { "SymbioticFury",      PerkRarity.Secret },

            // New perks
            { "CondensedFury",      PerkRarity.Epic },
            { "SymbioticArsenal",   PerkRarity.Rare },
            { "IronWill",           PerkRarity.Rare },
            { "NeuralHijack",       PerkRarity.Legendary },
            { "SeismicStep",        PerkRarity.Legendary },
            { "CascadeProtocol",    PerkRarity.Legendary },
            { "DiceHoarder",        PerkRarity.Legendary },
            { "PentUpStrike",       PerkRarity.Legendary },
        };

        int prefabUpdated = 0;
        int collectionUpdated = 0;

        // --- Step 1: Update prefab serialized rarity ---
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Perks" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            // Match by prefab name
            if (!rarityMap.TryGetValue(prefab.name, out PerkRarity targetRarity)) continue;

            if (perk.rarity != targetRarity)
            {
                SerializedObject so = new SerializedObject(perk);
                SerializedProperty rarityProp = so.FindProperty("rarity");
                if (rarityProp != null)
                {
                    Debug.Log($"[PREFAB] {prefab.name}: {(PerkRarity)rarityProp.enumValueIndex} -> {targetRarity}");
                    rarityProp.enumValueIndex = (int)targetRarity;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                    prefabUpdated++;
                }
            }
        }

        // --- Step 2: Update collection entry rarity ---
        string[] collectionGuids = AssetDatabase.FindAssets("t:PerkCollectionData", new[] { "Assets/Resources/CollectionEntries" });

        foreach (string cguid in collectionGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(cguid);
            PerkCollectionData entry = AssetDatabase.LoadAssetAtPath<PerkCollectionData>(path);
            if (entry == null || entry.perkPrefab == null) continue;

            if (!rarityMap.TryGetValue(entry.perkPrefab.name, out PerkRarity targetRarity)) continue;

            if (entry.rarity != targetRarity)
            {
                Debug.Log($"[COLLECTION] {entry.perkId}: {entry.rarity} -> {targetRarity}");
                entry.rarity = targetRarity;
                EditorUtility.SetDirty(entry);
                collectionUpdated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Sync complete: {prefabUpdated} prefab(s), {collectionUpdated} collection entry(ies) updated.");
    }
}
