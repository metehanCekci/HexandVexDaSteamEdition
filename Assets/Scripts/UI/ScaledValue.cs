using System.Collections.Generic;

/// <summary>
/// Perk/item sayisal degerleri icin base + perLevel + modifier zinciri.
///
/// AMAC: "Sagdaki perki ikiye katlar" gibi joker perklerin degerleri
/// gecici olarak scale etmesini saglamak. Joker gidince deger eski
/// haline doner (base + perLevel hesabina). Level atlanirsa base degeri
/// degismez, modifier'lar calismaya devam eder.
///
/// KULLANIM:
///   var v = new ScaledValue(baseValue: 2f, perLevel: 0.5f);
///   float current = v.Get(currentLevel);          // 2 + 0.5*(level-1)
///
///   // Joker perk sagdaki perke cift etki:
///   var mod = new MultiplicativeModifier(2f);
///   v.AddModifier(mod);
///   current = v.Get(currentLevel);                 // (2 + 0.5*(lvl-1)) * 2
///
///   // Joker cikartildiginda:
///   v.RemoveModifier(mod);
///   current = v.Get(currentLevel);                 // orijinal deger
///
/// Modifier'lar eklenme sirasina gore uygulanir. Additive once, multiplicative sonra
/// tercih ediliyorsa AddModifier cagrilarini dogru sirayla yapin ya da Order alanini kullanin.
/// </summary>
public class ScaledValue
{
    public float Base;
    public float PerLevel;

    private readonly List<IValueModifier> _modifiers = new List<IValueModifier>();

    public ScaledValue(float baseValue = 0f, float perLevel = 0f)
    {
        Base = baseValue;
        PerLevel = perLevel;
    }

    public void AddModifier(IValueModifier mod)
    {
        if (mod == null || _modifiers.Contains(mod)) return;
        _modifiers.Add(mod);
    }

    public void RemoveModifier(IValueModifier mod)
    {
        if (mod == null) return;
        _modifiers.Remove(mod);
    }

    public void ClearModifiers() => _modifiers.Clear();

    /// <summary>Mevcut efektif degeri hesaplar. level 1-indexli (ilk seviye = 1).</summary>
    public float Get(int level)
    {
        float v = Base + PerLevel * System.Math.Max(0, level - 1);
        for (int i = 0; i < _modifiers.Count; i++)
            v = _modifiers[i].Apply(v);
        return v;
    }

    /// <summary>Seviye 0 ile hesaplanan taban + level artisi (modifiersiz).</summary>
    public float GetRaw(int level) => Base + PerLevel * System.Math.Max(0, level - 1);
}

public interface IValueModifier
{
    float Apply(float value);
}

/// <summary>value + delta</summary>
public class AdditiveModifier : IValueModifier
{
    public float Delta;
    public AdditiveModifier(float delta) { Delta = delta; }
    public float Apply(float value) => value + Delta;
}

/// <summary>value * factor</summary>
public class MultiplicativeModifier : IValueModifier
{
    public float Factor;
    public MultiplicativeModifier(float factor) { Factor = factor; }
    public float Apply(float value) => value * Factor;
}
