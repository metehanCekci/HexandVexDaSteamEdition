using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// Oyuncu ve düşman için ortak sağlık sistemi.
/// Hasar, iyileştirme, yumuşak renk geçişleri (Damage Flash), yumuşak saydamlaşma ve ölüm animasyonunu yönetir.
/// </summary>
public class HealthScript : MonoBehaviour
{    public static HitstopManager hitstopManager;
    [Header("HP Settings")]
    public long maxHP = 3;
    public long currentHP;

    public System.Action OnDeath;
    public System.Action<long> OnDamaged;

    public TMP_Text hptext;

    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;
    private Coroutine alphaFadeCoroutine; // YENİ: Saydamlığın yavaşça değişmesini sağlayan animasyon
    private bool isDead = false;
    public bool IsDead => isDead;

    private bool isDeepStunnedAlpha = false;
    [Header("VFX")]
    public GameObject damageTextPrefab; // Hazırladığın prefabı buraya sürükle

    public GameObject deathMenuUI; // Ölme ekranını buraya da sürükle
    void Start()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        if (gameObject.CompareTag("Player"))
        {
            if (RunManager.instance != null)
            {
                maxHP = RunManager.instance.playerMaxHealth;
                currentHP = RunManager.instance.playerCurrentHealth;
            }
        }
        else
        {
            currentHP = maxHP;
        }
        updateHealth();
    }

    void Update()
    {
        if (!isDead) return;
        if (!gameObject.CompareTag("Player")) return;
        if (Input.GetKeyDown(KeyCode.F3))
            CheatRevive();
    }

    private void CheatRevive()
    {
        isDead = false;
        currentHP = maxHP;
        if (RunManager.instance != null)
        {
            RunManager.instance.playerCurrentHealth = maxHP;
        }
        updateHealth();

        if (hptext != null) hptext.gameObject.SetActive(true);

        if (deathMenuUI != null) deathMenuUI.SetActive(false);
        Time.timeScale = 1f;

        if (spriteRenderer != null)
        {
            originalColor = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
            spriteRenderer.color = originalColor;
        }
    }

    public void TakeDamage(long dmg, bool applyHitstop = false, bool applyStun = true)
    {
        if (isDead) return;

        // Boss kalkanı varken hasar vurulmaz
        EnemyMovement enemyAI = GetComponentInParent<EnemyMovement>();
        if (enemyAI != null && enemyAI.IsBoss)
        {
            var boss = enemyAI.GetComponent<SpawnerBossAI>();
            if (boss != null && boss.isShielded)
            {
                Debug.Log("Boss kalkanı aktif - Hasar verilmedi!");
                return;
            }
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayTakeDamage();
        currentHP -= dmg;

        // Impact anında hafif camera shake
        CameraController.ShakeLight();
        
        // Hitstop uygula
        if (applyHitstop && HitstopManager.instance != null)
            HitstopManager.instance.TriggerHitstop();

        if (gameObject.CompareTag("Player"))
        {
            RunManager.instance.totalDamageReceived += dmg; // Alınan hasarı kaydet
            RunManager.instance.playerCurrentHealth = currentHP; // Senkronize et
        }
        
        if (damageTextPrefab != null)
        {
            // Sayıyı tam düşmanın merkezinde oluştur
            GameObject dmgObj = Instantiate(damageTextPrefab, transform.position, Quaternion.identity);

            // Setup fonksiyonunu çağırarak içindeki rakamı yazdır
            dmgObj.GetComponent<DamageNumber>().Setup(dmg);
        }

        EnemyMovement enemy = GetComponentInParent<EnemyMovement>();
        if (applyStun && enemy != null && !enemy.IsBoss && !enemy.IsTotem)
        {
            enemy.ApplyStun(1, false);
        }

        OnDamaged?.Invoke(currentHP);
        updateHealth();

        if (currentHP <= 0)
        {
            Die();
        }
        else
        {
            if (spriteRenderer != null && gameObject.activeInHierarchy)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(DamageFlash());
            }
        }
    }

    private IEnumerator DamageFlash()
    {
        spriteRenderer.color = Color.red;

        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // originalColor'ın alpha'sı o sırada değişiyor olsa bile sorunsuz takip eder
            spriteRenderer.color = Color.Lerp(Color.red, originalColor, elapsed / duration);
            yield return null;
        }

        spriteRenderer.color = originalColor;
        flashCoroutine = null;
    }

    // --- YENİ: YUMUŞAK SAYDAMLAŞMA (FADE IN / FADE OUT) ---
    public void SetStunnedAlpha(bool deepStun)
    {
        if (spriteRenderer == null || isDead) return;

        // Eğer zaten ayarlandıysa ve aynı statüdeyse çık
        if (isDeepStunnedAlpha == deepStun) return;

        isDeepStunnedAlpha = deepStun;
        float targetAlpha = deepStun ? 0.45f : 1f;

        // Eğer halihazırda bir saydamlaşma animasyonu varsa durdur, yenisini başlat
        if (alphaFadeCoroutine != null) StopCoroutine(alphaFadeCoroutine);
        alphaFadeCoroutine = StartCoroutine(FadeAlphaCoroutine(targetAlpha, deepStun));
    }

    private IEnumerator FadeAlphaCoroutine(float targetAlpha, bool fadingIn)
    {
        float startAlpha = originalColor.a;
        float duration = fadingIn ? 0.3f : 0.7f; // Saydamlaşma hızlı, belirginleşme yavaş
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);

            // Orijinal rengin hafızasını güncelle
            originalColor = new Color(originalColor.r, originalColor.g, originalColor.b, newAlpha);

            // Eğer o an kırmızı parlama YOKSA, yeni saydamlığı direkt uygula
            // (Kırmızı parlama varsa, zaten üstteki DamageFlash bu originalColor'ı kullanıyor)
            if (flashCoroutine == null)
            {
                spriteRenderer.color = originalColor;
            }

            yield return null;
        }

        // Animasyon bitince tam değeri oturt
        originalColor = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);
        if (flashCoroutine == null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    public void SetOriginalColor(Color color)
    {
        originalColor = color;
        if (spriteRenderer != null) spriteRenderer.color = color;
    }

    /// <summary>
    /// Hasar ver ama damage text ve camera shake gösterme (scaffold düşüşü gibi sessiz ölümler için).
    /// </summary>
    public void TakeDamageSilent(long dmg)
    {
        if (isDead) return;
        currentHP -= dmg;

        if (gameObject.CompareTag("Player") && RunManager.instance != null)
        {
            RunManager.instance.totalDamageReceived += dmg;
            RunManager.instance.playerCurrentHealth = currentHP;
        }

        OnDamaged?.Invoke(currentHP);
        updateHealth();

        if (currentHP <= 0) Die();
    }

    public void Heal(long amount)
    {
        if (isDead) return;
        currentHP = System.Math.Min(currentHP + amount, maxHP);

        if (gameObject.CompareTag("Player") && RunManager.instance != null)
        {
            RunManager.instance.playerCurrentHealth = currentHP;
        }

        updateHealth();
    }

    private void Die()
    {
        if (isDead) return; // Çift ölüm çağrısını engelle
        isDead = true;
        OnDeath?.Invoke();

        if (hptext != null) hptext.gameObject.SetActive(false);

        if (gameObject.CompareTag("Player"))
        {
            if (RunManager.instance != null) RunManager.instance.SaveBestRun();
            GameEvents.RunCompleted(false); // Oyuncu öldü = run kaybedildi
            if (deathMenuUI != null)
            {
                deathMenuUI.SetActive(true);
                // Ensure death canvas is on top of everything (below fade)
                Canvas deathCanvas = deathMenuUI.GetComponentInParent<Canvas>();
                if (deathCanvas == null) deathCanvas = deathMenuUI.GetComponent<Canvas>();
                if (deathCanvas != null)
                {
                    deathCanvas.overrideSorting = true;
                    deathCanvas.sortingOrder = 500;
                }
                Time.timeScale = 0f;
            }
            return;
        }

        // Scaffold: düşman scaffold üzerinde öldüyse scaffold çöksün
        if (ScaffoldManager.instance != null)
        {
            EnemyMovement enemyAI = GetComponentInParent<EnemyMovement>();
            if (enemyAI == null) enemyAI = GetComponent<EnemyMovement>();
            if (enemyAI != null && enemyAI.groundMap != null)
            {
                Vector3Int deathCell = enemyAI.GetCurrentCellPosition();
                ScaffoldManager.instance.OnEntityDied(deathCell);
            }
        }

        // Düşman: DeathAnimation ile yok et (TurnManager gold verene kadar hayatta kalır)
        Debug.Log(gameObject.name + " öldü.");
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(DeathAnimation());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator DeathAnimation()
    {
        // Animator'ı kapat ki sprite override etmesin
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        float duration = 0.4f;
        float elapsed = 0f;

        Vector3 startScale = transform.localScale;
        // Tüm SpriteRenderer'ları yakala (child'lar dahil)
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            foreach (var sr in allRenderers)
            {
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t));
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    public void updateHealth()
    {
        if (hptext != null) hptext.text = currentHP.ToString() + "/" + maxHP;

        var healthBar = GetComponent<EnemyHealthBar>();
        if (healthBar != null) healthBar.UpdateBar();
    }
}
