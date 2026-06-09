using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EvacuationZone : MonoBehaviour
{
    [SerializeField] private string zoneId = "ExitZone";
    [SerializeField] private Color gizmoColor = new(0.2f, 0.9f, 0.3f, 0.2f);

    private readonly HashSet<int> rescuedVictimIds = new();
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"EvacuationZone '{name}' requires Collider.isTrigger enabled. Enabling automatically.", this);
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        var victim = FindVictimComponent(other);
        if (victim == null)
        {
            return;
        }

        var victimId = victim.gameObject.GetInstanceID();
        if (!rescuedVictimIds.Add(victimId))
        {
            return;
        }

        InvokeMarkRescued(victim);
        NotifyScenarioManager(victim);
    }

    private Component FindVictimComponent(Collider other)
    {
        var victim = other.GetComponent("VictimNPC");
        if (victim != null)
        {
            return victim;
        }

        var current = other.transform.parent;
        while (current != null)
        {
            victim = current.GetComponent("VictimNPC");
            if (victim != null)
            {
                return victim;
            }

            current = current.parent;
        }

        return null;
    }

    private void InvokeMarkRescued(Component victim)
    {
        var method = victim.GetType().GetMethod("MarkRescued", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogWarning($"EvacuationZone '{zoneId}' found VictimNPC on '{victim.name}', but MarkRescued() is missing.", victim);
            return;
        }

        method.Invoke(victim, null);
    }

    private void NotifyScenarioManager(Component victim)
    {
        var manager = FindScenarioManager();
        if (manager == null)
        {
            return;
        }

        var scenarioManagerType = manager.GetType();

        var method = scenarioManagerType.GetMethod("OnVictimRescued", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? scenarioManagerType.GetMethod("RegisterRescuedVictim", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (method == null)
        {
            return;
        }

        // TODO: Replace reflective notification with a direct ScenarioManager reference when the manager API is defined.
        var parameters = method.GetParameters();
        if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(victim))
        {
            method.Invoke(manager, new object[] { victim });
            return;
        }

        if (parameters.Length == 2 &&
            parameters[0].ParameterType == typeof(string) &&
            parameters[1].ParameterType.IsInstanceOfType(victim))
        {
            method.Invoke(manager, new object[] { zoneId, victim });
        }
    }

    private MonoBehaviour FindScenarioManager()
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var behaviour in behaviours)
        {
            if (behaviour != null && behaviour.GetType().Name == "ScenarioManager")
            {
                return behaviour;
            }
        }

        // TODO: Replace scene-wide lookup with a direct serialized reference when ScenarioManager is implemented.
        return null;
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

        switch (zoneCollider)
        {
            case BoxCollider boxCollider:
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                break;
            case SphereCollider sphereCollider:
                Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
                break;
        }
    }
}
