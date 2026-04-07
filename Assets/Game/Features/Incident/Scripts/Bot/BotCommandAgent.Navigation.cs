using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    public bool TryNavigateTo(Vector3 destination)
    {
        return navigationModule != null && navigationModule.TryNavigateTo(destination);
    }

    private bool TrySetDestinationDirect(Vector3 destination)
    {
        return navigationModule != null && navigationModule.TrySetDestinationDirect(destination);
    }

    private bool TryCalculatePreviewPath(Vector3 destination, out Vector3 sampledDestination, out NavMeshPath previewPath)
    {
        if (navigationModule == null)
        {
            sampledDestination = destination;
            previewPath = null;
            return false;
        }

        return navigationModule.TryCalculatePreviewPath(destination, out sampledDestination, out previewPath);
    }

    public bool ShouldRefreshPathClearingCheck()
    {
        return navigationModule != null && navigationModule.ShouldRefreshPathClearingCheck();
    }

    private bool MoveTo(Vector3 destination)
    {
        return navigationModule != null && navigationModule.MoveTo(destination);
    }

    private bool TryTraverseOffMeshLink()
    {
        return navigationModule != null && navigationModule.TryTraverseOffMeshLink();
    }

    private void CacheMovementSpeedDefaults()
    {
        navigationModule?.CacheMovementSpeedDefaults();
    }

    private void RestoreMovementSpeedDefaults()
    {
        navigationModule?.RestoreMovementSpeedDefaults();
    }

    private void UpdateCarryMovementSpeed()
    {
        navigationModule?.UpdateCarryMovementSpeed();
    }

    private float EvaluateCarryMovementSpeedMultiplier()
    {
        return navigationModule != null ? navigationModule.EvaluateCarryMovementSpeedMultiplier() : 1f;
    }

    private float ResolveCurrentCarryWeightKg()
    {
        return navigationModule != null ? navigationModule.ResolveCurrentCarryWeightKg() : 0f;
    }

    private bool ShouldPreserveRescueRuntimeState()
    {
        return navigationModule != null && navigationModule.ShouldPreserveRescueRuntimeState();
    }

    private IRescuableTarget ResolveCarriedRescueTarget()
    {
        return navigationModule != null ? navigationModule.ResolveCarriedRescueTarget() : null;
    }

    private bool IsWithinArrivalDistance(Vector3 destination)
    {
        return navigationModule != null && navigationModule.IsWithinArrivalDistance(destination);
    }

    private void AimTowards(Vector3 worldPoint)
    {
        navigationModule?.AimTowards(worldPoint);
    }
}
