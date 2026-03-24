using System.Collections.Generic;
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
    [SerializeField] private float extinguisherApproachRetargetDistance = 0.75f;
    [SerializeField] private float pointFireApproachSearchRadius = 8f;
    [SerializeField] private float pointFireApproachSampleStep = 1.5f;
    [SerializeField] private int pointFireApproachDirections = 12;
    [SerializeField] private float pointFireApproachHeightWeight = 1.5f;

    [Header("Route Fire")]
    [SerializeField] private bool enableRouteFireClearing = true;
    [SerializeField] private float routeFireDetectionPadding = 0.5f;
    [SerializeField] private float routeFireVerticalTolerance = 2f;

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

    [Header("Rescue")]
    [SerializeField] private float rescueSearchRadius = 12f;
    [SerializeField] private float rescueInteractionDistance = 1.5f;
    [SerializeField] private float rescueSafeZoneArrivalDistance = 2f;
    [SerializeField] private Transform rescueCarryAnchor;
    [SerializeField] private Vector3 rescueCarryLocalPosition = new Vector3(0f, 1.1f, 0.6f);
    [SerializeField] private Vector3 rescueDropOffset = new Vector3(0.75f, 0f, 0f);

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
    private IFireTarget currentRouteBlockingFire;
    private IFireTarget currentFireTarget;
    private IFireTarget commandedPointFireTarget;
    private IFireGroupTarget commandedFireGroupTarget;
    private IFireTarget lockedExtinguisherFireTarget;
    private float lockedExtinguisherFireRadius;
    private float lockedExtinguisherStandOffDistance;
    private IRescuableTarget currentRescueTarget;
    private ISafeZoneTarget currentSafeZoneTarget;
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
    private BotActivityDebug activityDebug;
    private bool extinguishStartupPending;
    private float sprayReadyTime = -1f;
    private float nextBreakUseTime;
    private float nextPathClearingRefreshTime;
    private float pathClearingResumeGraceUntilTime;
    private float temporarilyRejectedBreakToolUntilTime;
    private Transform runtimeRescueCarryAnchor;
    private BotRuntimeDecisionService runtimeDecisionService;
    private BotExtinguishController extinguishController;
    private BotPathClearingController pathClearingController;
    private BotMovePickupController movePickupController;
    private BotFollowController followController;
    private BotRescueController rescueController;

    public Vector3 LastIssuedDestination => lastIssuedDestination;
    public bool HasIssuedDestination => hasIssuedDestination;
    public bool IsPathClearingActive =>
        (enablePathClearing &&
        (
            Time.time < pathClearingResumeGraceUntilTime ||
            HasPendingCommittedBreakTool() ||
            (currentBlockedBreakable != null && !currentBlockedBreakable.IsBroken && currentBlockedBreakable.CanBeClearedByBot)
        )) ||
        IsRouteFireClearingActive();

    public bool HasMovePickupTarget => movePickupController != null && movePickupController.HasTarget;

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
        runtimeDecisionService = new BotRuntimeDecisionService();
        extinguishController = new BotExtinguishController(ProcessExtinguishOrder);
        pathClearingController = new BotPathClearingController(TryNavigateTo, ShouldRefreshPathClearingCheck);
        movePickupController = new BotMovePickupController();
        followController = new BotFollowController(runtimeDecisionService);
        rescueController = new BotRescueController(runtimeDecisionService);
        activityDebug = new BotActivityDebug();
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
            RunExtinguishController();
            return;
        }

        if (behaviorContext.HasRescueOrder)
        {
            PrepareNonExtinguishCommandRuntime();
            RunRescueController();
            return;
        }

        if (currentRescueTarget != null || (activityDebug != null && activityDebug.HasRescueActivity))
        {
            ClearRescueRuntimeState();
        }

        if (behaviorContext.HasFollowOrder)
        {
            PrepareNonExtinguishCommandRuntime();
            ProcessFollowOrder();
            return;
        }

        PrepareNonExtinguishCommandRuntime();

        if (HasMovePickupTarget &&
            (behaviorContext == null || !behaviorContext.UseMoveOrdersAsBehaviorInput))
        {
            if (TryCompleteMovePickupTarget())
            {
                if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.ResetPath();
                }

                ResetMoveActivityDebug();
            }

            return;
        }

        if (!behaviorContext.HasMoveOrder)
        {
            ClearBlockedPathRuntime();
            ClearRouteFireRuntime();
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
            case BotCommandType.Rescue:
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
                    PrepareForIssuedCommand(BotCommandType.Move);
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
                return TryIssueExtinguishCommand(destination, BotExtinguishCommandMode.Auto);
            case BotCommandType.Follow:
                if (behaviorContext == null)
                {
                    return false;
                }

                PrepareForIssuedCommand(BotCommandType.Follow);
                behaviorContext.SetFollowOrder();
                accepted = true;
                break;
            case BotCommandType.Rescue:
                if (behaviorContext == null)
                {
                    return false;
                }

                PrepareForIssuedCommand(BotCommandType.Rescue);
                behaviorContext.SetRescueOrder(destination);
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

    public bool TryIssueExtinguishCommand(
        Vector3 scanOrigin,
        BotExtinguishCommandMode mode,
        IFireTarget pointFireTarget = null,
        IFireGroupTarget fireGroupTarget = null)
    {
        if (!CanAcceptCommand(BotCommandType.Extinguish) || behaviorContext == null || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 approachDestination = scanOrigin;
        if (mode == BotExtinguishCommandMode.PointFire &&
            TryResolvePointFireApproachPosition(scanOrigin, out Vector3 sampledDestination))
        {
            approachDestination = sampledDestination;
        }
        else if (navMeshSampleDistance > 0f &&
                 NavMesh.SamplePosition(scanOrigin, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            approachDestination = navMeshHit.position;
        }

        PrepareForIssuedCommand(BotCommandType.Extinguish);
        CacheIssuedExtinguishTargets(mode, pointFireTarget, fireGroupTarget);
        behaviorContext.SetExtinguishOrder(approachDestination, scanOrigin, mode);
        extinguishStartupPending = true;
        lastIssuedDestination = approachDestination;
        hasIssuedDestination = true;
        return true;
    }

    public bool TryIssueMoveToPickup(IPickupable pickupTarget)
    {
        if (pickupTarget == null || pickupTarget.Rigidbody == null || !CanAcceptCommand(BotCommandType.Move) || !navMeshAgent.isOnNavMesh || movePickupController == null)
        {
            return false;
        }

        if (!movePickupController.TryIssueMoveToPickup(pickupTarget, CreateMovePickupOptions(), out Vector3 destination))
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

    private void RunExtinguishController()
    {
        extinguishController?.Tick();
    }

    private void RunRescueController()
    {
        rescueController?.Tick(
            this,
            navMeshAgent,
            behaviorContext,
            rescueSearchRadius,
            rescueInteractionDistance,
            rescueSafeZoneArrivalDistance,
            rescueDropOffset);
    }

    private void PrepareNonExtinguishCommandRuntime()
    {
        ResetViewPointPitch();
        if (activityDebug == null || !activityDebug.HasExtinguishDebugStage)
        {
            return;
        }

        ClearExtinguishRuntimeState();
    }

    private void PrepareForIssuedCommand(BotCommandType commandType)
    {
        if (behaviorContext == null)
        {
            return;
        }

        behaviorContext.ClearOrdersExcept(commandType);

        if (commandType != BotCommandType.Extinguish && activityDebug != null && activityDebug.HasExtinguishDebugStage)
        {
            ClearExtinguishRuntimeState();
        }

        if (commandType != BotCommandType.Rescue)
        {
            ClearRescueRuntimeState();
        }

        if (commandType != BotCommandType.Move)
        {
            ResetMoveActivityDebug();
        }

        ClearBlockedPathRuntime();
        ClearRouteFireRuntime();
    }

    internal Transform CurrentFollowTarget
    {
        get => followTarget;
        set => followTarget = value;
    }

    internal Vector3 LastFollowDestination
    {
        get => lastFollowDestination;
        set => lastFollowDestination = value;
    }

    internal IRescuableTarget CurrentRescueTarget
    {
        get => currentRescueTarget;
        set => currentRescueTarget = value;
    }

    internal ISafeZoneTarget CurrentSafeZoneTarget
    {
        get => currentSafeZoneTarget;
        set => currentSafeZoneTarget = value;
    }

    internal bool MoveToCommand(Vector3 destination)
    {
        return MoveTo(destination);
    }

    internal bool ShouldRefreshPathClearingCheckCommand()
    {
        return pathClearingController != null
            ? pathClearingController.ShouldRefreshPathClearingCheck()
            : ShouldRefreshPathClearingCheck();
    }

    internal void AimTowardsPoint(Vector3 worldPoint)
    {
        AimTowards(worldPoint);
    }

    internal Transform EnsureRescueCarryAnchor()
    {
        return GetRescueCarryAnchor();
    }

    internal string FormatFlowVectorKeyForLog(Vector3 value)
    {
        return FormatFlowVectorKey(value);
    }

    internal void LogRescueActivityMessage(string key, string detail)
    {
        LogRescueActivity(key, detail);
    }

    public void SetMovePickupTarget(IPickupable target)
    {
        movePickupController?.SetTarget(target);
    }

    public bool TryCompleteMovePickupTarget()
    {
        return movePickupController != null && movePickupController.TryCompleteMovePickupTarget(CreateMovePickupOptions());
    }

    private BotMovePickupOptions CreateMovePickupOptions()
    {
        return new BotMovePickupOptions
        {
            BotTransform = transform,
            NavMeshAgent = navMeshAgent,
            BehaviorContext = behaviorContext,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            NavMeshSampleDistance = navMeshSampleDistance,
            PrepareForIssuedCommand = PrepareForIssuedCommand,
            LogPathFlow = LogPathClearingFlow,
            GetPickupableName = GetPickupableName,
            SetPickupWindow = SetPickupWindow,
            TryEnsureExtinguisherEquipped = TryEnsureExtinguisherEquipped,
            TryEnsureBreakToolEquipped = TryEnsureBreakToolEquipped
        };
    }

    internal void AbortActiveRescueOrder()
    {
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    internal void CompleteActiveRescueOrder()
    {
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    private void ProcessFollowOrder()
    {
        followController?.Tick(
            this,
            navMeshAgent,
            navMeshSampleDistance,
            followTargetTag,
            followDistance,
            followRepathDistance,
            followCatchupDistance);
    }

    private void ProcessRescueOrder()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || behaviorContext == null || !behaviorContext.TryGetRescueOrder(out Vector3 orderPoint))
        {
            return;
        }

        LogRescueActivity($"rescue-order:{FormatFlowVectorKey(orderPoint)}", $"Received Rescue order to {orderPoint}.");

        IRescuableTarget rescueTarget = GetCommittedRescueTarget();
        if (rescueTarget == null)
        {
            rescueTarget = ResolveRescueTarget(orderPoint);
        }
        if (rescueTarget == null)
        {
            LogRescueActivity("rescue-notfound", "No rescue target found.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        currentRescueTarget = rescueTarget;
        currentSafeZoneTarget = ResolveNearestSafeZone(rescueTarget.GetWorldPosition());

        if (!rescueTarget.NeedsRescue)
        {
            LogRescueActivity("rescue-complete", "Rescue completed.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            currentSafeZoneTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        if (currentSafeZoneTarget == null)
        {
            LogRescueActivity("rescue-no-safezone", "No safe zone found.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            Vector3 safeZonePosition = currentSafeZoneTarget.GetWorldPosition();
            float distanceToSafeZone = GetHorizontalDistance(transform.position, safeZonePosition);
            bool hasReachedSafeZone =
                currentSafeZoneTarget.ContainsPoint(transform.position) ||
                distanceToSafeZone <= rescueSafeZoneArrivalDistance;

            if (!hasReachedSafeZone)
            {
                LogRescueActivity("rescue-carry", "Carrying victim to safe zone.");
                MoveTo(safeZonePosition);
                return;
            }

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
            Vector3 dropPosition = currentSafeZoneTarget.GetDropPoint(fallbackDropPosition);
            rescueTarget.CompleteRescueAt(dropPosition);
            LogRescueActivity("rescue-complete", "Rescue completed.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            currentSafeZoneTarget = null;
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
        if (horizontalDistance > rescueInteractionDistance)
        {
            LogRescueActivity("rescue-move", "Moving to victim.");
            MoveTo(targetPosition);
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(targetPosition);

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == gameObject)
        {
            LogRescueActivity("rescue-start", "Started rescue.");
            return;
        }

        if (rescueTarget.TryBeginCarry(gameObject, GetRescueCarryAnchor()))
        {
            LogRescueActivity("rescue-pickup", "Picked up victim.");
            return;
        }

        currentRescueTarget = null;
    }

    private void ProcessExtinguishOrder()
    {
        if (inventorySystem == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            !behaviorContext.TryGetExtinguishOrder(out Vector3 orderPoint, out Vector3 scanOrigin, out BotExtinguishCommandMode orderMode))
        {
            return;
        }

        Vector3 targetSearchPoint = orderMode == BotExtinguishCommandMode.PointFire ? scanOrigin : orderPoint;
        IFireGroupTarget fireGroup = orderMode == BotExtinguishCommandMode.PointFire ? null : ResolveIssuedFireGroupTarget(targetSearchPoint);
        IFireTarget fireTarget = orderMode == BotExtinguishCommandMode.PointFire
            ? ResolveIssuedPointFireTarget(targetSearchPoint)
            : ResolveActiveFireTarget(targetSearchPoint);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Targeting,
            $"target:{GetDebugTargetName(fireTarget)}:{GetDebugTargetName(fireGroup)}",
            $"Order={targetSearchPoint}, fireTarget={GetDebugTargetName(fireTarget)}, fireGroup={GetDebugTargetName(fireGroup)}, mode={orderMode}.");
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
        preferredExtinguishTool = ResolveCommittedExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"tool:{GetToolName(preferredExtinguishTool)}:{firePosition}",
            $"Selected tool={GetToolName(preferredExtinguishTool)} for fire={firePosition}.");
        if (preferredExtinguishTool == null)
        {
            if (orderMode == BotExtinguishCommandMode.FireGroup &&
                TryFallbackFireGroupOrderToPointFire(fireTarget, out Vector3 fallbackDestination))
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Tooling,
                    $"fallback-pointfire:{GetDebugTargetName(fireTarget)}:{fallbackDestination}",
                    $"No suitable FireGroup tool found. Falling back to PointFire at {fallbackDestination}.");
                return;
            }

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

        if (extinguishStartupPending)
        {
            extinguishStartupPending = false;
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
            fireTarget = orderMode == BotExtinguishCommandMode.PointFire
                ? ResolveIssuedPointFireTarget(targetSearchPoint)
                : ResolveExtinguisherRouteTarget(targetSearchPoint);
            if (fireTarget != null && fireTarget.IsBurning)
            {
                firePosition = fireTarget.GetWorldPosition();
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Targeting,
                    $"routetarget:{GetDebugTargetName(fireTarget)}",
                    $"Using route fire target={GetDebugTargetName(fireTarget)} at {firePosition}.");
            }

            if (orderMode == BotExtinguishCommandMode.FireGroup &&
                fireTarget != null &&
                fireTarget.IsBurning &&
                TryFallbackFireGroupOrderToPointFire(fireTarget, out Vector3 extinguisherFallbackDestination))
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Tooling,
                    $"fallback-extinguisher-pointfire:{GetDebugTargetName(fireTarget)}:{extinguisherFallbackDestination}",
                    $"Fire Extinguisher selected for FireGroup. Falling back to PointFire at {extinguisherFallbackDestination}.");
                return;
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
        if (!UsesPreciseAim(activeExtinguisher))
        {
            PrimeExtinguisherTargetLock(activeExtinguisher, fireTarget);
        }

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        bool shouldReposition;
        Vector3 desiredPosition = transform.position;
        float desiredHorizontalDistance;

        if (UsesPreciseAim(activeExtinguisher))
        {
            float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(activeExtinguisher, firePosition);
            desiredHorizontalDistance = Mathf.Max(activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
            shouldReposition =
                horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
                horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;
        }
        else
        {
            float edgeDistanceToFire = GetFireEdgeDistance(botPosition, firePosition, fireTarget);
            float desiredStandOffDistance = GetDesiredExtinguisherStandOffDistanceLocked(activeExtinguisher, fireTarget);
            float allowedEdgeRange = GetAllowedExtinguisherEdgeRange(activeExtinguisher);
            desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(activeExtinguisher, fireTarget);
            float verticalOffsetToFire = Mathf.Abs(firePosition.y - botPosition.y);
            bool keepCurrentStandDistance = IsExtinguisherTargetLocked(fireTarget);
            shouldReposition =
                edgeDistanceToFire > allowedEdgeRange ||
                verticalOffsetToFire > activeExtinguisher.MaxVerticalReach ||
                (!keepCurrentStandDistance && edgeDistanceToFire < desiredStandOffDistance - extinguisherStandDistanceTolerance);
        }

        if (shouldReposition)
        {
            if (orderMode == BotExtinguishCommandMode.PointFire &&
                !CanExtinguishFromCurrentPosition(activeExtinguisher, firePosition, fireTarget) &&
                !IsNearOrderPoint(orderPoint))
            {
                UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to point-fire approach position {orderPoint}.");
                StopExtinguisher();
                sprayReadyTime = -1f;
                ResetViewPointPitch();
                MoveTo(orderPoint);
                return;
            }

            if (!UsesPreciseAim(activeExtinguisher) && orderMode == BotExtinguishCommandMode.PointFire)
            {
                desiredPosition = orderPoint;
            }
            else
            {
                desiredPosition = UsesPreciseAim(activeExtinguisher)
                    ? ResolveExtinguishPosition(targetSearchPoint, firePosition, desiredHorizontalDistance)
                    : ResolveExtinguisherApproachPosition(orderPoint, firePosition, desiredHorizontalDistance);
            }
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Movement,
                $"movefire:{desiredPosition}",
                $"Repositioning. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, preciseAim={UsesPreciseAim(activeExtinguisher)}, target={firePosition}, destination={desiredPosition}.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to extinguish position {desiredPosition} for fire at horizontal distance {horizontalDistanceToFire:F2}m.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            ResetViewPointPitch();
            if (ShouldIssueExtinguisherApproachMove(desiredPosition))
            {
                MoveTo(desiredPosition);
            }
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
        if (!UsesPreciseAim(activeExtinguisher))
        {
            LockExtinguisherTarget(fireTarget);
        }

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
            MoveToTool = toolPosition => TrySetDestinationDirect(toolPosition)
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private IRescuableTarget ResolveRescueTarget(Vector3 orderPoint)
    {
        return runtimeDecisionService != null
            ? runtimeDecisionService.ResolveRescueTarget(orderPoint, currentRescueTarget, gameObject, rescueSearchRadius)
            : null;
    }

    private IRescuableTarget GetCommittedRescueTarget()
    {
        if (currentRescueTarget == null || !currentRescueTarget.NeedsRescue)
        {
            return null;
        }

        if (currentRescueTarget.ActiveRescuer != gameObject)
        {
            return null;
        }

        if (!currentRescueTarget.IsCarried && !currentRescueTarget.IsRescueInProgress)
        {
            return null;
        }

        return currentRescueTarget;
    }

    private ISafeZoneTarget ResolveNearestSafeZone(Vector3 fromPosition)
    {
        return runtimeDecisionService != null
            ? runtimeDecisionService.ResolveNearestSafeZone(fromPosition, currentSafeZoneTarget)
            : null;
    }

    private Transform GetRescueCarryAnchor()
    {
        if (rescueCarryAnchor != null)
        {
            return rescueCarryAnchor;
        }

        if (runtimeRescueCarryAnchor == null)
        {
            GameObject anchorObject = new GameObject("RescueCarryAnchor");
            runtimeRescueCarryAnchor = anchorObject.transform;
            runtimeRescueCarryAnchor.SetParent(transform, false);
            runtimeRescueCarryAnchor.localPosition = rescueCarryLocalPosition;
            runtimeRescueCarryAnchor.localRotation = Quaternion.identity;
        }

        return runtimeRescueCarryAnchor;
    }

    private IBotExtinguisherItem SelectPreferredExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
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

            if (!DoesToolMatchExtinguishMode(candidate, orderMode) ||
                !CanToolReachFire(candidate, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
            {
                continue;
            }

            float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, transform.position, fireTarget);
            if (score < bestScore)
            {
                bestScore = score;
                bestTool = candidate;
            }
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            EvaluateWorldToolCandidate(extinguisher, orderPoint, firePosition, fireGroup, fireTarget, orderMode, searchRadiusSq, ref bestTool, ref bestScore);
        }

        return bestTool;
    }

    private void EvaluateWorldToolCandidate(
        IBotExtinguisherItem candidate,
        Vector3 orderPoint,
        Vector3 firePosition,
        IFireGroupTarget fireGroup,
        IFireTarget fireTarget,
        BotExtinguishCommandMode orderMode,
        float searchRadiusSq,
        ref IBotExtinguisherItem bestTool,
        ref float bestScore)
    {
        Component candidateComponent = candidate as Component;
        if (candidateComponent == null || candidate.IsHeld || candidate.Rigidbody == null || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
        {
            return;
        }

        if (!DoesToolMatchExtinguishMode(candidate, orderMode) ||
            !CanToolReachFire(candidate, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
        {
            return;
        }

        float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
        if (distanceSq > searchRadiusSq)
        {
            return;
        }

        float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, candidateComponent.transform.position, fireTarget) + Mathf.Sqrt(distanceSq);
        if (score < bestScore)
        {
            bestScore = score;
            bestTool = candidate;
        }
    }

    private IBotExtinguisherItem ResolveCommittedExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
    {
        if (activeExtinguisher != null &&
            activeExtinguisher.IsHeld &&
            activeExtinguisher.ClaimOwner == gameObject &&
            activeExtinguisher.HasUsableCharge &&
            DoesToolMatchExtinguishMode(activeExtinguisher, orderMode))
        {
            committedExtinguishTool = activeExtinguisher;
            return activeExtinguisher;
        }

        if (IsToolStillUsable(committedExtinguishTool, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
        {
            return committedExtinguishTool;
        }

        ReleaseCommittedTool();
        IBotExtinguisherItem selectedTool = SelectPreferredExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
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

    private bool TryFallbackFireGroupOrderToPointFire(IFireTarget fireTarget, out Vector3 fallbackDestination)
    {
        fallbackDestination = default;
        if (behaviorContext == null ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            fireTarget == null ||
            !fireTarget.IsBurning)
        {
            return false;
        }

        Vector3 scanOrigin = fireTarget.GetWorldPosition();
        fallbackDestination = scanOrigin;
        if (TryResolvePointFireApproachPosition(scanOrigin, out Vector3 approachDestination))
        {
            fallbackDestination = approachDestination;
        }
        else if (navMeshSampleDistance > 0f &&
                 NavMesh.SamplePosition(scanOrigin, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            fallbackDestination = navMeshHit.position;
        }

        ClearExtinguishRuntimeState();
        CacheIssuedExtinguishTargets(BotExtinguishCommandMode.PointFire, fireTarget, null);
        behaviorContext.SetExtinguishOrder(fallbackDestination, scanOrigin, BotExtinguishCommandMode.PointFire);
        extinguishStartupPending = true;
        lastIssuedDestination = fallbackDestination;
        hasIssuedDestination = true;
        return true;
    }

    private void CacheIssuedExtinguishTargets(
        BotExtinguishCommandMode mode,
        IFireTarget pointFireTarget,
        IFireGroupTarget fireGroupTarget)
    {
        commandedPointFireTarget = mode == BotExtinguishCommandMode.PointFire && pointFireTarget != null && pointFireTarget.IsBurning
            ? pointFireTarget
            : null;
        commandedFireGroupTarget = mode == BotExtinguishCommandMode.FireGroup && fireGroupTarget != null && fireGroupTarget.HasActiveFires
            ? fireGroupTarget
            : null;
    }

    private IFireTarget ResolveIssuedPointFireTarget(Vector3 scanOrigin)
    {
        IFireTarget localTarget = ResolvePointFireTarget(scanOrigin);
        if (localTarget != null && localTarget.IsBurning)
        {
            if (commandedPointFireTarget != null && !commandedPointFireTarget.IsBurning)
            {
                commandedPointFireTarget = null;
            }

            currentFireTarget = localTarget;
            return currentFireTarget;
        }

        if (commandedPointFireTarget != null && commandedPointFireTarget.IsBurning)
        {
            currentFireTarget = commandedPointFireTarget;
            return currentFireTarget;
        }

        currentFireTarget = null;
        commandedPointFireTarget = null;
        return currentFireTarget;
    }

    private IFireGroupTarget ResolveIssuedFireGroupTarget(Vector3 orderPoint)
    {
        if (commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires)
        {
            return commandedFireGroupTarget;
        }

        commandedFireGroupTarget = FindClosestActiveFireGroup(orderPoint);
        return commandedFireGroupTarget;
    }

    private bool IsToolStillUsable(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode, Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        if (!tool.HasUsableCharge || !tool.IsAvailableTo(gameObject) || !DoesToolMatchExtinguishMode(tool, orderMode))
        {
            return false;
        }

        return CanToolReachFire(tool, orderMode, orderPoint, firePosition, fireGroup, fireTarget);
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

        if (TryResolvePointFireApproachPosition(orderPoint, out desiredPosition))
        {
            return desiredPosition;
        }

        if (TryResolveReachableReferencePosition(orderPoint, Mathf.Max(navMeshSampleDistance, pointFireApproachSampleStep, 2f), out desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    private bool ShouldIssueExtinguisherApproachMove(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (navMeshAgent.isStopped || !navMeshAgent.hasPath || navMeshAgent.pathPending || navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return true;
        }

        return GetHorizontalDistance(navMeshAgent.destination, destination) > Mathf.Max(0.1f, extinguisherApproachRetargetDistance);
    }

    public bool TryNavigateTo(Vector3 destination)
    {
        LogPathClearingFlow(
            $"move-destination:{FormatFlowVectorKey(destination)}",
            $"Received Move order to {destination}.");

        if (TryHandleBlockedPath(destination))
        {
            return true;
        }

        if (TryHandleRouteBlockingFire(destination))
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
            "Moving.");
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

    private bool TryCalculatePreviewPath(Vector3 destination, out Vector3 sampledDestination, out NavMeshPath previewPath)
    {
        sampledDestination = destination;
        previewPath = null;

        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            sampledDestination = navMeshHit.position;
        }

        previewPath = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, sampledDestination, navMeshAgent.areaMask, previewPath);
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
        return pathClearingController != null
            ? pathClearingController.TryNavigateTo(destination)
            : TryNavigateTo(destination);
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

        float edgeDistance = GetFireEdgeDistance(transform.position, firePosition, fireTarget);
        if (edgeDistance > GetAllowedExtinguisherEdgeRange(tool))
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

    private float ScoreSuppressionTool(IBotExtinguisherItem tool, Vector3 orderPoint, Vector3 firePosition, Vector3 toolPosition, IFireTarget fireTarget)
    {
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        float preferredDistance = tool.PreferredSprayDistance;

        if (!UsesPreciseAim(tool) && fireTarget != null)
        {
            desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(tool, fireTarget);
            preferredDistance = GetDesiredExtinguisherStandOffDistance(tool);
        }

        Vector3 attackPosition = ResolveExtinguishPosition(orderPoint, firePosition, desiredHorizontalDistance);
        float travelToAttack = Vector3.Distance(toolPosition, attackPosition);
        float fitPenalty = Mathf.Abs((!UsesPreciseAim(tool) && fireTarget != null ? GetDesiredExtinguisherStandOffDistance(tool) : desiredHorizontalDistance) - preferredDistance) * 0.35f;
        float rangePenalty = !UsesPreciseAim(tool) && fireTarget != null
            ? 0f
            : desiredHorizontalDistance > tool.MaxSprayDistance
            ? (desiredHorizontalDistance - tool.MaxSprayDistance) * 4f
            : 0f;
        float verticalPenalty = GetVerticalAimPenalty(toolPosition, firePosition);
        float throughputBonus = Mathf.Max(0f, tool.ApplyWaterPerSecond) * 0.1f;
        return travelToAttack + fitPenalty + rangePenalty + verticalPenalty - throughputBonus;
    }

    private bool CanToolReachFire(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode, Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
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
            if (orderMode == BotExtinguishCommandMode.PointFire)
            {
                if (fireTarget == null || !fireTarget.IsBurning)
                {
                    return false;
                }

                if (CanExtinguishFromCurrentPosition(tool, firePosition, fireTarget))
                {
                    return true;
                }

                return CanReachDestination(orderPoint) || TryResolvePointFireApproachPosition(orderPoint, out _);
            }

            if (CanExtinguishFromCurrentPosition(tool, firePosition, fireTarget))
            {
                return true;
            }

            if (TryResolveReachableReferencePosition(orderPoint, out Vector3 reachableOrderPoint) &&
                CanExtinguishFromPosition(tool, reachableOrderPoint, firePosition, fireTarget))
            {
                return true;
            }

            float fireReferenceSampleDistance = Mathf.Max(tool.MaxSprayDistance + tool.MaxVerticalReach, 8f);
            if (TryResolveReachableReferencePosition(firePosition, fireReferenceSampleDistance, out Vector3 reachableFireReference) &&
                CanExtinguishFromPosition(tool, reachableFireReference, firePosition, fireTarget))
            {
                return true;
            }

            float desiredCenterDistance = GetDesiredExtinguisherCenterDistance(tool, fireTarget);
            if (!TryResolveExtinguisherStandPosition(orderPoint, firePosition, desiredCenterDistance, out Vector3 standPosition) &&
                !TryResolveExtinguisherStandPosition(transform.position, firePosition, desiredCenterDistance, out standPosition))
            {
                return false;
            }

            return CanExtinguishFromPosition(tool, standPosition, firePosition, fireTarget);
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

    private static bool DoesToolMatchExtinguishMode(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode)
    {
        if (tool == null)
        {
            return false;
        }

        switch (orderMode)
        {
            case BotExtinguishCommandMode.FireGroup:
                return true;
            case BotExtinguishCommandMode.PointFire:
                return !UsesPreciseAim(tool);
            default:
                return true;
        }
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
        float sampleDistance = Mathf.Max(navMeshSampleDistance, standDistance + 2f, 8f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        float[] distanceScales = { 0.65f, 0.85f, 1f, 1.15f, 1.35f, 1.6f, 1.8f, 2.1f, 2.5f };

        for (int distanceIndex = 0; distanceIndex < distanceScales.Length; distanceIndex++)
        {
            float candidateDistance = Mathf.Max(0.75f, standDistance * distanceScales[distanceIndex]);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * primaryDirection;
                Vector3 candidate = firePosition + direction * candidateDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float horizontalDistance = GetHorizontalDistance(navMeshHit.position, firePosition);
                float fitPenalty = Mathf.Abs(horizontalDistance - standDistance);
                float directionPenalty = (1f - Mathf.Clamp01(Vector3.Dot((navMeshHit.position - firePosition).normalized, primaryDirection))) * 0.75f;
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.03f;
                float score = fitPenalty + directionPenalty + pathPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    standPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private bool CanExtinguishFromCurrentPosition(IBotExtinguisherItem tool, Vector3 firePosition, IFireTarget fireTarget)
    {
        return CanExtinguishFromPosition(tool, transform.position, firePosition, fireTarget);
    }

    private bool CanExtinguishFromPosition(IBotExtinguisherItem tool, Vector3 position, Vector3 firePosition, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        float edgeDistance = GetFireEdgeDistance(position, firePosition, fireTarget);
        if (edgeDistance > GetAllowedExtinguisherEdgeRange(tool))
        {
            return false;
        }

        float verticalOffset = Mathf.Abs(firePosition.y - position.y);
        return verticalOffset <= tool.MaxVerticalReach;
    }

    private bool TryResolveReachableReferencePosition(Vector3 referencePoint, out Vector3 resolvedPosition)
    {
        return TryResolveReachableReferencePosition(referencePoint, Mathf.Max(navMeshSampleDistance, 2f), out resolvedPosition);
    }

    private bool TryResolveReachableReferencePosition(Vector3 referencePoint, float sampleDistance, out Vector3 resolvedPosition)
    {
        resolvedPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        float effectiveSampleDistance = Mathf.Max(sampleDistance, navMeshSampleDistance, 2f);
        if (!NavMesh.SamplePosition(referencePoint, out NavMeshHit navMeshHit, effectiveSampleDistance, navMeshAgent.areaMask))
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        resolvedPosition = navMeshHit.position;
        return true;
    }

    private bool TryResolvePointFireApproachPosition(Vector3 scanOrigin, out Vector3 resolvedPosition)
    {
        resolvedPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        float searchRadius = Mathf.Max(pointFireApproachSearchRadius, fireSearchRadius, navMeshSampleDistance, 2f);
        float ringStep = Mathf.Max(0.5f, pointFireApproachSampleStep);
        int directionCount = Mathf.Clamp(pointFireApproachDirections, 6, 24);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, ringStep, 1f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();

        for (float radius = 0f; radius <= searchRadius + 0.01f; radius += ringStep)
        {
            int samples = radius <= 0.01f ? 1 : directionCount;
            for (int i = 0; i < samples; i++)
            {
                float angle = samples == 1 ? 0f : (360f * i) / samples;
                Vector3 offset = samples == 1
                    ? Vector3.zero
                    : Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 candidate = scanOrigin + offset;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float toClickPenalty = GetHorizontalDistance(navMeshHit.position, scanOrigin);
                float heightPenalty = Mathf.Abs(navMeshHit.position.y - scanOrigin.y) * Mathf.Max(0f, pointFireApproachHeightWeight);
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.03f;
                float score = toClickPenalty + heightPenalty + pathPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    resolvedPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private bool CanReachDestination(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, destination, navMeshAgent.areaMask, path) &&
               path.status == NavMeshPathStatus.PathComplete;
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

    private IFireTarget ResolvePointFireTarget(Vector3 scanOrigin)
    {
        float keepRange = GetExtinguisherOrderAreaRadius();
        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            float currentDistance = GetHorizontalDistance(transform.position, currentFireTarget.GetWorldPosition());
            if (currentDistance <= keepRange)
            {
                return currentFireTarget;
            }
        }

        if (interactionSensor != null &&
            interactionSensor.TryFindNearbyFire(keepRange, out IFireTarget nearbyFire))
        {
            currentFireTarget = nearbyFire;
            return currentFireTarget;
        }

        if (interactionSensor != null &&
            interactionSensor.TryFindFireNearPoint(scanOrigin, fireSearchRadius, out IFireTarget scanOriginFire))
        {
            currentFireTarget = scanOriginFire;
            return currentFireTarget;
        }

        currentFireTarget = null;
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
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        preferredExtinguishTool = null;
        currentFireTarget = null;
        commandedPointFireTarget = null;
        commandedFireGroupTarget = null;
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;
        extinguishStartupPending = false;
        sprayReadyTime = -1f;
        activityDebug?.ResetExtinguish();
    }

    private bool IsRouteFireClearingActive()
    {
        return currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning;
    }

    private void ClearRouteFireRuntime()
    {
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        sprayReadyTime = -1f;
        currentRouteBlockingFire = null;
    }

    private bool TryHandleRouteBlockingFire(Vector3 destination)
    {
        if (!enableRouteFireClearing ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            behaviorContext.HasExtinguishOrder ||
            behaviorContext.HasFollowOrder)
        {
            ClearRouteFireRuntime();
            return false;
        }

        if (!TryCalculatePreviewPath(destination, out _, out NavMeshPath previewPath) || previewPath == null)
        {
            ClearRouteFireRuntime();
            return false;
        }

        if (!TryResolveBlockingFireOnPath(previewPath, out IFireTarget blockingFire))
        {
            if (currentRouteBlockingFire != null)
            {
                LogPathClearingFlow(
                    $"route-fire-open:{GetDebugTargetName(currentRouteBlockingFire)}",
                    "Route is clear.");
            }

            ClearRouteFireRuntime();
            return false;
        }

        currentRouteBlockingFire = blockingFire;
        LogPathClearingFlow(
            $"route-fire-detected:{GetDebugTargetName(blockingFire)}",
            "Detected fire blocking the path.");

        Vector3 firePosition = blockingFire.GetWorldPosition();
        IBotExtinguisherItem routeTool = ResolveCommittedExtinguishTool(
            firePosition,
            firePosition,
            null,
            blockingFire,
            BotExtinguishCommandMode.PointFire);
        if (routeTool == null || UsesPreciseAim(routeTool))
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            LogPathClearingFlow(
                $"route-fire-no-tool:{GetDebugTargetName(blockingFire)}",
                "No usable tool available.");
            LogPathClearingFlow(
                $"route-fire-stop:{GetDebugTargetName(blockingFire)}",
                "Stop.");
            return true;
        }

        if (!TryEnsureExtinguisherEquipped(routeTool))
        {
            LogPathClearingFlow(
                $"route-fire-search-tool:{GetToolName(routeTool)}",
                "Searching for Fire Extinguisher.");
            return true;
        }

        IBotExtinguisherItem equippedTool = activeExtinguisher ?? routeTool;
        if (equippedTool == null || UsesPreciseAim(equippedTool))
        {
            return true;
        }

        PrimeExtinguisherTargetLock(equippedTool, blockingFire);

        float desiredStandOffDistance = GetDesiredExtinguisherStandOffDistanceLocked(equippedTool, blockingFire);
        float desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(equippedTool, blockingFire);
        float horizontalDistanceToFire = GetHorizontalDistance(transform.position, firePosition);
        float edgeDistanceToFire = GetFireEdgeDistance(transform.position, firePosition, blockingFire);
        float allowedEdgeRange = GetAllowedExtinguisherEdgeRange(equippedTool);
        float verticalOffsetToFire = Mathf.Abs(firePosition.y - transform.position.y);
        bool keepCurrentStandDistance = IsExtinguisherTargetLocked(blockingFire);
        bool shouldReposition =
            edgeDistanceToFire > allowedEdgeRange ||
            verticalOffsetToFire > equippedTool.MaxVerticalReach ||
            (!keepCurrentStandDistance && edgeDistanceToFire < desiredStandOffDistance - extinguisherStandDistanceTolerance);

        if (shouldReposition)
        {
            Vector3 desiredPosition = ResolveExtinguisherApproachPosition(transform.position, firePosition, desiredHorizontalDistance);
            LogPathClearingFlow(
                $"route-fire-move:{GetDebugTargetName(blockingFire)}:{FormatFlowVectorKey(desiredPosition)}",
                "Moving.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            TrySetDestinationDirect(desiredPosition);
            return true;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(firePosition);

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            StopExtinguisher();
            return true;
        }

        if (Time.time < sprayReadyTime)
        {
            StopExtinguisher();
            return true;
        }

        LogPathClearingFlow(
            $"route-fire-spray:{GetDebugTargetName(blockingFire)}",
            "Clearing fire from route.");
        LockExtinguisherTarget(blockingFire);
        equippedTool.SetExternalSprayState(true, gameObject);
        TryApplyWaterToFireTarget(equippedTool, blockingFire, firePosition);

        if (!blockingFire.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            LogPathClearingFlow(
                $"route-fire-open:{GetDebugTargetName(blockingFire)}",
                "Route is clear.");
            ClearRouteFireRuntime();
            return false;
        }

        return true;
    }

    private bool TryResolveBlockingFireOnPath(NavMeshPath previewPath, out IFireTarget blockingFire)
    {
        blockingFire = null;
        if (!enableRouteFireClearing || previewPath == null || previewPath.corners == null || previewPath.corners.Length == 0)
        {
            return false;
        }

        float bestPathDistance = float.PositiveInfinity;
        Vector3 segmentStart = transform.position;
        float accumulatedPathDistance = 0f;

        for (int i = 0; i < previewPath.corners.Length; i++)
        {
            Vector3 segmentEnd = previewPath.corners[i];
            float segmentLength = GetHorizontalDistance(segmentStart, segmentEnd);
            if (segmentLength <= 0.01f)
            {
                segmentStart = segmentEnd;
                continue;
            }

            foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
            {
                if (candidate == null || !candidate.IsBurning)
                {
                    continue;
                }

                Vector3 firePosition = candidate.GetWorldPosition();
                if (Mathf.Abs(firePosition.y - transform.position.y) > routeFireVerticalTolerance)
                {
                    continue;
                }

                float detectionRadius = Mathf.Max(0.05f, candidate.GetWorldRadius() + routeFireDetectionPadding);
                float distanceToSegment = DistanceToSegment2D(firePosition, segmentStart, segmentEnd, out float t);
                if (distanceToSegment > detectionRadius)
                {
                    continue;
                }

                float pathDistance = accumulatedPathDistance + segmentLength * Mathf.Clamp01(t);
                if (pathDistance < bestPathDistance)
                {
                    bestPathDistance = pathDistance;
                    blockingFire = candidate;
                }
            }

            accumulatedPathDistance += segmentLength;
            segmentStart = segmentEnd;
        }

        return blockingFire != null;
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
                    $"Blocker detected: '{GetDebugTargetName(routeBlockedBreakable)}'.");
                LogPathClearingFlow(
                    $"retry-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    "Searching for another breaching tool.");
                LogPathClearingFlow(
                    $"discard-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    $"Discarding {GetBreakToolName(committedBreakTool)}.");
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
                    "No usable tool available.");
                LogPathClearingFlow(
                    $"stop-breaktool:committed:{GetBreakToolName(committedBreakTool)}",
                    "Stopped.");
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
                "No usable tool available.");
            LogPathClearingFlow(
                $"stop-breaktool:blocker:{GetDebugTargetName(blockedTarget)}",
                "Stopped.");
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
                    "Breaking Breakable.");
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
                $"Blocker detected: '{GetDebugTargetName(sensedBreakable)}'.");
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
                LogPathClearingFlow($"pickup-breaktool:{toolName}", $"Picked up {toolName}.");
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
                            $"Discarding {GetBreakToolName(desiredTool)}.");
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
                    "Moving.");
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
            $"Searching for {toolName}.");
        LogPathClearingFlow(
            $"create-path:{toolName}:{FormatFlowVectorKey(toolPosition)}",
            $"Creating path to {toolName}.");

        if (interactionSensor != null &&
            TryFindBreakableOnPreviewPath(toolName, toolPosition, out IBotBreakableTarget sensedBreakable))
        {
            if (sensedBreakable != null && !sensedBreakable.IsBroken && sensedBreakable.CanBeClearedByBot)
            {
                LogPathClearingFlow(
                    $"retry-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    "Searching for another breaching tool.");
                LogPathClearingFlow(
                    $"discard-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    $"Discarding {toolName}.");
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
            "Searching for another breaching tool.");
        LogPathClearingFlow(
            $"discard-breaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            $"Discarding {toolName}.");
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
                    $"Blocker detected: '{GetDebugTargetName(breakableTarget)}'.");
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
                    $"Blocker detected: '{GetDebugTargetName(breakableTarget)}'.");
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
        activityDebug?.ResetPathClearing();
        ReleaseCommittedBreakTool();
    }

    public void ResetMoveActivityDebug()
    {
        activityDebug?.ResetMovePathFlow();
        movePickupController?.Reset();
    }

    private void ClearRescueRuntimeState()
    {
        currentRescueTarget = null;
        currentSafeZoneTarget = null;
        activityDebug?.ResetRescue();
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

    private static string GetPickupableName(IPickupable pickupable)
    {
        if (pickupable is Component component && component != null)
        {
            return component.name;
        }

        return "(unknown item)";
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

    private static float GetDesiredExtinguisherStandOffDistance(IBotExtinguisherItem tool)
    {
        return tool != null
            ? Mathf.Clamp(tool.PreferredSprayDistance, 0.5f, tool.MaxSprayDistance)
            : 0f;
    }

    private float GetDesiredExtinguisherStandOffDistanceLocked(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        float currentStandOff = GetDesiredExtinguisherStandOffDistance(tool);
        if (tool == null || fireTarget == null || !ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            return currentStandOff;
        }

        return Mathf.Max(currentStandOff, lockedExtinguisherStandOffDistance);
    }

    private float GetDesiredExtinguisherCenterDistance(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        float fireRadius = GetTrackedExtinguisherFireRadius(fireTarget);
        return fireRadius + GetDesiredExtinguisherStandOffDistanceLocked(tool, fireTarget);
    }

    private float GetAllowedExtinguisherEdgeRange(IBotExtinguisherItem tool)
    {
        if (tool == null)
        {
            return 0f;
        }

        return tool.MaxSprayDistance + Mathf.Max(0.25f, extinguisherStandDistanceTolerance);
    }

    private float GetFireEdgeDistance(Vector3 fromPosition, Vector3 firePosition, IFireTarget fireTarget)
    {
        float fireRadius = GetTrackedExtinguisherFireRadius(fireTarget);
        return Mathf.Max(0f, GetHorizontalDistance(fromPosition, firePosition) - fireRadius);
    }

    private float GetTrackedExtinguisherFireRadius(IFireTarget fireTarget)
    {
        float currentRadius = fireTarget != null ? Mathf.Max(0f, fireTarget.GetWorldRadius()) : 0f;
        if (fireTarget == null || !ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            return currentRadius;
        }

        return Mathf.Max(currentRadius, lockedExtinguisherFireRadius);
    }

    private bool IsExtinguisherTargetLocked(IFireTarget fireTarget)
    {
        return fireTarget != null && ReferenceEquals(lockedExtinguisherFireTarget, fireTarget);
    }

    private void LockExtinguisherTarget(IFireTarget fireTarget)
    {
        if (fireTarget == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(0f, fireTarget.GetWorldRadius());
        if (!ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            lockedExtinguisherFireTarget = fireTarget;
            lockedExtinguisherFireRadius = currentRadius;
            return;
        }

        lockedExtinguisherFireRadius = Mathf.Max(lockedExtinguisherFireRadius, currentRadius);
    }

    private void PrimeExtinguisherTargetLock(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(0f, fireTarget.GetWorldRadius());
        float currentStandOff = GetDesiredExtinguisherStandOffDistance(tool);
        if (!ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            lockedExtinguisherFireTarget = fireTarget;
            lockedExtinguisherFireRadius = currentRadius;
            lockedExtinguisherStandOffDistance = currentStandOff;
            return;
        }

        lockedExtinguisherFireRadius = Mathf.Max(lockedExtinguisherFireRadius, currentRadius);
        lockedExtinguisherStandOffDistance = Mathf.Max(lockedExtinguisherStandOffDistance, currentStandOff);
    }

    private void ClearExtinguisherTargetLock()
    {
        lockedExtinguisherFireTarget = null;
        lockedExtinguisherFireRadius = 0f;
        lockedExtinguisherStandOffDistance = 0f;
    }

    private static float DistanceToSegment2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd, out float t)
    {
        Vector2 point2 = new Vector2(point.x, point.z);
        Vector2 start2 = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 end2 = new Vector2(segmentEnd.x, segmentEnd.z);
        Vector2 segment = end2 - start2;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            t = 0f;
            return Vector2.Distance(point2, start2);
        }

        t = Mathf.Clamp01(Vector2.Dot(point2 - start2, segment) / segmentLengthSq);
        Vector2 projection = start2 + segment * t;
        return Vector2.Distance(point2, projection);
    }

    private static string NormalizeExtinguishActivityMessage(ExtinguishDebugStage stage, string detail)
    {
        switch (stage)
        {
            case ExtinguishDebugStage.NoFireGroupFound:
                return "No fire target found.";
            case ExtinguishDebugStage.NoReachableTool:
                return "No usable firefighting tool available.";
            case ExtinguishDebugStage.SearchingExtinguisher:
                return "Searching for firefighting tool.";
            case ExtinguishDebugStage.MovingToExtinguisher:
                return "Moving to firefighting tool.";
            case ExtinguishDebugStage.PickingUpExtinguisher:
                return "Picked up firefighting tool.";
            case ExtinguishDebugStage.MovingToFire:
                return "Moving to firefighting position.";
            case ExtinguishDebugStage.Spraying:
                return "Started extinguishing fire.";
            case ExtinguishDebugStage.OutOfCharge:
                return "Out of charge.";
            case ExtinguishDebugStage.Completed:
                return "Extinguishing completed.";
            default:
                return null;
        }
    }

    private void UpdateExtinguishDebugStage(ExtinguishDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (activityDebug == null || !activityDebug.TryUpdateExtinguishStage(stageValue))
        {
            return;
        }

        string normalizedDetail = NormalizeExtinguishActivityMessage(stage, detail);
        activityDebug.LogExtinguish(this, enableActivityDebug, normalizedDetail);
    }

    private void LogRescueActivity(string key, string detail)
    {
        activityDebug?.LogRescue(this, enableActivityDebug, key, detail);
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
        string normalizedDetail = NormalizePathClearingFlowMessage(key, detail);
        activityDebug?.LogPathFlow(this, enableActivityDebug, key, normalizedDetail);
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
                ? $"Received Move order to {detail.Substring(vectorIndex)}"
                : "Received Move order.";
        }

        if (key.StartsWith("move-start:") || key.StartsWith("move-breaktool:"))
        {
            return "Moving.";
        }

        if (key.StartsWith("sensor-blocker"))
        {
            return "Blocker detected.";
        }

        if (key.StartsWith("candidate-breaktool:"))
        {
            string toolName = TryGetFlowKeyName(key, "candidate-breaktool:");
            return string.IsNullOrEmpty(toolName) ? "Searching for breaching tool." : $"Searching for {toolName}.";
        }

        if (key.StartsWith("discard-breaktool:") || key.StartsWith("discard-breaktool-route:") || key.StartsWith("discard-breaktool-move:"))
        {
            string toolName =
                TryGetFlowKeyName(key, "discard-breaktool:") ??
                TryGetFlowKeyName(key, "discard-breaktool-route:") ??
                TryGetFlowKeyName(key, "discard-breaktool-move:");
            return string.IsNullOrEmpty(toolName) ? "Discarding breaching tool." : $"Discarding {toolName}.";
        }

        if (key.StartsWith("retry-breaktool"))
        {
            return "Searching for another breaching tool.";
        }

        if (key.StartsWith("no-break-tool:"))
        {
            return "No usable tool available.";
        }

        if (key.StartsWith("stop-breaktool:"))
        {
            return "Stopped.";
        }

        return detail;
    }




    private static string TryGetFlowKeyName(string key, string prefix)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(prefix) || !key.StartsWith(prefix))
        {
            return null;
        }

        int nameStartIndex = prefix.Length;
        int nameEndIndex = key.IndexOf(':', nameStartIndex);
        if (nameEndIndex < 0)
        {
            return key.Substring(nameStartIndex);
        }

        return key.Substring(nameStartIndex, nameEndIndex - nameStartIndex);
    }

    private void UpdatePathClearingDebugStage(PathClearingDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (activityDebug == null || !activityDebug.TryUpdatePathClearingStage(stageValue))
        {
            return;
        }
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

        if (behaviorContext != null && behaviorContext.HasRescueOrder && currentRescueTarget != null)
        {
            Vector3 rescuePosition = currentRescueTarget.GetWorldPosition();
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, rescuePosition);
            Gizmos.DrawWireSphere(rescuePosition, 0.2f);

            if (currentSafeZoneTarget != null)
            {
                Vector3 safeZonePosition = currentSafeZoneTarget.GetWorldPosition();
                Gizmos.color = Color.white;
                Gizmos.DrawLine(rescuePosition, safeZonePosition);
                Gizmos.DrawWireSphere(safeZonePosition, 0.25f);
            }
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
