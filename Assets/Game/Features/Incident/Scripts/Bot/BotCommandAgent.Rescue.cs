using UnityEngine;
using TrueJourney.BotBehavior;
using System.Collections.Generic;

public partial class BotCommandAgent
{
    private enum BotRescueV2Step
    {
        None = 0,
        ResolveTarget = 1,
        ResolveSafeZone = 2,
        IssueMoveToTarget = 3,
        WaitReachTarget = 4,
        BeginStabilizeTarget = 5,
        WaitStabilizeTarget = 6,
        BeginPickupTarget = 7,
        WaitPickupTarget = 8,
        AcquireDropPoint = 9,
        IssueMoveToSafeZone = 10,
        WaitReachSafeZone = 11,
        DropOffTarget = 12,
        Complete = 13,
        Failed = 14
    }

    private enum BotRescueV2PauseReason
    {
        None = 0,
        BlockedPath = 1,
        RouteFire = 2
    }

    private sealed class BotRescueV2State
    {
        public BotRescueV2Step Step;
        public bool IsPaused;
        public BotRescueV2PauseReason PauseReason;
        public string PauseDetail;
        public float PausedAtTime;
        public Vector3 OrderPoint;
        public IRescuableTarget RescueTarget;
        public ISafeZoneTarget SafeZoneTarget;
        public bool HasReservedDropPoint;
        public Vector3 ReservedDropPoint;
        public bool HasIssuedTargetMove;
        public Vector3 IssuedTargetDestination;
        public bool HasIssuedSafeZoneMove;
        public Vector3 IssuedSafeZoneDestination;
        public float StepReadyTime;
        public string FailureReason;

        public void Reset()
        {
            Step = BotRescueV2Step.None;
            IsPaused = false;
            PauseReason = BotRescueV2PauseReason.None;
            PauseDetail = string.Empty;
            PausedAtTime = 0f;
            OrderPoint = default;
            RescueTarget = null;
            SafeZoneTarget = null;
            HasReservedDropPoint = false;
            ReservedDropPoint = default;
            HasIssuedTargetMove = false;
            IssuedTargetDestination = default;
            HasIssuedSafeZoneMove = false;
            IssuedSafeZoneDestination = default;
            StepReadyTime = 0f;
            FailureReason = string.Empty;
        }
    }

    [Header("Rescue Task Flow")]
    [SerializeField] private BotRescueSubtask currentRescueSubtask;
    [SerializeField] private string rescueTaskDetail = "Awaiting rescue assignment.";
    [SerializeField] private string lastRescueFailureReason;
    [SerializeField] private float rescueSubtaskStartedAtTime;
    private readonly BotRescueV2State rescueV2State = new BotRescueV2State();

    private bool IsRescueV2Active => rescueV2State.Step != BotRescueV2Step.None;

    internal void AbortActiveRescueOrder()
    {
        lastRescueFailureReason = "Rescue order aborted.";
        FailCurrentTask(lastRescueFailureReason, BotTaskStatus.Blocked);
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
        lastRescueFailureReason = string.Empty;
        SetRescueSubtask(BotRescueSubtask.CompleteRescue, "Rescue completed.");
        CompleteCurrentTask("Rescue order completed.");
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

    private void ClearRescueRuntimeState()
    {
        rescueV2State.Reset();
        SetCurrentRescueTarget(null);
        if (currentSafeZoneTarget != null)
        {
            currentSafeZoneTarget.ReleaseSlot(gameObject);
        }
        currentSafeZoneTarget = null;
        claimedSlotPosition = null;
        currentRescueSubtask = BotRescueSubtask.None;
        rescueTaskDetail = "Awaiting rescue assignment.";
        lastRescueFailureReason = string.Empty;
        rescueSubtaskStartedAtTime = 0f;
        activityDebug?.ResetRescue();
    }

    internal void SetRescueSubtask(BotRescueSubtask subtask, string detail)
    {
        if (currentRescueSubtask != subtask)
        {
            rescueSubtaskStartedAtTime = Application.isPlaying ? Time.time : 0f;
        }

        currentRescueSubtask = subtask;
        rescueTaskDetail = string.IsNullOrWhiteSpace(detail) ? "Executing rescue order." : detail;
    }

    internal void SetRescueFailureReason(string detail)
    {
        lastRescueFailureReason = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
    }

    internal string GetActiveRescueTaskDetail()
    {
        if (IsRescueV2Active)
        {
            return GetRescueV2TaskDetail();
        }

        if (!string.IsNullOrWhiteSpace(lastRescueFailureReason))
        {
            return lastRescueFailureReason;
        }

        return string.IsNullOrWhiteSpace(rescueTaskDetail)
            ? "Executing rescue order."
            : rescueTaskDetail;
    }

    internal void ReacquireRescueTarget(string detail)
    {
        SetCurrentRescueTarget(null);
        if (currentSafeZoneTarget != null)
        {
            currentSafeZoneTarget.ReleaseSlot(gameObject);
        }
        currentSafeZoneTarget = null;
        claimedSlotPosition = null;
        lastRescueFailureReason = string.Empty;
        SetRescueSubtask(BotRescueSubtask.Recover, detail);
    }

    internal void FailActiveRescueOrder(string detail, BotTaskStatus failureStatus = BotTaskStatus.Failed)
    {
        SetRescueFailureReason(detail);
        FailCurrentTask(detail, failureStatus);
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

    private void UnequipCurrentToolsForCarry()
    {
        List<IPickupable> toolsToUnequip = new List<IPickupable>(8);

        if (inventorySystem != null)
        {
            inventorySystem.ClearEquippedSelection();
        }

        CollectToolForUnequip(toolsToUnequip, activeExtinguisher as IPickupable);
        CollectToolForUnequip(toolsToUnequip, activeBreakTool as IPickupable);

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            if (extinguisher == null || extinguisher.CurrentHolder != gameObject)
            {
                continue;
            }

            CollectToolForUnequip(toolsToUnequip, extinguisher as IPickupable);
        }

        foreach (IBotBreakTool breakTool in BotRuntimeRegistry.ActiveBreakTools)
        {
            if (breakTool == null || !breakTool.IsHeldBy(gameObject))
            {
                continue;
            }

            CollectToolForUnequip(toolsToUnequip, breakTool as IPickupable);
        }

        for (int i = 0; i < toolsToUnequip.Count; i++)
        {
            ForceUnequipTool(toolsToUnequip[i]);
        }

        ClearExtinguishRuntimeState();
        ClearBlockedPathRuntime();
        activeExtinguisher = null;
        activeBreakTool = null;
    }

    private static void CollectToolForUnequip(List<IPickupable> toolsToUnequip, IPickupable pickupable)
    {
        if (toolsToUnequip == null || pickupable == null || toolsToUnequip.Contains(pickupable))
        {
            return;
        }

        toolsToUnequip.Add(pickupable);
    }

    private void ForceUnequipTool(IPickupable pickupable)
    {
        if (pickupable == null || inventorySystem == null)
        {
            return;
        }

        inventorySystem.ForceUnequipItem(pickupable);
    }

    private void BeginRescueV2Order(Vector3 orderPoint)
    {
        rescueV2State.Reset();
        rescueV2State.FailureReason = string.Empty;
        SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
        rescueV2State.OrderPoint = orderPoint;
    }

    private void TickRescueV2()
    {
        if (rescueV2State.IsPaused)
        {
            ApplyPausedRescueV2State();
            return;
        }

        if (Time.time < rescueV2State.StepReadyTime)
        {
            return;
        }

        if (TryPauseRescueV2ForInterrupts())
        {
            return;
        }

        switch (rescueV2State.Step)
        {
            case BotRescueV2Step.ResolveTarget:
                TryResolveRescueV2Target();
                break;
            case BotRescueV2Step.ResolveSafeZone:
                TryResolveRescueV2SafeZone();
                break;
            case BotRescueV2Step.IssueMoveToTarget:
                TryIssueRescueV2MoveToTarget();
                break;
            case BotRescueV2Step.WaitReachTarget:
                TryWaitRescueV2ReachTarget();
                break;
            case BotRescueV2Step.BeginStabilizeTarget:
                TryBeginRescueV2Stabilization();
                break;
            case BotRescueV2Step.WaitStabilizeTarget:
                TryWaitRescueV2Stabilization();
                break;
            case BotRescueV2Step.BeginPickupTarget:
                TryBeginRescueV2Pickup();
                break;
            case BotRescueV2Step.WaitPickupTarget:
                TryWaitRescueV2Pickup();
                break;
            case BotRescueV2Step.AcquireDropPoint:
                TryAcquireRescueV2DropPoint();
                break;
            case BotRescueV2Step.IssueMoveToSafeZone:
                TryIssueRescueV2MoveToSafeZone();
                break;
            case BotRescueV2Step.WaitReachSafeZone:
                TryWaitRescueV2ReachSafeZone();
                break;
            case BotRescueV2Step.DropOffTarget:
                TryCompleteRescueV2DropOff();
                break;
            case BotRescueV2Step.Complete:
                CompleteRescueV2();
                break;
            case BotRescueV2Step.Failed:
                FailRescueV2();
                break;
        }
    }

    private void TryResolveRescueV2Target()
    {
        rescueV2State.RescueTarget = runtimeDecisionService.ResolveRescueTarget(
            rescueV2State.OrderPoint,
            CurrentRescueTarget,
            gameObject,
            rescueSearchRadius);

        if (rescueV2State.RescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 could not resolve a rescue target.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueV2State.RescueTarget;

        if (!rescueV2State.RescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (rescueV2State.RescueTarget.IsCarried && rescueV2State.RescueTarget.ActiveRescuer != gameObject)
        {
            rescueV2State.FailureReason = "RescueV2 target is already being carried by another rescuer.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        SetRescueV2Step(BotRescueV2Step.ResolveSafeZone);
    }

    private void TryResolveRescueV2SafeZone()
    {
        Vector3 fromPosition = rescueV2State.RescueTarget != null
            ? rescueV2State.RescueTarget.GetWorldPosition()
            : rescueV2State.OrderPoint;
        rescueV2State.SafeZoneTarget = runtimeDecisionService.ResolveNearestSafeZone(fromPosition, CurrentSafeZoneTarget);

        if (rescueV2State.SafeZoneTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 could not resolve a safe zone.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentSafeZoneTarget = rescueV2State.SafeZoneTarget;
        SetRescueV2Step(BotRescueV2Step.IssueMoveToTarget);
    }

    private void TryIssueRescueV2MoveToTarget()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.AcquireDropPoint, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        if (IsWithinHorizontalDistance(targetPosition, rescueInteractionDistance))
        {
            StopAndAimTowards(targetPosition);
            SetRescueV2Step(
                rescueTarget.RequiresStabilization
                    ? BotRescueV2Step.BeginStabilizeTarget
                    : BotRescueV2Step.BeginPickupTarget,
                false);
            return;
        }

        rescueV2State.HasIssuedTargetMove = true;
        rescueV2State.IssuedTargetDestination = targetPosition;
        if (!MoveToCommand(targetPosition))
        {
            rescueV2State.FailureReason = "RescueV2 failed to path to casualty.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        SetRescueV2Step(BotRescueV2Step.WaitReachTarget, false);
    }

    private void TryWaitRescueV2ReachTarget()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target while moving.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.AcquireDropPoint, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        if (IsWithinHorizontalDistance(targetPosition, rescueInteractionDistance))
        {
            StopAndAimTowards(targetPosition);
            rescueV2State.HasIssuedTargetMove = false;
            SetRescueV2Step(
                rescueTarget.RequiresStabilization
                    ? BotRescueV2Step.BeginStabilizeTarget
                    : BotRescueV2Step.BeginPickupTarget,
                false);
            return;
        }

        if (!rescueV2State.HasIssuedTargetMove ||
            GetHorizontalDistance(rescueV2State.IssuedTargetDestination, targetPosition) > 0.75f)
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToTarget, false);
        }
    }

    private void TryBeginRescueV2Stabilization()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target before stabilization.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (!rescueTarget.RequiresStabilization)
        {
            SetRescueV2Step(BotRescueV2Step.BeginPickupTarget, false);
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        if (!IsWithinHorizontalDistance(targetPosition, rescueInteractionDistance))
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToTarget, false);
            return;
        }

        StopNavMeshMovement();
        AimTowardsPoint(targetPosition);

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.WaitStabilizeTarget, false);
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        if (rescueTarget.TryStabilize(gameObject))
        {
            SetRescueV2Step(BotRescueV2Step.WaitStabilizeTarget, false);
            return;
        }

        rescueV2State.FailureReason = "RescueV2 failed to start casualty stabilization.";
        SetRescueV2Step(BotRescueV2Step.Failed, false);
    }

    private void TryWaitRescueV2Stabilization()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target during stabilization.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (!rescueTarget.RequiresStabilization)
        {
            SetRescueV2Step(BotRescueV2Step.BeginPickupTarget, false);
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == gameObject)
        {
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        SetRescueV2Step(BotRescueV2Step.BeginStabilizeTarget, false);
    }

    private void TryBeginRescueV2Pickup()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target before pickup.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (rescueTarget.RequiresStabilization)
        {
            SetRescueV2Step(BotRescueV2Step.BeginStabilizeTarget, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.AcquireDropPoint, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        if (!IsWithinHorizontalDistance(targetPosition, rescueInteractionDistance))
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToTarget, false);
            return;
        }

        PrepareCarryRescueCommand();
        if (rescueTarget.TryBeginCarry(gameObject, EnsureRescueCarryAnchor()))
        {
            PrepareCarryRescueCommand();
            SetRescueV2Step(BotRescueV2Step.WaitPickupTarget, false);
            return;
        }

        rescueV2State.FailureReason = "RescueV2 failed to begin carrying casualty.";
        SetRescueV2Step(BotRescueV2Step.Failed, false);
    }

    private void TryWaitRescueV2Pickup()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target during pickup.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;

        if (!rescueTarget.NeedsRescue)
        {
            SetRescueV2Step(BotRescueV2Step.Complete, false);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.AcquireDropPoint, false);
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == gameObject)
        {
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
        }

        SetRescueV2Step(BotRescueV2Step.BeginPickupTarget, false);
    }

    private void TryAcquireRescueV2DropPoint()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        ISafeZoneTarget safeZone = rescueV2State.SafeZoneTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target before acquiring a drop point.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        if (!rescueTarget.IsCarried || rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        if (safeZone == null)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveSafeZone, false);
            return;
        }

        CurrentSafeZoneTarget = safeZone;
        if (safeZone.TryClaimSlot(gameObject, out Vector3 slotPosition))
        {
            rescueV2State.HasReservedDropPoint = true;
            rescueV2State.ReservedDropPoint = slotPosition;
            ClaimedSlotPosition = slotPosition;
        }
        else
        {
            Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
            rescueV2State.HasReservedDropPoint = true;
            rescueV2State.ReservedDropPoint = safeZone.GetDropPoint(fallbackDropPosition);
            ClaimedSlotPosition = null;
        }

        SetRescueV2Step(BotRescueV2Step.IssueMoveToSafeZone, false);
    }

    private void TryIssueRescueV2MoveToSafeZone()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        ISafeZoneTarget safeZone = rescueV2State.SafeZoneTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target while moving to safe zone.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        if (!rescueTarget.IsCarried || rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        if (safeZone == null)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveSafeZone, false);
            return;
        }

        CurrentRescueTarget = rescueTarget;
        CurrentSafeZoneTarget = safeZone;
        PrepareCarryRescueCommand();

        Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
        Vector3 dropPosition = rescueV2State.HasReservedDropPoint
            ? rescueV2State.ReservedDropPoint
            : safeZone.GetDropPoint(fallbackDropPosition);

        if (IsWithinHorizontalDistance(dropPosition, rescueSafeZoneArrivalDistance))
        {
            StopNavMeshMovement();
            SetRescueV2Step(BotRescueV2Step.DropOffTarget, false);
            return;
        }

        rescueV2State.HasIssuedSafeZoneMove = true;
        rescueV2State.IssuedSafeZoneDestination = dropPosition;
        if (!MoveToRescueCarrySafeZoneCommand(dropPosition))
        {
            rescueV2State.FailureReason = "RescueV2 failed to path to rescue safe zone.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        SetRescueV2Step(BotRescueV2Step.WaitReachSafeZone, false);
    }

    private void TryWaitRescueV2ReachSafeZone()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        ISafeZoneTarget safeZone = rescueV2State.SafeZoneTarget;
        if (rescueTarget == null)
        {
            rescueV2State.FailureReason = "RescueV2 lost its rescue target while approaching the safe zone.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        if (!rescueTarget.IsCarried || rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        if (safeZone == null)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveSafeZone, false);
            return;
        }

        Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
        Vector3 dropPosition = rescueV2State.HasReservedDropPoint
            ? rescueV2State.ReservedDropPoint
            : safeZone.GetDropPoint(fallbackDropPosition);

        if (IsWithinHorizontalDistance(dropPosition, rescueSafeZoneArrivalDistance))
        {
            StopNavMeshMovement();
            rescueV2State.HasIssuedSafeZoneMove = false;
            SetRescueV2Step(BotRescueV2Step.DropOffTarget, false);
            return;
        }

        if (!rescueV2State.HasIssuedSafeZoneMove ||
            GetHorizontalDistance(rescueV2State.IssuedSafeZoneDestination, dropPosition) > 0.75f)
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToSafeZone, false);
        }
    }

    private void TryCompleteRescueV2DropOff()
    {
        IRescuableTarget rescueTarget = rescueV2State.RescueTarget;
        ISafeZoneTarget safeZone = rescueV2State.SafeZoneTarget;
        if (rescueTarget == null || safeZone == null)
        {
            rescueV2State.FailureReason = "RescueV2 cannot complete drop-off without a casualty and safe zone.";
            SetRescueV2Step(BotRescueV2Step.Failed, false);
            return;
        }

        if (!rescueTarget.IsCarried || rescueTarget.ActiveRescuer != gameObject)
        {
            SetRescueV2Step(BotRescueV2Step.ResolveTarget, false);
            return;
        }

        Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
        Vector3 dropPosition = rescueV2State.HasReservedDropPoint
            ? rescueV2State.ReservedDropPoint
            : safeZone.GetDropPoint(fallbackDropPosition);

        if (!IsWithinHorizontalDistance(dropPosition, rescueSafeZoneArrivalDistance))
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToSafeZone, false);
            return;
        }

        rescueTarget.CompleteRescueAt(dropPosition, safeZone.GetSlotRotation(dropPosition));
        safeZone.OccupySlotAt(dropPosition);
        safeZone.ReleaseSlot(gameObject);
        ClaimedSlotPosition = null;
        rescueV2State.HasReservedDropPoint = false;
        rescueV2State.HasIssuedSafeZoneMove = false;
        SetRescueV2Step(BotRescueV2Step.Complete, false);
    }

    private void CompleteRescueV2()
    {
        rescueV2State.Reset();
        LogRescueActivityMessage("rescuev2-complete", "RescueV2 completed.");
        CompleteActiveRescueOrder();
    }

    private void FailRescueV2()
    {
        string failureReason = string.IsNullOrWhiteSpace(rescueV2State.FailureReason)
            ? "RescueV2 failed."
            : rescueV2State.FailureReason;
        rescueV2State.Reset();
        LogRescueActivityMessage("rescuev2-failed", failureReason);
        FailActiveRescueOrder(failureReason, BotTaskStatus.Failed);
    }

    private bool TryPauseRescueV2(BotRescueV2PauseReason reason, string detail = null)
    {
        if (!IsRescueV2Active || rescueV2State.IsPaused)
        {
            return false;
        }

        PauseRescueV2(reason, detail);
        return true;
    }

    private bool TryResumeRescueV2()
    {
        if (!IsRescueV2Active || !rescueV2State.IsPaused)
        {
            return false;
        }

        ResumeRescueV2();
        return true;
    }

    private bool TryPauseRescueV2ForInterrupts()
    {
        if (!TryDetectRescueV2PauseReason(
            out BotRescueV2PauseReason reason,
            out string detail,
            out IBotBreakableTarget blockedBreakable,
            out IBotPryTarget blockedPryTarget,
            out IFireTarget routeFireTarget,
            out IFireGroupTarget routeFireGroup,
            out Vector3 pauseDestination))
        {
            return false;
        }

        if (!TryPauseRescueV2(reason, detail))
        {
            return false;
        }

        if (reason == BotRescueV2PauseReason.BlockedPath)
        {
            BeginPathClearingV2(
                BotPathClearingV2Caller.RescueV2,
                pauseDestination,
                blockedBreakable,
                blockedPryTarget);
        }
        else if (reason == BotRescueV2PauseReason.RouteFire)
        {
            BeginRouteFireV2(
                BotRouteFireV2Caller.RescueV2,
                pauseDestination,
                routeFireTarget,
                routeFireGroup);
        }

        return true;
    }

    private bool TryDetectRescueV2PauseReason(
        out BotRescueV2PauseReason reason,
        out string detail,
        out IBotBreakableTarget blockedBreakable,
        out IBotPryTarget blockedPryTarget,
        out IFireTarget routeFireTarget,
        out IFireGroupTarget routeFireGroup,
        out Vector3 pauseDestination)
    {
        if (TryDetectRescueV2BlockedPathPause(
            out detail,
            out blockedBreakable,
            out blockedPryTarget,
            out pauseDestination))
        {
            reason = BotRescueV2PauseReason.BlockedPath;
            routeFireTarget = null;
            routeFireGroup = null;
            return true;
        }

        if (TryDetectRescueV2RouteFirePause(
            out detail,
            out routeFireTarget,
            out routeFireGroup,
            out pauseDestination))
        {
            reason = BotRescueV2PauseReason.RouteFire;
            blockedBreakable = null;
            blockedPryTarget = null;
            return true;
        }

        reason = BotRescueV2PauseReason.None;
        blockedBreakable = null;
        blockedPryTarget = null;
        routeFireTarget = null;
        routeFireGroup = null;
        detail = null;
        pauseDestination = default;
        return false;
    }

    private bool TryDetectRescueV2BlockedPathPause(
        out string detail,
        out IBotBreakableTarget blockedBreakable,
        out IBotPryTarget blockedPryTarget,
        out Vector3 pauseDestination)
    {
        detail = null;
        blockedBreakable = null;
        blockedPryTarget = null;
        pauseDestination = default;
        if (!enablePathClearing ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            !ShouldRefreshPathClearingCheckCommand() ||
            !TryGetRescueV2PauseMovementDestination(out Vector3 destination))
        {
            return false;
        }

        pauseDestination = destination;

        if (currentBlockedPryTarget != null &&
            !currentBlockedPryTarget.IsBreached &&
            (currentBlockedPryTarget.CanBePriedOpen || currentBlockedPryTarget.IsPryInProgress))
        {
            blockedPryTarget = currentBlockedPryTarget;
            detail = $"Pry target '{GetDebugTargetName(currentBlockedPryTarget)}' blocks the rescue route.";
            return true;
        }

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            blockedBreakable = currentBlockedBreakable;
            detail = $"Breakable '{GetDebugTargetName(currentBlockedBreakable)}' blocks the rescue route.";
            return true;
        }

        if (TryResolveBlockedPryTarget(destination, out IBotPryTarget pryTarget))
        {
            blockedPryTarget = pryTarget;
            detail = $"Pry target '{GetDebugTargetName(pryTarget)}' blocks the rescue route.";
            return true;
        }

        if (TryResolveBlockedBreakable(destination, out IBotBreakableTarget breakableTarget))
        {
            blockedBreakable = breakableTarget;
            detail = $"Breakable '{GetDebugTargetName(breakableTarget)}' blocks the rescue route.";
            return true;
        }

        return false;
    }

    private bool TryDetectRescueV2RouteFirePause(
        out string detail,
        out IFireTarget routeFireTarget,
        out IFireGroupTarget routeFireGroup,
        out Vector3 pauseDestination)
    {
        detail = null;
        routeFireTarget = null;
        routeFireGroup = null;
        pauseDestination = default;
        if (!enableRouteFireClearing ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            !TryGetRescueV2PauseMovementDestination(out Vector3 destination))
        {
            return false;
        }

        pauseDestination = destination;
        if (!TryResolveNearbyRouteFire(out IFireTarget detectedBlockingFire))
        {
            return false;
        }

        routeFireTarget = detectedBlockingFire;
        routeFireGroup = FindClosestActiveFireGroup(detectedBlockingFire.GetWorldPosition());
        detail = $"Fire '{GetDebugTargetName(detectedBlockingFire)}' blocks the rescue route.";
        return true;
    }

    private bool TryGetRescueV2PauseMovementDestination(out Vector3 destination)
    {
        destination = default;
        switch (rescueV2State.Step)
        {
            case BotRescueV2Step.IssueMoveToTarget:
            case BotRescueV2Step.WaitReachTarget:
                destination = rescueV2State.HasIssuedTargetMove
                    ? rescueV2State.IssuedTargetDestination
                    : rescueV2State.RescueTarget != null
                        ? rescueV2State.RescueTarget.GetWorldPosition()
                        : default;
                return rescueV2State.RescueTarget != null;
            default:
                return false;
        }
    }

    private void PauseRescueV2(BotRescueV2PauseReason reason, string detail)
    {
        if (!IsRescueV2Active || rescueV2State.IsPaused)
        {
            return;
        }

        rescueV2State.IsPaused = true;
        rescueV2State.PauseReason = reason;
        rescueV2State.PauseDetail = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
        rescueV2State.PausedAtTime = Time.time;
        ApplyPausedRescueV2State();
        LogRescueActivityMessage("rescuev2-paused", GetRescueV2PauseSummary());
    }

    private void ResumeRescueV2()
    {
        if (!rescueV2State.IsPaused)
        {
            return;
        }

        BotRescueV2PauseReason resumeReason = rescueV2State.PauseReason;
        RestartRescueV2FromPausedState();
        rescueV2State.IsPaused = false;
        rescueV2State.PauseReason = BotRescueV2PauseReason.None;
        rescueV2State.PauseDetail = string.Empty;
        rescueV2State.PausedAtTime = 0f;
        LogRescueActivityMessage(
            "rescuev2-resumed",
            resumeReason switch
            {
                BotRescueV2PauseReason.BlockedPath => "RescueV2 resumed after blocked-path clearing.",
                BotRescueV2PauseReason.RouteFire => "RescueV2 resumed after route-fire clearing.",
                _ => "RescueV2 resumed."
            });
    }

    private void ApplyPausedRescueV2State()
    {
        StopNavMeshMovement();
        ClearHandAimFocus();
        ClearHeadAimFocus();
    }

    private string GetRescueV2PauseSummary()
    {
        string label = rescueV2State.PauseReason switch
        {
            BotRescueV2PauseReason.BlockedPath => "Blocked path",
            BotRescueV2PauseReason.RouteFire => "Route fire",
            _ => "Paused"
        };

        return string.IsNullOrWhiteSpace(rescueV2State.PauseDetail)
            ? label
            : $"{label}: {rescueV2State.PauseDetail}";
    }

    private void RestartRescueV2FromPausedState()
    {
        BotRescueV2Step resumeStep = rescueV2State.Step;
        if (resumeStep == BotRescueV2Step.IssueMoveToTarget || resumeStep == BotRescueV2Step.WaitReachTarget)
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToTarget, false);
            return;
        }

        if (resumeStep == BotRescueV2Step.IssueMoveToSafeZone || resumeStep == BotRescueV2Step.WaitReachSafeZone)
        {
            SetRescueV2Step(BotRescueV2Step.IssueMoveToSafeZone, false);
            return;
        }

        SetRescueV2Step(resumeStep, false);
    }

    private void SetRescueV2Step(BotRescueV2Step nextStep, bool applyDelay = true)
    {
        if (rescueV2State.Step == nextStep)
        {
            return;
        }

        rescueV2State.Step = nextStep;
        bool shouldDelay =
            applyDelay &&
            nextStep != BotRescueV2Step.None &&
            nextStep != BotRescueV2Step.Complete &&
            nextStep != BotRescueV2Step.Failed;
        rescueV2State.StepReadyTime = shouldDelay
            ? Time.time + Mathf.Max(0f, v2FlowStepTransitionDelay)
            : 0f;
    }

    private string GetRescueV2TaskDetail()
    {
        if (rescueV2State.IsPaused)
        {
            return $"RescueV2 paused: {GetRescueV2PauseSummary()}";
        }

        return rescueV2State.Step switch
        {
            BotRescueV2Step.ResolveTarget => "Resolving RescueV2 target.",
            BotRescueV2Step.ResolveSafeZone => "Resolving RescueV2 safe zone.",
            BotRescueV2Step.IssueMoveToTarget => "Issuing RescueV2 move to target.",
            BotRescueV2Step.WaitReachTarget => "Moving to RescueV2 target.",
            BotRescueV2Step.BeginStabilizeTarget => "Starting RescueV2 stabilization.",
            BotRescueV2Step.WaitStabilizeTarget => "Stabilizing RescueV2 target.",
            BotRescueV2Step.BeginPickupTarget => "Starting RescueV2 pickup.",
            BotRescueV2Step.WaitPickupTarget => "Picking up RescueV2 target.",
            BotRescueV2Step.AcquireDropPoint => "Acquiring RescueV2 drop point.",
            BotRescueV2Step.IssueMoveToSafeZone => "Issuing RescueV2 move to safe zone.",
            BotRescueV2Step.WaitReachSafeZone => "Moving RescueV2 target to safe zone.",
            BotRescueV2Step.DropOffTarget => "Dropping off RescueV2 target.",
            BotRescueV2Step.Failed => rescueV2State.FailureReason,
            _ => "Executing RescueV2."
        };
    }
}
