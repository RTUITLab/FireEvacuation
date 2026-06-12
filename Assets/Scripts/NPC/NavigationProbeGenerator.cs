using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class NavigationProbeGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] [Min(0.25f)] private float probeSpacing = 1f;
    [SerializeField] [Min(0.25f)] private float verticalProbeSpacing = 2f;
    [SerializeField] private bool useAttachedBoxCollider = true;
    [SerializeField] private Vector3 manualBoundsSize = new Vector3(10f, 2f, 10f);
    [SerializeField] [Min(0.1f)] private float sampleMaxDistance = 1.5f;

    [Header("Probe Setup")]
    [SerializeField] private NavigationProbePoint probePrefab;
    [SerializeField] private Transform generatedRoot;

    [Header("Danger Update")]
    [SerializeField] [Range(0f, 1f)] private float smokeDangerWeight = 1f;
    [SerializeField] [Min(1f)] private float fireBlockRadiusMultiplier = 1f;
    [SerializeField] [Min(1f)] private float fireDangerRadiusMultiplier = 2f;

    public float ProbeSpacing => probeSpacing;

    private void Reset()
    {
        EnsureGeneratedRoot();
    }

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        RegenerateProbes();
    }

    private void OnValidate()
    {
        probeSpacing = Mathf.Max(0.25f, probeSpacing);
        verticalProbeSpacing = Mathf.Max(0.25f, verticalProbeSpacing);
        sampleMaxDistance = Mathf.Max(0.1f, sampleMaxDistance);
        manualBoundsSize.x = Mathf.Max(0.1f, manualBoundsSize.x);
        manualBoundsSize.y = Mathf.Max(0.1f, manualBoundsSize.y);
        manualBoundsSize.z = Mathf.Max(0.1f, manualBoundsSize.z);
    }

    [ContextMenu("Regenerate Probes")]
    public void RegenerateProbes()
    {
        EnsureGeneratedRoot();
        ClearGeneratedProbes();

        var generationBounds = ResolveGenerationBounds();
        var createdCount = 0;
        var usedPositions = new HashSet<string>();

        for (var x = generationBounds.min.x; x <= generationBounds.max.x; x += probeSpacing)
        {
            for (var y = generationBounds.min.y; y <= generationBounds.max.y; y += verticalProbeSpacing)
            {
                for (var z = generationBounds.min.z; z <= generationBounds.max.z; z += probeSpacing)
                {
                    var samplePosition = new Vector3(x, y, z);
                    if (!NavMesh.SamplePosition(samplePosition, out var hit, sampleMaxDistance, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    var positionKey = BuildPositionKey(hit.position);
                    if (!usedPositions.Add(positionKey))
                    {
                        continue;
                    }

                    // Проверяем точки по всей высоте объёма, чтобы многоэтажный NavMesh тоже получал пробы.
                    CreateProbe(hit.position, createdCount);
                    createdCount++;
                }
            }
        }

        RecalculateProbeDanger();
        ExitProximityCalculator.RecalculateSceneExitProximity();
        Debug.Log($"NavigationProbeGenerator '{name}' generated {createdCount} probes.", this);
    }

    [ContextMenu("Recalculate Probe Danger")]
    public void RecalculateProbeDanger()
    {
        var probePoints = generatedRoot != null
            ? generatedRoot.GetComponentsInChildren<NavigationProbePoint>(true)
            : FindObjectsByType<NavigationProbePoint>(FindObjectsInactive.Include);
        var smokeZones = FindObjectsByType<SmokeZone>(FindObjectsInactive.Include);
        var fireSources = FindObjectsByType<FireSource>(FindObjectsInactive.Include);

        for (var probeIndex = 0; probeIndex < probePoints.Length; probeIndex++)
        {
            var probePoint = probePoints[probeIndex];
            if (probePoint == null)
            {
                continue;
            }

            var pointPosition = probePoint.transform.position;
            var pointDanger = CalculateSmokeDanger(pointPosition, smokeZones) + CalculateFireDanger(pointPosition, fireSources, out var isBlocked);

            // Опасность суммируется из дыма и огня, а затем ограничивается диапазоном 0..1.
            probePoint.SetDangerState(pointDanger, isBlocked);
        }
    }

    private Bounds ResolveGenerationBounds()
    {
        if (useAttachedBoxCollider && TryGetComponent<BoxCollider>(out var boxCollider))
        {
            return new Bounds(transform.TransformPoint(boxCollider.center), Vector3.Scale(boxCollider.size, transform.lossyScale));
        }

        return new Bounds(transform.position, manualBoundsSize);
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        var rootObject = new GameObject("GeneratedProbes");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
    }

    private void ClearGeneratedProbes()
    {
        if (generatedRoot == null)
        {
            return;
        }

        for (var childIndex = generatedRoot.childCount - 1; childIndex >= 0; childIndex--)
        {
            var child = generatedRoot.GetChild(childIndex);
            if (child == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
                continue;
            }

            DestroyImmediate(child.gameObject);
        }
    }

    private void CreateProbe(Vector3 position, int probeIndex)
    {
        NavigationProbePoint probeInstance;

        if (probePrefab != null)
        {
            probeInstance = Instantiate(probePrefab, position, Quaternion.identity, generatedRoot);
        }
        else
        {
            var probeObject = new GameObject($"Probe_{probeIndex:D3}");
            probeObject.transform.SetParent(generatedRoot, false);
            probeObject.transform.position = position;
            probeInstance = probeObject.AddComponent<NavigationProbePoint>();
        }

        if (probeInstance == null)
        {
            return;
        }

        probeInstance.transform.position = position;
        probeInstance.name = $"Probe_{probeIndex:D3}";
    }

    private static string BuildPositionKey(Vector3 position)
    {
        return $"{position.x:F2}_{position.y:F2}_{position.z:F2}";
    }

    private float CalculateSmokeDanger(Vector3 pointPosition, SmokeZone[] smokeZones)
    {
        var danger = 0f;

        for (var smokeIndex = 0; smokeIndex < smokeZones.Length; smokeIndex++)
        {
            var smokeZone = smokeZones[smokeIndex];
            if (smokeZone == null)
            {
                continue;
            }

            if (!TryGetZoneCollider(smokeZone.gameObject, out var zoneCollider))
            {
                continue;
            }

            if (!IsPointInsideCollider(zoneCollider, pointPosition))
            {
                continue;
            }

            danger += smokeZone.GetSmokeLevel() * smokeDangerWeight;
        }

        return Mathf.Clamp01(danger);
    }

    private float CalculateFireDanger(Vector3 pointPosition, FireSource[] fireSources, out bool isBlocked)
    {
        var danger = 0f;
        isBlocked = false;

        for (var fireIndex = 0; fireIndex < fireSources.Length; fireIndex++)
        {
            var fireSource = fireSources[fireIndex];
            if (fireSource == null || !fireSource.IsBurning())
            {
                continue;
            }

            var distanceToFire = Vector3.Distance(pointPosition, fireSource.transform.position);
            var blockRadius = fireSource.GetFireRadius() * fireBlockRadiusMultiplier;
            var dangerRadius = fireSource.GetFireRadius() * fireDangerRadiusMultiplier;

            if (distanceToFire <= blockRadius)
            {
                isBlocked = true;
            }

            if (distanceToFire > dangerRadius || Mathf.Approximately(dangerRadius, blockRadius))
            {
                continue;
            }

            // Внешний радиус огня добавляет опасность, но не обязан блокировать точку.
            var normalizedDanger = 1f - Mathf.InverseLerp(blockRadius, dangerRadius, distanceToFire);
            danger += normalizedDanger * fireSource.GetPathDangerContribution();
        }

        return Mathf.Clamp01(danger);
    }

    private static bool TryGetZoneCollider(GameObject zoneObject, out Collider zoneCollider)
    {
        zoneCollider = null;
        if (zoneObject == null)
        {
            return false;
        }

        zoneCollider = zoneObject.GetComponent<Collider>();
        return zoneCollider != null;
    }

    private static bool IsPointInsideCollider(Collider zoneCollider, Vector3 pointPosition)
    {
        if (zoneCollider == null)
        {
            return false;
        }

        var closestPoint = zoneCollider.ClosestPoint(pointPosition);
        return (closestPoint - pointPosition).sqrMagnitude <= 0.0001f;
    }
}
