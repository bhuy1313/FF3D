using System;
using UnityEngine;

[Serializable]
public sealed class BotTask
{
    [SerializeField] private BotTaskType taskType;
    [SerializeField] private BotTaskStatus status;
    [SerializeField] private string detail;
    [SerializeField] private string targetName;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private bool hasTargetPosition;
    [SerializeField] private float startedAtTime;
    [SerializeField] private float lastUpdatedAtTime;

    public BotTaskType TaskType => taskType;
    public BotTaskStatus Status => status;
    public string Detail => detail;
    public string TargetName => targetName;
    public Vector3 TargetPosition => targetPosition;
    public bool HasTargetPosition => hasTargetPosition;
    public float StartedAtTime => startedAtTime;
    public float LastUpdatedAtTime => lastUpdatedAtTime;
    public bool HasTask => taskType != BotTaskType.None;

    public void Begin(BotTaskType newTaskType, string newDetail, string newTargetName = null, Vector3? newTargetPosition = null)
    {
        if (newTaskType == BotTaskType.None)
        {
            Clear();
            return;
        }

        float now = Application.isPlaying ? Time.time : 0f;
        bool isTaskTypeChanged = taskType != newTaskType || status != BotTaskStatus.Active;
        taskType = newTaskType;
        status = BotTaskStatus.Active;
        detail = newDetail ?? string.Empty;
        targetName = newTargetName ?? string.Empty;
        if (newTargetPosition.HasValue)
        {
            targetPosition = newTargetPosition.Value;
            hasTargetPosition = true;
        }
        else
        {
            targetPosition = default;
            hasTargetPosition = false;
        }

        if (isTaskTypeChanged || startedAtTime <= 0f)
        {
            startedAtTime = now;
        }

        lastUpdatedAtTime = now;
    }

    public void Mark(BotTaskStatus newStatus, string newDetail = null)
    {
        if (!HasTask)
        {
            return;
        }

        status = newStatus;
        if (newDetail != null)
        {
            detail = newDetail;
        }

        lastUpdatedAtTime = Application.isPlaying ? Time.time : 0f;
    }

    public void Clear()
    {
        taskType = BotTaskType.None;
        status = BotTaskStatus.None;
        detail = string.Empty;
        targetName = string.Empty;
        targetPosition = default;
        hasTargetPosition = false;
        startedAtTime = 0f;
        lastUpdatedAtTime = 0f;
    }
}
