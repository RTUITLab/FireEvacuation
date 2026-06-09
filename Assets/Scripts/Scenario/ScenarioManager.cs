using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ScenarioManager : MonoBehaviour
{
    [SerializeField] private List<VictimNPC> activeVictims = new List<VictimNPC>();
    [SerializeField] private int totalNpcCount;
    [SerializeField] private int rescuedNpcCount;

    private readonly HashSet<VictimNPC> registeredVictims = new HashSet<VictimNPC>();
    private readonly HashSet<VictimNPC> rescuedVictims = new HashSet<VictimNPC>();

    public IReadOnlyList<VictimNPC> ActiveVictims => activeVictims;
    public int TotalNpcCount => totalNpcCount;
    public int RescuedNpcCount => rescuedNpcCount;

    private void Awake()
    {
        RebuildRegistriesFromSerializedList();
        RegisterSceneVictims();
        RefreshCounters();
    }

    private void OnValidate()
    {
        RebuildRegistriesFromSerializedList();
        RefreshCounters();
    }

    public static ScenarioManager FindInScene()
    {
        return FindAnyObjectByType<ScenarioManager>(FindObjectsInactive.Include);
    }

    public void RegisterVictim(VictimNPC victim)
    {
        if (victim == null)
        {
            return;
        }

        var wasRegistered = registeredVictims.Add(victim);
        if (wasRegistered)
        {
            activeVictims.Add(victim);
        }

        if (victim.IsRescued)
        {
            rescuedVictims.Add(victim);
        }

        RefreshCounters();

        if (wasRegistered)
        {
            Debug.Log($"ScenarioManager registered victim '{victim.NpcId}'. Progress: {rescuedNpcCount}/{totalNpcCount}.", this);
        }
    }

    public void NotifyVictimRescued(VictimNPC victim)
    {
        if (victim == null)
        {
            return;
        }

        RegisterVictim(victim);
        if (!rescuedVictims.Add(victim))
        {
            Debug.Log($"ScenarioManager ignored duplicate rescued victim '{victim.NpcId}'. Progress: {rescuedNpcCount}/{totalNpcCount}.", this);
            return;
        }

        RefreshCounters();
        Debug.Log($"ScenarioManager rescued progress: {rescuedNpcCount}/{totalNpcCount} after '{victim.NpcId}'.", this);
    }

    private void RegisterSceneVictims()
    {
        var sceneVictims = FindObjectsByType<VictimNPC>(FindObjectsInactive.Include);
        for (var index = 0; index < sceneVictims.Length; index++)
        {
            RegisterVictim(sceneVictims[index]);
        }
    }

    private void RebuildRegistriesFromSerializedList()
    {
        registeredVictims.Clear();
        rescuedVictims.Clear();

        for (var index = activeVictims.Count - 1; index >= 0; index--)
        {
            var victim = activeVictims[index];
            if (victim == null)
            {
                activeVictims.RemoveAt(index);
                continue;
            }

            if (!registeredVictims.Add(victim))
            {
                activeVictims.RemoveAt(index);
                continue;
            }

            if (victim.IsRescued)
            {
                rescuedVictims.Add(victim);
            }
        }
    }

    private void RefreshCounters()
    {
        totalNpcCount = registeredVictims.Count;
        rescuedNpcCount = rescuedVictims.Count;
    }
}
