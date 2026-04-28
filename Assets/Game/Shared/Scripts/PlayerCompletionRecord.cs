using System;
using System.Collections.Generic;

[Serializable]
public class PlayerCompletionRecord
{
    public string recordId = string.Empty;
    public string playerName = string.Empty;
    public string levelId = string.Empty;
    public string missionId = string.Empty;
    public string missionTitle = string.Empty;
    public string caseId = string.Empty;
    public string scenarioId = string.Empty;
    public string logicalFireLocation = string.Empty;
    public long savedUtcTicks;
    public CallPhaseResultSnapshot callPhase = new CallPhaseResultSnapshot();
    public int onsiteScore;
    public int onsiteMaximumScore;
    public string onsiteRank = string.Empty;
    public float onsiteElapsedSeconds;
    public int totalTrackedFires;
    public int extinguishedFireCount;
    public int totalTrackedRescuables;
    public int rescuedCount;
    public int totalTrackedVictims;
    public int urgentVictimCount;
    public int criticalVictimCount;
    public int stabilizedVictimCount;
    public int extractedVictimCount;
    public int deceasedVictimCount;
    public List<PlayerCompletionObjectiveRecord> objectives = new List<PlayerCompletionObjectiveRecord>();
    public int totalScore;
    public int totalMaximumScore;
    public bool isBest;
}

[Serializable]
public class PlayerCompletionObjectiveRecord
{
    public string title = string.Empty;
    public string summary = string.Empty;
    public bool isComplete;
    public bool hasFailed;
    public int score;
    public int maximumScore;
}
