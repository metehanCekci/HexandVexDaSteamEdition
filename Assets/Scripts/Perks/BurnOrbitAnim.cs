using UnityEngine;

/// <summary>
/// 3 child objeyi parent etrafinda orbit halinde dondurur.
/// PyrogenicGlands tarafindan burn efekti icin kullanilir.
/// </summary>
public class BurnOrbitAnim : MonoBehaviour
{
    public float orbitRadius = 0.35f;
    public float orbitSpeed = 180f; // derece/saniye

    private float angle;

    void Update()
    {
        angle += orbitSpeed * Time.deltaTime;
        if (angle > 360f) angle -= 360f;

        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            float childAngle = (angle + i * (360f / childCount)) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(childAngle) * orbitRadius,
                Mathf.Sin(childAngle) * orbitRadius,
                -0.05f
            );
            transform.GetChild(i).localPosition = pos;
        }
    }
}
