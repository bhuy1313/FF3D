using UnityEngine;

public partial class IncidentMissionSystem
{
    private bool TryAdvanceMissionStageIfReady()
    {
        return Stages.TryAdvanceMissionStageIfReady();
    }

    private bool HasActiveStageSequence()
    {
        return Stages.HasActiveStageSequence();
    }

    private bool IsFinalMissionStage()
    {
        return Stages.IsFinalMissionStage();
    }

    private float ResolveCurrentStageTransitionDelaySeconds()
    {
        return Stages.ResolveCurrentStageTransitionDelaySeconds();
    }

    private void ResetMissionStageRuntime()
    {
        Stages.ResetMissionStageRuntime();
    }

    private void ClearStageRuntimePresentation()
    {
        Stages.ClearStageRuntimePresentation();
    }

    private void ResetSignalState()
    {
        Stages.ResetSignalState();
    }

    private bool UpdatePendingStageTransition()
    {
        return Stages.UpdatePendingStageTransition();
    }

    private void ScheduleStageTransition(int nextStageIndex, float delaySeconds)
    {
        Stages.ScheduleStageTransition(nextStageIndex, delaySeconds);
    }

    private void BeginStage(int stageIndex)
    {
        Stages.BeginStage(stageIndex);
    }

    private void ClearPendingStageTransition()
    {
        Stages.ClearPendingStageTransition();
    }

    private void InvokeCurrentStageStarted()
    {
        Stages.InvokeCurrentStageStarted();
    }

    private void InvokeCurrentStageCompleted()
    {
        Stages.InvokeCurrentStageCompleted();
    }

    private void ExecuteCurrentStageActions(MissionActionTrigger trigger, string stageId)
    {
        Stages.ExecuteCurrentStageActions(trigger, stageId);
    }

    private void InvokeStageBindings(string stageId, bool started)
    {
        Stages.InvokeStageBindings(stageId, started);
    }

    private string ResolveCurrentStageId()
    {
        return Stages.ResolveCurrentStageId();
    }

    private bool TryGetCurrentStageDefinition(out MissionStageDefinition stage)
    {
        return Stages.TryGetCurrentStageDefinition(out stage);
    }
}
