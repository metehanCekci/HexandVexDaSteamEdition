using UnityEngine;

public class NeuralRebootPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "low", GameKeywords.Counter("3") },
        { "reroll", GameKeywords.Action("rerolled") }
    };

    public override void OnAcquire()
    {
        base.OnAcquire();
    }

    // 3 veya altÄ± gelen her zarÄ±, 3'Ã¼n Ã¼stÃ¼ gelene kadar tekrar tekrar atar
    public override void ModifyCombat(CombatPayload payload)
    {
        int delta = 0;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            if (payload.diceRolls[i] <= 3)
            {
                int oldVal = payload.diceRolls[i];
                int safety = 0;
                while (payload.diceRolls[i] <= 3 && safety < 100)
                {
                    payload.diceRolls[i] = Random.Range(1, 7);
                    safety++;
                }
                delta += payload.diceRolls[i] - oldVal;
                Debug.Log($"NeuralReboot: Zar {i + 1} yeniden atildi: {oldVal} -> {payload.diceRolls[i]}");
            }
        }
        payload.ApplyAdd(delta);
    }
}
