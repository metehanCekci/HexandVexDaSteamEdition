using UnityEngine;

/// <summary>
/// Layer 2'ye özel düşman AI stub'ı.
/// Şu an için MeleeEnemyAI mantığını devralır.
/// İleride Layer 2'ye özgü mekanikler (örn. teleport farkındalığı, patlayan tile çevresinden kaçma) eklenecek.
/// </summary>
public class Layer2EnemyAI : MonoBehaviour
{
    private EnemyMovement movement;
    private EnemyVisuals visuals;
    private MeleeEnemyAI meleeDelegate;

    void Start()
    {
        movement = GetComponent<EnemyMovement>();
        visuals = GetComponent<EnemyVisuals>();
        meleeDelegate = GetComponent<MeleeEnemyAI>();
    }

    /// <summary>
    /// Intent hesapla. Şimdilik melee delegate'e yönlendir.
    /// </summary>
    public void LockNextMove(Vector3Int playerCell, bool isStunned)
    {
        if (meleeDelegate != null)
            meleeDelegate.LockNextMove(playerCell, isStunned);
    }
}
