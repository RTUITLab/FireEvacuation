using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EvacuationZone : MonoBehaviour
{
    [SerializeField] private string zoneId = "ExitZone";
    [SerializeField] private ScenarioManager scenarioManager;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 0.3f, 0.2f);

    private readonly HashSet<string> rescuedVictimIds = new HashSet<string>();
    private Collider triggerCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveScenarioManager();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ResolveScenarioManager();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        var victim = other.GetComponentInParent<VictimNPC>();
        if (victim == null)
        {
            return;
        }

        var victimKey = string.IsNullOrWhiteSpace(victim.NpcId)
            ? victim.gameObject.name
            : victim.NpcId;

        if (!rescuedVictimIds.Add(victimKey))
        {
            return;
        }

        if (!victim.MarkRescued())
        {
            return;
        }

        Debug.Log($"EvacuationZone '{zoneId}' rescued '{victimKey}'.", this);
        NotifyScenarioManager(victim);
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"EvacuationZone '{name}' requires Collider.isTrigger enabled. Enabling automatically.", this);
            triggerCollider.isTrigger = true;
        }
    }

    private void NotifyScenarioManager(VictimNPC victim)
    {
        var manager = ResolveScenarioManager();
        if (manager == null)
        {
            return;
        }

        manager.NotifyVictimRescued(victim);
    }

    private ScenarioManager ResolveScenarioManager()
    {
        if (scenarioManager == null)
        {
            scenarioManager = ScenarioManager.FindInScene();
        }

        return scenarioManager;
    }

    private void OnDrawGizmos()
    {
        var zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (zoneCollider is BoxCollider boxCollider)
        {
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            return;
        }

        if (zoneCollider is SphereCollider sphereCollider)
        {
            Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
        }
    }
}
