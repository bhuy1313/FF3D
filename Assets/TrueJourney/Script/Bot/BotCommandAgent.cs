using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class BotCommandAgent : MonoBehaviour, ICommandable, IInteractable
{
    private enum ExtinguishDebugStage
    {
        None = 0,
        SearchingFireGroup = 1,
        NoFireGroupFound = 2,
        NoReachableTool = 3,
        SearchingExtinguisher = 4,
        MovingToExtinguisher = 5,
        PickingUpExtinguisher = 6,
        MovingToFire = 7,
        Spraying = 8,
        OutOfCharge = 9,
        Completed = 10
    }

    private enum VerboseExtinguishLogCategory
    {
        Targeting,
        Tooling,
        Movement,
        Timing,
        Distance,
        Spray
    }

    private enum PathClearingDebugStage
    {
        None = 0,
        SearchingBlocker = 1,
        BlockedByBreakable = 2,
        NoBreakTool = 3,
        SearchingBreakTool = 4,
        MovingToBreakTool = 5,
        PickingUpBreakTool = 6,
        MovingToBreakable = 7,
        Breaking = 8,
        Cleared = 9
    }

    private enum VerbosePathClearingLogCategory
    {
        Detection,
        Tooling,
        Movement,
        Attack
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private BotBehaviorContext behaviorContext;
    [SerializeField] private Transform viewPoint;

    [Header("Navigation")]
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private float turnSpeed = 360f;

    [Header("Aim")]
    [SerializeField] private float pitchTurnSpeed = 180f;
    [SerializeField] private float minPitchAngle = -45f;
    [SerializeField] private float maxPitchAngle = 60f;
    [SerializeField] private float settleFacingThreshold = 0.96f;

    [Header("Extinguish")]
    [SerializeField] private float toolSearchRadius = 30f;
    [SerializeField] private float fireSearchRadius = 12f;
    [SerializeField] private float pickupDistance = 1.5f;
    [SerializeField] private float sprayFacingThreshold = 0.9f;
    [SerializeField] private float sprayStartDelay = 0.3f;
    [SerializeField] private float extinguisherRouteCorridorWidth = 3f;
    [SerializeField] private float extinguisherStandDistanceTolerance = 0.5f;

    [Header("Path Clearing")]
    [SerializeField] private bool enablePathClearing = true;
    [SerializeField] private float breakableSearchRadius = 18f;
    [SerializeField] private float breakableCorridorWidth = 1.5f;
    [SerializeField] private float breakableLookAheadDistance = 8f;
    [SerializeField] private float pathClearingRefreshInterval = 0.2f;
    [SerializeField] private float breakStandDistanceTolerance = 0.35f;
    [SerializeField] private float pathClearingResumeGraceTime = 0.2f;
    [SerializeField] private float blockedBreakToolRetryDelay = 1f;

    [Header("Follow")]
    [SerializeField] private string followTargetTag = "Player";
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float followRepathDistance = 0.75f;
    [SerializeField] private float followCatchupDistance = 4f;

    [Header("Gizmos")]
    [SerializeField] private bool drawDestinationGizmo = true;
    [SerializeField] private bool drawAimGizmo = true;
    [SerializeField] private float aimGizmoLength = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool enableActivityDebug = false;

    private Vector3 lastIssuedDestination;
    private bool hasIssuedDestination;
    private BotInventorySystem inventorySystem;
    private BotInteractionSensor interactionSensor;
    private IBotExtinguisherItem activeExtinguisher;
    private IBotExtinguisherItem preferredExtinguishTool;
    private IBotExtinguisherItem committedExtinguishTool;
    private IBotBreakTool activeBreakTool;
    private IBotBreakTool committedBreakTool;
    private IBotBreakTool temporarilyRejectedBreakTool;
    private IBotBreakableTarget currentBlockedBreakable;
    private IBotBreakableTarget temporarilyRejectedBreakable;
    private IFireTarget currentFireTarget;
    private Vector3 currentExtinguishTargetPosition;
    private Vector3 currentExtinguishAimPoint;
    private Vector3 currentExtinguishLaunchDirection;
    private bool hasCurrentExtinguishTargetPosition;
    private bool hasCurrentExtinguishAimPoint;
    private bool hasCurrentExtinguishLaunchDirection;
    private readonly Vector3[] currentExtinguishTrajectoryPoints = new Vector3[24];
    private int currentExtinguishTrajectoryPointCount;
    private Transform followTarget;
    private Vector3 lastFollowDestination;
    private int lastExtinguishDebugStage = -1;
    private int lastPathClearingDebugStage = -1;
    private string lastPathClearingFlowKey;
    private string lastPathClearingFlowMessage;
    private string lastMoveCommandFlowKey;
    private string lastMoveStartFlowKey;
    private float sprayReadyTime = -1f;
    private float nextBreakUseTime;
    private float nextPathClearingRefreshTime;
    private float pathClearingResumeGraceUntilTime;
    private float temporarilyRejectedBreakToolUntilTime;

    public Vector3 LastIssuedDestination => lastIssuedDestination;
    public bool HasIssuedDestination => hasIssuedDestination;
    public bool IsPathClearingActive =>
        enablePathClearing &&
        (
            Time.time < pathClearingResumeGraceUntilTime ||
            HasPendingCommittedBreakTool() ||
            (currentBlockedBreakable != null && !currentBlockedBreakable.IsBroken && currentBlockedBreakable.CanBeClearedByBot)
        );

    private void Awake()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (behaviorContext == null)
        {
            behaviorContext = GetComponent<BotBehaviorContext>();
        }

        inventorySystem = GetComponent<BotInventorySystem>();
        interactionSensor = GetComponent<BotInteractionSensor>();
        ResolveViewPointReference();
    }

    private void Update()
    {
        ResolveViewPointReference();
        if (behaviorContext == null)
        {
            return;
        }

        if (behaviorContext.HasExtinguishOrder)
        {
            ProcessExtinguishOrder();
            return;
        }

        if (behaviorContext.HasFollowOrder)
        {
            ResetViewPointPitch();
            if (lastExtinguishDebugStage != -1)
            {
                ClearExtinguishRuntimeState();
                lastExtinguishDebugStage = -1;
            }

            ProcessFollowOrder();
            return;
        }

        ResetViewPointPitch();
        if (lastExtinguishDebugStage != -1)
        {
            ClearExtinguishRuntimeState();
            lastExtinguishDebugStage = -1;
        }

        if (!behaviorContext.HasMoveOrder)
        {
            ClearBlockedPathRuntime();
        }
    }

    public bool CanAcceptCommand(BotCommandType commandType)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !isActiveAndEnabled)
        {
            return false;
        }

        switch (commandType)
        {
            case BotCommandType.Move:
                return true;
            case BotCommandType.Extinguish:
                return behaviorContext != null && inventorySystem != null;
            case BotCommandType.Follow:
                return behaviorContext != null;
            default:
                return false;
        }
    }

    public bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint)
    {
        if (!CanAcceptCommand(commandType))
        {
            return false;
        }

        if (!navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 destination = worldPoint;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(worldPoint, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        bool accepted;
        switch (commandType)
        {
            case BotCommandType.Move:
                if (behaviorContext != null && behaviorContext.UseMoveOrdersAsBehaviorInput)
                {
                    behaviorContext.ClearFollowOrder();
                    behaviorContext.ClearExtinguishOrder();
                    behaviorContext.SetMoveOrder(destination);
                    accepted = true;
                }
                else
                {
                    navMeshAgent.isStopped = false;
                    accepted = navMeshAgent.SetDestination(destination);
                }
                break;
            case BotCommandType.Extinguish:
                if (behaviorContext == null)
                {
                    return false;
                }

                behaviorContext.ClearFollowOrder();
                behaviorContext.ClearMoveOrder();
                behaviorContext.SetExtinguishOrder(destination);
                accepted = true;
                break;
            case BotCommandType.Follow:
                if (behaviorContext == null)
                {
                    return false;
                }

                behaviorContext.ClearMoveOrder();
                behaviorContext.ClearExtinguishOrder();
                behaviorContext.SetFollowOrder();
                accepted = true;
                break;
            default:
                return false;
        }

        if (!accepted)
        {
            return false;
        }

        lastIssuedDestination = destination;
        hasIssuedDestination = true;
        return true;
    }

    public void Interact(GameObject interactor)
    {
        // Intentionally empty. This lets bots participate in the focus/outline pipeline.
    }

    private void ProcessFollowOrder()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || behaviorContext == null || !behaviorContext.HasFollowOrder)
        {
            return;
        }

        if (!TryResolveFollowTarget(out Transform target))
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            return;
        }

        followTarget = target;
        Vector3 targetPosition = target.position;
        Vector3 toTarget = targetPosition - transform.position;
        float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);

        if (horizontalDistance <= followDistance)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            AimTowards(targetPosition);
            return;
        }

        Vector3 desiredPosition = targetPosition;
        float desiredStandDistance = horizontalDistance > followCatchupDistance ? followDistance * 0.5f : followDistance;
        Vector3 flatToBot = transform.position - targetPosition;
        flatToBot.y = 0f;
        if (flatToBot.sqrMagnitude > 0.001f)
        {
            desiredPosition = targetPosition + flatToBot.normalized * desiredStandDistance;
        }

        desiredPosition.y = transform.position.y;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(desiredPosition, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            desiredPosition = navMeshHit.position;
        }

        if ((desiredPosition - lastFollowDestination).sqrMagnitude >= followRepathDistance * followRepathDistance || navMeshAgent.isStopped || !navMeshAgent.hasPath)
        {
            lastFollowDestination = desiredPosition;
            MoveTo(desiredPosition);
        }
        else if (ShouldRefreshPathClearingCheck())
        {
            MoveTo(lastFollowDestination);
        }

        AimTowards(targetPosition);
    }

    private void ProcessExtinguishOrder()
    {
        if (inventorySystem == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || !behaviorContext.TryGetExtinguishOrder(out Vector3 orderPoint))
        {
            return;
        }

        IFireGroupTarget fireGroup = FindClosestActiveFireGroup(orderPoint);
        IFireTarget fireTarget = ResolveActiveFireTarget(orderPoint);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Targeting,
            $"target:{GetDebugTargetName(fireTarget)}:{GetDebugTargetName(fireGroup)}",
            $"Order={orderPoint}, fireTarget={GetDebugTargetName(fireTarget)}, fireGroup={GetDebugTargetName(fireGroup)}");
        if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
        {
            UpdateExtinguishDebugStage(ExtinguishDebugStage.NoFireGroupFound, $"No active FireGroup found near {orderPoint}. Clearing order.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            ResetViewPointPitch();
            return;
        }

        UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingFireGroup, $"Resolved FireGroup near {orderPoint}.");

        Vector3 botPosition = transform.position;
        Vector3 firePosition = fireTarget != null && fireTarget.IsBurning
            ? fireTarget.GetWorldPosition()
            : fireGroup.GetClosestActiveFirePosition(botPosition);
        preferredExtinguishTool = ResolveCommittedExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"tool:{GetToolName(preferredExtinguishTool)}:{firePosition}",
            $"Selected tool={GetToolName(preferredExtinguishTool)} for fire={firePosition}.");
        if (preferredExtinguishTool == null)
        {
            UpdateExtinguishDebugStage(ExtinguishDebugStage.NoReachableTool, $"No available suppression tool can reach fire near {firePosition}.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            ResetViewPointPitch();
            return;
        }

        if (!TryEnsureExtinguisherEquipped(preferredExtinguishTool))
        {
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"equipped:{GetToolName(activeExtinguisher)}",
            $"Equipped tool={GetToolName(activeExtinguisher)}.");

        if (UsesPreciseAim(activeExtinguisher) && fireGroup != null)
        {
            firePosition = fireGroup.GetWorldCenter();
        }

        if (!UsesPreciseAim(activeExtinguisher))
        {
            fireTarget = ResolveExtinguisherRouteTarget(orderPoint);
            if (fireTarget != null && fireTarget.IsBurning)
            {
                firePosition = fireTarget.GetWorldPosition();
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Targeting,
                    $"routetarget:{GetDebugTargetName(fireTarget)}",
                    $"Using route fire target={GetDebugTargetName(fireTarget)} at {firePosition}.");
            }
        }

        if (!activeExtinguisher.HasUsableCharge)
        {
            UpdateExtinguishDebugStage(ExtinguishDebugStage.OutOfCharge, "Extinguisher is out of charge.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            ResetViewPointPitch();
            return;
        }

        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(activeExtinguisher, firePosition);

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        bool shouldReposition;
        Vector3 desiredPosition;
        float desiredHorizontalDistance;

        if (UsesPreciseAim(activeExtinguisher))
        {
            float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(activeExtinguisher, firePosition);
            desiredHorizontalDistance = Mathf.Max(activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
            shouldReposition =
                horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
                horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;
            desiredPosition = ResolveExtinguishPosition(orderPoint, firePosition, desiredHorizontalDistance);
        }
        else
        {
            desiredHorizontalDistance = Mathf.Clamp(activeExtinguisher.PreferredSprayDistance, 0.5f, activeExtinguisher.MaxSprayDistance);
            float verticalOffsetToFire = Mathf.Abs(firePosition.y - botPosition.y);
            float standDistanceDelta = Mathf.Abs(horizontalDistanceToFire - desiredHorizontalDistance);
            shouldReposition =
                horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
                verticalOffsetToFire > activeExtinguisher.MaxVerticalReach ||
                standDistanceDelta > extinguisherStandDistanceTolerance;
            desiredPosition = ResolveExtinguisherApproachPosition(orderPoint, firePosition, desiredHorizontalDistance);
        }

        if (shouldReposition)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Movement,
                $"movefire:{desiredPosition}",
                $"Repositioning. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, preciseAim={UsesPreciseAim(activeExtinguisher)}, target={firePosition}, destination={desiredPosition}.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to extinguish position {desiredPosition} for fire at horizontal distance {horizontalDistanceToFire:F2}m.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            ResetViewPointPitch();
            MoveTo(desiredPosition);
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        Vector3 aimPoint = hasCurrentExtinguishAimPoint ? currentExtinguishAimPoint : GetAimPoint(activeExtinguisher, firePosition);
        currentExtinguishAimPoint = aimPoint;
        hasCurrentExtinguishAimPoint = true;
        if (hasCurrentExtinguishLaunchDirection)
        {
            activeExtinguisher.SetExternalAimDirection(currentExtinguishLaunchDirection, gameObject);
        }
        else
        {
            activeExtinguisher.ClearExternalAimDirection(gameObject);
        }

        AimTowards(aimPoint);

        if (UsesPreciseAim(activeExtinguisher) && !IsAimSettled(activeExtinguisher, firePosition))
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"aimwait:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for precise aim settle. fire={firePosition}.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            return;
        }

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delay:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Starting spray delay until {sprayReadyTime:F2} for fire={firePosition}.");
            StopExtinguisher();
            return;
        }

        if (Time.time < sprayReadyTime)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delaywait:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for spray delay. now={Time.time:F2}, ready={sprayReadyTime:F2}.");
            StopExtinguisher();
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Distance,
            $"dist:{GetDebugTargetName(fireTarget)}:{horizontalDistanceToFire:F2}:{desiredHorizontalDistance:F2}",
            $"Distance to target. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, vertical={Mathf.Abs(firePosition.y - botPosition.y):F2}.");

        UpdateExtinguishDebugStage(ExtinguishDebugStage.Spraying, $"Spraying fire at {firePosition}.");
        activeExtinguisher.SetExternalSprayState(true, gameObject);
        if (UsesPreciseAim(activeExtinguisher))
        {
            TryApplyWaterToFireGroup(activeExtinguisher, fireGroup, firePosition);

            if (fireGroup == null || !fireGroup.HasActiveFires)
            {
                CompleteExtinguishOrder("FireGroup extinguished.");
            }
        }
        else
        {
            TryApplyWaterToFireTarget(activeExtinguisher, fireTarget, firePosition);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Spray,
                $"sprayfire:{GetDebugTargetName(fireTarget)}",
                $"Applying extinguisher to fireTarget={GetDebugTargetName(fireTarget)} at {firePosition}.");

            if (fireTarget == null || !fireTarget.IsBurning)
            {
                StopExtinguisher();
                sprayReadyTime = -1f;
                currentFireTarget = ResolveExtinguisherRouteTarget(orderPoint);
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Targeting,
                    $"nextfire:{GetDebugTargetName(currentFireTarget)}",
                    $"Current fire extinguished. Next fire={GetDebugTargetName(currentFireTarget)}.");
                if (currentFireTarget == null || !currentFireTarget.IsBurning)
                {
                    CompleteExtinguishOrder("All nearby fires extinguished.");
                }
            }
        }
    }

    private bool TryEnsureExtinguisherEquipped(IBotExtinguisherItem desiredTool)
    {
        BotToolAcquisitionOptions<IBotExtinguisherItem> options = new BotToolAcquisitionOptions<IBotExtinguisherItem>
        {
            BotTransform = transform,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            IsAvailableToBot = tool => tool.IsAvailableTo(gameObject),
            IsHeldByBot = tool => tool.IsHeld && tool.ClaimOwner == gameObject,
            SetActiveTool = tool => activeExtinguisher = tool,
            OnUnavailable = () => ReleaseCommittedToolIfMatches(desiredTool),
            OnBeforeAcquire = StopExtinguisher,
            ReportSearching = toolName => UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingExtinguisher, $"Acquiring tool '{toolName}'."),
            ReportPickingUp = toolName => UpdateExtinguishDebugStage(ExtinguishDebugStage.PickingUpExtinguisher, $"Picking up extinguisher '{toolName}'."),
            ReportMovingToTool = (toolName, toolPosition) => UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToExtinguisher, $"Moving to tool '{toolName}' at {toolPosition}."),
            SetPickupWindow = SetPickupWindow,
            MoveToTool = toolPosition => MoveTo(toolPosition)
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private IBotExtinguisherItem SelectPreferredExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        IBotExtinguisherItem bestTool = null;
        float bestScore = float.PositiveInfinity;
        System.Collections.Generic.List<IBotExtinguisherItem> inventoryTools = new System.Collections.Generic.List<IBotExtinguisherItem>();
        inventorySystem.CollectItems(inventoryTools);
        for (int i = 0; i < inventoryTools.Count; i++)
        {
            IBotExtinguisherItem candidate = inventoryTools[i];
            if (candidate == null || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
            {
                continue;
            }

            if (!CanToolReachFire(candidate, firePosition, fireGroup, fireTarget))
            {
                continue;
            }

            float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, transform.position);
            if (score < bestScore)
            {
                bestScore = score;
                bestTool = candidate;
            }
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            EvaluateWorldToolCandidate(extinguisher, orderPoint, firePosition, fireGroup, fireTarget, searchRadiusSq, ref bestTool, ref bestScore);
        }

        return bestTool;
    }

    private void EvaluateWorldToolCandidate(
        IBotExtinguisherItem candidate,
        Vector3 orderPoint,
        Vector3 firePosition,
        IFireGroupTarget fireGroup,
        IFireTarget fireTarget,
        float searchRadiusSq,
        ref IBotExtinguisherItem bestTool,
        ref float bestScore)
    {
        Component candidateComponent = candidate as Component;
        if (candidateComponent == null || candidate.IsHeld || candidate.Rigidbody == null || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
        {
            return;
        }

        if (!CanToolReachFire(candidate, firePosition, fireGroup, fireTarget))
        {
            return;
        }

        float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
        if (distanceSq > searchRadiusSq)
        {
            return;
        }

        float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, candidateComponent.transform.position) + Mathf.Sqrt(distanceSq);
        if (score < bestScore)
        {
            bestScore = score;
            bestTool = candidate;
        }
    }

    private IBotExtinguisherItem ResolveCommittedExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (IsToolStillUsable(committedExtinguishTool, firePosition, fireGroup, fireTarget))
        {
            return committedExtinguishTool;
        }

        ReleaseCommittedTool();
        IBotExtinguisherItem selectedTool = SelectPreferredExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget);
        if (selectedTool == null)
        {
            return null;
        }

        if (!selectedTool.TryClaim(gameObject))
        {
            return null;
        }

        committedExtinguishTool = selectedTool;
        return committedExtinguishTool;
    }

    private bool IsToolStillUsable(IBotExtinguisherItem tool, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        if (!tool.HasUsableCharge || !tool.IsAvailableTo(gameObject))
        {
            return false;
        }

        return CanToolReachFire(tool, firePosition, fireGroup, fireTarget);
    }

    private void ReleaseCommittedToolIfMatches(IBotExtinguisherItem tool)
    {
        if (ReferenceEquals(committedExtinguishTool, tool))
        {
            ReleaseCommittedTool();
        }
    }

    private void ReleaseCommittedTool()
    {
        if (committedExtinguishTool != null)
        {
            committedExtinguishTool.ReleaseClaim(gameObject);
            committedExtinguishTool = null;
        }
    }

    private IFireGroupTarget FindClosestActiveFireGroup(Vector3 orderPoint)
    {
        IFireGroupTarget bestGroup = null;
        IFireGroupTarget nearestGroup = null;
        float bestDistanceSq = float.PositiveInfinity;
        float nearestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;

        foreach (IFireGroupTarget candidate in BotRuntimeRegistry.ActiveFireGroups)
        {
            if (candidate == null || !candidate.HasActiveFires)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldCenter() - orderPoint).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearestGroup = candidate;
            }

            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestGroup = candidate;
        }

        return bestGroup != null ? bestGroup : nearestGroup;
    }

    private Vector3 ResolveExtinguishPosition(Vector3 requestedPoint, Vector3 firePosition, float preferredDistance)
    {
        if (TryResolvePreciseStandPosition(requestedPoint, firePosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    private Vector3 ResolveExtinguisherApproachPosition(Vector3 orderPoint, Vector3 firePosition, float preferredDistance)
    {
        if (TryResolveExtinguisherStandPosition(orderPoint, firePosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    public bool TryNavigateTo(Vector3 destination)
    {
        LogPathClearingFlow(
            $"move-destination:{FormatFlowVectorKey(destination)}",
            $"Tiến tới điểm chỉ định {destination}.");

        if (TryHandleBlockedPath(destination))
        {
            return true;
        }

        navMeshAgent.isStopped = false;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        LogPathClearingFlow(
            $"move-start:{FormatFlowVectorKey(destination)}",
            "Di chuyển.");
        return navMeshAgent.SetDestination(destination);
    }

    private bool TrySetDestinationDirect(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        navMeshAgent.isStopped = false;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        return navMeshAgent.SetDestination(destination);
    }

    public bool ShouldRefreshPathClearingCheck()
    {
        if (!enablePathClearing || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        if (currentBlockedBreakable != null && !currentBlockedBreakable.IsBroken && currentBlockedBreakable.CanBeClearedByBot)
        {
            return true;
        }

        if (Time.time < nextPathClearingRefreshTime)
        {
            return false;
        }

        nextPathClearingRefreshTime = Time.time + Mathf.Max(0.05f, pathClearingRefreshInterval);
        return true;
    }

    private bool MoveTo(Vector3 destination)
    {
        return TryNavigateTo(destination);
    }

    private bool IsWithinArrivalDistance(Vector3 destination)
    {
        float threshold = (behaviorContext != null ? behaviorContext.ArrivalDistance : 0.35f) + 0.2f;
        return (destination - transform.position).sqrMagnitude <= threshold * threshold;
    }

    private void AimTowards(Vector3 worldPoint)
    {
        Vector3 yawDirection = worldPoint - transform.position;
        yawDirection.y = 0f;
        if (yawDirection.sqrMagnitude >= 0.001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(yawDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetYaw, turnSpeed * Time.deltaTime);
        }

        if (viewPoint == null)
        {
            return;
        }

        Vector3 localDirection = transform.InverseTransformPoint(worldPoint);
        float horizontalMagnitude = new Vector2(localDirection.x, localDirection.z).magnitude;
        if (horizontalMagnitude <= 0.001f && Mathf.Abs(localDirection.y) <= 0.001f)
        {
            return;
        }

        float targetPitch = -Mathf.Atan2(localDirection.y, Mathf.Max(0.001f, horizontalMagnitude)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, minPitchAngle, maxPitchAngle);
        Quaternion targetLocalRotation = Quaternion.Euler(targetPitch, 0f, 0f);
        viewPoint.localRotation = Quaternion.RotateTowards(viewPoint.localRotation, targetLocalRotation, pitchTurnSpeed * Time.deltaTime);
    }

    private void StopExtinguisher()
    {
        if (activeExtinguisher != null)
        {
            activeExtinguisher.ClearExternalAimDirection(gameObject);
            activeExtinguisher.SetExternalSprayState(false, gameObject);
        }
    }

    private void TryApplyWaterToFireGroup(IBotExtinguisherItem tool, IFireGroupTarget fireGroup, Vector3 firePosition)
    {
        if (tool == null || fireGroup == null)
        {
            return;
        }

        Transform aimTransform = UsesPreciseAim(tool) && viewPoint != null ? viewPoint : transform;
        Vector3 toFire = hasCurrentExtinguishLaunchDirection
            ? currentExtinguishLaunchDirection
            : GetAimPoint(tool, firePosition) - aimTransform.position;
        if (toFire.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (!UsesPreciseAim(tool))
        {
            toFire.y = 0f;
        }

        Vector3 forward = aimTransform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (!UsesPreciseAim(tool))
        {
            forward.y = 0f;
        }

        float facingDot = Vector3.Dot(forward.normalized, toFire.normalized);
        if (facingDot < sprayFacingThreshold)
        {
            return;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return;
        }

        fireGroup.ApplyWater(waterAmount);
    }

    private void TryApplyWaterToFireTarget(IBotExtinguisherItem tool, IFireTarget fireTarget, Vector3 firePosition)
    {
        if (tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return;
        }

        float horizontalDistance = GetHorizontalDistance(transform.position, firePosition);
        if (horizontalDistance > tool.MaxSprayDistance)
        {
            return;
        }

        float verticalOffset = Mathf.Abs(firePosition.y - transform.position.y);
        if (verticalOffset > tool.MaxVerticalReach)
        {
            return;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return;
        }

        fireTarget.ApplyWater(waterAmount);
    }

    private bool IsAimSettled(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        if (tool == null)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
        {
            Vector3 flatToFire = firePosition - transform.position;
            flatToFire.y = 0f;
            if (flatToFire.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            return Vector3.Dot(flatForward.normalized, flatToFire.normalized) >= 0.7f;
        }

        Transform aimTransform = UsesPreciseAim(tool) && viewPoint != null ? viewPoint : transform;
        Vector3 toFire = hasCurrentExtinguishLaunchDirection
            ? currentExtinguishLaunchDirection
            : GetAimPoint(tool, firePosition) - aimTransform.position;
        if (toFire.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector3 forward = aimTransform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float facingDot = Vector3.Dot(forward.normalized, toFire.normalized);
        return facingDot >= Mathf.Max(sprayFacingThreshold, settleFacingThreshold);
    }

    private float ScoreSuppressionTool(IBotExtinguisherItem tool, Vector3 orderPoint, Vector3 firePosition, Vector3 toolPosition)
    {
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        Vector3 attackPosition = ResolveExtinguishPosition(orderPoint, firePosition, desiredHorizontalDistance);
        float travelToAttack = Vector3.Distance(toolPosition, attackPosition);
        float desiredDistance = GetHorizontalDistance(orderPoint, firePosition);
        float fitPenalty = Mathf.Abs(desiredHorizontalDistance - tool.PreferredSprayDistance) * 0.35f;
        float rangePenalty = desiredHorizontalDistance > tool.MaxSprayDistance
            ? (desiredHorizontalDistance - tool.MaxSprayDistance) * 4f
            : 0f;
        float verticalPenalty = GetVerticalAimPenalty(toolPosition, firePosition);
        float throughputBonus = Mathf.Max(0f, tool.ApplyWaterPerSecond) * 0.1f;
        return travelToAttack + fitPenalty + rangePenalty + verticalPenalty - throughputBonus;
    }

    private bool CanToolReachFire(IBotExtinguisherItem tool, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        if (UsesPreciseAim(tool))
        {
            if (fireGroup == null || !fireGroup.HasActiveFires)
            {
                return false;
            }
        }
        else if (fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
        {
            if (!TryResolveExtinguisherStandPosition(transform.position, firePosition, tool.PreferredSprayDistance, out Vector3 standPosition))
            {
                return false;
            }

            float horizontalDistance = GetHorizontalDistance(standPosition, firePosition);
            float standVerticalOffset = Mathf.Abs(firePosition.y - standPosition.y);
            return horizontalDistance <= tool.MaxSprayDistance && standVerticalOffset <= tool.MaxVerticalReach;
        }

        float verticalOffset = Mathf.Abs(firePosition.y - transform.position.y);
        if (verticalOffset > tool.MaxVerticalReach)
        {
            return false;
        }

        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        return desiredHorizontalDistance <= tool.MaxSprayDistance;
    }

    private bool TryResolveExtinguisherStandPosition(Vector3 originPoint, Vector3 firePosition, float preferredDistance, out Vector3 standPosition)
    {
        standPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 primaryOffset = originPoint - firePosition;
        primaryOffset.y = 0f;
        if (primaryOffset.sqrMagnitude < 0.01f)
        {
            primaryOffset = transform.position - firePosition;
            primaryOffset.y = 0f;
        }

        if (primaryOffset.sqrMagnitude < 0.01f)
        {
            primaryOffset = transform.forward;
            primaryOffset.y = 0f;
        }

        Vector3 primaryDirection = primaryOffset.sqrMagnitude > 0.001f ? primaryOffset.normalized : Vector3.forward;
        float standDistance = Mathf.Max(0.75f, preferredDistance);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, standDistance + 2f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * primaryDirection;
            Vector3 candidate = firePosition + direction * standDistance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
            {
                continue;
            }

            if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float horizontalDistance = GetHorizontalDistance(navMeshHit.position, firePosition);
            float score = Mathf.Abs(horizontalDistance - standDistance);
            if (score < bestScore)
            {
                bestScore = score;
                standPosition = navMeshHit.position;
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private bool TryResolvePreciseStandPosition(Vector3 requestedPoint, Vector3 firePosition, float preferredDistance, out Vector3 standPosition)
    {
        standPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 preferredOffset = requestedPoint - firePosition;
        preferredOffset.y = 0f;
        if (preferredOffset.sqrMagnitude < 0.01f)
        {
            preferredOffset = transform.position - firePosition;
            preferredOffset.y = 0f;
        }

        if (preferredOffset.sqrMagnitude < 0.01f)
        {
            preferredOffset = transform.forward;
            preferredOffset.y = 0f;
        }

        Vector3 preferredDirection = preferredOffset.sqrMagnitude > 0.001f ? preferredOffset.normalized : Vector3.forward;
        float standDistance = Mathf.Max(0.5f, preferredDistance);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, standDistance + 2f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        float[] distanceScales = { 0.85f, 1f, 1.15f };

        for (int distanceIndex = 0; distanceIndex < distanceScales.Length; distanceIndex++)
        {
            float candidateDistance = Mathf.Max(0.5f, standDistance * distanceScales[distanceIndex]);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * preferredDirection;
                Vector3 candidate = firePosition + direction * candidateDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                Vector3 fromFire = navMeshHit.position - firePosition;
                fromFire.y = 0f;
                if (fromFire.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                float horizontalDistance = fromFire.magnitude;
                float fitPenalty = Mathf.Abs(horizontalDistance - standDistance);
                float directionPenalty = (1f - Mathf.Clamp01(Vector3.Dot(fromFire.normalized, preferredDirection))) * 2.5f;
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.04f;
                float orderPointPenalty = GetHorizontalDistance(navMeshHit.position, requestedPoint) * 0.08f;
                float score = fitPenalty + directionPenalty + pathPenalty + orderPointPenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    standPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private static float GetNavMeshPathLength(NavMeshPath path, Vector3 startPosition)
    {
        if (path == null || path.corners == null || path.corners.Length == 0)
        {
            return 0f;
        }

        float total = Vector3.Distance(startPosition, path.corners[0]);
        for (int i = 1; i < path.corners.Length; i++)
        {
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return total;
    }

    private IFireTarget FindClosestActiveFire(Vector3 orderPoint)
    {
        IFireTarget bestFire = null;
        IFireTarget nearestFire = null;
        float bestDistanceSq = float.PositiveInfinity;
        float nearestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - orderPoint).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearestFire = candidate;
            }

            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestFire = candidate;
        }

        return bestFire != null ? bestFire : nearestFire;
    }

    private IFireTarget ResolveActiveFireTarget(Vector3 orderPoint)
    {
        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            return currentFireTarget;
        }

        currentFireTarget = FindClosestActiveFire(orderPoint);
        return currentFireTarget;
    }

    private IFireTarget ResolveExtinguisherRouteTarget(Vector3 orderPoint)
    {
        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            float keepTargetRange = GetExtinguisherOrderAreaRadius();
            float currentTargetDistance = GetHorizontalDistance(transform.position, currentFireTarget.GetWorldPosition());
            if (sprayReadyTime >= 0f || currentTargetDistance <= keepTargetRange)
            {
                return currentFireTarget;
            }
        }

        if (IsNearOrderPoint(orderPoint))
        {
            currentFireTarget = FindClosestActiveFireAroundOrderPoint(orderPoint, transform.position, GetExtinguisherOrderAreaRadius());
            return currentFireTarget;
        }

        IFireTarget corridorFire = null;
        IFireTarget bestFire = null;
        IFireTarget fallbackFire = null;
        float bestCorridorProgress = float.PositiveInfinity;
        float bestCorridorOffsetSq = float.PositiveInfinity;
        float bestDistanceSq = float.PositiveInfinity;
        float fallbackDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;
        Vector3 botPosition = transform.position;

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            Vector3 firePosition = candidate.GetWorldPosition();
            float toOrderSq = (firePosition - orderPoint).sqrMagnitude;
            float toBotSq = (firePosition - botPosition).sqrMagnitude;
            float progress;
            float lateralDistanceSq = GetDistanceToSegmentXZSquared(botPosition, orderPoint, firePosition, out progress);

            if (progress >= 0f && progress <= 1.05f && lateralDistanceSq <= extinguisherRouteCorridorWidth * extinguisherRouteCorridorWidth)
            {
                if (progress < bestCorridorProgress || (Mathf.Approximately(progress, bestCorridorProgress) && lateralDistanceSq < bestCorridorOffsetSq))
                {
                    bestCorridorProgress = progress;
                    bestCorridorOffsetSq = lateralDistanceSq;
                    corridorFire = candidate;
                }
            }

            if (toOrderSq < searchRadiusSq && toBotSq < bestDistanceSq)
            {
                bestDistanceSq = toBotSq;
                bestFire = candidate;
            }

            if (toBotSq < fallbackDistanceSq)
            {
                fallbackDistanceSq = toBotSq;
                fallbackFire = candidate;
            }
        }

        currentFireTarget = corridorFire != null
            ? corridorFire
            : bestFire != null
                ? bestFire
                : fallbackFire;
        return currentFireTarget;
    }

    private IFireTarget FindClosestActiveFireAroundOrderPoint(Vector3 orderPoint, Vector3 fromPosition, float searchRadius)
    {
        IFireTarget bestFire = null;
        IFireTarget fallbackFire = null;
        float bestDistanceSq = float.PositiveInfinity;
        float fallbackDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = searchRadius * searchRadius;

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            Vector3 firePosition = candidate.GetWorldPosition();
            float toFromSq = (firePosition - fromPosition).sqrMagnitude;
            float toOrderSq = (firePosition - orderPoint).sqrMagnitude;

            if (toOrderSq <= searchRadiusSq && toFromSq < bestDistanceSq)
            {
                bestDistanceSq = toFromSq;
                bestFire = candidate;
            }

            if (toFromSq < fallbackDistanceSq)
            {
                fallbackDistanceSq = toFromSq;
                fallbackFire = candidate;
            }
        }

        return bestFire != null ? bestFire : fallbackFire;
    }

    private static float GetDistanceToSegmentXZSquared(Vector3 start, Vector3 end, Vector3 point, out float progress)
    {
        Vector2 start2 = new Vector2(start.x, start.z);
        Vector2 end2 = new Vector2(end.x, end.z);
        Vector2 point2 = new Vector2(point.x, point.z);
        Vector2 segment = end2 - start2;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            progress = 0f;
            return (point2 - start2).sqrMagnitude;
        }

        progress = Vector2.Dot(point2 - start2, segment) / segmentLengthSq;
        Vector2 projected = start2 + segment * Mathf.Clamp01(progress);
        return (point2 - projected).sqrMagnitude;
    }

    private bool IsNearOrderPoint(Vector3 orderPoint)
    {
        float threshold = GetExtinguisherOrderAreaRadius();
        return (orderPoint - transform.position).sqrMagnitude <= threshold * threshold;
    }

    private float GetExtinguisherOrderAreaRadius()
    {
        float toolRadius = activeExtinguisher != null ? activeExtinguisher.MaxSprayDistance + 1.5f : 0f;
        return Mathf.Max(fireSearchRadius, toolRadius);
    }

    private void CompleteExtinguishOrder(string detail)
    {
        UpdateExtinguishDebugStage(ExtinguishDebugStage.Completed, detail);
        ClearExtinguishRuntimeState();
        behaviorContext.ClearExtinguishOrder();
        navMeshAgent.isStopped = false;
        ResetViewPointPitch();
    }

    private void ClearExtinguishRuntimeState()
    {
        StopExtinguisher();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        preferredExtinguishTool = null;
        currentFireTarget = null;
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;
        sprayReadyTime = -1f;
    }

    private bool TryHandleBlockedPath(Vector3 destination)
    {
        if (!enablePathClearing || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            ClearBlockedPathRuntime();
            return false;
        }

        if (HasPendingCommittedBreakTool())
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget routeBlockedBreakable) &&
                routeBlockedBreakable != null &&
                routeBlockedBreakable.CanBeClearedByBot &&
                !routeBlockedBreakable.IsBroken)
            {
                LogPathClearingFlow(
                    $"sensor-blocker-reroute:{GetDebugTargetName(routeBlockedBreakable)}:{GetBreakToolName(committedBreakTool)}",
                    $"Phát hiện Blocker '{GetDebugTargetName(routeBlockedBreakable)}'.");
                LogPathClearingFlow(
                    $"retry-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    "Xác định Fire Axe khác.");
                LogPathClearingFlow(
                    $"discard-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    "Loại Fire Axe.");
                RejectBreakToolForCurrentBlocker(committedBreakTool, routeBlockedBreakable);
                StopBreakToolRoute();
                ReleaseCommittedBreakTool();
                UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Break tool route is blocked by '{GetDebugTargetName(routeBlockedBreakable)}'. Re-evaluating tool.");
                return true;
            }

            if (!IsBreakToolStillUsable(committedBreakTool))
            {
                StopBreakToolRoute();
                LogPathClearingFlow(
                    $"no-break-tool:committed:{GetBreakToolName(committedBreakTool)}",
                    "Không còn Tool khả dụng.");
                LogPathClearingFlow(
                    $"stop-breaktool:committed:{GetBreakToolName(committedBreakTool)}",
                    "Stop.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.NoBreakTool, $"Committed break tool '{GetBreakToolName(committedBreakTool)}' is no longer usable.");
                ReleaseCommittedBreakTool();
                RefreshPathClearingResumeGrace();
                return true;
            }

            UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Continuing acquisition of break tool '{GetBreakToolName(committedBreakTool)}'.");
            RefreshPathClearingResumeGrace();
            return !TryEnsureBreakToolEquipped(committedBreakTool) || activeBreakTool != null;
        }

        if (activeBreakTool != null &&
            currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            IBotBreakableTarget lockedBlockedTarget = currentBlockedBreakable;
            UpdatePathClearingDebugStage(PathClearingDebugStage.BlockedByBreakable, $"Continuing to clear blocking breakable '{GetDebugTargetName(lockedBlockedTarget)}'.");
            RefreshPathClearingResumeGrace();
            return HandleEquippedBreakToolAgainstTarget(activeBreakTool, lockedBlockedTarget);
        }

        UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBlocker, $"Checking route to {destination} for blocking breakables.");
        if (!TryResolveBlockedBreakable(destination, out IBotBreakableTarget blockedTarget))
        {
            UpdatePathClearingDebugStage(PathClearingDebugStage.Cleared, $"No blocking breakable detected toward {destination}.");
            ClearBlockedPathRuntime();
            return false;
        }

        currentBlockedBreakable = blockedTarget;
        UpdatePathClearingDebugStage(PathClearingDebugStage.BlockedByBreakable, $"Detected blocking breakable '{GetDebugTargetName(blockedTarget)}' at {blockedTarget.GetWorldPosition()}.");
        RefreshPathClearingResumeGrace();
        IBotBreakTool breakTool = ResolveCommittedBreakTool();
        if (breakTool == null)
        {
            StopBreakToolRoute();
            LogPathClearingFlow(
                $"no-break-tool:blocker:{GetDebugTargetName(blockedTarget)}",
                "Không còn Tool khả dụng.");
            LogPathClearingFlow(
                $"stop-breaktool:blocker:{GetDebugTargetName(blockedTarget)}",
                "Stop.");
            UpdatePathClearingDebugStage(PathClearingDebugStage.NoBreakTool, $"No usable break tool found for '{GetDebugTargetName(blockedTarget)}'.");
            RefreshPathClearingResumeGrace();
            return true;
        }

        if (!TryEnsureBreakToolEquipped(breakTool))
        {
            return true;
        }

        IBotBreakTool equippedBreakTool = activeBreakTool ?? breakTool;
        if (equippedBreakTool == null)
        {
            return false;
        }

        return HandleEquippedBreakToolAgainstTarget(equippedBreakTool, blockedTarget);
    }

    private bool HandleEquippedBreakToolAgainstTarget(IBotBreakTool equippedBreakTool, IBotBreakableTarget blockedTarget)
    {
        if (equippedBreakTool == null || blockedTarget == null || blockedTarget.IsBroken || !blockedTarget.CanBeClearedByBot)
        {
            return false;
        }

        Vector3 targetPosition = blockedTarget.GetWorldPosition();
        float desiredDistance = Mathf.Clamp(equippedBreakTool.PreferredBreakDistance, 0.5f, equippedBreakTool.MaxBreakDistance);
        Vector3 desiredPosition = ResolveStandPositionAroundPoint(transform.position, targetPosition, desiredDistance);
        float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
        float standDistanceDelta = horizontalDistance - desiredDistance;

        if (horizontalDistance > equippedBreakTool.MaxBreakDistance || standDistanceDelta > breakStandDistanceTolerance)
        {
            UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakable, $"Moving into break range of '{GetDebugTargetName(blockedTarget)}'.");
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Movement,
                $"movebreak:{GetDebugTargetName(blockedTarget)}:{desiredPosition}",
                $"Moving to breakable '{GetDebugTargetName(blockedTarget)}'. target={targetPosition}, destination={desiredPosition}, horizontal={horizontalDistance:F2}, desired={desiredDistance:F2}, max={equippedBreakTool.MaxBreakDistance:F2}.");
            navMeshAgent.isStopped = false;
            if (navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(desiredPosition, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
            {
                desiredPosition = navMeshHit.position;
            }

            navMeshAgent.SetDestination(desiredPosition);
            return true;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(targetPosition);

        if (blockedTarget.IsBreakInProgress)
        {
            return true;
        }

        if (Time.time >= nextBreakUseTime)
        {
            bool startedBreak = equippedBreakTool.UseOnTarget(gameObject, blockedTarget);
            if (startedBreak)
            {
                UpdatePathClearingDebugStage(PathClearingDebugStage.Breaking, $"Breaking '{GetDebugTargetName(blockedTarget)}' with '{GetBreakToolName(equippedBreakTool)}'.");
                LogPathClearingFlow(
                    $"break-breakable:{GetDebugTargetName(blockedTarget)}",
                    "Phá Breakable.");
            }

            nextBreakUseTime = Time.time + equippedBreakTool.UseCooldown;
        }

        return true;
    }

    private bool TryResolveBlockedBreakable(Vector3 destination, out IBotBreakableTarget blockedTarget)
    {
        blockedTarget = null;

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot &&
            IsBreakableStillRelevant(currentBlockedBreakable))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"reusebreakable:{GetDebugTargetName(currentBlockedBreakable)}",
                $"Continuing with current breakable '{GetDebugTargetName(currentBlockedBreakable)}'.");
            blockedTarget = currentBlockedBreakable;
            return true;
        }

        if (interactionSensor != null && interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget sensedBreakable))
        {
            LogPathClearingFlow(
                $"sensor-blocker-ahead:{GetDebugTargetName(sensedBreakable)}",
                $"Phát hiện Blocker '{GetDebugTargetName(sensedBreakable)}'.");
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"sensorbreakable:{GetDebugTargetName(sensedBreakable)}",
                $"Sensor detected breakable '{GetDebugTargetName(sensedBreakable)}' ahead.");
            blockedTarget = sensedBreakable;
            return true;
        }

        if (TryFindBreakableInFront(destination, out IBotBreakableTarget fallbackBreakable))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"fallbackbreakable:{GetDebugTargetName(fallbackBreakable)}",
                $"Fallback probe detected breakable '{GetDebugTargetName(fallbackBreakable)}' toward {destination}.");
            blockedTarget = fallbackBreakable;
            return true;
        }

        return false;
    }

    private bool IsBreakableStillRelevant(IBotBreakableTarget candidate)
    {
        if (candidate == null || candidate.IsBroken || !candidate.CanBeClearedByBot)
        {
            return false;
        }

        Vector3 candidatePosition = candidate.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(transform.position, candidatePosition);
        if (horizontalDistance > breakableSearchRadius)
        {
            return false;
        }

        if (interactionSensor != null && interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget sensedBreakable))
        {
            return ReferenceEquals(candidate, sensedBreakable);
        }

        if (TryFindBreakableInFront(lastIssuedDestination, out IBotBreakableTarget fallbackBreakable))
        {
            return ReferenceEquals(candidate, fallbackBreakable);
        }

        return horizontalDistance <= Mathf.Max(breakableLookAheadDistance, breakableCorridorWidth + GetBreakableRouteRadius(candidate));
    }

    private bool TryFindBreakableInFront(Vector3 destination, out IBotBreakableTarget breakableTarget)
    {
        breakableTarget = null;
        Vector3 forwardDirection = GetPathClearingProbeDirection(destination);
        if (forwardDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float bestDistance = float.PositiveInfinity;
        float searchRadiusSq = breakableSearchRadius * breakableSearchRadius;
        Vector3 origin = transform.position;

        foreach (IBotBreakableTarget candidate in BotRuntimeRegistry.ActiveBreakableTargets)
        {
            if (candidate == null || candidate.IsBroken || !candidate.CanBeClearedByBot)
            {
                continue;
            }

            Vector3 candidatePosition = candidate.GetWorldPosition();
            float toBotSq = (candidatePosition - origin).sqrMagnitude;
            if (toBotSq > searchRadiusSq)
            {
                continue;
            }

            Vector3 toCandidate = candidatePosition - origin;
            float forwardDistance = Vector3.Dot(forwardDirection, toCandidate);
            if (forwardDistance < 0f || forwardDistance > breakableLookAheadDistance)
            {
                continue;
            }

            Vector3 projectedPoint = origin + forwardDirection * forwardDistance;
            float lateralDistance = GetHorizontalDistance(projectedPoint, candidatePosition);
            float maxLateralDistance = breakableCorridorWidth + GetBreakableRouteRadius(candidate);
            if (lateralDistance > maxLateralDistance)
            {
                continue;
            }

            if (forwardDistance < bestDistance)
            {
                bestDistance = forwardDistance;
                breakableTarget = candidate;
            }
        }

        return breakableTarget != null;
    }

    private Vector3 GetPathClearingProbeDirection(Vector3 destination)
    {
        Vector3 direction = navMeshAgent != null ? navMeshAgent.velocity : Vector3.zero;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f && navMeshAgent != null)
        {
            direction = navMeshAgent.desiredVelocity;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f && navMeshAgent != null && navMeshAgent.hasPath)
        {
            direction = navMeshAgent.steeringTarget - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = destination - transform.position;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    private float GetBreakableRouteRadius(IBotBreakableTarget candidate)
    {
        if (candidate is Component component && TryGetWorldBounds(component, out Bounds bounds))
        {
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        return 0.5f;
    }

    private static bool TryGetWorldBounds(Component component, out Bounds bounds)
    {
        bounds = default;
        if (component == null)
        {
            return false;
        }

        Collider[] colliders = component.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = collider.bounds;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (bounds.size != Vector3.zero)
        {
            return true;
        }

        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = renderer.bounds;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds.size != Vector3.zero;
    }

    private IBotBreakTool ResolveCommittedBreakTool()
    {
        if (IsBreakToolStillUsable(committedBreakTool))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"reusebreaktool:{GetBreakToolName(committedBreakTool)}",
                $"Reusing committed break tool '{GetBreakToolName(committedBreakTool)}'.");
            return committedBreakTool;
        }

        ReleaseCommittedBreakTool();
        IBotBreakTool bestTool = null;
        float bestScore = float.PositiveInfinity;
        System.Collections.Generic.List<IBotBreakTool> inventoryTools = new System.Collections.Generic.List<IBotBreakTool>();
        inventorySystem.CollectItems(inventoryTools);

        for (int i = 0; i < inventoryTools.Count; i++)
        {
            IBotBreakTool candidate = inventoryTools[i];
            if (candidate == null ||
                !candidate.IsAvailableTo(gameObject) ||
                (currentBlockedBreakable != null && !currentBlockedBreakable.SupportsBreakTool(candidate.ToolKind)) ||
                IsBreakToolBlockedByCurrentBreakable(candidate) ||
                IsBreakToolTemporarilyRejected(candidate))
            {
                continue;
            }

            if (0f < bestScore)
            {
                bestScore = 0f;
                bestTool = candidate;
            }
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;
        foreach (IBotBreakTool candidate in BotRuntimeRegistry.ActiveBreakTools)
        {
            Component candidateComponent = candidate as Component;
            if (candidateComponent == null ||
                candidate.IsHeld ||
                !candidate.IsAvailableTo(gameObject) ||
                (currentBlockedBreakable != null && !currentBlockedBreakable.SupportsBreakTool(candidate.ToolKind)) ||
                IsBreakToolBlockedByCurrentBreakable(candidate) ||
                IsBreakToolTemporarilyRejected(candidate))
            {
                continue;
            }

            float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
            if (distanceSq > searchRadiusSq)
            {
                continue;
            }

            float score = Mathf.Sqrt(distanceSq);
            if (score < bestScore)
            {
                bestScore = score;
                bestTool = candidate;
            }
        }

        if (bestTool == null || !bestTool.TryClaim(gameObject))
        {
            return null;
        }

        committedBreakTool = bestTool;
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"claimbreaktool:{GetBreakToolName(committedBreakTool)}",
            $"Committed break tool '{GetBreakToolName(committedBreakTool)}'.");
        return committedBreakTool;
    }

    private bool TryEnsureBreakToolEquipped(IBotBreakTool desiredTool)
    {
        BotToolAcquisitionOptions<IBotBreakTool> options = new BotToolAcquisitionOptions<IBotBreakTool>
        {
            BotTransform = transform,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            IsAvailableToBot = tool => tool.IsAvailableTo(gameObject),
            IsHeldByBot = tool => tool.IsHeldBy(gameObject),
            SetActiveTool = tool => activeBreakTool = tool,
            OnUnavailable = ReleaseCommittedBreakTool,
            ReportSearching = toolName => UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Acquiring break tool '{toolName}'."),
            ReportPickingUp = toolName =>
            {
                LogPathClearingFlow($"pickup-breaktool:{toolName}", "Nhặt Fire Axe.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.PickingUpBreakTool, $"Picking up break tool '{toolName}'.");
            },
            ReportMovingToTool = (toolName, toolPosition) => UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakTool, $"Moving to break tool '{toolName}' at {toolPosition}."),
            LogHeld = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"heldbreaktool:{toolName}",
                $"Using already-held break tool '{toolName}'."),
            LogEquipped = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"equipbreaktool:{toolName}",
                $"Equipped break tool '{toolName}' from inventory."),
            LogPickedUp = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"pickupbreaktool:{toolName}",
                $"Picked up and equipped break tool '{toolName}'."),
            SetPickupWindow = SetPickupWindow,
            MoveToTool = toolPosition =>
            {
                if (IsBreakToolBlockedByCurrentBreakable(desiredTool))
                {
                    if (currentBlockedBreakable != null)
                    {
                        LogPathClearingFlow(
                            $"discard-breaktool-move:{GetBreakToolName(desiredTool)}:{GetDebugTargetName(currentBlockedBreakable)}",
                            "Loại Fire Axe.");
                        RejectBreakToolForCurrentBlocker(desiredTool, currentBlockedBreakable);
                    }

                    StopBreakToolRoute();
                    SetPickupWindow(false, null);
                    ReleaseCommittedBreakTool();
                    UpdatePathClearingDebugStage(
                        PathClearingDebugStage.SearchingBreakTool,
                        $"Break tool route is blocked. Re-evaluating tool.");
                    return;
                }

                LogVerbosePathClearing(
                    VerbosePathClearingLogCategory.Movement,
                    $"movetobreaktool:{toolPosition}",
                    $"Moving to break tool at {toolPosition}.");
                LogPathClearingFlow(
                    $"move-breaktool:{FormatFlowVectorKey(toolPosition)}",
                    "Di chuyển.");
                TrySetDestinationDirect(toolPosition);
            }
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private bool IsBreakToolStillUsable(IBotBreakTool tool)
    {
        return tool != null &&
               tool.IsAvailableTo(gameObject) &&
               (currentBlockedBreakable == null || currentBlockedBreakable.SupportsBreakTool(tool.ToolKind)) &&
               !IsBreakToolBlockedByCurrentBreakable(tool) &&
               !IsBreakToolTemporarilyRejected(tool);
    }

    private bool IsBreakToolBlockedByCurrentBreakable(IBotBreakTool tool)
    {
        if (tool == null ||
            tool.IsHeldBy(gameObject) ||
            currentBlockedBreakable == null ||
            currentBlockedBreakable.IsBroken ||
            !currentBlockedBreakable.CanBeClearedByBot)
        {
            return false;
        }

        if (!(tool is Component toolComponent))
        {
            return false;
        }

        Vector3 toolPosition = toolComponent.transform.position;
        string toolName = GetBreakToolName(tool);
        LogPathClearingFlow(
            $"candidate-breaktool:{toolName}:{FormatFlowVectorKey(toolPosition)}",
            $"Xác định Fire Axe '{toolName}'.");
        LogPathClearingFlow(
            $"create-path:{toolName}:{FormatFlowVectorKey(toolPosition)}",
            $"Tạo Path tới Fire Axe '{toolName}'.");

        if (interactionSensor != null &&
            TryFindBreakableOnPreviewPath(toolName, toolPosition, out IBotBreakableTarget sensedBreakable))
        {
            if (sensedBreakable != null && !sensedBreakable.IsBroken && sensedBreakable.CanBeClearedByBot)
            {
                LogPathClearingFlow(
                    $"retry-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    "Xác định Fire Axe khác.");
                LogPathClearingFlow(
                    $"discard-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    "Loại Fire Axe.");
                LogVerbosePathClearing(
                    VerbosePathClearingLogCategory.Tooling,
                    $"blockedbreaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    $"Break tool '{toolName}' is blocked by '{GetDebugTargetName(sensedBreakable)}' on the previewed route to the tool.");
                return true;
            }
        }

        if (currentBlockedBreakable.IsOnSameSide(transform.position, toolPosition))
        {
            return false;
        }

        LogPathClearingFlow(
            $"retry-breaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            "Xác định Fire Axe khác.");
        LogPathClearingFlow(
            $"discard-breaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            "Loại Fire Axe.");
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"blockedbreaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            $"Break tool '{toolName}' is on the opposite side of '{GetDebugTargetName(currentBlockedBreakable)}'.");
        return true;
    }

    private bool TryFindBreakableOnPreviewPath(string toolName, Vector3 toolPosition, out IBotBreakableTarget breakableTarget)
    {
        breakableTarget = null;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 sampledToolPosition = toolPosition;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(toolPosition, out NavMeshHit toolNavMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            sampledToolPosition = toolNavMeshHit.position;
        }

        NavMeshPath path = new NavMeshPath();
        if (!navMeshAgent.CalculatePath(sampledToolPosition, path) || path.corners == null || path.corners.Length < 2)
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableTowards(sampledToolPosition, out breakableTarget))
            {
                LogPathClearingFlow(
                    $"sensor-blocker-tool:{toolName}:{GetDebugTargetName(breakableTarget)}",
                    $"Phát hiện Blocker '{GetDebugTargetName(breakableTarget)}'.");
                return true;
            }

            return false;
        }

        for (int i = 1; i < path.corners.Length; i++)
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableBetween(path.corners[i - 1], path.corners[i], out breakableTarget))
            {
                LogPathClearingFlow(
                    $"sensor-blocker-path:{toolName}:{GetDebugTargetName(breakableTarget)}:{i}",
                    $"Phát hiện Blocker '{GetDebugTargetName(breakableTarget)}'.");
                return true;
            }
        }

        return false;
    }

    private void ReleaseCommittedBreakTool()
    {
        if (committedBreakTool != null)
        {
            committedBreakTool.ReleaseClaim(gameObject);
            committedBreakTool = null;
        }
    }

    private void StopBreakToolRoute()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
    }

    private void ClearBlockedPathRuntime()
    {
        SetPickupWindow(false);
        activeBreakTool = null;
        currentBlockedBreakable = null;
        temporarilyRejectedBreakTool = null;
        temporarilyRejectedBreakable = null;
        nextBreakUseTime = 0f;
        nextPathClearingRefreshTime = 0f;
        RefreshPathClearingResumeGrace();
        temporarilyRejectedBreakToolUntilTime = 0f;
        lastPathClearingDebugStage = -1;
        lastPathClearingFlowKey = null;
        lastPathClearingFlowMessage = null;
        ReleaseCommittedBreakTool();
    }

    public void ResetMoveActivityDebug()
    {
        lastMoveCommandFlowKey = null;
        lastMoveStartFlowKey = null;
        lastPathClearingFlowMessage = null;
    }

    private bool IsBreakToolTemporarilyRejected(IBotBreakTool tool)
    {
        if (tool == null || temporarilyRejectedBreakTool == null || currentBlockedBreakable == null)
        {
            return false;
        }

        if (Time.time >= temporarilyRejectedBreakToolUntilTime)
        {
            return false;
        }

        return ReferenceEquals(tool, temporarilyRejectedBreakTool) &&
               ReferenceEquals(currentBlockedBreakable, temporarilyRejectedBreakable);
    }

    private void RejectBreakToolForCurrentBlocker(IBotBreakTool tool, IBotBreakableTarget blocker)
    {
        if (tool == null || blocker == null)
        {
            return;
        }

        temporarilyRejectedBreakTool = tool;
        temporarilyRejectedBreakable = blocker;
        temporarilyRejectedBreakToolUntilTime = Time.time + Mathf.Max(0.1f, blockedBreakToolRetryDelay);
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"rejectbreaktool:{GetBreakToolName(tool)}:{GetDebugTargetName(blocker)}",
            $"Temporarily rejecting break tool '{GetBreakToolName(tool)}' because route is blocked by '{GetDebugTargetName(blocker)}'.");
    }

    private Vector3 ResolveStandPositionAroundPoint(Vector3 referencePoint, Vector3 targetPosition, float preferredDistance)
    {
        if (TryResolveExtinguisherStandPosition(referencePoint, targetPosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    private bool TryResolveFollowTarget(out Transform target)
    {
        if (followTarget != null && followTarget.gameObject.activeInHierarchy)
        {
            target = followTarget;
            return true;
        }

        if (string.IsNullOrWhiteSpace(followTargetTag))
        {
            target = null;
            return false;
        }

        GameObject targetObject = GameObject.FindGameObjectWithTag(followTargetTag);
        target = targetObject != null ? targetObject.transform : null;
        followTarget = target;
        return target != null;
    }

    private void LogVerboseExtinguish(VerboseExtinguishLogCategory category, string key, string detail)
    {
        return;
    }

    private static string GetDebugTargetName(object target)
    {
        if (target is Component component && component != null)
        {
            return component.name;
        }

        return target != null ? target.GetType().Name : "null";
    }

    private static string GetToolName(IBotExtinguisherItem tool)
    {
        Component component = tool as Component;
        return component != null ? component.name : "(unknown tool)";
    }

    private static string GetBreakToolName(IBotBreakTool tool)
    {
        Component component = tool as Component;
        return component != null ? component.name : "(unknown break tool)";
    }

    private void SetPickupWindow(bool enabled, IPickupable target = null)
    {
        if (interactionSensor == null)
        {
            return;
        }

        interactionSensor.SetPickupWindow(enabled, target);
    }

    private void ResolveViewPointReference()
    {
        if (viewPoint != null)
        {
            return;
        }

        if (inventorySystem != null && inventorySystem.EquippedRoot != null)
        {
            viewPoint = inventorySystem.EquippedRoot;
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            if (childTransforms[i] != null && childTransforms[i].name == "ViewPoint")
            {
                viewPoint = childTransforms[i];
                return;
            }
        }
    }

    private void ResetViewPointPitch()
    {
        if (viewPoint == null)
        {
            return;
        }

        viewPoint.localRotation = Quaternion.RotateTowards(viewPoint.localRotation, Quaternion.identity, pitchTurnSpeed * Time.deltaTime);
    }

    private float GetRequiredHorizontalDistanceForAim(IBotExtinguisherItem tool, Vector3 worldPoint)
    {
        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return 0f;
        }

        Vector3 fromViewPoint = worldPoint - viewPoint.position;
        float horizontalDistance = new Vector2(fromViewPoint.x, fromViewPoint.z).magnitude;
        float verticalOffset = fromViewPoint.y;

        if (horizontalDistance <= 0.001f)
        {
            horizontalDistance = 0.001f;
        }

        float requiredPitch = -Mathf.Atan2(verticalOffset, horizontalDistance) * Mathf.Rad2Deg;
        if (requiredPitch >= minPitchAngle && requiredPitch <= maxPitchAngle)
        {
            return 0f;
        }

        float allowedPitch = requiredPitch < minPitchAngle ? minPitchAngle : maxPitchAngle;
        float allowedPitchAbs = Mathf.Max(1f, Mathf.Abs(allowedPitch));
        return Mathf.Abs(verticalOffset) / Mathf.Tan(allowedPitchAbs * Mathf.Deg2Rad);
    }

    private Vector3 GetAimPoint(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return firePosition;
        }

        if (TryGetBallisticAimDirection(tool, viewPoint.position, firePosition, out Vector3 aimDirection))
        {
            return viewPoint.position + aimDirection * Mathf.Max(5f, tool.MaxSprayDistance);
        }

        return firePosition;
    }

    private void UpdateCurrentExtinguishAimData(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        currentExtinguishAimPoint = firePosition;
        currentExtinguishLaunchDirection = Vector3.zero;
        hasCurrentExtinguishAimPoint = true;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;

        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return;
        }

        if (!TryGetBallisticAimDirection(tool, viewPoint.position, firePosition, out Vector3 launchDirection))
        {
            return;
        }

        currentExtinguishLaunchDirection = launchDirection;
        hasCurrentExtinguishLaunchDirection = true;
        currentExtinguishAimPoint = viewPoint.position + launchDirection * Mathf.Max(5f, tool.MaxSprayDistance);
        BuildBallisticTrajectoryPoints(tool, viewPoint.position, launchDirection, currentExtinguishTrajectoryPoints, out currentExtinguishTrajectoryPointCount);
    }

    private static void BuildBallisticTrajectoryPoints(
        IBotExtinguisherItem tool,
        Vector3 origin,
        Vector3 launchDirection,
        Vector3[] points,
        out int pointCount)
    {
        pointCount = 0;
        if (tool == null || points == null || points.Length == 0 || launchDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float speed = Mathf.Max(0.01f, tool.BallisticLaunchSpeed);
        float maxTravelDistance = Mathf.Max(0.5f, tool.MaxSprayDistance);
        Vector3 gravity = Physics.gravity * Mathf.Max(0f, tool.BallisticGravityMultiplier);
        float estimatedTime = Mathf.Max(0.15f, maxTravelDistance / speed * 1.5f);
        int maxPoints = Mathf.Min(points.Length, 24);
        float travelledDistance = 0f;
        Vector3 previousPoint = origin;

        points[0] = origin;
        pointCount = 1;

        for (int i = 1; i < maxPoints; i++)
        {
            float t = estimatedTime * i / (maxPoints - 1);
            Vector3 point = origin + launchDirection * speed * t + 0.5f * gravity * t * t;
            travelledDistance += Vector3.Distance(previousPoint, point);
            points[pointCount++] = point;
            previousPoint = point;

            if (travelledDistance >= maxTravelDistance)
            {
                break;
            }
        }
    }

    private bool TryGetBallisticAimDirection(IBotExtinguisherItem tool, Vector3 origin, Vector3 target, out Vector3 aimDirection)
    {
        aimDirection = Vector3.zero;
        if (tool == null || tool.BallisticLaunchSpeed <= 0f)
        {
            return false;
        }

        Vector3 toTarget = target - origin;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = toTargetXZ.magnitude;
        if (horizontalDistance <= 0.001f)
        {
            aimDirection = toTarget.normalized;
            return aimDirection.sqrMagnitude > 0.001f;
        }

        float speed = tool.BallisticLaunchSpeed;
        float gravity = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0f, tool.BallisticGravityMultiplier);
        if (gravity <= 0.001f)
        {
            aimDirection = toTarget.normalized;
            return true;
        }

        float speedSquared = speed * speed;
        float speedFourth = speedSquared * speedSquared;
        float verticalOffset = toTarget.y;
        float discriminant = speedFourth - gravity * (gravity * horizontalDistance * horizontalDistance + 2f * verticalOffset * speedSquared);
        if (discriminant < 0f)
        {
            return false;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float lowAngle = Mathf.Atan2(speedSquared - sqrtDiscriminant, gravity * horizontalDistance);
        Vector3 horizontalDirection = toTargetXZ / horizontalDistance;
        aimDirection = (horizontalDirection * Mathf.Cos(lowAngle) + Vector3.up * Mathf.Sin(lowAngle)).normalized;
        return aimDirection.sqrMagnitude > 0.001f;
    }

    private static bool UsesPreciseAim(IBotExtinguisherItem tool)
    {
        return tool != null && tool.RequiresPreciseAim;
    }

    private float GetVerticalAimPenalty(Vector3 originPosition, Vector3 firePosition)
    {
        float horizontalDistance = new Vector2(firePosition.x - originPosition.x, firePosition.z - originPosition.z).magnitude;
        float verticalOffset = firePosition.y - originPosition.y;
        if (horizontalDistance <= 0.001f)
        {
            horizontalDistance = 0.001f;
        }

        float requiredPitch = -Mathf.Atan2(verticalOffset, horizontalDistance) * Mathf.Rad2Deg;
        if (requiredPitch >= minPitchAngle && requiredPitch <= maxPitchAngle)
        {
            return 0f;
        }

        float deltaToNearestLimit = requiredPitch < minPitchAngle
            ? minPitchAngle - requiredPitch
            : requiredPitch - maxPitchAngle;
        return deltaToNearestLimit * 0.5f;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        return Vector2.Distance(a2, b2);
    }

    private static string NormalizeExtinguishActivityMessage(ExtinguishDebugStage stage, string detail)
    {
        switch (stage)
        {
            case ExtinguishDebugStage.NoFireGroupFound:
                return "Không thấy mục tiêu cháy.";
            case ExtinguishDebugStage.NoReachableTool:
                return "Không có tool dập lửa khả dụng.";
            case ExtinguishDebugStage.SearchingExtinguisher:
                return "Tìm tool dập lửa.";
            case ExtinguishDebugStage.MovingToExtinguisher:
                return "Di chuyển tới tool dập lửa.";
            case ExtinguishDebugStage.PickingUpExtinguisher:
                return "Nhặt tool dập lửa.";
            case ExtinguishDebugStage.MovingToFire:
                return "Di chuyển tới vị trí dập lửa.";
            case ExtinguishDebugStage.Spraying:
                return "Bắt đầu dập lửa.";
            case ExtinguishDebugStage.OutOfCharge:
                return "Hết nước.";
            case ExtinguishDebugStage.Completed:
                return "Hoàn tất dập lửa.";
            default:
                return null;
        }
    }

    private void UpdateExtinguishDebugStage(ExtinguishDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (lastExtinguishDebugStage == stageValue)
        {
            return;
        }

        lastExtinguishDebugStage = stageValue;
        if (!enableActivityDebug)
        {
            return;
        }

        string normalizedDetail = NormalizeExtinguishActivityMessage(stage, detail);
        if (string.IsNullOrEmpty(normalizedDetail))
        {
            return;
        }

        Debug.Log($"[BotExtinguish] [{name}] {normalizedDetail}", this);
    }

    private bool HasPendingCommittedBreakTool()
    {
        return committedBreakTool != null && !committedBreakTool.IsHeldBy(gameObject);
    }

    private void RefreshPathClearingResumeGrace()
    {
        pathClearingResumeGraceUntilTime = Time.time + Mathf.Max(0.05f, pathClearingResumeGraceTime);
    }

    private void LogVerbosePathClearing(VerbosePathClearingLogCategory category, string key, string detail)
    {
        return;
    }

    private void LogPathClearingFlow(string key, string detail)
    {
        if (!enableActivityDebug)
        {
            return;
        }

        string normalizedDetail = NormalizePathClearingFlowMessage(key, detail);
        if (string.IsNullOrEmpty(normalizedDetail))
        {
            return;
        }

        if (key.StartsWith("move-destination:"))
        {
            if (lastMoveCommandFlowKey == key)
            {
                return;
            }

            lastMoveCommandFlowKey = key;
        }

        if (key.StartsWith("move-start:"))
        {
            if (lastMoveStartFlowKey == key)
            {
                return;
            }

            lastMoveStartFlowKey = key;
        }

        if (lastPathClearingFlowKey == key)
        {
            return;
        }

        if (lastPathClearingFlowMessage == normalizedDetail)
        {
            lastPathClearingFlowKey = key;
            return;
        }

        lastPathClearingFlowKey = key;
        lastPathClearingFlowMessage = normalizedDetail;
        Debug.Log($"[BotPathFlow] [{name}] {normalizedDetail}", this);
    }

    private static string FormatFlowVectorKey(Vector3 value)
    {
        return $"{Mathf.RoundToInt(value.x * 10f)}:{Mathf.RoundToInt(value.y * 10f)}:{Mathf.RoundToInt(value.z * 10f)}";
    }

    private static string NormalizePathClearingFlowMessage(string key, string detail)
    {
        if (string.IsNullOrEmpty(key))
        {
            return detail;
        }

        if (key.StartsWith("create-path:"))
        {
            return null;
        }

        if (key.StartsWith("move-destination:"))
        {
            int vectorIndex = detail.IndexOf('(');
            return vectorIndex >= 0
                ? $"Nhận lệnh Move tới {detail.Substring(vectorIndex)}"
                : "Nhận lệnh Move.";
        }

        if (key.StartsWith("move-start:") || key.StartsWith("move-breaktool:"))
        {
            return "Di chuyển.";
        }

        if (key.StartsWith("sensor-blocker"))
        {
            return "Thấy Blocker.";
        }

        if (key.StartsWith("candidate-breaktool:"))
        {
            return "Tìm Fire Axe.";
        }

        if (key.StartsWith("discard-breaktool:") || key.StartsWith("discard-breaktool-route:") || key.StartsWith("discard-breaktool-move:"))
        {
            return "Loại Fire Axe.";
        }

        if (key.StartsWith("retry-breaktool"))
        {
            return "Tìm Fire Axe khác.";
        }

        if (key.StartsWith("no-break-tool:"))
        {
            return "Không còn Tool khả dụng.";
        }

        if (key.StartsWith("stop-breaktool:"))
        {
            return "Stop.";
        }

        return detail;
    }

    private void UpdatePathClearingDebugStage(PathClearingDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (lastPathClearingDebugStage == stageValue)
        {
            return;
        }

        lastPathClearingDebugStage = stageValue;
        return;
    }

    private void OnDrawGizmosSelected()
    {
        if (drawDestinationGizmo && hasIssuedDestination)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastIssuedDestination, 0.3f);
            Gizmos.DrawLine(transform.position, lastIssuedDestination);
        }

        if (!drawAimGizmo)
        {
            return;
        }

        float rayLength = Mathf.Max(0.1f, aimGizmoLength);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * rayLength);

        if (viewPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(viewPoint.position, viewPoint.forward * rayLength);
        }

        if (behaviorContext != null && behaviorContext.HasFollowOrder && followTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, followTarget.position);
            Gizmos.DrawWireSphere(followTarget.position, 0.2f);
        }

        if (behaviorContext != null && behaviorContext.HasExtinguishOrder)
        {
            if (hasCurrentExtinguishTargetPosition)
            {
                Vector3 firePosition = currentExtinguishTargetPosition;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, firePosition);
                Gizmos.DrawWireSphere(firePosition, 0.2f);
            }

            if (hasCurrentExtinguishAimPoint)
            {
                Vector3 aimOrigin = viewPoint != null ? viewPoint.position : transform.position;
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(aimOrigin, currentExtinguishAimPoint);
                Gizmos.DrawWireSphere(currentExtinguishAimPoint, 0.15f);
            }

            if (currentExtinguishTrajectoryPointCount >= 2)
            {
                Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
                for (int i = 1; i < currentExtinguishTrajectoryPointCount; i++)
                {
                    Gizmos.DrawLine(currentExtinguishTrajectoryPoints[i - 1], currentExtinguishTrajectoryPoints[i]);
                }
            }
            else if (preferredExtinguishTool != null && preferredExtinguishTool.RequiresPreciseAim)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(viewPoint != null ? viewPoint.position : transform.position, (viewPoint != null ? viewPoint.forward : transform.forward) * rayLength);
            }
        }
    }
}
