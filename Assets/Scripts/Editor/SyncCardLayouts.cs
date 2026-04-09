using UnityEngine;
using UnityEditor;

public static class SyncCardLayouts
{
    [MenuItem("Tools/Hex and Vex/Sync Card Layouts from ItemCard_0")]
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

        // ShopPanel > ItemSection & PerkSection bul
        Transform panel = canvasGO.transform.Find("ShopPanel");
        if (panel == null) { Debug.LogError("ShopPanel not found!"); return; }

        Transform itemSection = panel.Find("ItemSection");
        Transform perkSection = panel.Find("PerkSection");
        if (itemSection == null || perkSection == null) { Debug.LogError("ItemSection or PerkSection not found!"); return; }

        // Kaynak: ItemCard_0
        Transform source = itemSection.Find("ItemCard_0");
        if (source == null) { Debug.LogError("ItemCard_0 not found!"); return; }

        RectTransform sourceRT = source.GetComponent<RectTransform>();

        // === ITEM KARTLARI (ItemCard_1, ItemCard_2) ===
        // Kart boyutu aynı, sadece X pozisyonu kaydırılacak
        float cardWidth = sourceRT.anchorMax.x - sourceRT.anchorMin.x; // ~0.313
        float gap = 0.015f;

        for (int i = 1; i <= 2; i++)
        {
            Transform target = itemSection.Find($"ItemCard_{i}");
            if (target == null) continue;

            float x = sourceRT.anchorMin.x + (cardWidth + gap) * i;
            RectTransform targetRT = target.GetComponent<RectTransform>();
            targetRT.anchorMin = new Vector2(x, sourceRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(x + cardWidth, sourceRT.anchorMax.y);
            targetRT.anchoredPosition = sourceRT.anchoredPosition;
            targetRT.sizeDelta = sourceRT.sizeDelta;
            targetRT.pivot = sourceRT.pivot;

            // Child'ları kopyala (isim eşleştirmesi)
            CopyChildRT(source, target, "Icon");
            CopyChildRT(source, target, "Name");
            CopyChildRT(source, target, "Description");
            CopyChildRT(source, target, "PriceText");
            CopyChildRT(source, target, "CoinIcon");
            CopyChildRT(source, target, "SoldOut");

            EditorUtility.SetDirty(target.gameObject);
        }

        // === PERK KARTLARI (PerkCard_0, PerkCard_1, PerkCard_2) ===
        for (int i = 0; i <= 2; i++)
        {
            Transform target = perkSection.Find($"PerkCard_{i}");
            if (target == null) continue;

            float x = sourceRT.anchorMin.x + (cardWidth + gap) * i;
            RectTransform targetRT = target.GetComponent<RectTransform>();
            targetRT.anchorMin = new Vector2(x, sourceRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(x + cardWidth, sourceRT.anchorMax.y);
            targetRT.anchoredPosition = sourceRT.anchoredPosition;
            targetRT.sizeDelta = sourceRT.sizeDelta;
            targetRT.pivot = sourceRT.pivot;

            // Ortak child'lar
            CopyChildRT(source, target, "Icon");
            CopyChildRT(source, target, "Name");
            CopyChildRT(source, target, "Description");

            // SoldOut -> SoldOutOverlay (isim farklı, aynı layout)
            CopyChildRTRenamed(source, "SoldOut", target, "SoldOutOverlay");

            // Perk-specific: Rarity ve Level, ItemCard_0'daki PriceText ve CoinIcon pozisyonlarına map'le
            // Rarity -> PriceText pozisyonuna
            CopyChildRTRenamed(source, "PriceText", target, "Rarity");
            // Level -> CoinIcon pozisyonuna
            CopyChildRTRenamed(source, "CoinIcon", target, "Level");

            EditorUtility.SetDirty(target.gameObject);
        }

        Debug.Log("[SyncCardLayouts] All cards synced from ItemCard_0. Save scene (Ctrl+S).");
    }

    static void CopyChildRT(Transform source, Transform target, string childName)
    {
        Transform srcChild = source.Find(childName);
        Transform tgtChild = target.Find(childName);
        if (srcChild == null || tgtChild == null) return;

        CopyRectTransform(srcChild.GetComponent<RectTransform>(), tgtChild.GetComponent<RectTransform>());
        EditorUtility.SetDirty(tgtChild.gameObject);
    }

    static void CopyChildRTRenamed(Transform source, string srcChildName, Transform target, string tgtChildName)
    {
        Transform srcChild = source.Find(srcChildName);
        Transform tgtChild = target.Find(tgtChildName);
        if (srcChild == null || tgtChild == null) return;

        CopyRectTransform(srcChild.GetComponent<RectTransform>(), tgtChild.GetComponent<RectTransform>());
        EditorUtility.SetDirty(tgtChild.gameObject);
    }

    static void CopyRectTransform(RectTransform src, RectTransform tgt)
    {
        tgt.anchorMin = src.anchorMin;
        tgt.anchorMax = src.anchorMax;
        tgt.anchoredPosition = src.anchoredPosition;
        tgt.sizeDelta = src.sizeDelta;
        tgt.pivot = src.pivot;
    }
}
