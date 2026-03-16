using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > Setup Perk Inventory
/// Sahnede PerkInventoryUI yan panelini oluşturur.
/// Map ekranında sağ tarafta her zaman görünür.
/// Inspector'dan düzenlenebilir — pozisyon, boyut, renk vs.
/// </summary>
public class PerkInventorySetup : EditorWindow
{
    [MenuItem("Tools/Setup Perk Inventory")]
    public static void Setup()
    {
        // 1. Eski PerkInventoryUI varsa kaldır
        foreach (var old in Object.FindObjectsByType<PerkInventoryUI>(FindObjectsSortMode.None))
        {
            Debug.Log($"Eski PerkInventoryUI kaldırılıyor: {old.gameObject.name}");
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 2. Font yükle
        TMP_FontAsset font = UIStyle.LoadFont();

        // ─── ROOT: PerkInventoryUI ───
        GameObject rootGO = new GameObject("PerkInventoryUI");
        PerkInventoryUI ui = rootGO.AddComponent<PerkInventoryUI>();
        Undo.RegisterCreatedObjectUndo(rootGO, "Create PerkInventoryUI");

        // ─── BuildUI ile tüm alt objeleri oluştur ───
        ui.BuildUI();

        // ─── Font uygula ───
        if (font != null)
        {
            var allTMP = rootGO.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTMP)
            {
                tmp.font = font;
            }
        }

        // ─── SELECTION ───
        Selection.activeGameObject = rootGO;
        EditorUtility.SetDirty(rootGO);

        Debug.Log("PerkInventoryUI sahnede oluşturuldu!\n" +
                  "• Sağ tarafta yan panel olarak gösterilir\n" +
                  "• Map ekranında otomatik açılır/kapanır\n" +
                  "• Drag & drop ile perk yönetimi\n" +
                  "• Inspector'dan pozisyon, boyut, renk ayarlanabilir");
    }
}
