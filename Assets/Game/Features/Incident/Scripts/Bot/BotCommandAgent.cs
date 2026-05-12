using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using TrueJourney.BotBehavior;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BotEquippedItemPoseDriver))]
public partial class BotCommandAgent : MonoBehaviour, IIntentCommandable, IInteractable
{
    private const float MinExtinguisherStandOffDistance = 0.25f;
    private const float ExtinguisherRangeSlack = 0.1f;
    private const float ExtinguisherTargetStickinessRadiusSlack = 1.25f;

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

    private enum RouteFirePhase
    {
        Idle = 0,
        AcquireTool = 1,
        ReturnToFire = 2,
        Extinguish = 3
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private BotBehaviorContext behaviorContext;
    [SerializeField] private Transform viewPoint;
    [SerializeField] private Transform handAimTarget;
    [SerializeField] private MultiAimConstraint headAimConstraint;
    [SerializeField] private MultiAimConstraint spineAimConstraint;
    [SerializeField] private RigBuilder headAimRigBuilder;

    [Header("Navigation")]
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private bool enableManualOffMeshTraversal = true;
    [SerializeField] private float offMeshTraverseSpeed = 2.25f;
    [SerializeField] private float offMeshArrivalDistance = 0.05f;
    [SerializeField] private bool applyCarryWeightSpeedPenalty = true;
    [SerializeField] private float carryWeightForMinimumSpeed = 80f;
    [SerializeField, Range(0.05f, 1f)] private float minimumCarrySpeedMultiplier = 0.45f;
    [SerializeField, Range(0f, 1f)] private float carryOffMeshPenaltyScale = 0.85f;

    [Header("Aim")]
    [SerializeField] private float handAimDefaultDistance = 4f;
    [SerializeField] private float handAimVerticalOffset = 0.9f;
    [SerializeField] private float handAimTargetLerpSpeed = 12f;
    [SerializeField] private bool enableHeadAim = true;
    [SerializeField] private bool enableSpineAim = true;
    [SerializeField] private float headAimVerticalOffset = 0.9f;
    [SerializeField] private float headAimTargetLerpSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float defaultSpineAimMaxWeight = 0.3f;
    [SerializeField] private float spineAimWeightLerpSpeed = 12f;

    [Header("Extinguish")]
    [SerializeField] private float toolSearchRadius = 30f;
    [SerializeField] private float fireSearchRadius = 12f;
    [SerializeField] private float pickupDistance = 1.5f;
    [SerializeField] private Vector3 bulkyToolDropOffset = new Vector3(0.65f, 0f, 0.75f);
    [SerializeField] private float bulkyToolDropGroundProbeDistance = 2f;
    [SerializeField] private float sprayFacingThreshold = 0.9f;
    [SerializeField] private float sprayStartDelay = 0.3f;
    [SerializeField] private bool crouchBeforeFireHoseSpray = true;
    [SerializeField] private float fireHoseCrouchDelay = 0.35f;
    [SerializeField] private float extinguisherRouteCorridorWidth = 3f;
    [SerializeField] private float extinguisherApproachRetargetDistance = 0.75f;
    [SerializeField] private float blockedExtinguishToolRetryDelay = 1f;
    [SerializeField] private float pointFireApproachSearchRadius = 8f;
    [SerializeField] private float pointFireApproachSampleStep = 1.5f;
    [SerializeField] private int pointFireApproachDirections = 12;
    [SerializeField] private float pointFireApproachHeightWeight = 1.5f;

    [Header("Route Fire")]
    [SerializeField] private bool enableRouteFireClearing = true;
    [SerializeField] private float routeFireDetectionRadius = 4.5f;
    [SerializeField] private float routeFireVerticalTolerance = 2f;

    [Header("Path Clearing")]
    [SerializeField] private bool enablePathClearing = true;
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
    [SerializeField] private float followTargetLossTimeout = 1.5f;
    [SerializeField] private bool cancelFollowWhenTargetIsLost = true;
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

    [Header("Hazard Isolation")]
    [SerializeField] private float hazardIsolationSearchRadius = 6f;
    [SerializeField] private float hazardIsolationInteractionDistance = 1.75f;
    [SerializeField] private float hazardIsolationUnavailableTimeout = 1.5f;
    [SerializeField] private int hazardIsolationMaxUnavailableRetries = 1;

    [Header("Breach")]
    [SerializeField] private float breachSearchRadius = 6f;
    [SerializeField] private float breachInteractionDistance = 1.75f;

    [Header("Gizmos")]
    [SerializeField] private bool drawDestinationGizmo = true;
    [SerializeField] private bool drawAimGizmo = true;
    [SerializeField] private float aimGizmoLength = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool enableActivityDebug = false;
    [SerializeField] private bool showCommandPlanOverlay = false;
    [SerializeField] private Vector2 commandPlanOverlayScreenOffset = new Vector2(24f, -24f);
    [SerializeField] private float commandPlanOverlayWidth = 300f;
    [SerializeField, Range(1, 12)] private int commandPlanOverlayMaxQueuedTasks = 6;

    private Vector3 lastIssuedDestination;
    private bool hasIssuedDestination;
    private BotInventorySystem inventorySystem;
    private BotInteractionSensor interactionSensor;
    private BotPerceptionMemory perceptionMemory;
    private IBotExtinguisherItem activeExtinguisher;
    private IBotExtinguisherItem preferredExtinguishTool;
    private IBotExtinguisherItem committedExtinguishTool;
    private IBotExtinguisherItem temporarilyRejectedExtinguishTool;
    private IBotBreakTool activeBreakTool;
    private IBotBreakTool committedBreakTool;
    private IBotBreakTool temporarilyRejectedBreakTool;
    private IBotBreakableTarget currentBlockedBreakable;
    private IBotBreakableTarget temporarilyRejectedBreakable;
    private IFireGroupTarget currentRouteBlockingFireGroupTarget;
    private IFireTarget currentRouteBlockingFire;
    private RouteFirePhase currentRouteFirePhase;
    private IFireGroupTarget currentFireGroupTarget;
    private IFireTarget currentFireTarget;
    private IFireTarget commandedPointFireTarget;
    private IFireGroupTarget commandedFireGroupTarget;
    private IFireTarget lockedExtinguisherFireTarget;
    private float lockedExtinguisherFireRadius;
    private float lockedExtinguisherStandOffDistance;
    private bool lockedExtinguisherHasConfirmedLineOfSight;
    private IRescuableTarget currentRescueTarget;
    private ISafeZoneTarget currentSafeZoneTarget;
    private Vector3? claimedSlotPosition;
    private IBotPryTarget currentBreachPryTarget;
    private IBotPryTarget currentBlockedPryTarget;
    private IBotHazardIsolationTarget currentHazardIsolationTarget;
    private float cachedHazardIsolationStoppingDistance = -1f;
    private float hazardIsolationUnavailableSinceTime = -1f;
    private int hazardIsolationUnavailableRetryCount;
    private Vector3 currentExtinguishTargetPosition;
    private Vector3 currentExtinguishAimPoint;
    private Vector3 currentExtinguishLaunchDirection;
    private bool hasCurrentExtinguishTargetPosition;
    private bool hasCurrentExtinguishAimPoint;
    private bool hasCurrentExtinguishLaunchDirection;
    private readonly Vector3[] currentExtinguishTrajectoryPoints = new Vector3[24];
    private int currentExtinguishTrajectoryPointCount;
    private readonly Collider[] routeFireDetectionHits = new Collider[64];
    private Transform followTarget;
    private Vector3 lastFollowDestination;
    private int currentEscortSlotIndex = -1;
    private float followTargetLostSinceTime = -1f;
    private BotFollowOrder suspendedFollowOrder;
    private BotCommandType suspendedFollowCommandType;
    private bool hasSuspendedFollowResume;
    private readonly Vector3[] escortSlotOffsets = new Vector3[5];
    private readonly int[] occupiedEscortSlotIndices = new int[5];
    private BotActivityDebug activityDebug;
    private bool extinguishStartupPending;
    private float sprayReadyTime = -1f;
    private float crouchReadyTime = -1f;
    private float nextBreakUseTime;
    private float nextPathClearingRefreshTime;
    private float pathClearingResumeGraceUntilTime;
    private float temporarilyRejectedExtinguishToolUntilTime;
    private float temporarilyRejectedBreakToolUntilTime;
    private readonly List<string> commandPlanDebugLines = new List<string>(16);
    private readonly List<string> commandPlanPendingTaskNames = new List<string>(8);
    private Transform runtimeRescueCarryAnchor;
    private BotRuntimeDecisionService runtimeDecisionService;
    private BotPathClearingController pathClearingController;
    private BotMovePickupController movePickupController;
    private BotFollowController followController;
    private bool headAimConstraintConfigured;
    private bool spineAimConstraintConfigured;
    private Vector3 currentHandAimWorldPosition;
    private bool handAimWorldPositionInitialized;
    private bool handAimFocusActive;
    private Vector3 handAimFocusWorldPosition;
    private bool headAimFocusActive;
    private float baseNavMeshAgentSpeed;
    private float baseOffMeshTraverseSpeed;
    private bool movementSpeedDefaultsCached;

    public Vector3 LastIssuedDestination => lastIssuedDestination;
    public bool HasIssuedDestination => hasIssuedDestination;
    public float RescueSearchRadius => rescueSearchRadius;
    public bool HasActiveFollowCommand => behaviorContext != null && behaviorContext.HasFollowOrder;
    public bool IsPathClearingActive =>
        (enablePathClearing &&
        (
            Time.time < pathClearingResumeGraceUntilTime ||
            HasPendingCommittedBreakTool() ||
            (currentBlockedPryTarget != null && !currentBlockedPryTarget.IsBreached) ||
            (currentBlockedBreakable != null && !currentBlockedBreakable.IsBroken && currentBlockedBreakable.CanBeClearedByBot)
        )) ||
        IsRouteFireClearingActive();

    public bool HasMovePickupTarget => movePickupController != null && movePickupController.HasTarget;
    private IPickupable CurrentMovePickupTarget => movePickupController != null ? movePickupController.CurrentTarget : null;
    public bool IsAimingEquippedItemPose => IsExtinguisherAimPoseActive() || IsBreakToolAimPoseActive();
    public bool IsUsingEquippedItemPose => IsExtinguisherUsePoseActive();
    public float CurrentCarryWeightKg => ResolveCurrentCarryWeightKg();
    public bool IsCarryingRescueTarget => ResolveCarriedRescueTarget() != null;
    internal BotPerceptionMemory PerceptionMemory => perceptionMemory;

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
        perceptionMemory = GetComponent<BotPerceptionMemory>();
        runtimeDecisionService = new BotRuntimeDecisionService();
        navigationModule = new BotCommandNavigationModule(this);
        commandExecutionModule = new BotCommandExecutionModule(this);
        planProcessor = new BotPlanProcessor();
        pathClearingController = new BotPathClearingController(TryNavigateTo, ShouldRefreshPathClearingCheck);
        movePickupController = new BotMovePickupController();
        followController = new BotFollowController(runtimeDecisionService);
        activityDebug = new BotActivityDebug();
        ResolveViewPointReference();
        ResolveHandAimReference();
        ResolveHeadAimReferences();
        ResolveSpineAimReferences();
        EnsureHeadAimConstraintConfigured(true);
        EnsureSpineAimConstraintConfigured(true);
        CacheMovementSpeedDefaults();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterCommandAgent(this);
        BotOutlineVisibilityManager.ApplyTo(this);
    }

    private void OnDisable()
    {
        RestoreMovementSpeedDefaults();
        ResetCommandPlanProcessor();
        ReleaseAllTaskReservations();
        ClearCurrentTask();
        BotRuntimeRegistry.UnregisterCommandAgent(this);
    }

    private void LateUpdate()
    {
        ResolveViewPointReference();
        ResolveHandAimReference();
        ResolveHeadAimReferences();
        ResolveSpineAimReferences();
        UpdateHandAimTarget();
        UpdateHeadAimTarget();
        UpdateSpineAimTarget();
    }

    private void Update()
    {
        ResolveViewPointReference();
        ResolveHandAimReference();
        UpdateCarryMovementSpeed();
        if (TryTraverseOffMeshLink())
        {
            return;
        }

        if (behaviorContext == null)
        {
            RefreshTaskState();
            return;
        }

        if (behaviorContext.HasExtinguishOrder)
        {
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        if (behaviorContext.HasRescueOrder)
        {
            PrepareNonExtinguishCommandRuntime();
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        if ((currentRescueTarget != null || (activityDebug != null && activityDebug.HasRescueActivity)) &&
            !ShouldPreserveRescueRuntimeState())
        {
            ClearRescueRuntimeState();
        }

        if (behaviorContext.HasFollowOrder)
        {
            PrepareNonExtinguishCommandRuntime();
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        if (IsBreachCommandActive())
        {
            PrepareNonExtinguishCommandRuntime();
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        if (IsHazardIsolationCommandActive())
        {
            PrepareNonExtinguishCommandRuntime();
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        PrepareNonExtinguishCommandRuntime();

        if (HasMovePickupTarget &&
            (behaviorContext == null || !behaviorContext.UseMoveOrdersAsBehaviorInput))
        {
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        if (HasPlannerDrivenCommandIntent())
        {
            RefreshTaskState();
            RunActiveCommandPlan();
            return;
        }

        RefreshTaskState();

        if (!behaviorContext.HasMoveOrder)
        {
            ClearBlockedPathRuntime();
            ClearRouteFireRuntime();
        }
    }

    public bool CanAcceptCommand(BotCommandType commandType)
    {
        return commandExecutionModule != null && commandExecutionModule.CanAcceptCommand(commandType);
    }

    private bool IsExtinguisherAimPoseActive()
    {
        return HasActiveExtinguisherPoseRequest() && IsInteractionPoseStationary();
    }

    private bool IsExtinguisherUsePoseActive()
    {
        return HasActiveExtinguisherPoseRequest() &&
               sprayReadyTime >= 0f &&
               Time.time >= sprayReadyTime;
    }

    private bool IsBreakToolAimPoseActive()
    {
        return activeBreakTool != null &&
               currentBlockedBreakable != null &&
               !currentBlockedBreakable.IsBroken &&
               currentBlockedBreakable.CanBeClearedByBot &&
               IsInteractionPoseStationary();
    }

    private bool HasActiveExtinguisherPoseRequest()
    {
        return activeExtinguisher != null &&
               ((behaviorContext != null && behaviorContext.HasExtinguishOrder) || IsRouteFireClearingActive());
    }

    private bool IsInteractionPoseStationary()
    {
        return navMeshAgent == null ||
               !navMeshAgent.enabled ||
               !navMeshAgent.isOnNavMesh ||
               navMeshAgent.isStopped ||
               !navMeshAgent.hasPath;
    }

    public bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint)
    {
        return commandExecutionModule != null && commandExecutionModule.TryIssueCommand(commandType, worldPoint);
    }

    public bool CanAcceptCommandIntent(BotCommandIntentPayload payload)
    {
        return commandExecutionModule != null && commandExecutionModule.CanAcceptCommandIntent(payload);
    }

    public bool TryIssueCommandIntent(BotCommandIntentPayload payload)
    {
        return commandExecutionModule != null && commandExecutionModule.TryIssueCommandIntent(payload);
    }

    public bool TryIssueExtinguishCommand(
        Vector3 scanOrigin,
        BotExtinguishCommandMode mode,
        IFireTarget pointFireTarget = null,
        IFireGroupTarget fireGroupTarget = null)
    {
        return commandExecutionModule != null &&
               commandExecutionModule.TryIssueExtinguishCommand(scanOrigin, mode, pointFireTarget, fireGroupTarget);
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

    private void PrepareNonExtinguishCommandRuntime()
    {
        if ((behaviorContext == null || !behaviorContext.HasExtinguishOrder) &&
            !HasPlannerDrivenCommandIntent() &&
            !HasMovePickupTarget)
        {
            ResetCommandPlanProcessor();
        }

        if (!IsRouteFireClearingActive())
        {
            ClearHandAimFocus();
        }

        ClearInactiveTacticalCommandRuntime();
        if (IsRouteFireClearingActive() || activityDebug == null || !activityDebug.HasExtinguishDebugStage)
        {
            return;
        }

        ClearExtinguishRuntimeState();
    }

    private void ClearInactiveTacticalCommandRuntime()
    {
        if (!IsBreachCommandActive())
        {
            SetCurrentBreachPryTarget(null);
        }

        if (!IsHazardIsolationCommandActive())
        {
            ClearHazardIsolationRuntimeState();
        }
    }

    private void PrepareForIssuedCommand(BotCommandType commandType)
    {
        commandExecutionModule?.PrepareForIssuedCommand(commandType);
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
        set => SetCurrentRescueTarget(value);
    }

    internal IRescuableTarget CurrentCarriedRescueTarget => ResolveCarriedRescueTarget();

    internal bool TryGetFollowOrderSnapshot(out BotFollowOrder followOrder)
    {
        if (behaviorContext == null)
        {
            followOrder = default;
            return false;
        }

        return behaviorContext.TryGetFollowOrder(out followOrder);
    }

    private bool TrySuspendFollowIntoMove(Vector3 destination)
    {
        if (behaviorContext == null ||
            hasSuspendedFollowResume ||
            !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) ||
            !BotCommandTypeUtility.UsesFollowOrder(payload.CommandType) ||
            !behaviorContext.TryGetFollowOrder(out BotFollowOrder followOrder))
        {
            return false;
        }

        suspendedFollowOrder = followOrder;
        suspendedFollowCommandType = payload.CommandType;
        hasSuspendedFollowResume = true;
        behaviorContext.ClearFollowOrder();
        behaviorContext.SetMoveOrder(destination);
        lastIssuedDestination = destination;
        hasIssuedDestination = true;
        return true;
    }

    private void TryResumeSuspendedFollow()
    {
        if (!hasSuspendedFollowResume || behaviorContext == null)
        {
            return;
        }

        BotFollowOrder followOrder = suspendedFollowOrder;
        BotCommandType commandType = suspendedFollowCommandType;
        ClearSuspendedFollowResume();

        switch (commandType)
        {
            case BotCommandType.Assist:
                behaviorContext.SetAssistOrder(followOrder);
                break;
            case BotCommandType.Regroup:
                behaviorContext.SetRegroupOrder(followOrder);
                break;
            default:
                behaviorContext.SetFollowOrder(followOrder);
                break;
        }
    }

    private void ClearSuspendedFollowResume()
    {
        suspendedFollowOrder = default;
        suspendedFollowCommandType = BotCommandType.None;
        hasSuspendedFollowResume = false;
    }

    internal ISafeZoneTarget CurrentSafeZoneTarget
    {
        get => currentSafeZoneTarget;
        set => currentSafeZoneTarget = value;
    }

    internal Vector3? ClaimedSlotPosition
    {
        get => claimedSlotPosition;
        set => claimedSlotPosition = value;
    }

    internal bool MoveToCommand(Vector3 destination)
    {
        return MoveTo(destination);
    }

    internal bool MoveToRescueCarrySafeZoneCommand(Vector3 destination)
    {
        ClearBlockedPathRuntime();
        ClearRouteFireRuntime();
        return TrySetDestinationDirect(destination);
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

    internal void ClearFollowTargetLossState()
    {
        followTargetLostSinceTime = -1f;
    }

    internal bool ShouldCancelFollowAfterTargetLoss()
    {
        if (followTargetLostSinceTime < 0f)
        {
            followTargetLostSinceTime = Time.time;
        }

        if (!cancelFollowWhenTargetIsLost)
        {
            return false;
        }

        return Time.time - followTargetLostSinceTime >= Mathf.Max(0f, followTargetLossTimeout);
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

    internal void PrepareCarryRescueCommand()
    {
        UnequipCurrentToolsForCarry();
    }

    public void SetMovePickupTarget(IPickupable target)
    {
        movePickupController?.SetTarget(target);
    }

    public bool TryCompleteMovePickupTarget()
    {
        return movePickupController != null && movePickupController.TryCompleteMovePickupTarget(CreateMovePickupOptions());
    }

    internal bool TryStartPlanMovePickupTarget(IPickupable pickupTarget, bool allowBehaviorMoveOrders = true)
    {
        return movePickupController != null &&
               movePickupController.TryIssueMoveToPickup(pickupTarget, CreateMovePickupOptions(false, allowBehaviorMoveOrders), out _);
    }

    private BotMovePickupOptions CreateMovePickupOptions(bool prepareIssuedCommand = true, bool allowBehaviorMoveOrders = true)
    {
        return new BotMovePickupOptions
        {
            BotTransform = transform,
            NavMeshAgent = navMeshAgent,
            BehaviorContext = allowBehaviorMoveOrders ? behaviorContext : null,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            NavMeshSampleDistance = navMeshSampleDistance,
            PrepareForIssuedCommand = prepareIssuedCommand ? PrepareForIssuedCommand : null,
            LogPathFlow = LogPathClearingFlow,
            GetPickupableName = GetPickupableName,
            MoveToDestination = MoveTo,
            ShouldRefreshPathCheck = ShouldRefreshPathClearingCheckCommand,
            SetPickupWindow = SetPickupWindow,
            TryEnsureExtinguisherEquipped = tool => TryEnsureExtinguisherEquipped(tool, false),
            TryEnsureBreakToolEquipped = TryEnsureBreakToolEquipped
        };
    }

    private void ProcessFollowOrder()
    {
        if (HasActiveTacticalMovementInterrupt())
        {
            return;
        }

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

    private bool HasActiveTacticalMovementInterrupt()
    {
        return HasMovePickupTarget ||
               IsRouteFireClearingActive() ||
               (currentBlockedBreakable != null &&
                !currentBlockedBreakable.IsBroken &&
                currentBlockedBreakable.CanBeClearedByBot);
    }

    private void CancelFollowCommand()
    {
        if (behaviorContext == null)
        {
            return;
        }

        behaviorContext.ClearFollowOrder();
        ClearSuspendedFollowResume();
        ClearFollowTargetLossState();
        followTarget = null;
        lastFollowDestination = Vector3.zero;
        currentEscortSlotIndex = -1;
        ClearBlockedPathRuntime();
        ClearRouteFireRuntime();
        ResetMoveActivityDebug();

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }
    }

    public bool TryCancelFollowCommand()
    {
        if (!HasActiveFollowCommand)
        {
            return false;
        }

        CancelFollowCommand();
        return true;
    }

    private BotFollowOrder CreateFollowOrder(BotCommandType commandType = BotCommandType.Follow)
    {
        ClearFollowTargetLossState();
        Transform initialTarget = runtimeDecisionService != null
            ? runtimeDecisionService.ResolveFollowTarget(null, followTargetTag, perceptionMemory)
            : null;
        BotFollowMode followMode = commandType == BotCommandType.Regroup
            ? BotFollowMode.Escort
            : defaultFollowMode;
        bool allowAssist = commandType == BotCommandType.Follow ||
                           commandType == BotCommandType.Assist ||
                           commandType == BotCommandType.Regroup ||
                           followAllowAssist;
        Vector3 localOffset = followMode == BotFollowMode.Escort
            ? escortFollowOffset
            : Vector3.zero;
        return new BotFollowOrder(
            initialTarget,
            followTargetTag,
            followMode,
            followDistance,
            localOffset,
            allowAssist);
    }

}
