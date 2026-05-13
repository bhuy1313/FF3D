using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private enum BotInterruptV2Kind
    {
        None = 0,
        RouteFire = 1
    }

    private enum BotInterruptV2Step
    {
        None = 0,
        ResolveTarget = 1,
        ValidateTool = 2,
        MoveToFire = 3,
        SuppressFire = 4,
        Complete = 5,
        Failed = 6
    }

    private sealed class BotInterruptV2State
    {
        public BotInterruptV2Kind Kind;
        public BotInterruptV2Step Step;
        public IFireTarget FireTarget;
        public IFireGroupTarget FireGroup;
        public Vector3 FirePosition;
        public IBotExtinguisherItem Tool;
        public bool HasIssuedMove;
        public Vector3 IssuedDestination;
        public string FailureReason;

        public void Reset()
        {
            Kind = BotInterruptV2Kind.None;
            Step = BotInterruptV2Step.None;
            FireTarget = null;
            FireGroup = null;
            FirePosition = default;
            Tool = null;
            HasIssuedMove = false;
            IssuedDestination = default;
            FailureReason = string.Empty;
        }
    }

    private sealed class BotExtinguishV2PauseSnapshot
    {
        public bool IsValid;
        public BotExtinguishV2Step Step;
        public IFireTarget FireTarget;
        public IFireGroupTarget FireGroup;
        public Vector3 FirePosition;
        public IBotExtinguisherItem Tool;
        public IPickupable PickupTarget;
        public bool HasIssuedToolMove;
        public Vector3 IssuedToolDestination;
        public bool HasIssuedFireMove;
        public Vector3 IssuedFireDestination;
        public bool HasComputedFireDestination;

        public void Reset()
        {
            IsValid = false;
            Step = BotExtinguishV2Step.None;
            FireTarget = null;
            FireGroup = null;
            FirePosition = default;
            Tool = null;
            PickupTarget = null;
            HasIssuedToolMove = false;
            IssuedToolDestination = default;
            HasIssuedFireMove = false;
            IssuedFireDestination = default;
            HasComputedFireDestination = false;
        }
    }

    private readonly BotInterruptV2State interruptV2State = new BotInterruptV2State();
    private readonly BotExtinguishV2PauseSnapshot extinguishV2PauseSnapshot = new BotExtinguishV2PauseSnapshot();
    private bool isExtinguishV2Paused;
    private IFireTarget lastFailedInterruptFireTarget;
    private float lastFailedInterruptFireUntilTime;
    private IFireTarget lastResolvedInterruptFireTarget;
    private float lastResolvedInterruptFireUntilTime;

    private bool IsInterruptV2Active => interruptV2State.Step != BotInterruptV2Step.None;

    private bool TryStartInterruptV2ForExtinguish()
    {
        if (!enableRouteFireClearing ||
            IsInterruptV2Active ||
            !IsExtinguishV2Active ||
            isExtinguishV2Paused ||
            !CanInterruptCurrentExtinguishV2Step() ||
            !TryGetCurrentExtinguishV2PlannedMovement(out Vector3 from, out Vector3 to) ||
            !TryDetectRouteFireForExtinguishMove(from, to, out IFireGroupTarget fireGroup, out IFireTarget fireTarget) ||
            fireTarget == null)
        {
            return false;
        }

        LogInterruptV2(
            $"interruptv2-detected:{GetDebugTargetName(fireTarget)}:{FormatFlowVectorKey(to)}",
            $"Detected route fire '{GetDebugTargetName(fireTarget)}' while moving toward {to}.");

        IBotExtinguisherItem heldTool = ResolveHeldSuppressionTool();
        if (!IsRouteFireInterruptToolEligible(heldTool, fireTarget))
        {
            LogInterruptV2(
                $"interruptv2-skip-tool:{GetDebugTargetName(fireTarget)}",
                $"Skipping route fire interrupt because held tool is not eligible for '{GetDebugTargetName(fireTarget)}'.");
            return false;
        }

        if (!PauseExtinguishV2ForInterrupt())
        {
            return false;
        }

        BeginRouteFireInterruptV2(fireGroup, fireTarget, heldTool);
        return true;
    }

    private void BeginRouteFireInterruptV2(IFireGroupTarget fireGroup, IFireTarget fireTarget, IBotExtinguisherItem heldTool)
    {
        interruptV2State.Reset();
        interruptV2State.Kind = BotInterruptV2Kind.RouteFire;
        interruptV2State.Step = BotInterruptV2Step.ResolveTarget;
        interruptV2State.FireGroup = fireGroup;
        interruptV2State.FireTarget = fireTarget;
        interruptV2State.Tool = heldTool;
        LogInterruptV2(
            $"interruptv2-begin:{GetDebugTargetName(fireTarget)}:{GetToolName(heldTool)}",
            $"Starting route fire interrupt for '{GetDebugTargetName(fireTarget)}' with '{GetToolName(heldTool)}'.");
    }

    private void TickInterruptV2()
    {
        switch (interruptV2State.Step)
        {
            case BotInterruptV2Step.ResolveTarget:
                TryResolveInterruptV2Target();
                break;
            case BotInterruptV2Step.ValidateTool:
                TryValidateInterruptV2Tool();
                break;
            case BotInterruptV2Step.MoveToFire:
                TryMoveInterruptV2ToFire();
                break;
            case BotInterruptV2Step.SuppressFire:
                TryApplyInterruptV2Suppression();
                break;
            case BotInterruptV2Step.Complete:
                CompleteInterruptV2Order();
                break;
            case BotInterruptV2Step.Failed:
                FailInterruptV2Order();
                break;
        }
    }

    private bool PauseExtinguishV2ForInterrupt()
    {
        if (!IsExtinguishV2Active || isExtinguishV2Paused)
        {
            return false;
        }

        extinguishV2PauseSnapshot.IsValid = true;
        extinguishV2PauseSnapshot.Step = extinguishV2State.Step;
        extinguishV2PauseSnapshot.FireTarget = extinguishV2State.FireTarget;
        extinguishV2PauseSnapshot.FireGroup = extinguishV2State.FireGroup;
        extinguishV2PauseSnapshot.FirePosition = extinguishV2State.FirePosition;
        extinguishV2PauseSnapshot.Tool = extinguishV2State.Tool;
        extinguishV2PauseSnapshot.PickupTarget = extinguishV2State.PickupTarget;
        extinguishV2PauseSnapshot.HasIssuedToolMove = extinguishV2State.HasIssuedToolMove;
        extinguishV2PauseSnapshot.IssuedToolDestination = extinguishV2State.IssuedToolDestination;
        extinguishV2PauseSnapshot.HasIssuedFireMove = extinguishV2State.HasIssuedFireMove;
        extinguishV2PauseSnapshot.IssuedFireDestination = extinguishV2State.IssuedFireDestination;
        extinguishV2PauseSnapshot.HasComputedFireDestination = extinguishV2State.HasComputedFireDestination;

        StopExtinguishV2Tool();
        ClearHeadAimFocus();
        ClearHandAimFocus();
        isExtinguishV2Paused = true;
        LogInterruptV2(
            $"interruptv2-pause-main:{extinguishV2State.Step}",
            $"Paused Extinguish V2 at step {extinguishV2State.Step}.");
        return true;
    }

    private bool ResumeExtinguishV2FromInterrupt()
    {
        if (!isExtinguishV2Paused || !extinguishV2PauseSnapshot.IsValid)
        {
            return false;
        }

        extinguishV2State.Step = extinguishV2PauseSnapshot.Step;
        extinguishV2State.FireTarget = extinguishV2PauseSnapshot.FireTarget;
        extinguishV2State.FireGroup = extinguishV2PauseSnapshot.FireGroup;
        extinguishV2State.FirePosition = extinguishV2PauseSnapshot.FirePosition;
        extinguishV2State.Tool = extinguishV2PauseSnapshot.Tool;
        extinguishV2State.PickupTarget = extinguishV2PauseSnapshot.PickupTarget;
        extinguishV2State.HasIssuedToolMove = extinguishV2PauseSnapshot.HasIssuedToolMove;
        extinguishV2State.IssuedToolDestination = extinguishV2PauseSnapshot.IssuedToolDestination;
        extinguishV2State.HasIssuedFireMove = extinguishV2PauseSnapshot.HasIssuedFireMove;
        extinguishV2State.IssuedFireDestination = extinguishV2PauseSnapshot.IssuedFireDestination;
        extinguishV2State.HasComputedFireDestination = extinguishV2PauseSnapshot.HasComputedFireDestination;

        isExtinguishV2Paused = false;
        extinguishV2PauseSnapshot.Reset();

        if (!TryRefreshExtinguishV2TargetPosition())
        {
            extinguishV2State.Step = BotExtinguishV2Step.Complete;
        }

        LogInterruptV2(
            $"interruptv2-resume-main:{extinguishV2State.Step}",
            $"Resuming Extinguish V2 at step {extinguishV2State.Step}.");

        return true;
    }

    private bool CanInterruptCurrentExtinguishV2Step()
    {
        return extinguishV2State.Step == BotExtinguishV2Step.MoveToTool ||
               extinguishV2State.Step == BotExtinguishV2Step.MoveToFire;
    }

    private bool TryGetCurrentExtinguishV2PlannedMovement(out Vector3 from, out Vector3 to)
    {
        from = transform.position;
        to = default;

        if (extinguishV2State.Step == BotExtinguishV2Step.MoveToTool)
        {
            if (extinguishV2State.PickupTarget?.Rigidbody == null)
            {
                return false;
            }

            to = ResolveExtinguishV2NavDestination(extinguishV2State.PickupTarget.Rigidbody.transform.position);
            return true;
        }

        if (extinguishV2State.Step == BotExtinguishV2Step.MoveToFire)
        {
            if (extinguishV2State.HasComputedFireDestination)
            {
                to = extinguishV2State.IssuedFireDestination;
                return true;
            }

            if (!TryRefreshExtinguishV2TargetPosition())
            {
                return false;
            }

            to = ResolveExtinguishV2FireStandPosition();
            return true;
        }

        return false;
    }

    private bool TryDetectRouteFireForExtinguishMove(
        Vector3 from,
        Vector3 to,
        out IFireGroupTarget fireGroup,
        out IFireTarget fireTarget)
    {
        fireGroup = null;
        fireTarget = null;

        if (!TryResolveBurningFireWithinRadius(from, routeFireDetectionRadius, out IFireTarget nearbyFire) ||
            nearbyFire == null ||
            !nearbyFire.IsBurning)
        {
            return false;
        }

        if (IsInterruptRouteFireBlockedByCooldown(nearbyFire))
        {
            return false;
        }

        Vector3 firePosition = nearbyFire.GetWorldPosition();
        Vector3 fromTo = to - from;
        fromTo.y = 0f;
        Vector3 fireOffset = firePosition - from;
        fireOffset.y = 0f;

        float segmentLengthSq = fromTo.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            return false;
        }

        float projection = Mathf.Clamp01(Vector3.Dot(fireOffset, fromTo) / segmentLengthSq);
        Vector3 closestPoint = from + fromTo * projection;
        closestPoint.y = firePosition.y;

        float corridorDistance = GetExtinguishV2HorizontalDistance(closestPoint, firePosition);
        if (corridorDistance > Mathf.Max(0.5f, extinguisherRouteCorridorWidth))
        {
            return false;
        }

        fireTarget = nearbyFire;
        fireGroup = FindClosestActiveFireGroup(firePosition);
        return true;
    }

    private bool IsInterruptRouteFireBlockedByCooldown(IFireTarget fireTarget)
    {
        if (fireTarget == null)
        {
            return true;
        }

        if (ReferenceEquals(fireTarget, lastFailedInterruptFireTarget) &&
            Time.time < lastFailedInterruptFireUntilTime)
        {
            return true;
        }

        if (ReferenceEquals(fireTarget, lastResolvedInterruptFireTarget) &&
            Time.time < lastResolvedInterruptFireUntilTime)
        {
            return true;
        }

        return false;
    }

    private bool IsRouteFireInterruptToolEligible(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        return tool != null &&
               tool.HasUsableCharge &&
               tool.CurrentHolder == gameObject &&
               !UsesPreciseAim(tool) &&
               !IsUnsafeSuppressionToolForFire(tool, fireTarget);
    }

    private bool TryResolveInterruptV2Target()
    {
        IFireTarget fireTarget = interruptV2State.FireTarget;
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            return FailInterruptV2("Interrupt route fire target is no longer active.");
        }

        interruptV2State.FirePosition = fireTarget.GetWorldPosition();
        interruptV2State.Step = BotInterruptV2Step.ValidateTool;
        LogInterruptV2(
            $"interruptv2-target:{GetDebugTargetName(fireTarget)}",
            $"Resolved interrupt fire target '{GetDebugTargetName(fireTarget)}' at {interruptV2State.FirePosition}.");
        return true;
    }

    private bool TryValidateInterruptV2Tool()
    {
        IFireTarget fireTarget = interruptV2State.FireTarget;
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            return FailInterruptV2("Interrupt route fire target is no longer active.");
        }

        IBotExtinguisherItem heldTool = ResolveHeldSuppressionTool();
        if (!IsRouteFireInterruptToolEligible(heldTool, fireTarget))
        {
            return FailInterruptV2("No eligible held tool for route fire interrupt.");
        }

        interruptV2State.Tool = heldTool;
        activeExtinguisher = heldTool;
        interruptV2State.Step = BotInterruptV2Step.MoveToFire;
        LogInterruptV2(
            $"interruptv2-tool:{GetToolName(heldTool)}",
            $"Validated held tool '{GetToolName(heldTool)}' for route fire interrupt.");
        return true;
    }

    private bool TryMoveInterruptV2ToFire()
    {
        if (interruptV2State.Tool == null)
        {
            return FailInterruptV2("Interrupt route fire tool was lost.");
        }

        if (!TryRefreshInterruptV2FirePosition())
        {
            interruptV2State.Step = BotInterruptV2Step.Complete;
            return true;
        }

        if (CanInterruptV2SuppressFromCurrentPosition())
        {
            interruptV2State.HasIssuedMove = false;
            interruptV2State.Step = BotInterruptV2Step.SuppressFire;
            LogInterruptV2(
                $"interruptv2-in-range:{GetDebugTargetName(interruptV2State.FireTarget)}",
                $"Reached route fire suppression position for '{GetDebugTargetName(interruptV2State.FireTarget)}'.");
            return true;
        }

        Vector3 destination = ResolveInterruptV2FireStandPosition();
        if (!interruptV2State.HasIssuedMove ||
            (destination - interruptV2State.IssuedDestination).sqrMagnitude > 0.04f)
        {
            interruptV2State.HasIssuedMove = true;
            interruptV2State.IssuedDestination = destination;
            MoveToIgnoringRouteFireInterrupt(destination);
            LogInterruptV2(
                $"interruptv2-move:{FormatFlowVectorKey(destination)}",
                $"Moving to route fire position {destination}.");
        }

        return false;
    }

    private bool TryApplyInterruptV2Suppression()
    {
        if (interruptV2State.Tool == null)
        {
            return FailInterruptV2("Interrupt route fire tool was lost during suppression.");
        }

        if (!TryRefreshInterruptV2FirePosition())
        {
            interruptV2State.Step = BotInterruptV2Step.Complete;
            return true;
        }

        activeExtinguisher = interruptV2State.Tool;
        IFireTarget fireTarget = interruptV2State.FireTarget;
        Vector3 firePosition = interruptV2State.FirePosition;
        PreparePointFireExtinguisherSuppression(fireTarget, firePosition, transform.position);

        if (!IsPointFireExtinguisherSprayReady(firePosition, false))
        {
            LogInterruptV2(
                $"interruptv2-ready-wait:{GetDebugTargetName(fireTarget)}",
                $"Waiting for spray readiness on '{GetDebugTargetName(fireTarget)}'.");
            return false;
        }

        if (!CanApplyDirectSuppressionToFireTarget(activeExtinguisher, fireTarget, firePosition))
        {
            StopInterruptV2Tool();
            sprayReadyTime = -1f;
            interruptV2State.Step = BotInterruptV2Step.MoveToFire;
            LogInterruptV2(
                $"interruptv2-reposition:{GetDebugTargetName(fireTarget)}",
                $"Repositioning for route fire '{GetDebugTargetName(fireTarget)}' due to suppression gate failure.");
            return false;
        }

        SprayPointFireExtinguisher(
            fireTarget,
            firePosition,
            routeFireDetectionRadius,
            false,
            false,
            "Interrupt V2 clearing route fire.");
        LogInterruptV2(
            $"interruptv2-spray:{GetDebugTargetName(fireTarget)}",
            $"Spraying route fire '{GetDebugTargetName(fireTarget)}'.");

        if (IsInterruptRouteFireResolved())
        {
            interruptV2State.Step = BotInterruptV2Step.Complete;
        }

        return true;
    }

    private bool TryRefreshInterruptV2FirePosition()
    {
        if (interruptV2State.FireTarget == null || !interruptV2State.FireTarget.IsBurning)
        {
            return false;
        }

        interruptV2State.FirePosition = interruptV2State.FireTarget.GetWorldPosition();
        return true;
    }

    private bool CanInterruptV2SuppressFromCurrentPosition()
    {
        if (interruptV2State.Tool == null)
        {
            return false;
        }

        float horizontalDistance = GetExtinguishV2HorizontalDistance(transform.position, interruptV2State.FirePosition);
        float verticalDistance = Mathf.Abs(transform.position.y - interruptV2State.FirePosition.y);
        return horizontalDistance <= interruptV2State.Tool.MaxSprayDistance &&
               verticalDistance <= interruptV2State.Tool.MaxVerticalReach &&
               HasExtinguishV2LineOfSight(transform.position, interruptV2State.FirePosition);
    }

    private Vector3 ResolveInterruptV2FireStandPosition()
    {
        Vector3 firePosition = interruptV2State.FirePosition;
        Vector3 awayFromFire = transform.position - firePosition;
        awayFromFire.y = 0f;
        if (awayFromFire.sqrMagnitude <= 0.001f)
        {
            awayFromFire = transform.forward;
            awayFromFire.y = 0f;
        }

        float preferredDistance = Mathf.Clamp(
            interruptV2State.Tool != null ? interruptV2State.Tool.PreferredSprayDistance : 1f,
            0.5f,
            interruptV2State.Tool != null ? interruptV2State.Tool.MaxSprayDistance : 2f);
        return ResolveExtinguishV2NavDestination(firePosition + awayFromFire.normalized * preferredDistance);
    }

    private bool IsInterruptRouteFireResolved()
    {
        if (interruptV2State.FireTarget == null || !interruptV2State.FireTarget.IsBurning)
        {
            return true;
        }

        if (!extinguishV2PauseSnapshot.IsValid)
        {
            return true;
        }

        if (!TryGetPausedExtinguishV2PlannedMovement(out Vector3 from, out Vector3 to))
        {
            return true;
        }

        Vector3 firePosition = interruptV2State.FireTarget.GetWorldPosition();
        Vector3 fromTo = to - from;
        fromTo.y = 0f;
        Vector3 fireOffset = firePosition - from;
        fireOffset.y = 0f;

        float segmentLengthSq = fromTo.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            return true;
        }

        float projection = Mathf.Clamp01(Vector3.Dot(fireOffset, fromTo) / segmentLengthSq);
        Vector3 closestPoint = from + fromTo * projection;
        closestPoint.y = firePosition.y;
        float corridorDistance = GetExtinguishV2HorizontalDistance(closestPoint, firePosition);
        return corridorDistance > Mathf.Max(0.5f, extinguisherRouteCorridorWidth);
    }

    private bool TryGetPausedExtinguishV2PlannedMovement(out Vector3 from, out Vector3 to)
    {
        from = transform.position;
        to = default;

        if (!extinguishV2PauseSnapshot.IsValid)
        {
            return false;
        }

        if (extinguishV2PauseSnapshot.Step == BotExtinguishV2Step.MoveToTool)
        {
            if (extinguishV2PauseSnapshot.PickupTarget?.Rigidbody == null)
            {
                return false;
            }

            to = ResolveExtinguishV2NavDestination(extinguishV2PauseSnapshot.PickupTarget.Rigidbody.transform.position);
            return true;
        }

        if (extinguishV2PauseSnapshot.Step == BotExtinguishV2Step.MoveToFire)
        {
            to = extinguishV2PauseSnapshot.HasComputedFireDestination
                ? extinguishV2PauseSnapshot.IssuedFireDestination
                : ResolveExtinguishV2NavDestination(extinguishV2PauseSnapshot.FirePosition);
            return true;
        }

        return false;
    }

    private void StopInterruptV2Tool()
    {
        if (interruptV2State.Tool == null)
        {
            return;
        }

        StopExtinguisher();
    }

    private void CompleteInterruptV2Order()
    {
        StopInterruptV2Tool();
        sprayReadyTime = -1f;
        LogInterruptV2(
            $"interruptv2-complete:{GetDebugTargetName(interruptV2State.FireTarget)}",
            $"Completed route fire interrupt for '{GetDebugTargetName(interruptV2State.FireTarget)}'.");
        lastResolvedInterruptFireTarget = interruptV2State.FireTarget;
        lastResolvedInterruptFireUntilTime = Time.time + Mathf.Max(0.1f, interruptRouteFireRepeatBlockDuration);
        interruptV2State.Reset();
        ResumeExtinguishV2FromInterrupt();
    }

    private void FailInterruptV2Order()
    {
        StopInterruptV2Tool();
        sprayReadyTime = -1f;
        LogInterruptV2(
            $"interruptv2-failed:{GetDebugTargetName(interruptV2State.FireTarget)}",
            string.IsNullOrWhiteSpace(interruptV2State.FailureReason)
                ? "Interrupt V2 failed."
                : interruptV2State.FailureReason);
        lastFailedInterruptFireTarget = interruptV2State.FireTarget;
        lastFailedInterruptFireUntilTime = Time.time + Mathf.Max(0.1f, interruptRouteFireRetryDelay);
        interruptV2State.Reset();
        ResumeExtinguishV2FromInterrupt();
    }

    private bool FailInterruptV2(string reason)
    {
        interruptV2State.FailureReason = string.IsNullOrWhiteSpace(reason) ? "Interrupt V2 failed." : reason;
        interruptV2State.Step = BotInterruptV2Step.Failed;
        StopInterruptV2Tool();
        return false;
    }

    private string GetInterruptV2TaskDetail()
    {
        return interruptV2State.Step switch
        {
            BotInterruptV2Step.ResolveTarget => "Interrupt V2: resolving route fire.",
            BotInterruptV2Step.ValidateTool => "Interrupt V2: validating suppression tool.",
            BotInterruptV2Step.MoveToFire => "Interrupt V2: moving to route fire.",
            BotInterruptV2Step.SuppressFire => "Interrupt V2: clearing route fire.",
            BotInterruptV2Step.Failed => interruptV2State.FailureReason,
            _ => "Interrupt V2 active."
        };
    }

    private Component GetInterruptV2TaskTargetComponent()
    {
        return (interruptV2State.FireTarget as Component) ??
               (interruptV2State.FireGroup as Component) ??
               (interruptV2State.Tool as Component);
    }

    private Vector3? GetInterruptV2TaskPosition()
    {
        return interruptV2State.Step switch
        {
            BotInterruptV2Step.MoveToFire or BotInterruptV2Step.SuppressFire => interruptV2State.FirePosition,
            _ => hasIssuedDestination ? lastIssuedDestination : null
        };
    }

    private void LogInterruptV2(string key, string detail)
    {
        activityDebug?.LogInterrupt(this, enableActivityDebug, key, detail);
    }
}
