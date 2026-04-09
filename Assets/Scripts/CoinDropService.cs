using UnityEngine;

public class CoinDropService
{
    public int CalculateAndAwardCoins(EnemyMovement enemy)
    {
        if (enemy.IsTotem) return 0;

        RunManager rm = RunManager.instance;
        bool isBossRoom = rm.currentNodeType == MapNodeType.Boss;
        int coinDrop = 0;

        if (isBossRoom)
        {
            if (enemy.IsBoss)
                coinDrop = 20;
            else
                coinDrop = 0; // Boss odasında sadece boss gold verir
        }
        else
        {
            coinDrop = Random.Range(2, 6) + rm.bonusGold;
            if (enemy.isElite) coinDrop *= 2;
            if (rm.doubleGoldNextKill)
            {
                coinDrop *= 2;
                rm.doubleGoldNextKill = false;
            }
        }

        if (coinDrop > 0)
        {
            rm.currentGold += coinDrop;
            rm.totalGoldEarned += coinDrop;
            GameEvents.GoldChanged(rm.currentGold);
            if (CoinDropVFX.instance != null)
                CoinDropVFX.instance.SpawnCoins(enemy.transform.position, coinDrop);

            // Collection sistemi — lifetime toplam gold (run'lar arası birikim)
            int lifetimeGold = PlayerPrefs.GetInt("lifetime_total_gold", 0) + coinDrop;
            PlayerPrefs.SetInt("lifetime_total_gold", lifetimeGold);
            GameEvents.TotalGoldLifetime(lifetimeGold);
        }

        return coinDrop;
    }

    public void ProcessKillRewards(EnemyMovement deadEnemy)
    {
        CalculateAndAwardCoins(deadEnemy);
        foreach (var p in RunManager.instance.activePerks)
            p.OnEnemyKilled(deadEnemy);
        RunManager.instance.totalEnemiesKilled++;

        // Collection sistemi — lifetime toplam kill (run'lar arası birikim)
        int lifetimeKills = PlayerPrefs.GetInt("lifetime_total_kills", 0) + 1;
        PlayerPrefs.SetInt("lifetime_total_kills", lifetimeKills);
        GameEvents.EnemyKilledTotal(lifetimeKills);

        // Boss öncesi kill sayacı (boss node'u değilse say)
        if (RunManager.instance.currentNodeType != MapNodeType.Boss)
        {
            int preBossKills = PlayerPrefs.GetInt("kills_before_boss", 0) + 1;
            PlayerPrefs.SetInt("kills_before_boss", preBossKills);
            GameEvents.KillsBeforeBossUpdated(preBossKills);
        }
    }
}
