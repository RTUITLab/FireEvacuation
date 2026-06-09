using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class ScenarioLogger : MonoBehaviour
{
    [Header("Данные участника")]
    [SerializeField] private string participantId = "TEST_001";
    [SerializeField] private int sessionNumber = 1;
    [SerializeField] private string scenarioId = "Scenario_MVP";
    [SerializeField] private string mode = "debug";
    [SerializeField] private string successScoreType = "temporary_rescue_ratio";

    private readonly HashSet<VictimNPC> registeredVictims = new HashSet<VictimNPC>();
    private readonly HashSet<VictimNPC> rescuedVictims = new HashSet<VictimNPC>();
    private readonly HashSet<VictimNPC> lostVictims = new HashSet<VictimNPC>();

    private ScenarioRunLog currentRunLog = new ScenarioRunLog();
    private DateTimeOffset runStartTimestamp;
    private bool runStarted;
    private bool hasExplicitLostMarks;
    private string lastSavedLogPath = string.Empty;

    public string LastSavedLogPath => lastSavedLogPath;

    // Если объект выгружается вместе со сценой во время Play Mode,
    // сохраняем промежуточный лог как аварийное завершение сценария.
    private void OnDisable()
    {
        FlushRunIfNeeded("manual_stop");
    }

    // При закрытии приложения также сохраняем всё, что уже успело произойти.
    private void OnApplicationQuit()
    {
        FlushRunIfNeeded("manual_stop");
    }

    // Запускает новый прогон и сбрасывает накопленные значения.
    public void StartRun()
    {
        runStartTimestamp = DateTimeOffset.UtcNow;
        runStarted = true;
        hasExplicitLostMarks = false;
        lastSavedLogPath = string.Empty;

        registeredVictims.Clear();
        rescuedVictims.Clear();
        lostVictims.Clear();

        currentRunLog = new ScenarioRunLog
        {
            logVersion = "0.1",
            participantId = participantId,
            sessionNumber = Mathf.Max(1, sessionNumber),
            scenarioId = string.IsNullOrWhiteSpace(scenarioId) ? "Scenario_MVP" : scenarioId,
            mode = string.IsNullOrWhiteSpace(mode) ? "debug" : mode,
            startTime = runStartTimestamp.ToString("o"),
            completionStatus = "unknown",
            successScoreType = string.IsNullOrWhiteSpace(successScoreType) ? "temporary_rescue_ratio" : successScoreType
        };

        Debug.Log($"ScenarioLogger started run for '{currentRunLog.scenarioId}' / participant '{currentRunLog.participantId}'.", this);
    }

    // Ручная регистрация NPC без ссылки на объект.
    public void RegisterNpc()
    {
        currentRunLog.totalNpcCount++;
    }

    // Регистрация NPC с защитой от двойного учёта.
    public void RegisterNpc(VictimNPC victim)
    {
        if (victim == null)
        {
            RegisterNpc();
            return;
        }

        if (!registeredVictims.Add(victim))
        {
            return;
        }

        currentRunLog.totalNpcCount++;
    }

    // Ручная отметка спасённого NPC.
    public void MarkNpcRescued()
    {
        currentRunLog.rescuedCount++;
    }

    // Отметка спасённого NPC с защитой от двойного учёта.
    public void MarkNpcRescued(VictimNPC victim)
    {
        if (victim == null)
        {
            MarkNpcRescued();
            return;
        }

        RegisterNpc(victim);
        if (!rescuedVictims.Add(victim))
        {
            return;
        }

        if (lostVictims.Remove(victim) && currentRunLog.lostCount > 0)
        {
            currentRunLog.lostCount--;
        }

        currentRunLog.rescuedCount++;
    }

    // Ручная отметка потерянного NPC.
    public void MarkNpcLost()
    {
        hasExplicitLostMarks = true;
        currentRunLog.lostCount++;
    }

    // Отметка потерянного NPC с защитой от повторов.
    public void MarkNpcLost(VictimNPC victim)
    {
        if (victim == null)
        {
            MarkNpcLost();
            return;
        }

        RegisterNpc(victim);
        if (rescuedVictims.Contains(victim) || !lostVictims.Add(victim))
        {
            return;
        }

        hasExplicitLostMarks = true;
        currentRunLog.lostCount++;
    }

    // Завершает прогон, рассчитывает итоговые поля и сохраняет JSON.
    public void FinishRun(string completionStatus)
    {
        if (!runStarted)
        {
            Debug.LogWarning("ScenarioLogger.FinishRun was called before StartRun. A zero-duration log will be written.", this);
            StartRun();
        }

        var runEndTimestamp = DateTimeOffset.UtcNow;
        currentRunLog.endTime = runEndTimestamp.ToString("o");
        currentRunLog.durationSeconds = Mathf.Max(0f, (float)(runEndTimestamp - runStartTimestamp).TotalSeconds);
        currentRunLog.completionStatus = NormalizeCompletionStatus(completionStatus);

        var remainingNpcCount = Mathf.Max(0, currentRunLog.totalNpcCount - currentRunLog.rescuedCount);
        if (!hasExplicitLostMarks)
        {
            currentRunLog.lostCount = remainingNpcCount;
        }
        else
        {
            currentRunLog.lostCount = Mathf.Clamp(currentRunLog.lostCount, 0, remainingNpcCount);
        }

        currentRunLog.successScore = currentRunLog.totalNpcCount > 0
            ? (float)currentRunLog.rescuedCount / currentRunLog.totalNpcCount
            : 0f;

        lastSavedLogPath = SaveCurrentRunLog();
        runStarted = false;

        Debug.Log($"ScenarioLogger wrote log to '{lastSavedLogPath}'.", this);
    }

    [ContextMenu("Test_StartRun")]
    private void TestStartRun()
    {
        StartRun();
    }

    [ContextMenu("Test_RegisterNpc")]
    private void TestRegisterNpc()
    {
        RegisterNpc();
        Debug.Log($"ScenarioLogger test total NPC count: {currentRunLog.totalNpcCount}.", this);
    }

    [ContextMenu("Test_MarkNpcRescued")]
    private void TestMarkNpcRescued()
    {
        MarkNpcRescued();
        Debug.Log($"ScenarioLogger test rescued count: {currentRunLog.rescuedCount}.", this);
    }

    [ContextMenu("Test_FinishRun")]
    private void TestFinishRun()
    {
        FinishRun("all_rescued");
    }

    private string SaveCurrentRunLog()
    {
        var logsDirectoryPath = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(logsDirectoryPath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName =
            $"log_{SanitizeFileNamePart(currentRunLog.participantId)}_{SanitizeFileNamePart(currentRunLog.scenarioId)}_session{Mathf.Max(1, currentRunLog.sessionNumber)}_{timestamp}.json";
        var filePath = Path.Combine(logsDirectoryPath, fileName);

        var json = JsonUtility.ToJson(currentRunLog, true);
        File.WriteAllText(filePath, json);
        return filePath;
    }

    private static string NormalizeCompletionStatus(string completionStatus)
    {
        return completionStatus switch
        {
            "all_rescued" => "all_rescued",
            "time_expired" => "time_expired",
            "manual_stop" => "manual_stop",
            _ => "unknown"
        };
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = value.ToCharArray();
        for (var index = 0; index < sanitizedCharacters.Length; index++)
        {
            if (Array.IndexOf(invalidFileNameChars, sanitizedCharacters[index]) >= 0)
            {
                sanitizedCharacters[index] = '_';
            }
        }

        return new string(sanitizedCharacters);
    }

    private void FlushRunIfNeeded(string completionStatus)
    {
        if (!Application.isPlaying || !runStarted)
        {
            return;
        }

        FinishRun(completionStatus);
    }
}
