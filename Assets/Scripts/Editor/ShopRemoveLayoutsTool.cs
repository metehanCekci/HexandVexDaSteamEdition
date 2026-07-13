using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Tools > Remove Shop Layouts
/// ShopCanvas'taki tüm LayoutGroup, LayoutElement, ContentSizeFitter bileşenlerini kaldırır.
/// Mevcut pozisyonları korur — her child'ın hesaplanmış konumunu RectTransform'a sabitler.
/// Sonuç: Hierarchy'de her şeyi özgürce hareket ettirebilirsin.
/// </summary>
public static class ShopRemoveLayoutsTool
{
    [MenuItem("Tools/Remove Shop Layouts")]
    public static void Execute()
    {
        GameObject canvasGO = GameObject.Find("ShopCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[RemoveLayouts] Sahnede 'ShopCanvas' bulunamadı!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvasGO, "Remove Shop Layouts");

        // 1. Layout'ları yeniden hesaplat ki pozisyonlar güncel olsun
        Canvas.ForceUpdateCanvases();
        foreach (var lr in canvasGO.GetComponentsInChildren<LayoutGroup>(true))
            LayoutRebuilder.ForceRebuildLayoutImmediate(lr.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();

        // 2. Tüm layout-driven child'ların pozisyon/boyutlarını kaydet
        //    (LayoutGroup'un child'ları — bunların pozisyonları layout tarafından hesaplanıyor)
        var snapshots = new Dictionary<RectTransform, Snapshot>();
        foreach (var lg in canvasGO.GetComponentsInChildren<LayoutGroup>(true))
        {
            RectTransform parent = lg.GetComponent<RectTransform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child == null) continue;

                // LayoutElement.ignoreLayout olan elemanlar zaten serbest — onlara dokunma
                LayoutElement le = child.GetComponent<LayoutElement>();
                if (le != null && le.ignoreLayout) continue;

                if (!snapshots.ContainsKey(child))
                {
                    snapshots[child] = new Snapshot
                    {
                        anchoredPosition = child.anchoredPosition,
                        sizeDelta = child.sizeDelta,
                        anchorMin = child.anchorMin,
                        anchorMax = child.anchorMax,
                        pivot = child.pivot
                    };
                }
            }
        }

        // 3. Tüm LayoutElement'leri kaldır
        int leCount = 0;
        foreach (var le in canvasGO.GetComponentsInChildren<LayoutElement>(true))
        {
            Undo.DestroyObjectImmediate(le);
            leCount++;
        }

        // 4. Tüm ContentSizeFitter'ları kaldır
        int csfCount = 0;
        foreach (var csf in canvasGO.GetComponentsInChildren<ContentSizeFitter>(true))
        {
            Undo.DestroyObjectImmediate(csf);
            csfCount++;
        }

        // 5. Tüm LayoutGroup'ları kaldır (HorizontalLayoutGroup, VerticalLayoutGroup, GridLayoutGroup)
        int lgCount = 0;
        foreach (var lg in canvasGO.GetComponentsInChildren<LayoutGroup>(true))
        {
            Undo.DestroyObjectImmediate(lg);
            lgCount++;
        }

        // 6. Kaydedilen pozisyon/boyutları geri yaz
        foreach (var kvp in snapshots)
        {
            RectTransform rt = kvp.Key;
            if (rt == null) continue;
            Snapshot s = kvp.Value;

            rt.anchorMin = s.anchorMin;
            rt.anchorMax = s.anchorMax;
            rt.pivot = s.pivot;
            rt.anchoredPosition = s.anchoredPosition;
            rt.sizeDelta = s.sizeDelta;
        }

        EditorUtility.SetDirty(canvasGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[RemoveLayouts] Tamamlandı!\n" +
                  $"• {lgCount} LayoutGroup kaldırıldı\n" +
                  $"• {leCount} LayoutElement kaldırıldı\n" +
                  $"• {csfCount} ContentSizeFitter kaldırıldı\n" +
                  $"• {snapshots.Count} child pozisyonu korundu\n" +
                  $"Artık her şeyi özgürce hareket ettirebilirsin!");
    }

    private struct Snapshot
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
    }
}
