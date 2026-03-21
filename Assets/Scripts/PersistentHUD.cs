using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Persistent HUD that shows Gold and HP on every screen.
/// All UI elements are set up in the scene/prefab — nothing is created via code.
/// Assign references in Inspector.
/// </summary>
public class PersistentHUD : MonoBehaviour
{
    public static PersistentHUD instance;

    [Header("References (assign in Inspector)")]
    public TMP_Text goldText;
    public TMP_Text hpText;
    public Image coinIconImage;

    [Header("Pulse Animation")]
    public float pulseDuration = 0.35f;
    public float pulseScaleMultiplier = 0.2f;
    public Color hpDamagePulseColor = new Color(1f, 0.2f, 0.2f);
    public Color goldPulseColor = new Color(1f, 0.95f, 0.4f);

    private int lastHP = -1;
    private int lastGold = -1;
    private Coroutine hpPulseCoroutine;
    private Coroutine goldPulseCoroutine;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(instance.gameObject);
        }
        instance = this;
    }

    void Start()
    {
        AutoFindReferences();
        Refresh();
    }

    private void AutoFindReferences()
    {
        // Find text components by parent row name if not assigned
        if (goldText == null || hpText == null)
        {
            var allTexts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allTexts)
            {
                string parentName = t.transform.parent != null ? t.transform.parent.name.ToLower() : "";
                if (goldText == null && (parentName.Contains("gold") || parentName.Contains("coin")))
                    goldText = t;
                else if (hpText == null && (parentName.Contains("hp") || parentName.Contains("health")))
                    hpText = t;
            }
        }

        if (coinIconImage == null)
        {
            var allImages = GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                string parentName = img.transform.parent != null ? img.transform.parent.name.ToLower() : "";
                if (img.name.ToLower().Contains("icon") && (parentName.Contains("gold") || parentName.Contains("coin")))
                {
                    coinIconImage = img;
                    break;
                }
            }
        }

        if (goldText == null) Debug.LogWarning("[PersistentHUD] goldText not found! Make sure GOLDRow/Value exists.");
        if (hpText == null) Debug.LogWarning("[PersistentHUD] hpText not found! Make sure HPRow/Value exists.");
    }

    void OnEnable()  { GameEvents.OnGoldChanged += OnGoldChanged; }
    void OnDisable() { GameEvents.OnGoldChanged -= OnGoldChanged; }

    void Update()
    {
        if (RunManager.instance == null) return;

        int curHP = RunManager.instance.playerCurrentHealth;
        int maxHP = RunManager.instance.playerMaxHealth;

        if (hpText != null) hpText.text = curHP + "/" + maxHP;
        if (goldText != null) goldText.text = RunManager.instance.currentGold.ToString();

        // HP pulse on damage
        if (curHP != lastHP && lastHP >= 0 && curHP < lastHP && hpText != null)
        {
            if (hpPulseCoroutine != null) StopCoroutine(hpPulseCoroutine);
            hpPulseCoroutine = StartCoroutine(PulseText(hpText, hpDamagePulseColor));
        }
        lastHP = curHP;
    }

    private void OnGoldChanged(int amount)
    {
        if (goldText != null)
        {
            int diff = lastGold >= 0 ? amount - lastGold : 0;
            lastGold = amount;

            goldText.text = amount.ToString();
            if (goldPulseCoroutine != null) StopCoroutine(goldPulseCoroutine);
            goldPulseCoroutine = StartCoroutine(PulseText(goldText, goldPulseColor));

            if (diff > 0) SpawnFloatingGoldText(diff);
        }
    }

    private void SpawnFloatingGoldText(int amount)
    {
        if (goldText == null) return;

        var go = new GameObject("GoldFloat");
        go.transform.SetParent(goldText.transform.parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "+" + amount;
        tmp.fontSize = goldText.fontSize * 0.85f;
        tmp.color = goldPulseColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (goldText.font != null) tmp.font = goldText.font;

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = goldText.rectTransform.anchoredPosition + new Vector2(goldText.preferredWidth + 8f, 0f);

        StartCoroutine(FloatAndFade(go, rt));
    }

    private IEnumerator FloatAndFade(GameObject go, RectTransform rt)
    {
        float duration = 0.9f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        var tmp = go.GetComponent<TMP_Text>();
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            if (go == null) yield break;
            float t = elapsed / duration;
            rt.anchoredPosition = startPos + new Vector2(0f, 20f * t);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    public void Refresh()
    {
        if (RunManager.instance == null) return;
        if (goldText != null) goldText.text = RunManager.instance.currentGold.ToString();
        if (hpText != null)
            hpText.text = RunManager.instance.playerCurrentHealth + "/" + RunManager.instance.playerMaxHealth;
        lastHP = RunManager.instance.playerCurrentHealth;
        lastGold = RunManager.instance.currentGold;
    }

    private IEnumerator PulseText(TMP_Text text, Color flashColor)
    {
        if (text == null) yield break;
        Color original = text.color;
        float elapsed = 0f;
        Transform t = text.transform;
        Vector3 baseScale = Vector3.one;

        while (elapsed < pulseDuration)
        {
            if (text == null) yield break;
            float p = elapsed / pulseDuration;
            float scalePunch = 1f + Mathf.Sin(p * Mathf.PI) * pulseScaleMultiplier;
            t.localScale = baseScale * scalePunch;
            text.color = Color.Lerp(flashColor, original, p);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            t.localScale = baseScale;
            text.color = original;
        }
    }
}
