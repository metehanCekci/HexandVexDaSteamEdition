using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// Tools > Setup Piggy Bank
/// Sahnedeki PersistentHUD'un altına PIGGYRow'u inşa eder, PiggyBankManager + PiggyBankUI component'lerini
/// ekler ve referansları bağlar. Resources/PiggyBankConfig.asset yoksa da oluşturur.
///
/// Çalıştırmadan önce: Combat sahnesini aç, sahnede PersistentHUD GameObject'i olduğundan emin ol.
/// Tool çalıştıktan sonra Inspector'dan spacing, position, size tweak'leyebilirsin.
/// </summary>
public class PiggyBankSetup : EditorWindow
{
    private const string ConfigAssetPath = "Assets/Resources/PiggyBankConfig.asset";

    [MenuItem("Tools/Setup Piggy Bank")]
    public static void Setup()
    {
        // 1. PersistentHUD'u bul
        var hud = Object.FindFirstObjectByType<PersistentHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog(
                "Piggy Bank Setup",
                "Sahnede PersistentHUD bulunamadı. Önce combat sahnesini aç ve PersistentHUD GameObject'inin var olduğundan emin ol.",
                "OK");
            return;
        }

        // 2. Config asset'ini bul/oluştur
        PiggyBankConfig config = EnsureConfig();

        // 3. Font yükle
        TMP_FontAsset font = UIStyle.LoadFont();

        // 4. Eski PIGGYRow / PiggyTooltip varsa temizle (idempotent)
        CleanupOld(hud.transform);

        // 5. PiggyBankManager (singleton) — ayrı bir root GameObject
        EnsureManager(config);

        // 6. PIGGYRow'u GOLDRow'un hemen altına yerleştir
        Transform goldRow = FindGoldRow(hud);
        if (goldRow == null)
        {
            EditorUtility.DisplayDialog(
                "Piggy Bank Setup",
                "GOLDRow bulunamadı. PersistentHUD'da 'gold' veya 'coin' isimli bir parent altında TMP_Text olmalı.",
                "OK");
            return;
        }

        // PIGGYRow'u GOLDRow'un parent'ina (HUDContainer) kardes olarak ekle, GOLDRow'dan hemen sonra
        Transform rowParent = goldRow.parent != null ? goldRow.parent : hud.transform;
        (GameObject rowGO, Image piggyIcon, TMP_Text valueText) = BuildRow(rowParent, goldRow, font, hud);

        // 7. Tooltip (inactive) - PersistentHUDCanvas'a konulsun ki gold row'un yaninda gorunur olsun
        Transform tooltipParent = FindTooltipParent(hud.transform);
        (GameObject tooltipGO, TMP_Text tooltipText) = BuildTooltip(tooltipParent, font);

        // 8. PiggyBankUI component + referanslar
        var ui = hud.gameObject.GetComponent<PiggyBankUI>();
        if (ui == null) ui = Undo.AddComponent<PiggyBankUI>(hud.gameObject);
        Undo.RecordObject(ui, "Setup Piggy Bank UI");

        ui.piggyRoot = rowGO.GetComponent<RectTransform>();
        ui.piggyIcon = piggyIcon;
        ui.valueText = valueText;
        ui.coinLandingTarget = hud.goldText != null ? hud.goldText.rectTransform : null;
        ui.tooltipRoot = tooltipGO;
        ui.tooltipText = tooltipText;

        // Flying coin sprite'ı — sahnedeki coin icon'unu dene
        var existingCoinIcon = hud.coinIconImage;
        if (existingCoinIcon != null && existingCoinIcon.sprite != null)
            ui.flyingCoinSprite = existingCoinIcon.sprite;

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(hud.gameObject);

        // 9. Seç ve başarı mesajı
        Selection.activeGameObject = rowGO;
        EditorGUIUtility.PingObject(rowGO);

        Debug.Log("Piggy Bank sahnede kuruldu!\n" +
                  "• PIGGYRow GOLDRow'un altına eklendi\n" +
                  "• PiggyBankManager sahne root'una eklendi\n" +
                  "• PiggyBankUI component'i PersistentHUD'a bağlandı\n" +
                  "• Config: " + ConfigAssetPath + "\n" +
                  "• Inspector'dan pozisyon/font/boyut tweak'leyebilirsin\n" +
                  "• Çizerden sprite gelince Config asset'ine sürükle");
    }

    // ────────────────────────────────────────────────────────────────────────────
    private static PiggyBankConfig EnsureConfig()
    {
        string dir = Path.GetDirectoryName(ConfigAssetPath);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var config = AssetDatabase.LoadAssetAtPath<PiggyBankConfig>(ConfigAssetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PiggyBankConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("PiggyBankConfig.asset oluşturuldu: " + ConfigAssetPath);
        }
        return config;
    }

    private static void EnsureManager(PiggyBankConfig config)
    {
        var existing = Object.FindFirstObjectByType<PiggyBankManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Configure PiggyBankManager");
            existing.config = config;
            EditorUtility.SetDirty(existing);
            return;
        }

        var go = new GameObject("PiggyBankManager");
        Undo.RegisterCreatedObjectUndo(go, "Create PiggyBankManager");
        var mgr = Undo.AddComponent<PiggyBankManager>(go);
        mgr.config = config;
        EditorUtility.SetDirty(mgr);
    }

    private static void CleanupOld(Transform hudRoot)
    {
        // Recursive temizlik - nerede olursa olsun eski PIGGYRow/PiggyTooltip'i sil
        var toDelete = new System.Collections.Generic.List<GameObject>();
        CollectByName(hudRoot, toDelete, "PIGGYRow", "PiggyTooltip");
        foreach (var go in toDelete)
            if (go != null) Undo.DestroyObjectImmediate(go);
    }

    private static void CollectByName(Transform root, System.Collections.Generic.List<GameObject> output, params string[] names)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            foreach (var n in names)
                if (child.name == n) { output.Add(child.gameObject); break; }
            CollectByName(child, output, names);
        }
    }

    private static Transform FindTooltipParent(Transform hudRoot)
    {
        // Prefer 'PersistentHUDCanvas' or any Canvas descendant so the tooltip renders in the same canvas
        var canvas = hudRoot.GetComponentInChildren<Canvas>(true);
        if (canvas != null) return canvas.transform;
        return hudRoot;
    }

    private static Transform FindGoldRow(PersistentHUD hud)
    {
        // 1. goldText referansi atanmissa parent'ini al (en guvenilir yol)
        if (hud.goldText != null && hud.goldText.transform.parent != null)
            return hud.goldText.transform.parent;

        // 2. Tum hierarchy'de recursive olarak 'gold' veya 'coin' isimli bir node ara
        return FindDescendantByName(hud.transform, n => n.Contains("gold") || n.Contains("coin"));
    }

    private static Transform FindDescendantByName(Transform root, System.Func<string, bool> predicate)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            string n = child.name.ToLower();
            if (predicate(n)) return child;
            var found = FindDescendantByName(child, predicate);
            if (found != null) return found;
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────────────────
    private static (GameObject row, Image icon, TMP_Text value) BuildRow(
        Transform rowParent, Transform goldRow, TMP_FontAsset font, PersistentHUD hud)
    {
        var rowGO = new GameObject("PIGGYRow", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rowGO, "Create PIGGYRow");
        rowGO.transform.SetParent(rowParent, false);
        rowGO.transform.SetSiblingIndex(goldRow.GetSiblingIndex() + 1);

        // GOLDRow'un anchor/pivot/size'ini kopyala ki ayni layout icinde flush otursun
        var rowRT = rowGO.GetComponent<RectTransform>();
        if (goldRow is RectTransform goldRT)
        {
            rowRT.anchorMin = goldRT.anchorMin;
            rowRT.anchorMax = goldRT.anchorMax;
            rowRT.pivot = goldRT.pivot;
            rowRT.sizeDelta = goldRT.sizeDelta;
            rowRT.anchoredPosition = goldRT.anchoredPosition;

            // GOLDRow'un HorizontalLayoutGroup ayarlarini da kopyala (varsa)
            var goldHLG = goldRT.GetComponent<HorizontalLayoutGroup>();
            if (goldHLG != null)
            {
                var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = goldHLG.spacing;
                hlg.childAlignment = goldHLG.childAlignment;
                hlg.childControlWidth = goldHLG.childControlWidth;
                hlg.childControlHeight = goldHLG.childControlHeight;
                hlg.childForceExpandWidth = goldHLG.childForceExpandWidth;
                hlg.childForceExpandHeight = goldHLG.childForceExpandHeight;
                hlg.padding = goldHLG.padding;
            }
            else
            {
                var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }
        else
        {
            rowRT.sizeDelta = new Vector2(160f, 28f);
            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
        }

        // GOLDRow'daki Icon/Value child'larini referans al ki ayni boyutlari kullanalim
        RectTransform goldIconRT = goldRow.Find("Icon") as RectTransform;
        RectTransform goldValueRT = goldRow.Find("Value") as RectTransform;
        TMP_Text goldValueText = goldValueRT != null ? goldValueRT.GetComponent<TMP_Text>() : null;
        if (goldValueText == null) goldValueText = hud != null ? hud.goldText : null;

        // Icon
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(iconGO, "Create PiggyIcon");
        iconGO.transform.SetParent(rowGO.transform, false);
        var iconRT = iconGO.GetComponent<RectTransform>();
        if (goldIconRT != null)
        {
            iconRT.sizeDelta = goldIconRT.sizeDelta;
            iconRT.anchorMin = goldIconRT.anchorMin;
            iconRT.anchorMax = goldIconRT.anchorMax;
            iconRT.pivot = goldIconRT.pivot;
        }
        else
        {
            iconRT.sizeDelta = new Vector2(22f, 22f);
        }
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = iconRT.sizeDelta.x;
        iconLE.preferredHeight = iconRT.sizeDelta.y;
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.raycastTarget = true;
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        // Value
        var valGO = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(valGO, "Create PiggyValue");
        valGO.transform.SetParent(rowGO.transform, false);
        var valText = valGO.AddComponent<TextMeshProUGUI>();
        valText.text = "0/10";
        valText.alignment = TextAlignmentOptions.Left;
        valText.raycastTarget = false;

        if (goldValueText != null)
        {
            valText.fontSize = goldValueText.fontSize;
            if (goldValueText.font != null) valText.font = goldValueText.font;
            valText.color = goldValueText.color;
            valText.alignment = goldValueText.alignment;
        }
        else
        {
            valText.color = UIStyle.TextWhite;
            valText.fontSize = UIStyle.FontSizeNormal;
            if (font != null) valText.font = font;
        }

        var valRT = valGO.GetComponent<RectTransform>();
        if (goldValueRT != null)
        {
            valRT.sizeDelta = goldValueRT.sizeDelta;
            valRT.anchorMin = goldValueRT.anchorMin;
            valRT.anchorMax = goldValueRT.anchorMax;
            valRT.pivot = goldValueRT.pivot;
        }
        else
        {
            valRT.sizeDelta = new Vector2(60f, 22f);
        }
        var valLE = valGO.AddComponent<LayoutElement>();
        valLE.preferredWidth = valRT.sizeDelta.x;
        valLE.preferredHeight = valRT.sizeDelta.y;

        return (rowGO, iconImg, valText);
    }

    private static (GameObject root, TMP_Text text) BuildTooltip(Transform parent, TMP_FontAsset font)
    {
        var tipGO = new GameObject("PiggyTooltip", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(tipGO, "Create PiggyTooltip");
        tipGO.transform.SetParent(parent, false);

        var tipRT = tipGO.GetComponent<RectTransform>();
        tipRT.pivot = new Vector2(0f, 1f);
        tipRT.anchorMin = new Vector2(0f, 1f);
        tipRT.anchorMax = new Vector2(0f, 1f);
        tipRT.anchoredPosition = new Vector2(160f, -60f);
        tipRT.sizeDelta = new Vector2(280f, 60f);

        // Background
        var bgGO = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(bgGO, "Create TooltipBg");
        bgGO.transform.SetParent(tipGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0.02f, 0.05f, 0.9f);
        bgImg.raycastTarget = false;

        // Text
        var txtGO = new GameObject("Text", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(txtGO, "Create TooltipText");
        txtGO.transform.SetParent(tipGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8f, 6f);
        txtRT.offsetMax = new Vector2(-8f, -6f);
        var tipText = txtGO.AddComponent<TextMeshProUGUI>();
        tipText.text = "Every 10 gold earns 1 gold at the end of a wave (max 10).";
        tipText.fontSize = UIStyle.FontSizeSmall;
        tipText.color = UIStyle.TextWhite;
        tipText.alignment = TextAlignmentOptions.TopLeft;
        tipText.enableWordWrapping = true;
        tipText.raycastTarget = false;
        if (font != null) tipText.font = font;

        tipGO.SetActive(false);

        return (tipGO, tipText);
    }
}
