public readonly struct MissionFailConditionContext
{
    public MissionFailConditionContext(MissionProgressSnapshot snapshot, float elapsedTimeSeconds)
    {
        Snapshot = snapshot;
        ElapsedTimeSeconds = elapsedTimeSeconds;
    }

    public MissionProgressSnapshot Snapshot { get; }
    public float ElapsedTimeSeconds { get; }
}
