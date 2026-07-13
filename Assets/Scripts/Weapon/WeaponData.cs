using UnityEngine;

/// <summary>
/// Bir silahın tüm verilerini tutan ScriptableObject.
/// Assets > Create > Weapon Data ile yeni silah oluşturulabilir.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapon Data")]
public class WeaponData : ScriptableObject
{
    public WeaponType weaponType;
    public string weaponName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Saldırı Ayarları")]
    [Tooltip("true ise haritadaki herhangi bir düşmana tıklayarak saldırılabilir.")]
    public bool canAttackAnyRange = false;

    [Tooltip("Bu mesafe ve üzeri hex uzaklıkta saldırırsa zar cezası uygulanır.")]
    public int penaltyRangeThreshold = 3;

    [Tooltip("Ceza mesafesinde atılacak base zar sayısı (normal = 2, cezalı = 1 vb.)")]
    public int penaltyDiceCount = 1;
}
