/// <summary>
/// Büyük sayıları kısaltılmış formatta gösterir: 1.2K, 3.5M, 2.1B, 4.5T vb.
/// </summary>
public static class NumberFormatter
{
    private static readonly string[] suffixes = { "", "K", "M", "B", "T", "Q", "Qi", "Sx", "Sp", "Oc" };

    public static string Format(long value)
    {
        if (value < 0) return "-" + Format(-value);
        if (value < 10_000) return value.ToString();

        int tier = 0;
        double v = value;
        while (v >= 1000 && tier < suffixes.Length - 1)
        {
            v /= 1000;
            tier++;
        }

        // 1 decimal: "2.1B", ama tam sayıysa ondalık gösterme: "3K"
        if (v >= 100) return ((int)v).ToString() + suffixes[tier];
        if (v >= 10)  return v.ToString("F1") + suffixes[tier];
        return v.ToString("F2") + suffixes[tier];
    }
}
