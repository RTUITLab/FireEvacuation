using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HazardZone))]
public class SmokeZone : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float smokeLevel;
    [SerializeField] [Range(0f, 1f)] private float ventilationRate;
    [SerializeField] private float smokeDamageRate;
    [SerializeField] private float smokePanicGain;
    [SerializeField] [Range(0f, 1f)] private float smokeSignalReduction = 0.5f;
    [SerializeField] [Min(0f)] private float baseVisibility = 10f;
    [SerializeField] [Min(0f)] private float connectionFlowRate = 0.25f;
    [SerializeField] private List<SmokeZone> connectedZones = new List<SmokeZone>();

    private HazardZone hazardZone;

    private void Reset()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        SyncHazardZone();
    }

    private void Awake()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        SyncHazardZone();
    }

    private void OnValidate()
    {
        ResolveHazardZone();
        ClampSerializedValues();
        RemoveInvalidConnections();
        SyncHazardZone();
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        smokeLevel -= ventilationRate * deltaTime;
        smokeLevel = Mathf.Clamp01(smokeLevel);

        TransferSmokeToConnectedZones(deltaTime);
        SyncHazardZone();
    }

    public void AddSmoke(float amount)
    {
        smokeLevel = Mathf.Clamp01(smokeLevel + amount);
        SyncHazardZone();
    }

    public float GetSmokeLevel()
    {
        return smokeLevel;
    }

    public float GetEffectiveVisibility()
    {
        return baseVisibility * (1f - smokeLevel);
    }

    public float GetSmokeSignalMultiplier()
    {
        return Mathf.Clamp01(1f - smokeLevel * smokeSignalReduction);
    }

    public float GetSmokeDamageRate()
    {
        return smokeDamageRate;
    }

    public float GetSmokePanicGain()
    {
        return smokePanicGain;
    }

    private void TransferSmokeToConnectedZones(float deltaTime)
    {
        for (var index = 0; index < connectedZones.Count; index++)
        {
            var connectedZone = connectedZones[index];
            if (connectedZone == null || connectedZone == this)
            {
                continue;
            }

            if (GetInstanceID() > connectedZone.GetInstanceID() && connectedZone.HasConnectionTo(this))
            {
                continue;
            }

            var smokeFlow = (smokeLevel - connectedZone.smokeLevel) * connectionFlowRate * deltaTime;
            if (Mathf.Approximately(smokeFlow, 0f))
            {
                continue;
            }

            smokeLevel = Mathf.Clamp01(smokeLevel - smokeFlow);
            connectedZone.smokeLevel = Mathf.Clamp01(connectedZone.smokeLevel + smokeFlow);
            connectedZone.SyncHazardZone();
        }
    }

    private void ResolveHazardZone()
    {
        hazardZone = GetComponent<HazardZone>();
    }

    private void ClampSerializedValues()
    {
        smokeLevel = Mathf.Clamp01(smokeLevel);
        ventilationRate = Mathf.Clamp01(ventilationRate);
        smokeSignalReduction = Mathf.Clamp01(smokeSignalReduction);
        baseVisibility = Mathf.Max(0f, baseVisibility);
        connectionFlowRate = Mathf.Max(0f, connectionFlowRate);
    }

    private void RemoveInvalidConnections()
    {
        for (var index = connectedZones.Count - 1; index >= 0; index--)
        {
            if (connectedZones[index] == null || connectedZones[index] == this)
            {
                connectedZones.RemoveAt(index);
            }
        }
    }

    private bool HasConnectionTo(SmokeZone otherZone)
    {
        for (var index = 0; index < connectedZones.Count; index++)
        {
            if (connectedZones[index] == otherZone)
            {
                return true;
            }
        }

        return false;
    }

    private void SyncHazardZone()
    {
        if (hazardZone == null)
        {
            return;
        }

        hazardZone.SetHazardType(HazardZone.HazardType.Smoke);
        hazardZone.SetHazardLevel(smokeLevel);
        hazardZone.SetDamageRate(smokeDamageRate);
        hazardZone.SetPanicRate(smokePanicGain);
    }
}
