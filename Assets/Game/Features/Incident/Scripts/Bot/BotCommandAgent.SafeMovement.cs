using UnityEngine;
using TrueJourney.BotBehavior;
using UnityEngine.AI;

public partial class BotCommandAgent
{
    [Header("Safe Movement")]
    [SerializeField] private bool enableSafeMovement = true;
    [SerializeField] [Min(0.1f)] private float safeMovementFireScanRadius = 8f;
    [SerializeField] [Min(0.02f)] private float safeMovementScanInterval = 0.15f;
    [SerializeField] [Min(0.05f)] private float safeMovementBlockedTimeout = 1f;
    [SerializeField] private bool drawSafeMovementGizmo;

    private float nextSafeMovementScanTime;
    private bool hasDiscoveredSafeMovementFireObstacle;
    private bool isSafeMovementPathBlocked;
    private bool hasSafeMovementBlockedDestination;
    private Vector3 safeMovementBlockedDestination;
    private float safeMovementBlockedSinceTime = -1f;
    private string safeMovementBlockedDetail = string.Empty;

    internal bool TryRefreshSafeMovementObstacles(Vector3 destination, bool forceRepath)
    {
        if (IsV2TacticalNavigationActive())
        {
            ClearSafeMovementBlockedState();
            return false;
        }

        if (!enableSafeMovement ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh)
        {
            ClearSafeMovementBlockedState();
            return false;
        }

        if (!forceRepath && Time.time < nextSafeMovementScanTime)
        {
            return false;
        }

        nextSafeMovementScanTime = Time.time + Mathf.Max(0.02f, safeMovementScanInterval);

        bool activatedAnyObstacle = false;
        float scanRadius = Mathf.Max(0.1f, safeMovementFireScanRadius);
        float scanRadiusSq = scanRadius * scanRadius;
        Vector3 origin = transform.position;

        foreach (IFireTarget fireTarget in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (fireTarget == null || !fireTarget.IsBurning)
            {
                continue;
            }

            Vector3 firePosition = fireTarget.GetWorldPosition();
            if ((firePosition - origin).sqrMagnitude > scanRadiusSq)
            {
                continue;
            }

            bool activated = TryActivateFireNavigationObstacle(fireTarget);
            if (!activated)
            {
                continue;
            }

            activatedAnyObstacle = true;
            perceptionMemory?.RememberFire(fireTarget);
            BotRuntimeRegistry.SharedIncidentBlackboard.RememberFire(fireTarget);
        }

        if (activatedAnyObstacle && navMeshAgent.hasPath)
        {
            navMeshAgent.SetDestination(destination);
        }

        if (activatedAnyObstacle)
        {
            hasDiscoveredSafeMovementFireObstacle = true;
        }

        RefreshSafeMovementPathState(destination);
        return activatedAnyObstacle;
    }

    private bool IsV2TacticalNavigationActive()
    {
        return IsExtinguishV2Active || IsRouteFireV2Active || IsPathClearingV2Active;
    }

    internal bool IsSafeMovementPathBlocked => isSafeMovementPathBlocked;

    internal string GetSafeMovementDebugLine()
    {
        if (!isSafeMovementPathBlocked)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(safeMovementBlockedDetail)
            ? "SafeMovement/Blocked | No safe path around discovered fire obstacles."
            : $"SafeMovement/Blocked | {safeMovementBlockedDetail}";
    }

    private void RefreshSafeMovementPathState(Vector3 destination)
    {
        if (!hasDiscoveredSafeMovementFireObstacle)
        {
            ClearSafeMovementBlockedState();
            return;
        }

        if (hasSafeMovementBlockedDestination &&
            (destination - safeMovementBlockedDestination).sqrMagnitude > 0.25f)
        {
            ClearSafeMovementBlockedState();
        }

        if (navMeshAgent.pathPending)
        {
            return;
        }

        bool pathUnavailable =
            navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid ||
            navMeshAgent.pathStatus == NavMeshPathStatus.PathPartial ||
            (!navMeshAgent.hasPath && !IsWithinArrivalDistance(destination));
        if (!pathUnavailable)
        {
            ClearSafeMovementBlockedState();
            return;
        }

        if (safeMovementBlockedSinceTime < 0f)
        {
            safeMovementBlockedSinceTime = Time.time;
            safeMovementBlockedDestination = destination;
            hasSafeMovementBlockedDestination = true;
            safeMovementBlockedDetail = "Checking safe route around discovered fire obstacles.";
            return;
        }

        safeMovementBlockedDestination = destination;
        hasSafeMovementBlockedDestination = true;

        if (Time.time - safeMovementBlockedSinceTime < Mathf.Max(0.05f, safeMovementBlockedTimeout))
        {
            safeMovementBlockedDetail = "Checking safe route around discovered fire obstacles.";
            return;
        }

        isSafeMovementPathBlocked = true;
        safeMovementBlockedDetail = "No safe path to destination around discovered fire obstacles.";
    }

    private void ClearSafeMovementBlockedState()
    {
        isSafeMovementPathBlocked = false;
        hasSafeMovementBlockedDestination = false;
        safeMovementBlockedDestination = default;
        safeMovementBlockedSinceTime = -1f;
        safeMovementBlockedDetail = string.Empty;
    }

    private static bool TryActivateFireNavigationObstacle(IFireTarget fireTarget)
    {
        if (fireTarget is IFireNavigationObstacleTarget obstacleTarget)
        {
            return obstacleTarget.ActivateNavigationObstacle();
        }

        if (fireTarget is Component component &&
            component.GetComponent(typeof(IFireNavigationObstacleTarget)) is IFireNavigationObstacleTarget componentObstacleTarget)
        {
            return componentObstacleTarget.ActivateNavigationObstacle();
        }

        return false;
    }

    private void DrawSafeMovementGizmo()
    {
        if (!drawSafeMovementGizmo)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, safeMovementFireScanRadius));
    }
}
