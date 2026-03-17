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
            goldText.text = amount.ToString();
            if (goldPulseCoroutine != null) StopCoroutine(goldPulseCoroutine);
            goldPulseCoroutine = StartCoroutine(PulseText(goldText, goldPulseColor));
        }
    }

    public void Refresh()
    {
        if (RunManager.instance == null) return;
        if (goldText != null) goldText.text = RunManager.instance.currentGold.ToString();
        if (hpText != null)
            hpText.text = RunManager.instance.playerCurrentHealth + "/" + RunManager.instance.playerMaxHealth;
        lastHP = RunManager.instance.playerCurrentHealth;
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
