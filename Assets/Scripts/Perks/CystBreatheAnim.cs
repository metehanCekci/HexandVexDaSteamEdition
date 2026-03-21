using UnityEngine;

/// <summary>
/// Marker objesine breathe (nefes alma) animasyonu verir.
/// Scale surekli kucuk-buyuk pulse yapar.
/// ViralCysts tarafindan mark efekti icin kullanilir.
/// </summary>
public class CystBreatheAnim : MonoBehaviour
{
    public float baseScale = 0.3f;
    public float breatheAmount = 0.08f;
    public float breatheSpeed = 2.5f;

    private float phase;

    void Start()
    {
        // Her marker farkli fazda baslasin — uniform gorulmesin
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // breatheAmount baseScale'e orantili
        float amount = baseScale * 0.25f;
        float s = baseScale + Mathf.Sin(Time.time * breatheSpeed + phase) * amount;
        transform.localScale = Vector3.one * s;
    }
}
