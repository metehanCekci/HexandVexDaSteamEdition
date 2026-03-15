using UnityEngine;

/// <summary>
/// Dummy dealer NPC placed at the center of the shop arena.
/// When the player steps on this tile, the shop canvas opens.
/// Spawned by LevelGenerator.GenerateShopArena() — no prefab needed.
/// </summary>
public class ShopDealer : MonoBehaviour
{
    public static ShopDealer instance;

    /// <summary>
    /// The hex cell this dealer occupies (set by LevelGenerator).
    /// </summary>
    [HideInInspector] public Vector3Int dealerCell;

    private bool shopOpened = false;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        instance = this;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerMoved += OnPlayerMoved;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerMoved -= OnPlayerMoved;
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Idle bounce animation
        StartCoroutine(IdleBounce());
    }

    private void OnPlayerMoved(Vector3Int playerCell)
    {
        if (shopOpened) return;

        if (playerCell == dealerCell)
        {
            shopOpened = true;

            if (Shopmanager.instance != null)
                Shopmanager.instance.OpenAsMapNode();
        }
    }

    /// <summary>
    /// Reset so the dealer can trigger the shop again (e.g., after closing shop
    /// the player might walk away and come back — but for now we only open once).
    /// </summary>
    public void ResetDealer()
    {
        shopOpened = false;
    }

    private System.Collections.IEnumerator IdleBounce()
    {
        Vector3 basePos = transform.position;
        float amplitude = 0.05f;
        float speed = 2f;

        while (true)
        {
            float offset = Mathf.Sin(Time.time * speed) * amplitude;
            transform.position = basePos + new Vector3(0f, offset, 0f);
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
