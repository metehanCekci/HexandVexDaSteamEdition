using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class SlipperySecretionPerk : BasePerk
{
    [HideInInspector] public HashSet<Vector3Int> mucusCells = new HashSet<Vector3Int>();
    private List<Vector3Int> trailHistory = new List<Vector3Int>();
    private Dictionary<Vector3Int, GameObject> mucusVisuals = new Dictionary<Vector3Int, GameObject>();
    private Vector3Int lastTrackedCell;
    private bool initialized = false;
    private bool isEquipped = false;

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Common;
    }

    public override void OnAcquire()
    {
        isEquipped = true;
        InitTracking();
    }

    public override void OnEquip()
    {
        isEquipped = true;
        InitTracking();
    }

    public override void OnUnequip()
    {
        isEquipped = false;
        ClearAllVisuals();
        mucusCells.Clear();
        trailHistory.Clear();
        initialized = false;
    }

    public override void OnLevelStart()
    {
        ClearAllVisuals();
        mucusCells.Clear();
        trailHistory.Clear();
        initialized = false;
        // InitTracking burada çağrılmaz — eski level'daki pozisyonu almasın.
        // Update'teki lazy init yeni level'daki doğru pozisyonu alacak.
    }

    private void InitTracking()
    {
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            lastTrackedCell = TurnManager.instance.player.GetCurrentCellPosition();
            initialized = true;
        }
    }

    void Update()
    {
        if (!isEquipped) return;
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        // Lazy init: OnLevelStart'ta player henuz null olabilir
        if (!initialized)
        {
            InitTracking();
            if (!initialized) return;
        }

        Vector3Int currentCell = TurnManager.instance.player.GetCurrentCellPosition();
        if (currentCell != lastTrackedCell)
        {
            AddToTrail(lastTrackedCell);
            lastTrackedCell = currentCell;
        }
    }

    private void AddToTrail(Vector3Int cell)
    {
        int maxTrail = 2; // Sabit 2 karelik mucus izi

        trailHistory.Add(cell);

        while (trailHistory.Count > maxTrail)
        {
            Vector3Int oldCell = trailHistory[0];
            trailHistory.RemoveAt(0);
            mucusCells.Remove(oldCell);
            RemoveMucusVisual(oldCell);
        }

        mucusCells.Add(cell);
        CreateMucusVisual(cell);
    }

    public int GetSlideDirectionIndex(Vector3Int enteredCell, Vector3Int cameFromCell)
    {
        Vector3Int[] offsets = (cameFromCell.y % 2 != 0)
            ? EnemyMovement.evenOffsets
            : EnemyMovement.oddOffsets;

        for (int i = 0; i < 6; i++)
        {
            if (cameFromCell + offsets[i] == enteredCell)
                return i;
        }
        return -1;
    }

    public Vector3Int GetRawSlideTarget(Vector3Int enteredCell, int dirIndex)
    {
        if (dirIndex < 0) return enteredCell;
        Vector3Int[] slideOffsets = (enteredCell.y % 2 != 0)
            ? EnemyMovement.evenOffsets
            : EnemyMovement.oddOffsets;
        return enteredCell + slideOffsets[dirIndex];
    }

    public bool IsMucusCell(Vector3Int cell)
    {
        return mucusCells.Contains(cell);
    }

    // --- Bagimsiz SpriteRenderer-based gorseller ---

    private void CreateMucusVisual(Vector3Int cell)
    {
        if (mucusVisuals.ContainsKey(cell)) return;

        Tilemap groundMap = null;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            groundMap = TurnManager.instance.player.groundMap;
        if (groundMap == null) return;

        Vector3 worldPos = groundMap.GetCellCenterWorld(cell);
        worldPos.z = -0.05f;

        GameObject obj = new GameObject("MucusTrail");
        obj.transform.position = worldPos;
        obj.transform.localScale = Vector3.one * 0.8f;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.3f, 0.9f, 0.3f, 0.4f);
        sr.sortingOrder = 2;

        mucusVisuals[cell] = obj;
    }

    private void RemoveMucusVisual(Vector3Int cell)
    {
        if (mucusVisuals.TryGetValue(cell, out GameObject obj))
        {
            if (obj != null) Object.Destroy(obj);
            mucusVisuals.Remove(cell);
        }
    }

    private void ClearAllVisuals()
    {
        foreach (var kvp in mucusVisuals)
        {
            if (kvp.Value != null) Object.Destroy(kvp.Value);
        }
        mucusVisuals.Clear();
    }

    void OnDestroy()
    {
        ClearAllVisuals();
    }

    // --- Placeholder circle sprite ---

    private static Sprite cachedCircle;
    private static Sprite CreateCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha *= alpha; // Soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircle;
    }
}
