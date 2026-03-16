using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tüm silah verilerini tutan statik erişimli sınıf.
/// Inspector'dan WeaponData asset'lerini weapons listesine ekle.
/// </summary>
public class WeaponDatabase : MonoBehaviour
{
    public static WeaponDatabase instance;

    public List<WeaponData> weapons = new List<WeaponData>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// WeaponType'a göre WeaponData döndürür.
    /// </summary>
    public WeaponData GetWeapon(WeaponType type)
    {
        foreach (var w in weapons)
            if (w != null && w.weaponType == type) return w;
        return null;
    }

    /// <summary>
    /// Şu an seçili silahın verisini döndürür.
    /// </summary>
    public WeaponData GetCurrentWeapon()
    {
        if (RunManager.instance == null) return null;
        return GetWeapon(RunManager.instance.selectedWeapon);
    }
}
