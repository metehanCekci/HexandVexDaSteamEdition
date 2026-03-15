using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools menüsünden tek tıkla ActivePerkBar + PerkInventoryUI sistemini kurar.
/// Runtime'da otomatik oluşturulan UI'ları sahneye koyar.
/// Eski PerkListUI varsa kaldırır.
/// </summary>
public class ActivePerkBarSetup : EditorWindow
{
    [MenuItem("Tools/Setup Active Perk Bar")]
    public static void Setup()
    {
        // 1. Eski PerkListUI'ları kaldır (artık kullanılmıyor)
        foreach (var old in Object.FindObjectsByType<PerkListUI>(FindObjectsSortMode.None))
        {
            Debug.Log($"Eski PerkListUI kaldırılıyor: {old.gameObject.name}");
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 2. Eski ActivePerkBar varsa kaldır
        foreach (var old in Object.FindObjectsByType<ActivePerkBar>(FindObjectsSortMode.None))
        {
            // Root objeyi bul (canvas'ın parent'ı)
            Transform root = old.transform.root;
            Debug.Log($"Eski ActivePerkBar kaldırılıyor: {root.name}");
            Undo.DestroyObjectImmediate(root.gameObject);
        }

        // 3. Eski PerkInventoryUI varsa kaldır
        foreach (var old in Object.FindObjectsByType<PerkInventoryUI>(FindObjectsSortMode.None))
        {
            Transform root = old.transform.root;
            Debug.Log($"Eski PerkInventoryUI kaldırılıyor: {root.name}");
            Undo.DestroyObjectImmediate(root.gameObject);
        }

        // 4. ActivePerkBar ve PerkInventoryUI runtime'da CreateFromCode() ile otomatik oluşturulur.
        //    RunManager.Start() -> ActivePerkBar.CreateFromCode() çağırır.
        //    INV butonuna basılınca PerkInventoryUI.CreateFromCode() çağrılır.
        //    Bu yüzden sahnede sadece RunManager'ın varlığını kontrol edelim.

        var runManager = Object.FindFirstObjectByType<RunManager>();
        if (runManager == null)
        {
            Debug.LogError("Sahnede RunManager bulunamadı! RunManager olmadan perk bar çalışmaz.");
            return;
        }

        // RunManager'ın Start() metodu ActivePerkBar.CreateFromCode() çağıracak.
        // Ama edit modda test için şimdi preview olarak oluşturabiliriz.

        Debug.Log("✅ Perk Bar Setup tamamlandı!\n" +
                  "• Eski PerkListUI kaldırıldı\n" +
                  "• ActivePerkBar runtime'da otomatik oluşturulacak (RunManager.Start)\n" +
                  "• PerkInventoryUI, INV butonuna basılınca oluşturulacak\n" +
                  "• Oyunu başlatın ve test edin!");

        EditorUtility.DisplayDialog("Perk Bar Setup",
            "Eski PerkListUI kaldırıldı.\n\n" +
            "ActivePerkBar ve PerkInventoryUI runtime'da otomatik oluşturulur:\n" +
            "• Oyun başladığında üst ortada perk ikonları çıkar\n" +
            "• Sağ üstteki INV butonu ile envanter açılır\n" +
            "• Max 6 aktif perk, geri kalanı stash'te\n\n" +
            "Play'e basıp test edin!",
            "Tamam");
    }
}
