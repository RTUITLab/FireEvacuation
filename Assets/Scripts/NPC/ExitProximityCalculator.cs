using UnityEngine;

public static class ExitProximityCalculator
{
    public static void RecalculateSceneExitProximity()
    {
        var probePoints = Object.FindObjectsByType<NavigationProbePoint>(FindObjectsInactive.Include);
        var exitPoints = Object.FindObjectsByType<ExitPoint>(FindObjectsInactive.Include);
        RecalculateExitProximity(probePoints, exitPoints);
    }

    public static void RecalculateExitProximity(NavigationProbePoint[] probePoints, ExitPoint[] exitPoints)
    {
        if (probePoints == null || probePoints.Length == 0)
        {
            return;
        }

        if (exitPoints == null || exitPoints.Length == 0)
        {
            Debug.LogWarning("ExitProximityCalculator: no ExitPoint found in scene. ExitProximity is set to 0 for all probe points.");

            for (var index = 0; index < probePoints.Length; index++)
            {
                var probePoint = probePoints[index];
                if (probePoint == null)
                {
                    continue;
                }

                probePoint.SetExitProximity(0f);
            }

            return;
        }

        var minDistance = float.MaxValue;
        var maxDistance = 0f;
        var cachedDistances = new float[probePoints.Length];

        for (var probeIndex = 0; probeIndex < probePoints.Length; probeIndex++)
        {
            var probePoint = probePoints[probeIndex];
            if (probePoint == null)
            {
                cachedDistances[probeIndex] = float.PositiveInfinity;
                continue;
            }

            var nearestExitDistance = GetNearestExitDistance(probePoint.transform.position, exitPoints);
            cachedDistances[probeIndex] = nearestExitDistance;
            minDistance = Mathf.Min(minDistance, nearestExitDistance);
            maxDistance = Mathf.Max(maxDistance, nearestExitDistance);
        }

        for (var probeIndex = 0; probeIndex < probePoints.Length; probeIndex++)
        {
            var probePoint = probePoints[probeIndex];
            if (probePoint == null)
            {
                continue;
            }

            if (float.IsPositiveInfinity(cachedDistances[probeIndex]))
            {
                probePoint.SetExitProximity(0f);
                continue;
            }

            if (Mathf.Approximately(maxDistance, minDistance))
            {
                probePoint.SetExitProximity(1f);
                continue;
            }

            // Чем ближе точка к выходу, тем ближе ExitProximity к 1; дальние точки стремятся к 0.
            var normalizedDistance = Mathf.InverseLerp(minDistance, maxDistance, cachedDistances[probeIndex]);
            probePoint.SetExitProximity(1f - normalizedDistance);
        }
    }

    private static float GetNearestExitDistance(Vector3 probePosition, ExitPoint[] exitPoints)
    {
        var nearestDistance = float.MaxValue;

        for (var exitIndex = 0; exitIndex < exitPoints.Length; exitIndex++)
        {
            var exitPoint = exitPoints[exitIndex];
            if (exitPoint == null)
            {
                continue;
            }

            var currentDistance = Vector3.Distance(probePosition, exitPoint.transform.position);
            nearestDistance = Mathf.Min(nearestDistance, currentDistance);
        }

        return nearestDistance;
    }
}
