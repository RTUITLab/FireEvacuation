using System;

[Serializable]
public class ScenarioRunLog
{
    public string logVersion = "0.1";
    public string participantId = "TEST_001";
    public int sessionNumber = 1;
    public string scenarioId = "Scenario_MVP";
    public string mode = "debug";
    public string startTime = string.Empty;
    public string endTime = string.Empty;
    public float durationSeconds;
    public int totalNpcCount;
    public int rescuedCount;
    public int lostCount;
    public string completionStatus = "unknown";
    public float successScore;
    public string successScoreType = "temporary_rescue_ratio";
}
