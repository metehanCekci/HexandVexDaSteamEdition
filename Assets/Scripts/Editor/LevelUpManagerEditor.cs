using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(LevelUpManager))]
public class LevelUpManagerEditor : Editor
{
    private string searchFilter1 = "";
    private string searchFilter2 = "";
    private string searchFilter3 = "";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelUpManager manager = (LevelUpManager)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Auto-Assign Perks by Rarity", GUILayout.Height(30)))
        {
            AutoAssignPerks(manager);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Forced Perk Secici", EditorStyles.boldLabel);

        List<GameObject> allPerks = new List<GameObject>();
        if (manager.commonPerks != null) allPerks.AddRange(manager.commonPerks);
        if (manager.rarePerks != null) allPerks.AddRange(manager.rarePerks);
        if (manager.epicPerks != null) allPerks.AddRange(manager.epicPerks);
        if (manager.legendaryPerks != null) allPerks.AddRange(manager.legendaryPerks);
        allPerks.RemoveAll(p => p == null);

        DrawSearchablePerkField(manager, allPerks, "1st Forced Perk", ref manager.forcedPerk, ref searchFilter1);
        DrawSearchablePerkField(manager, allPerks, "2nd Forced Perk", ref manager.forcedPerk2, ref searchFilter2);
        DrawSearchablePerkField(manager, allPerks, "3rd Forced Perk", ref manager.forcedPerk3, ref searchFilter3);
    }

    private void AutoAssignPerks(LevelUpManager manager)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Perks" });

        List<GameObject> common = new List<GameObject>();
        List<GameObject> rare = new List<GameObject>();
        List<GameObject> epic = new List<GameObject>();
        List<GameObject> legendary = new List<GameObject>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            if (perk.rarity == PerkRarity.Secret) continue;

            switch (perk.rarity)
            {
                case PerkRarity.Common:    common.Add(prefab); break;
                case PerkRarity.Rare:      rare.Add(prefab); break;
                case PerkRarity.Epic:      epic.Add(prefab); break;
                case PerkRarity.Legendary: legendary.Add(prefab); break;
            }
        }

        Undo.RecordObject(manager, "Auto-Assign Perks by Rarity");
        manager.commonPerks = common;
        manager.rarePerks = rare;
        manager.epicPerks = epic;
        manager.legendaryPerks = legendary;
        EditorUtility.SetDirty(manager);

        Debug.Log($"Auto-Assign complete: {common.Count} Common, {rare.Count} Rare, {epic.Count} Epic, {legendary.Count} Legendary");
    }

    private void DrawSearchablePerkField(LevelUpManager manager, List<GameObject> allPerks, string label, ref GameObject field, ref string search)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Current value display
        string currentName = field != null ? GetPerkLabel(field) : "None";
        EditorGUILayout.LabelField(label, currentName, EditorStyles.boldLabel);

        // Search bar
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        search = EditorGUILayout.TextField(search);
        if (field != null && GUILayout.Button("X", GUILayout.Width(20)))
        {
            Undo.RecordObject(manager, "Clear " + label);
            field = null;
            EditorUtility.SetDirty(manager);
        }
        EditorGUILayout.EndHorizontal();

        // Filtered results
        if (!string.IsNullOrEmpty(search))
        {
            string lowerSearch = search.ToLower();
            int shown = 0;

            foreach (var perkObj in allPerks)
            {
                string perkLabel = GetPerkLabel(perkObj);
                if (!perkLabel.ToLower().Contains(lowerSearch)) continue;

                if (GUILayout.Button(perkLabel, EditorStyles.miniButton))
                {
                    Undo.RecordObject(manager, "Change " + label);
                    field = perkObj;
                    search = "";
                    EditorUtility.SetDirty(manager);
                    GUI.FocusControl(null);
                }

                shown++;
                if (shown >= 10) break; // Max 10 result to keep UI clean
            }

            if (shown == 0)
                EditorGUILayout.LabelField("No results.", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private string GetPerkLabel(GameObject perkObj)
    {
        BasePerk bp = perkObj.GetComponent<BasePerk>();
        string perkName = bp != null ? bp.perkName : perkObj.name;
        string rarity = bp != null ? bp.rarity.ToString() : "?";
        return $"{perkName} [{rarity}]";
    }
}
