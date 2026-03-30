using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using TrueJourney.BotBehavior;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public partial class BotCommandAgent : MonoBehaviour, ICommandable, IInteractable
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
    [SerializeField] private MultiAimConstraint headAimConstraint;
    [SerializeField] private RigBuilder headAimRigBuilder;

    [Header("Navigation")]
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private float turnSpeed = 360f;

    [Header("Aim")]
    [SerializeField] private float pitchTurnSpeed = 180f;
    [SerializeField] private float minPitchAngle = -45f;
    [SerializeField] private float maxPitchAngle = 60f;
    [SerializeField] private float settleFacingThreshold = 0.96f;
    [SerializeField] private bool enableHeadAim = true;
    [SerializeField] private float headAimDefaultDistance = 4f;
    [SerializeField] private float headAimVerticalOffset = 0.9f;
    [SerializeField] private float headAimTargetLerpSpeed = 12f;

    [Header("Extinguish")]
    [SerializeField] private float toolSearchRadius = 30f;
    [SerializeField] private float fireSearchRadius = 12f;
    [SerializeField] private float pickupDistance = 1.5f;
    [SerializeField] private float sprayFacingThreshold = 0.9f;
    [SerializeField] private float sprayStartDelay = 0.3f;
    [SerializeField] private bool crouchBeforeFireHoseSpray = true;
    [SerializeField] private float fireHoseCrouchDelay = 0.35f;
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
    [SerializeField] private BotFollowMode defaultFollowMode = BotFollowMode.Passive;
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float followRepathDistance = 0.75f;
    [SerializeField] private float followCatchupDistance = 4f;
    [SerializeField] private float followResumeDistanceBuffer = 0.5f;
    [SerializeField] private Vector3 escortFollowOffset = new Vector3(1.25f, 0f, -1.5f);
    [SerializeField] private float escortSlotPreferenceBias = 0.9f;
    [SerializeField] private bool followAllowAssist;

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
    private int currentEscortSlotIndex = -1;
    private readonly Vector3[] escortSlotOffsets = new Vector3[5];
    private readonly int[] occupiedEscortSlotIndices = new int[5];
    private BotActivityDebug activityDebug;
    private bool extinguishStartupPending;
    private float sprayReadyTime = -1f;
    private float crouchReadyTime = -1f;
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
    private Transform runtimeHeadAimTarget;
    private bool headAimConstraintConfigured;
    private Vector3 currentHeadAimWorldPosition;
    private bool headAimWorldPositionInitialized;
    private bool headAimFocusActive;
    private Vector3 headAimFocusWorldPosition;

    public Vector3 LastIssuedDestination => lastIssuedDestination;
    public bool HasIssuedDestination => hasIssuedDestination;
    public float RescueSearchRadius => rescueSearchRadius;
    public bool HasActiveFollowCommand => behaviorContext != null && behaviorContext.HasFollowOrder;
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
        ResolveHeadAimReferences();
        EnsureHeadAimConstraintConfigured(true);
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterCommandAgent(this);
        BotOutlineVisibilityManager.ApplyTo(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterCommandAgent(this);
    }

    private void LateUpdate()
    {
        ResolveViewPointReference();
        ResolveHeadAimReferences();
        UpdateHeadAimTarget();
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

                if (behaviorContext.HasFollowOrder)
                {
                    CancelFollowCommand();
                    accepted = true;
                    break;
                }

                PrepareForIssuedCommand(BotCommandType.Follow);
                behaviorContext.SetFollowOrder(CreateFollowOrder());
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

        if (commandType != BotCommandType.Follow)
        {
            followTarget = null;
            lastFollowDestination = Vector3.zero;
            currentEscortSlotIndex = -1;
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

    internal int CurrentEscortSlotIndex
    {
        get => currentEscortSlotIndex;
        set => currentEscortSlotIndex = value;
    }

    internal Vector3[] EscortSlotOffsets => escortSlotOffsets;
    internal int[] OccupiedEscortSlotIndices => occupiedEscortSlotIndices;

    internal IRescuableTarget CurrentRescueTarget
    {
        get => currentRescueTarget;
        set => currentRescueTarget = value;
    }

    internal bool TryGetFollowOrderSnapshot(out BotFollowOrder followOrder)
    {
        if (behaviorContext == null)
        {
            followOrder = default;
            return false;
        }

        return behaviorContext.TryGetFollowOrder(out followOrder);
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

    private void ProcessFollowOrder()
    {
        if (behaviorContext == null || !behaviorContext.TryGetFollowOrder(out BotFollowOrder followOrder))
        {
            return;
        }

        followController?.Tick(
            this,
            navMeshAgent,
            navMeshSampleDistance,
            followOrder,
            followRepathDistance,
            followCatchupDistance,
            followResumeDistanceBuffer,
            escortSlotPreferenceBias);
    }

    private void CancelFollowCommand()
    {
        if (behaviorContext == null)
        {
            return;
        }

        behaviorContext.ClearFollowOrder();
        followTarget = null;
        lastFollowDestination = Vector3.zero;
        currentEscortSlotIndex = -1;
        ClearBlockedPathRuntime();
        ClearRouteFireRuntime();
        ResetMoveActivityDebug();
        ResetViewPointPitch();

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }
    }

    private BotFollowOrder CreateFollowOrder()
    {
        Transform initialTarget = runtimeDecisionService != null
            ? runtimeDecisionService.ResolveFollowTarget(null, followTargetTag)
            : null;
        Vector3 localOffset = defaultFollowMode == BotFollowMode.Escort
            ? escortFollowOffset
            : Vector3.zero;
        return new BotFollowOrder(
            initialTarget,
            followTargetTag,
            defaultFollowMode,
            followDistance,
            localOffset,
            followAllowAssist);
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

}
