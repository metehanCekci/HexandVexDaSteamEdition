using UnityEngine;

/// <summary>
/// Totem düşman AI'ı. Hareket etmez, saldırmaz.
/// Ölünce SpawnerBossAI'a bildirir.
/// </summary>
public class TotemEnemyAI : MonoBehaviour
{
    private EnemyMovement movement;

    void Start()
    {
        movement = GetComponent<EnemyMovement>();
    }

    public void OnTotemDeath()
    {
        if (SpawnerBossAI.instance != null)
            SpawnerBossAI.instance.OnTotemDestroyed();
    }
}
