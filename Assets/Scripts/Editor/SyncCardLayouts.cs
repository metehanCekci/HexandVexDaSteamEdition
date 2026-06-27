using UnityEngine;
using UnityEditor;

public static class SyncCardLayouts
{
    [MenuItem("Tools/Hex and Vex/Sync Card Layouts")]
    public static void Sync()
    {
        // MergedShopCanvas'ı bul
        GameObject canvasGO = null;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas.gameObject.name == "MergedShopCanvas" && canvas.gameObject.scene.IsValid())
            {
                canvasGO = canvas.gameObject;
                break;
            }
        }
        if (canvasGO == null) { Debug.LogError("MergedShopCanvas not found!"); return; }

        Transform panel = canvasGO.transform.Find("ShopPanel");
        if (panel == null) { Debug.LogError("ShopPanel not found!"); return; }

        Transform itemSection = panel.Find("ItemSection");
        Transform perkSection = panel.Find("PerkSection");
        if (itemSection == null || perkSection == null) { Debug.LogError("ItemSection or PerkSection not found!"); return; }

        // === ITEM KARTLARI: ItemCard_0 -> ItemCard_1, ItemCard_2 ===
        Transform itemSource = itemSection.Find("ItemCard_0");
        if (itemSource != null)
        {
            RectTransform srcRT = itemSource.GetComponent<RectTransform>();
            float cardWidth = srcRT.anchorMax.x - srcRT.anchorMin.x;
            float gap = srcRT.anchorMin.x; // ilk kartın sol boşluğu = gap

            for (int i = 1; i <= 2; i++)
            {
                Transform target = itemSection.Find($"ItemCard_{i}");
                if (target == null) continue;

                float x = srcRT.anchorMin.x + (cardWidth + gap) * i;
                RectTransform tgt = target.GetComponent<RectTransform>();
                tgt.anchorMin = new Vector2(x, srcRT.anchorMin.y);
                tgt.anchorMax = new Vector2(x + cardWidth, srcRT.anchorMax.y);
                tgt.anchoredPosition = srcRT.anchoredPosition;
                tgt.sizeDelta = srcRT.sizeDelta;
                tgt.pivot = srcRT.pivot;

                CopyChildRT(itemSource, target, "Icon");
                CopyChildRT(itemSource, target, "Name");
                CopyChildRT(itemSource, target, "Description");
                CopyChildRT(itemSource, target, "PriceText");
                CopyChildRT(itemSource, target, "CoinIcon");
                CopyChildRT(itemSource, target, "SoldOut");

                EditorUtility.SetDirty(target.gameObject);
            }
            Debug.Log("[Sync] ItemCard_0 -> ItemCard_1, ItemCard_2");
        }

        // === PERK KARTLARI: PerkCard_0 -> PerkCard_1, PerkCard_2 ===
        Transform perkSource = perkSection.Find("PerkCard_0");
        if (perkSource != null)
        {
            RectTransform srcRT = perkSource.GetComponent<RectTransform>();
            float cardWidth = srcRT.anchorMax.x - srcRT.anchorMin.x;
            float gap = srcRT.anchorMin.x;

            for (int i = 1; i <= 2; i++)
            {
                Transform target = perkSection.Find($"PerkCard_{i}");
                if (target == null) continue;

                float x = srcRT.anchorMin.x + (cardWidth + gap) * i;
                RectTransform tgt = target.GetComponent<RectTransform>();
                tgt.anchorMin = new Vector2(x, srcRT.anchorMin.y);
                tgt.anchorMax = new Vector2(x + cardWidth, srcRT.anchorMax.y);
                tgt.anchoredPosition = srcRT.anchoredPosition;
                tgt.sizeDelta = srcRT.sizeDelta;
                tgt.pivot = srcRT.pivot;

                CopyChildRT(perkSource, target, "Icon");
                CopyChildRT(perkSource, target, "Name");
                CopyChildRT(perkSource, target, "Rarity");
                CopyChildRT(perkSource, target, "Level");
                CopyChildRT(perkSource, target, "Description");
                CopyChildRT(perkSource, target, "PriceText");
                CopyChildRT(perkSource, target, "CoinIcon");
                CopyChildRT(perkSource, target, "SoldOutOverlay");

                EditorUtility.SetDirty(target.gameObject);
            }
            Debug.Log("[Sync] PerkCard_0 -> PerkCard_1, PerkCard_2");
        }

        Debug.Log("[SyncCardLayouts] Done. Save scene (Ctrl+S).");
    }

    static void CopyChildRT(Transform source, Transform target, string childName)
    {
        Transform srcChild = source.Find(childName);
        Transform tgtChild = target.Find(childName);
        if (srcChild == null || tgtChild == null) return;

        RectTransform src = srcChild.GetComponent<RectTransform>();
        RectTransform tgt = tgtChild.GetComponent<RectTransform>();
        tgt.anchorMin = src.anchorMin;
        tgt.anchorMax = src.anchorMax;
        tgt.anchoredPosition = src.anchoredPosition;
        tgt.sizeDelta = src.sizeDelta;
        tgt.pivot = src.pivot;
        EditorUtility.SetDirty(tgtChild.gameObject);
    }
}
