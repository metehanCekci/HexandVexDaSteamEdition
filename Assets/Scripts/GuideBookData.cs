using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GuideBookPage
{
    public string title;
    [TextArea(4, 12)] public string bodyText;
    public Sprite illustration; // opsiyonel — null olabilir
    public string category;     // "Movement", "Combat", "Enemies", "Items", "Perks"
    public string subCategory;  // opsiyonel alt başlık — ör: "Hazards", "Bestiary"
}

[CreateAssetMenu(menuName = "HexAndVex/GuideBook Data")]
public class GuideBookData : ScriptableObject
{
    public List<GuideBookPage> pages = new List<GuideBookPage>();
}
