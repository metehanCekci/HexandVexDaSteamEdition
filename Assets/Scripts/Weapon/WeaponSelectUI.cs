using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Silah Seçme Ekranı — tamamen kodla oluşturulur, Inspector'dan bir şey bağlamaya gerek yok.
/// PlayButton çağrıldığında otomatik olarak gösterilir.
/// </summary>
public class WeaponSelectUI : MonoBehaviour
{
    public static WeaponSelectUI instance;

    public int gameSceneIndex = 2;

    private GameObject panelRoot;
    private bool isBuilt = false;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Silah seçme panelini gösterir. Henüz oluşturulmadıysa runtime'da oluşturur.
    /// </summary>
    public void Show()
    {
        if (!isBuilt) BuildUI();
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void SelectWeapon(WeaponType type)
    {
        if (RunManager.instance != null)
            RunManager.instance.selectedWeapon = type;

        if (panelRoot != null) panelRoot.SetActive(false);

        Time.timeScale = 1f;
        if (ScreenFader.instance != null)
        {
            ScreenFader.instance.FadeAndLoad(() =>
            {
                SceneManager.LoadScene(gameSceneIndex);
            });
        }
        else
        {
            SceneManager.LoadScene(gameSceneIndex);
        }
    }

    // ──────── Runtime UI Builder ────────

    private void BuildUI()
    {
        isBuilt = true;

        // Canvas
        GameObject canvasObj = new GameObject("WeaponSelectCanvas");
        canvasObj.transform.SetParent(transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        panelRoot = canvasObj;

        // Karartma arkaplan
        GameObject bg = CreateUIElement("Background", canvasObj.transform);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        StretchFull(bgRect);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);

        // Başlık
        GameObject titleObj = CreateUIElement("Title", canvasObj.transform);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -60f);
        titleRect.sizeDelta = new Vector2(800f, 80f);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SİLAH SEÇ";
        titleText.fontSize = 48;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;

        // Kart container (yatay)
        GameObject container = CreateUIElement("CardContainer", canvasObj.transform);
        RectTransform contRect = container.GetComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.5f, 0.5f);
        contRect.anchorMax = new Vector2(0.5f, 0.5f);
        contRect.pivot = new Vector2(0.5f, 0.5f);
        contRect.anchoredPosition = Vector2.zero;
        contRect.sizeDelta = new Vector2(900f, 450f);
        HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 60f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Greatsword kartı
        CreateWeaponCard(container.transform, "GREATSWORD",
            "Klasik kılıç.\nBitişik düşmanlara saldırır.\n2 zar atar.",
            new Color(0.3f, 0.5f, 0.8f, 1f),
            WeaponType.Greatsword);

        // MitsuriBlade kartı
        CreateWeaponCard(container.transform, "MITSURI BLADE",
            "Uzayan kılıç.\nHaritadaki herhangi bir düşmana saldır.\n3+ hex mesafede 1 zar cezası.",
            new Color(0.8f, 0.3f, 0.5f, 1f),
            WeaponType.MitsuriBlade);

        // Geri butonu
        GameObject backObj = CreateUIElement("BackButton", canvasObj.transform);
        RectTransform backRect = backObj.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 40f);
        backRect.sizeDelta = new Vector2(200f, 50f);
        Image backBg = backObj.AddComponent<Image>();
        backBg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Button backBtn = backObj.AddComponent<Button>();
        backBtn.onClick.AddListener(() => Hide());

        GameObject backTextObj = CreateUIElement("BackText", backObj.transform);
        StretchFull(backTextObj.GetComponent<RectTransform>());
        TMP_Text backText = backTextObj.AddComponent<TextMeshProUGUI>();
        backText.text = "GERİ";
        backText.fontSize = 24;
        backText.alignment = TextAlignmentOptions.Center;
        backText.color = Color.white;
    }

    private void CreateWeaponCard(Transform parent, string weaponName, string desc, Color cardColor, WeaponType type)
    {
        GameObject card = CreateUIElement("Card_" + weaponName, parent);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = cardColor;

        // Köşeleri yumuşak yapmak için sprite yok, düz renk yeterli

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Silah adı
        GameObject nameObj = CreateUIElement("Name", card.transform);
        LayoutElement nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 50f;
        TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = weaponName;
        nameText.fontSize = 32;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;

        // Silah görseli placeholder (renkli kare)
        GameObject imgObj = CreateUIElement("WeaponImage", card.transform);
        LayoutElement imgLE = imgObj.AddComponent<LayoutElement>();
        imgLE.preferredHeight = 180f;
        Image weaponImg = imgObj.AddComponent<Image>();

        // pngs klasöründen resim yüklemeyi dene
        Sprite weaponSprite = null;
        if (type == WeaponType.Greatsword)
            weaponSprite = Resources.Load<Sprite>("greatsword");
        else if (type == WeaponType.MitsuriBlade)
            weaponSprite = Resources.Load<Sprite>("mitsuri");

        if (weaponSprite != null)
        {
            weaponImg.sprite = weaponSprite;
            weaponImg.preserveAspect = true;
        }
        else
        {
            weaponImg.color = new Color(1f, 1f, 1f, 0.3f);
        }

        // Açıklama
        GameObject descObj = CreateUIElement("Description", card.transform);
        LayoutElement descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 100f;
        TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = desc;
        descText.fontSize = 18;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(1f, 1f, 1f, 0.9f);

        // Seç butonu
        GameObject btnObj = CreateUIElement("SelectButton", card.transform);
        LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 50f;
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(1f, 1f, 1f, 0.25f);
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => SelectWeapon(type));

        // Hover efekti
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.5f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.7f);
        btn.colors = colors;

        GameObject btnTextObj = CreateUIElement("BtnText", btnObj.transform);
        StretchFull(btnTextObj.GetComponent<RectTransform>());
        TMP_Text btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "SEÇ";
        btnText.fontSize = 24;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.fontStyle = FontStyles.Bold;

        // Tüm karta da tıklanabilirlik ekle
        Button cardBtn = card.GetComponent<Button>();
        if (cardBtn == null) cardBtn = card.AddComponent<Button>();
        cardBtn.onClick.AddListener(() => SelectWeapon(type));
        ColorBlock cardColors = cardBtn.colors;
        cardColors.normalColor = cardColor;
        cardColors.highlightedColor = cardColor * 1.2f;
        cardColors.pressedColor = cardColor * 1.4f;
        cardBtn.colors = cardColors;
    }

    // ──────── Yardımcı ────────

    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ──────── Otomatik Bootstrap ────────

    /// <summary>
    /// MainMenu sahnesinde otomatik olarak WeaponSelectUI oluşturur.
    /// Herhangi bir sahne objesine bu script'i eklemeye gerek yok.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        // Sadece MainMenu sahnesinde (index 1) otomatik oluştur
        if (SceneManager.GetActiveScene().buildIndex == 1 && instance == null)
        {
            CreateInstance();
        }

        // Sahne değişikliklerini dinle
        SceneManager.sceneLoaded -= OnSceneLoadedStatic;
        SceneManager.sceneLoaded += OnSceneLoadedStatic;
    }

    private static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
    {
        // MainMenu sahnesine her geldiğimizde instance yoksa oluştur
        if (scene.buildIndex == 1 && instance == null)
        {
            CreateInstance();
        }
        // Diğer sahnelerde UI'ı temizle
        else if (scene.buildIndex != 1 && instance != null)
        {
            Destroy(instance.gameObject);
        }
    }

    private static void CreateInstance()
    {
        GameObject obj = new GameObject("WeaponSelectUI");
        obj.AddComponent<WeaponSelectUI>();
    }
}
