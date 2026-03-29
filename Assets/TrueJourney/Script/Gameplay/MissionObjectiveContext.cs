public readonly struct MissionObjectiveContext
{
    public MissionObjectiveContext(IncidentMissionSystem missionSystem, MissionProgressSnapshot snapshot, float elapsedTimeSeconds)
    {
        MissionSystem = missionSystem;
        Snapshot = snapshot;
        ElapsedTimeSeconds = elapsedTimeSeconds;
    }

    public IncidentMissionSystem MissionSystem { get; }
    public MissionProgressSnapshot Snapshot { get; }
    public float ElapsedTimeSeconds { get; }

    public bool HasSignal(string signalKey)
    {
        return MissionSystem != null && MissionSystem.HasSignal(signalKey);
    }
}
