public readonly struct MissionProgressSnapshot
{
    public MissionProgressSnapshot(
        int totalTrackedFires,
        int extinguishedFireCount,
        int totalTrackedRescuables,
        int rescuedCount,
        int totalTrackedVictims,
        int aliveVictimCount,
        int urgentVictimCount,
        int criticalVictimCount,
        int stabilizedVictimCount,
        int extractedVictimCount,
        int deceasedVictimCount,
        bool hasFireSimulationManagers = false)
    {
        TotalTrackedFires = totalTrackedFires;
        ExtinguishedFireCount = extinguishedFireCount;
        TotalTrackedRescuables = totalTrackedRescuables;
        RescuedCount = rescuedCount;
        TotalTrackedVictims = totalTrackedVictims;
        AliveVictimCount = aliveVictimCount;
        UrgentVictimCount = urgentVictimCount;
        CriticalVictimCount = criticalVictimCount;
        StabilizedVictimCount = stabilizedVictimCount;
        ExtractedVictimCount = extractedVictimCount;
        DeceasedVictimCount = deceasedVictimCount;
        HasFireSimulationManagers = hasFireSimulationManagers;
    }

    public int TotalTrackedFires { get; }
    public int ExtinguishedFireCount { get; }
    public int TotalTrackedRescuables { get; }
    public int RescuedCount { get; }
    public int TotalTrackedVictims { get; }
    public int AliveVictimCount { get; }
    public int UrgentVictimCount { get; }
    public int CriticalVictimCount { get; }
    public int StabilizedVictimCount { get; }
    public int ExtractedVictimCount { get; }
    public int DeceasedVictimCount { get; }
    public bool HasFireSimulationManagers { get; }

    public int ActiveFireCount => UnityEngine.Mathf.Max(0, TotalTrackedFires - ExtinguishedFireCount);
    public int RemainingRescuableCount => UnityEngine.Mathf.Max(0, TotalTrackedRescuables - RescuedCount);
}
