using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(LevelUpManager))]
public class LevelUpManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelUpManager manager = (LevelUpManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Forced Perk Secici", EditorStyles.boldLabel);

        List<GameObject> allPerks = new List<GameObject>();
        if (manager.commonPerks != null) allPerks.AddRange(manager.commonPerks);
        if (manager.rarePerks != null) allPerks.AddRange(manager.rarePerks);
        if (manager.epicPerks != null) allPerks.AddRange(manager.epicPerks);
        if (manager.legendaryPerks != null) allPerks.AddRange(manager.legendaryPerks);
        allPerks.RemoveAll(p => p == null);

        List<string> perkNames = new List<string> { "None" };
        for (int i = 0; i < allPerks.Count; i++)
        {
            BasePerk bp = allPerks[i].GetComponent<BasePerk>();
            string label = bp != null ? bp.perkName : allPerks[i].name;
            perkNames.Add(label);
        }

        string[] names = perkNames.ToArray();

        DrawForcedPerkPopup(manager, allPerks, names, "1st Forced Perk", ref manager.forcedPerk);
        DrawForcedPerkPopup(manager, allPerks, names, "2nd Forced Perk", ref manager.forcedPerk2);
        DrawForcedPerkPopup(manager, allPerks, names, "3rd Forced Perk", ref manager.forcedPerk3);
    }

    private void DrawForcedPerkPopup(LevelUpManager manager, List<GameObject> allPerks, string[] names, string label, ref GameObject field)
    {
        int currentIndex = 0;
        if (field != null)
        {
            int found = allPerks.IndexOf(field);
            if (found >= 0) currentIndex = found + 1;
        }

        int newIndex = EditorGUILayout.Popup(label, currentIndex, names);
        GameObject newValue = newIndex == 0 ? null : allPerks[newIndex - 1];
        if (field != newValue)
        {
            Undo.RecordObject(manager, "Change " + label);
            field = newValue;
            EditorUtility.SetDirty(manager);
        }
    }
}
