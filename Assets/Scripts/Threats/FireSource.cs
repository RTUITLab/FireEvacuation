using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HazardZone))]
public class FireSource : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float fireIntensity = 1f;
    [SerializeField] [Range(0f, 1f)] private float smokeEmissionRate = 0.5f;
    [SerializeField] private bool canBeExtinguished = true;
    [SerializeField] [Min(0f)] private float fireRadius = 1f;
    [SerializeField] private float fireDamageRate = 10f;
    [SerializeField] private float firePanicGain = 1f;
    [SerializeField] private float firePathDangerWeight = 1f;
    [SerializeField] private SmokeZone linkedSmokeZone;
    [SerializeField] private GameObject fireVisual;

    private HazardZone hazardZone;

    private void Reset()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        SyncState();
    }

    private void Awake()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        SyncState();
    }

    private void OnValidate()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        SyncState();
    }

    private void Update()
    {
        if (!IsBurning())
        {
            return;
        }

        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        if (linkedSmokeZone != null)
        {
            var smokeAmount = fireIntensity * smokeEmissionRate * deltaTime;
            linkedSmokeZone.AddSmoke(smokeAmount);
        }

        SyncState();
    }

    public float GetFireIntensity()
    {
        return fireIntensity;
    }

    public float GetFireRadius()
    {
        return fireRadius;
    }

    public float GetFireDamageRate()
    {
        return IsBurning() ? fireIntensity * fireDamageRate : 0f;
    }

    public float GetBaseFireDamageRate()
    {
        return fireDamageRate;
    }

    public float GetFirePanicGain()
    {
        return IsBurning() ? fireIntensity * firePanicGain : 0f;
    }

    public float GetBaseFirePanicGain()
    {
        return firePanicGain;
    }

    public float GetPathDangerContribution()
    {
        return IsBurning() ? fireIntensity * firePathDangerWeight : 0f;
    }

    public bool IsBurning()
    {
        return fireIntensity > 0f;
    }

    public void Extinguish(float power)
    {
        if (!canBeExtinguished || power <= 0f || !IsBurning())
        {
            return;
        }

        SetFireIntensity(fireIntensity - power * Time.deltaTime);
    }

    private void ResolveHazardZone()
    {
        hazardZone = GetComponent<HazardZone>();
    }

    private void ClampSerializedValues()
    {
        fireIntensity = Mathf.Clamp01(fireIntensity);
        smokeEmissionRate = Mathf.Clamp01(smokeEmissionRate);
        fireRadius = Mathf.Max(0f, fireRadius);
    }

    private void SetFireIntensity(float value)
    {
        fireIntensity = Mathf.Clamp01(value);
        if (fireIntensity <= 0f)
        {
            fireIntensity = 0f;
        }

        SyncState();
    }

    private void SyncState()
    {
        if (hazardZone != null)
        {
            hazardZone.SetHazardType(HazardZone.HazardType.Fire);
            hazardZone.SetHazardLevel(fireIntensity);
            hazardZone.SetDamageRate(fireDamageRate);
            hazardZone.SetPanicRate(firePanicGain);
            hazardZone.SetPathDangerWeight(firePathDangerWeight);
            hazardZone.SetActiveHazard(IsBurning());
        }

        if (fireVisual != null)
        {
            fireVisual.SetActive(IsBurning());
        }
    }
}
