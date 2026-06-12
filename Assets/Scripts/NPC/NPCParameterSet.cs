using System;
using UnityEngine;

[Serializable]
public class NPCParameterSet
{
    [Range(0f, 1f)] public float MoveSpeed = 0.5f;
    [Range(0f, 1f)] public float CommandReactionDelay = 0.5f;
    [Range(0f, 1f)] public float FollowDistance = 0.5f;
    [Range(0f, 1f)] public float Weight = 0.5f;
    [Range(0f, 1f)] public float MobilityLimit = 0.5f;
    [Range(0f, 1f)] public float DangerAvoidance = 0.5f;
    [Range(0f, 1f)] public float TrustToRescuer = 0.5f;
    [Range(0f, 1f)] public float SpatialOrientation = 0.5f;
    [Range(0f, 1f)] public float SignalPower = 0.5f;
    [Range(0f, 1f)] public float SignalFrequency = 0.5f;
    [Range(0f, 1f)] public float Fearfulness = 0.5f;
    [Range(0f, 1f)] public float ChaoticBehaviorDuration = 0.5f;
    [Range(0f, 1f)] public float HidingTendency = 0f;
    [Range(0f, 1f)] public float BarricadeTendency = 0f;

    public void ClampAll()
    {
        // Все постоянные параметры NPC должны оставаться в диапазоне 0..1.
        MoveSpeed = Mathf.Clamp01(MoveSpeed);
        CommandReactionDelay = Mathf.Clamp01(CommandReactionDelay);
        FollowDistance = Mathf.Clamp01(FollowDistance);
        Weight = Mathf.Clamp01(Weight);
        MobilityLimit = Mathf.Clamp01(MobilityLimit);
        DangerAvoidance = Mathf.Clamp01(DangerAvoidance);
        TrustToRescuer = Mathf.Clamp01(TrustToRescuer);
        SpatialOrientation = Mathf.Clamp01(SpatialOrientation);
        SignalPower = Mathf.Clamp01(SignalPower);
        SignalFrequency = Mathf.Clamp01(SignalFrequency);
        Fearfulness = Mathf.Clamp01(Fearfulness);
        ChaoticBehaviorDuration = Mathf.Clamp01(ChaoticBehaviorDuration);
        HidingTendency = Mathf.Clamp01(HidingTendency);
        BarricadeTendency = Mathf.Clamp01(BarricadeTendency);
    }
}
