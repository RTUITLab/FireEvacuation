using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ScenarioManager : MonoBehaviour
{
    [SerializeField] private List<VictimNPC> activeVictims = new List<VictimNPC>();
    [SerializeField] private ScenarioLogger scenarioLogger;
    [SerializeField] private int totalNpcCount;
    [SerializeField] private int rescuedNpcCount;

    private readonly HashSet<VictimNPC> registeredVictims = new HashSet<VictimNPC>();
    private readonly HashSet<VictimNPC> rescuedVictims = new HashSet<VictimNPC>();
    private bool runStarted;
    private bool runFinished;

    public IReadOnlyList<VictimNPC> ActiveVictims => activeVictims;
    public int TotalNpcCount => totalNpcCount;
    public int RescuedNpcCount => rescuedNpcCount;

    private void Awake()
    {
        RebuildRegistriesFromSerializedList();
        RegisterSceneVictims();
        RefreshCounters();
    }

    private void Start()
    {
        ResolveScenarioLogger();
        if (scenarioLogger == null)
        {
            return;
        }

        scenarioLogger.StartRun();
        runStarted = true;
        runFinished = false;
        SyncLoggerState();
        TryFinishScenario();
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
        if (runStarted && scenarioLogger != null && wasRegistered)
        {
            scenarioLogger.RegisterNpc(victim);
            if (victim.IsRescued)
            {
                scenarioLogger.MarkNpcRescued(victim);
            }
        }

        if (wasRegistered)
        {
            Debug.Log($"ScenarioManager registered victim '{victim.NpcId}'. Progress: {rescuedNpcCount}/{totalNpcCount}.", this);
        }
    }

    public void NotifyVictimRescued(VictimNPC victim)
    {
        // Пустое уведомление игнорируем сразу.
        if (victim == null)
        {
            return;
        }

        // На всякий случай регистрируем NPC, если он ещё не был замечен менеджером.
        RegisterVictim(victim);

        // После завершения сценария повторно ничего не финализируем,
        // но прогресс в лог всё равно не дублируем.
        if (runFinished)
        {
            Debug.Log($"ScenarioManager ignored rescued victim '{victim.NpcId}' because scenario is already finished. Progress: {rescuedNpcCount}/{totalNpcCount}.", this);
            return;
        }

        // Одного и того же NPC дважды не засчитываем.
        if (!rescuedVictims.Add(victim))
        {
            Debug.Log($"ScenarioManager ignored duplicate rescued victim '{victim.NpcId}'. Progress: {rescuedNpcCount}/{totalNpcCount}.", this);
            return;
        }

        // Пересчитываем счётчики после успешного добавления в список спасённых.
        RefreshCounters();
        if (runStarted && scenarioLogger != null)
        {
            scenarioLogger.MarkNpcRescued(victim);
        }

        Debug.Log($"ScenarioManager rescued progress: {rescuedNpcCount}/{totalNpcCount} after '{victim.NpcId}'.", this);
        TryFinishScenario();
    }

    public void NotifyVictimLost(VictimNPC victim)
    {
        if (!runStarted || scenarioLogger == null || victim == null)
        {
            return;
        }

        scenarioLogger.MarkNpcLost(victim);
    }

    public void FinishScenario(string completionStatus)
    {
        if (!runStarted || runFinished || scenarioLogger == null)
        {
            return;
        }

        scenarioLogger.FinishRun(completionStatus);
        runFinished = true;
        Debug.Log($"ScenarioManager finished scenario with status '{completionStatus}'.", this);
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

    private void ResolveScenarioLogger()
    {
        if (scenarioLogger == null)
        {
            scenarioLogger = FindAnyObjectByType<ScenarioLogger>(FindObjectsInactive.Include);
        }
    }

    private void SyncLoggerState()
    {
        if (scenarioLogger == null)
        {
            return;
        }

        foreach (var victim in registeredVictims)
        {
            scenarioLogger.RegisterNpc(victim);
        }

        foreach (var victim in rescuedVictims)
        {
            scenarioLogger.MarkNpcRescued(victim);
        }
    }

    private void TryFinishScenario()
    {
        if (runFinished || !runStarted || totalNpcCount <= 0 || rescuedNpcCount < totalNpcCount)
        {
            return;
        }

        FinishScenario("AllVictimsRescued");
    }
}
