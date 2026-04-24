/// <summary>
/// Buyuk sayilari gosterir. Tasarim: son ana kadar (long.MaxValue'ya yakin)
/// HAM sayi yaz — oyuncu 10-20 basamakli sayilari acik gorsun. Ancak sayi
/// gercekten long sinirlarini zorlayacak kadar buyurse (~18+ basamak) K/M/B/T suffix'i
/// devreye gir. Clamp runningDamage double -> long donusumunde yapiliyor (CombatPayload.GetFinalDamage).
/// </summary>
public static class NumberFormatter
{
    private static readonly string[] suffixes = { "", "K", "M", "B", "T", "Q", "Qi", "Sx", "Sp", "Oc" };

    // Suffix'e gecis esigi: 10^17. long.MaxValue ~= 9.22 * 10^18 (19 basamak).
    // Bu esigin altinda HAM sayi yazilir (en fazla 17 basamak), ustunde K/M/B/T/Q... suffix.
    private const long FULL_DIGIT_THRESHOLD = 100_000_000_000_000_000L; // 10^17

    public static string Format(long value)
    {
        if (value < 0) return "-" + Format(-value);
        if (value < FULL_DIGIT_THRESHOLD) return value.ToString();

        int tier = 0;
        double v = value;
        while (v >= 1000 && tier < suffixes.Length - 1)
        {
            v /= 1000;
            tier++;
        }

        if (v >= 100) return ((int)v).ToString() + suffixes[tier];
        if (v >= 10)  return v.ToString("F1") + suffixes[tier];
        return v.ToString("F2") + suffixes[tier];
    }
}
