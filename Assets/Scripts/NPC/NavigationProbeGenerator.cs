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

        ExitProximityCalculator.RecalculateSceneExitProximity();
        Debug.Log($"NavigationProbeGenerator '{name}' generated {createdCount} probes.", this);
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
}
