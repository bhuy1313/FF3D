using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private enum BotExtinguishV2Step
    {
        None = 0,
        ResolveTarget = 1,
        SelectTool = 2,
        MoveToTool = 3,
        PickupTool = 4,
        MoveToFire = 5,
        SuppressFire = 6,
        Complete = 7,
        Failed = 8
    }

    private sealed class BotExtinguishV2State
    {
        public BotExtinguishV2Step Step;
        public Vector3 OrderPoint;
        public Vector3 ScanOrigin;
        public BotExtinguishCommandMode CommandMode;
        public BotExtinguishEngagementMode EngagementMode;
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
        public bool HasLockedSuppressTarget;
        public bool NeedsSuppressAcquire;
        public string FailureReason;

        public void Reset()
        {
            Step = BotExtinguishV2Step.None;
            OrderPoint = default;
            ScanOrigin = default;
            CommandMode = BotExtinguishCommandMode.Auto;
            EngagementMode = BotExtinguishEngagementMode.DirectBestTool;
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
            HasLockedSuppressTarget = false;
            NeedsSuppressAcquire = false;
            FailureReason = string.Empty;
        }
    }

    private readonly BotExtinguishV2State extinguishV2State = new BotExtinguishV2State();
    private readonly List<IBotExtinguisherItem> extinguishV2InventoryTools = new List<IBotExtinguisherItem>(4);

    private bool IsExtinguishV2Active => extinguishV2State.Step != BotExtinguishV2Step.None;

    private void BeginExtinguishV2Order(
        Vector3 orderPoint,
        Vector3 scanOrigin,
        BotExtinguishCommandMode commandMode,
        BotExtinguishEngagementMode engagementMode,
        IFireTarget pointFireTarget,
        IFireGroupTarget fireGroupTarget)
    {
        extinguishV2State.Reset();
        extinguishV2State.Step = BotExtinguishV2Step.ResolveTarget;
        extinguishV2State.OrderPoint = orderPoint;
        extinguishV2State.ScanOrigin = scanOrigin;
        extinguishV2State.CommandMode = commandMode;
        extinguishV2State.EngagementMode = engagementMode;
        extinguishV2State.FireTarget = pointFireTarget;
        extinguishV2State.FireGroup = fireGroupTarget;
        ClearExtinguishV2MoveTargetMarker();
    }

    private void TickExtinguishV2()
    {
        switch (extinguishV2State.Step)
        {
            case BotExtinguishV2Step.ResolveTarget:
                TryResolveExtinguishV2Target();
                break;
            case BotExtinguishV2Step.SelectTool:
                TrySelectExtinguishV2Tool();
                break;
            case BotExtinguishV2Step.MoveToTool:
                TryIssueExtinguishV2MoveToTool();
                break;
            case BotExtinguishV2Step.PickupTool:
                TryCompleteExtinguishV2ToolPickup();
                break;
            case BotExtinguishV2Step.MoveToFire:
                TryIssueExtinguishV2MoveToFire();
                break;
            case BotExtinguishV2Step.SuppressFire:
                TryApplyExtinguishV2Suppression();
                break;
            case BotExtinguishV2Step.Complete:
                CompleteExtinguishV2Order();
                break;
            case BotExtinguishV2Step.Failed:
                FailExtinguishV2Order();
                break;
        }
    }

    private void CompleteExtinguishV2Order()
    {
        StopExtinguishV2Tool();
        ClearExtinguishV2MoveTargetMarker();
        LogExtinguishV2Activity("Complete.");
        CompleteCurrentTask("Extinguish V2 complete.");
        behaviorContext?.ClearCommandIntent();
        extinguishV2State.Reset();
    }

    private void FailExtinguishV2Order()
    {
        string reason = string.IsNullOrWhiteSpace(extinguishV2State.FailureReason)
            ? "Extinguish V2 failed."
            : extinguishV2State.FailureReason;
        StopExtinguishV2Tool();
        ClearExtinguishV2MoveTargetMarker();
        LogExtinguishV2Activity($"Failed: {reason}");
        FailCurrentTask(reason);
        behaviorContext?.ClearCommandIntent();
        extinguishV2State.Reset();
    }

    private string GetExtinguishV2TaskDetail()
    {
        return extinguishV2State.Step switch
        {
            BotExtinguishV2Step.ResolveTarget => "Resolving Extinguish V2 target.",
            BotExtinguishV2Step.SelectTool => "Selecting Extinguish V2 tool.",
            BotExtinguishV2Step.MoveToTool => "Moving to Extinguish V2 tool.",
            BotExtinguishV2Step.PickupTool => "Picking up Extinguish V2 tool.",
            BotExtinguishV2Step.MoveToFire => "Moving to Extinguish V2 fire position.",
            BotExtinguishV2Step.SuppressFire => "Suppressing fire with Extinguish V2.",
            BotExtinguishV2Step.Failed => extinguishV2State.FailureReason,
            _ => "Executing Extinguish V2."
        };
    }

    private Component GetExtinguishV2TaskTargetComponent()
    {
        return (extinguishV2State.FireTarget as Component) ??
               (extinguishV2State.FireGroup as Component) ??
               (extinguishV2State.Tool as Component);
    }

    private Vector3? GetExtinguishV2TaskPosition()
    {
        return extinguishV2State.Step switch
        {
            BotExtinguishV2Step.MoveToTool or BotExtinguishV2Step.PickupTool
                when extinguishV2State.PickupTarget?.Rigidbody != null
                => extinguishV2State.PickupTarget.Rigidbody.transform.position,
            BotExtinguishV2Step.MoveToFire or BotExtinguishV2Step.SuppressFire
                => extinguishV2State.FirePosition,
            _ => hasIssuedDestination ? lastIssuedDestination : null
        };
    }

    private bool TryResolveExtinguishV2Target()
    {
        if (extinguishV2State.EngagementMode == BotExtinguishEngagementMode.PrecisionFireHose)
        {
            if (!TryResolveExtinguishV2FireGroup(extinguishV2State.ScanOrigin, extinguishV2State.FireGroup, out IFireGroupTarget fireGroup))
            {
                return FailExtinguishV2("Precision FireHose requires an active fire group.");
            }

            extinguishV2State.FireGroup = fireGroup;
            extinguishV2State.FireTarget = ResolveExtinguishV2RepresentativeFire(fireGroup, transform.position);
            extinguishV2State.FirePosition = extinguishV2State.FireTarget != null && extinguishV2State.FireTarget.IsBurning
                ? extinguishV2State.FireTarget.GetWorldPosition()
                : fireGroup.GetWorldCenter();
            LogExtinguishV2Activity($"Resolved precision target at {extinguishV2State.FirePosition}.");
            extinguishV2State.Step = BotExtinguishV2Step.SelectTool;
            return true;
        }

        if (!TryResolveExtinguishV2PointFire(extinguishV2State.ScanOrigin, extinguishV2State.FireTarget, out IFireTarget fireTarget))
        {
            return FailExtinguishV2("No active point fire found for Extinguish V2.");
        }

        extinguishV2State.FireTarget = fireTarget;
        extinguishV2State.FireGroup = null;
        extinguishV2State.FirePosition = fireTarget.GetWorldPosition();
        LogExtinguishV2Activity($"Resolved fire target '{GetDebugTargetName(fireTarget)}' at {extinguishV2State.FirePosition}.");
        extinguishV2State.Step = BotExtinguishV2Step.SelectTool;
        return true;
    }

    private bool TrySelectExtinguishV2Tool()
    {
        IBotExtinguisherItem bestTool = null;
        float bestScore = float.PositiveInfinity;

        if (inventorySystem != null)
        {
            inventorySystem.CollectItems(extinguishV2InventoryTools);
            for (int i = 0; i < extinguishV2InventoryTools.Count; i++)
            {
                ScoreExtinguishV2ToolCandidate(extinguishV2InventoryTools[i], transform.position, ref bestTool, ref bestScore);
            }
        }

        foreach (IBotExtinguisherItem candidate in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            if (candidate is not Component component || component == null)
            {
                continue;
            }

            ScoreExtinguishV2ToolCandidate(candidate, component.transform.position, ref bestTool, ref bestScore);
        }

        if (bestTool == null)
        {
            return FailExtinguishV2("No usable Extinguish V2 tool found.");
        }

        extinguishV2State.Tool = bestTool;
        extinguishV2State.PickupTarget = bestTool as IPickupable;
        LogExtinguishV2Activity($"Selected tool '{GetToolName(bestTool)}'.");
        extinguishV2State.Step = IsExtinguishV2ToolHeldByBot(bestTool)
            ? BotExtinguishV2Step.MoveToFire
            : BotExtinguishV2Step.MoveToTool;
        return true;
    }

    private bool TryIssueExtinguishV2MoveToTool()
    {
        if (extinguishV2State.Tool == null ||
            extinguishV2State.PickupTarget == null ||
            extinguishV2State.PickupTarget.Rigidbody == null)
        {
            return FailExtinguishV2("Selected Extinguish V2 tool cannot be picked up.");
        }

        Vector3 destination = ResolveExtinguishV2NavDestination(extinguishV2State.PickupTarget.Rigidbody.transform.position);
        if (extinguishV2State.HasIssuedToolMove &&
            (destination - extinguishV2State.IssuedToolDestination).sqrMagnitude <= 0.04f)
        {
            extinguishV2State.Step = BotExtinguishV2Step.PickupTool;
            return true;
        }

        extinguishV2State.HasIssuedToolMove = true;
        extinguishV2State.IssuedToolDestination = destination;
        UpdateExtinguishV2MoveTargetMarker(destination, "MoveToTool");
        LogExtinguishV2Activity($"Moving to tool at {destination}.");
        MoveToExtinguishV2ActivityDestination(destination);
        extinguishV2State.Step = BotExtinguishV2Step.PickupTool;
        return true;
    }

    private bool TryCompleteExtinguishV2ToolPickup()
    {
        if (extinguishV2State.Tool == null)
        {
            return FailExtinguishV2("No selected Extinguish V2 tool to pick up.");
        }

        if (TryEquipExtinguishV2Tool(extinguishV2State.Tool))
        {
            LogExtinguishV2Activity($"Equipped tool '{GetToolName(extinguishV2State.Tool)}'.");
            extinguishV2State.Step = BotExtinguishV2Step.MoveToFire;
            return true;
        }

        if (extinguishV2State.PickupTarget == null || extinguishV2State.PickupTarget.Rigidbody == null)
        {
            return FailExtinguishV2("Extinguish V2 pickup target disappeared.");
        }

        float distance = GetExtinguishV2HorizontalPickupDistance(extinguishV2State.PickupTarget, transform.position);
        if (distance > pickupDistance)
        {
            return false;
        }

        SetPickupWindow(true, extinguishV2State.PickupTarget);
        bool pickedUp = inventorySystem != null && inventorySystem.TryPickup(extinguishV2State.PickupTarget);
        SetPickupWindow(false, null);
        if (!pickedUp || !TryEquipExtinguishV2Tool(extinguishV2State.Tool))
        {
            return FailExtinguishV2("Failed to pick up selected Extinguish V2 tool.");
        }

        LogExtinguishV2Activity($"Picked up and equipped '{GetToolName(extinguishV2State.Tool)}'.");
        extinguishV2State.Step = BotExtinguishV2Step.MoveToFire;
        return true;
    }

    private bool TryIssueExtinguishV2MoveToFire()
    {
        if (extinguishV2State.Tool == null)
        {
            return FailExtinguishV2("No equipped Extinguish V2 tool.");
        }

        if (extinguishV2State.HasComputedFireDestination &&
            IsWithinArrivalDistance(extinguishV2State.IssuedFireDestination))
        {
            StopNavMeshMovement();
            LogExtinguishV2Activity($"Arrived at suppress position {extinguishV2State.IssuedFireDestination}.");
            extinguishV2State.HasLockedSuppressTarget = false;
            extinguishV2State.NeedsSuppressAcquire = true;
            extinguishV2State.Step = BotExtinguishV2Step.SuppressFire;
            return true;
        }

        if (!extinguishV2State.HasComputedFireDestination)
        {
            extinguishV2State.IssuedFireDestination = ResolveExtinguishV2FireStandPosition();
            extinguishV2State.HasComputedFireDestination = true;
            extinguishV2State.HasIssuedFireMove = false;
        }

        if (!extinguishV2State.HasIssuedFireMove)
        {
            extinguishV2State.HasIssuedFireMove = true;
            UpdateExtinguishV2MoveTargetMarker(extinguishV2State.IssuedFireDestination, "MoveToFire");
            LogExtinguishV2Activity($"Moving to suppress position {extinguishV2State.IssuedFireDestination}.");
            MoveToExtinguishV2ActivityDestination(extinguishV2State.IssuedFireDestination);
        }

        return false;
    }

    private bool TryApplyExtinguishV2Suppression()
    {
        if (extinguishV2State.Tool == null)
        {
            return FailExtinguishV2("No Extinguish V2 tool during suppression.");
        }

        if (extinguishV2State.NeedsSuppressAcquire)
        {
            if (!TryAcquireExtinguishV2SuppressTarget())
            {
                StopExtinguishV2Tool();
                ClearExtinguishV2MoveTargetMarker();
                LogExtinguishV2Activity("No nearby fire found in suppress radius.");
                extinguishV2State.Step = BotExtinguishV2Step.Complete;
                return true;
            }

            extinguishV2State.NeedsSuppressAcquire = false;
        }

        if (!extinguishV2State.HasLockedSuppressTarget ||
            extinguishV2State.FireTarget == null ||
            !extinguishV2State.FireTarget.IsBurning)
        {
            StopExtinguishV2Tool();
            sprayReadyTime = -1f;
            extinguishV2State.HasLockedSuppressTarget = false;
            extinguishV2State.NeedsSuppressAcquire = true;
            ClearExtinguisherTargetLock();
            LogExtinguishV2Activity("Locked fire extinguished. Reacquiring.");
            return false;
        }

        activeExtinguisher = extinguishV2State.Tool;
        IFireTarget fireTarget = extinguishV2State.FireTarget;
        Vector3 firePosition = extinguishV2State.FirePosition;
        activeExtinguisher.SetExternalSprayState(true, gameObject);
        ApplyWaterToFireTarget(activeExtinguisher, fireTarget);

        return true;
    }

    private void StopExtinguishV2Tool()
    {
        if (extinguishV2State.Tool == null)
        {
            return;
        }

        extinguishV2State.Tool.SetExternalSprayState(false, gameObject);
        extinguishV2State.Tool.ClearExternalAimDirection(gameObject);
    }

    private bool TryResolveExtinguishV2PointFire(Vector3 scanOrigin, IFireTarget preferredTarget, out IFireTarget fireTarget)
    {
        fireTarget = null;
        if (preferredTarget != null && preferredTarget.IsBurning)
        {
            fireTarget = preferredTarget;
            return true;
        }

        float maxDistanceSq = fireSearchRadius * fireSearchRadius;
        float bestDistanceSq = float.PositiveInfinity;
        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - scanOrigin).sqrMagnitude;
            if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            fireTarget = candidate;
        }

        return fireTarget != null;
    }

    private bool TryResolveExtinguishV2FireGroup(Vector3 scanOrigin, IFireGroupTarget preferredGroup, out IFireGroupTarget fireGroup)
    {
        fireGroup = null;
        if (preferredGroup != null && preferredGroup.HasActiveFires)
        {
            fireGroup = preferredGroup;
            return true;
        }

        float maxDistanceSq = fireSearchRadius * fireSearchRadius;
        float bestDistanceSq = float.PositiveInfinity;
        foreach (IFireGroupTarget candidate in BotRuntimeRegistry.ActiveFireGroups)
        {
            if (candidate == null || !candidate.HasActiveFires)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldCenter() - scanOrigin).sqrMagnitude;
            if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            fireGroup = candidate;
        }

        return fireGroup != null;
    }

    private IFireTarget ResolveExtinguishV2RepresentativeFire(IFireGroupTarget fireGroup, Vector3 fromPosition)
    {
        if (fireGroup == null || !fireGroup.HasActiveFires)
        {
            return null;
        }

        Vector3 closestPosition = fireGroup.GetClosestActiveFirePosition(fromPosition);
        TryResolveExtinguishV2PointFire(closestPosition, null, out IFireTarget fireTarget);
        return fireTarget;
    }

    private void ScoreExtinguishV2ToolCandidate(
        IBotExtinguisherItem candidate,
        Vector3 toolPosition,
        ref IBotExtinguisherItem bestTool,
        ref float bestScore)
    {
        if (!IsExtinguishV2ToolUsable(candidate))
        {
            return;
        }

        if (!DoesExtinguishV2ToolMatchMode(candidate))
        {
            return;
        }

        if (extinguishV2State.FireTarget != null &&
            extinguishV2State.FireTarget.EvaluateSuppressionOutcome(candidate.SuppressionAgent) == FireSuppressionOutcome.UnsafeWorsens)
        {
            return;
        }

        float distanceScore = Vector3.Distance(transform.position, toolPosition);
        float directHoseBonus = extinguishV2State.EngagementMode == BotExtinguishEngagementMode.DirectBestTool &&
            candidate.RequiresPreciseAim ? -10000f : 0f;
        float heldBonus = IsExtinguishV2ToolHeldByBot(candidate) ? -20000f : 0f;
        float score = distanceScore + directHoseBonus + heldBonus;
        if (score < bestScore)
        {
            bestScore = score;
            bestTool = candidate;
        }
    }

    private bool IsExtinguishV2ToolUsable(IBotExtinguisherItem tool)
    {
        return tool != null &&
               tool.HasUsableCharge &&
               tool.IsAvailableTo(gameObject);
    }

    private bool DoesExtinguishV2ToolMatchMode(IBotExtinguisherItem tool)
    {
        if (tool == null)
        {
            return false;
        }

        return extinguishV2State.EngagementMode != BotExtinguishEngagementMode.PrecisionFireHose ||
               tool.RequiresPreciseAim;
    }

    private bool IsExtinguishV2ToolHeldByBot(IBotExtinguisherItem tool)
    {
        return tool != null && tool.CurrentHolder == gameObject;
    }

    private bool TryEquipExtinguishV2Tool(IBotExtinguisherItem tool)
    {
        if (tool == null || inventorySystem == null)
        {
            return false;
        }

        if (tool is IPickupable pickupable && inventorySystem.TryEquipItem(pickupable))
        {
            activeExtinguisher = tool;
            return true;
        }

        if (IsExtinguishV2ToolHeldByBot(tool))
        {
            activeExtinguisher = tool;
            return true;
        }

        return false;
    }

    private bool TryRefreshExtinguishV2TargetPosition()
    {
        if (extinguishV2State.EngagementMode == BotExtinguishEngagementMode.PrecisionFireHose)
        {
            if (extinguishV2State.FireGroup == null || !extinguishV2State.FireGroup.HasActiveFires)
            {
                return false;
            }

            extinguishV2State.FireTarget = ResolveExtinguishV2RepresentativeFire(extinguishV2State.FireGroup, transform.position);
            extinguishV2State.FirePosition = extinguishV2State.FireTarget != null && extinguishV2State.FireTarget.IsBurning
                ? extinguishV2State.FireTarget.GetWorldPosition()
                : extinguishV2State.FireGroup.GetWorldCenter();
            return true;
        }

        if (extinguishV2State.FireTarget == null || !extinguishV2State.FireTarget.IsBurning)
        {
            return false;
        }

        extinguishV2State.FirePosition = extinguishV2State.FireTarget.GetWorldPosition();
        return true;
    }

    private bool TryAcquireExtinguishV2SuppressTarget()
    {
        float suppressRadius = Mathf.Max(
            0.05f,
            extinguishV2LocalSuppressRadius,
            extinguishV2State.Tool != null ? extinguishV2State.Tool.MaxSprayDistance : 0f);

        IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
        if (lockedTarget != null &&
            GetDistanceToFireEdge(transform.position, lockedTarget.GetWorldPosition(), lockedTarget) <= suppressRadius)
        {
            extinguishV2State.FireTarget = lockedTarget;
            extinguishV2State.FirePosition = lockedTarget.GetWorldPosition();
            extinguishV2State.HasLockedSuppressTarget = true;
            extinguishV2State.NeedsSuppressAcquire = false;
            LogExtinguishV2Activity($"Reusing locked target '{GetDebugTargetName(lockedTarget)}'.");
            return true;
        }

        IFireTarget candidateTarget = extinguishV2State.FireTarget;
        if (candidateTarget != null &&
            candidateTarget.IsBurning &&
            GetDistanceToFireEdge(transform.position, candidateTarget.GetWorldPosition(), candidateTarget) <= suppressRadius)
        {
            activeExtinguisher = extinguishV2State.Tool;
            extinguishV2State.FirePosition = candidateTarget.GetWorldPosition();
            sprayReadyTime = -1f;
            extinguishV2State.HasLockedSuppressTarget = true;
            extinguishV2State.NeedsSuppressAcquire = false;
            LogExtinguishV2Activity($"Locked suppress target '{GetDebugTargetName(candidateTarget)}'.");
            return true;
        }

        if (TryResolveBurningFireWithinRadius(transform.position, suppressRadius, out IFireTarget nearbyFire) &&
            nearbyFire != null &&
            nearbyFire.IsBurning)
        {
            activeExtinguisher = extinguishV2State.Tool;
            extinguishV2State.FireTarget = nearbyFire;
            extinguishV2State.FirePosition = nearbyFire.GetWorldPosition();
            sprayReadyTime = -1f;
            extinguishV2State.HasLockedSuppressTarget = true;
            extinguishV2State.NeedsSuppressAcquire = false;
            LogExtinguishV2Activity($"Locked nearest fire '{GetDebugTargetName(nearbyFire)}'.");
            return true;
        }

        extinguishV2State.FireTarget = null;
        extinguishV2State.HasLockedSuppressTarget = false;
        extinguishV2State.NeedsSuppressAcquire = false;
        return false;
    }

    private bool IsExtinguishV2TargetStillActive()
    {
        if (extinguishV2State.EngagementMode == BotExtinguishEngagementMode.PrecisionFireHose)
        {
            return extinguishV2State.FireGroup != null && extinguishV2State.FireGroup.HasActiveFires;
        }

        return extinguishV2State.FireTarget != null && extinguishV2State.FireTarget.IsBurning;
    }

    private Vector3 ResolveExtinguishV2FireStandPosition()
    {
        IFireTarget fireTarget = extinguishV2State.FireTarget;
        Vector3 firePosition = extinguishV2State.FirePosition;
        float preferredDistance = Mathf.Clamp(
            extinguishV2State.Tool != null ? GetDesiredExtinguisherCenterDistance(extinguishV2State.Tool, fireTarget) : 1f,
            Mathf.Max(0.5f, MinExtinguisherStandOffDistance),
            extinguishV2State.Tool != null ? Mathf.Max(1f, extinguishV2State.Tool.MaxSprayDistance) : 2f);

        if (fireTarget != null &&
            TryResolveExtinguisherStandPosition(extinguishV2State.OrderPoint, firePosition, preferredDistance, out Vector3 standPosition))
        {
            return standPosition;
        }

        Vector3 awayFromFire = extinguishV2State.OrderPoint - firePosition;
        awayFromFire.y = 0f;
        if (awayFromFire.sqrMagnitude <= 0.001f)
        {
            awayFromFire = transform.position - firePosition;
            awayFromFire.y = 0f;
        }

        if (awayFromFire.sqrMagnitude <= 0.001f)
        {
            awayFromFire = transform.forward;
            awayFromFire.y = 0f;
        }

        Vector3 rawDestination = firePosition + awayFromFire.normalized * preferredDistance;
        return ResolveExtinguishV2NavDestination(rawDestination);
    }

    private bool CanExtinguishV2SuppressFromCurrentPosition()
    {
        if (extinguishV2State.Tool == null)
        {
            return false;
        }

        float horizontalDistance = GetExtinguishV2HorizontalDistance(transform.position, extinguishV2State.FirePosition);
        float verticalDistance = Mathf.Abs(transform.position.y - extinguishV2State.FirePosition.y);
        return horizontalDistance <= extinguishV2State.Tool.MaxSprayDistance &&
               verticalDistance <= extinguishV2State.Tool.MaxVerticalReach &&
               HasExtinguishV2LineOfSight(transform.position, extinguishV2State.FirePosition);
    }

    private bool HasExtinguishV2LineOfSight(Vector3 originPosition, Vector3 firePosition)
    {
        Vector3 origin = originPosition + Vector3.up * Mathf.Max(0.4f, headAimVerticalOffset);
        Vector3 target = firePosition + Vector3.up * 0.35f;
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        return !Physics.Raycast(origin, toTarget / distance, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    private Vector3 ResolveExtinguishV2NavDestination(Vector3 worldPosition)
    {
        if (navMeshAgent != null &&
            navMeshAgent.enabled &&
            navMeshAgent.isOnNavMesh &&
            navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(worldPosition, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            return navMeshHit.position;
        }

        return worldPosition;
    }

    private float GetExtinguishV2HorizontalPickupDistance(IPickupable pickupable, Vector3 fromPosition)
    {
        if (pickupable?.Rigidbody == null)
        {
            return float.PositiveInfinity;
        }

        return GetExtinguishV2HorizontalDistance(fromPosition, pickupable.Rigidbody.transform.position);
    }

    private static float GetExtinguishV2HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private bool FailExtinguishV2(string reason)
    {
        extinguishV2State.FailureReason = string.IsNullOrWhiteSpace(reason) ? "Extinguish V2 failed." : reason;
        extinguishV2State.Step = BotExtinguishV2Step.Failed;
        StopExtinguishV2Tool();
        return false;
    }

    private void UpdateExtinguishV2MoveTargetMarker(Vector3 worldPosition, string context)
    {
        if (!spawnExtinguishV2MoveTargetMarker)
        {
            ClearExtinguishV2MoveTargetMarker();
            return;
        }

        hasExtinguishV2MoveTargetMarker = true;
        extinguishV2MoveTargetMarkerPosition = worldPosition;

        LogExtinguishV2Activity($"Move target '{context}' -> {worldPosition}");
    }

    private void ClearExtinguishV2MoveTargetMarker()
    {
        hasExtinguishV2MoveTargetMarker = false;
        extinguishV2MoveTargetMarkerPosition = default;
    }

    private void DrawExtinguishV2MoveTargetDebugLines()
    {
        if (!Application.isPlaying ||
            !spawnExtinguishV2MoveTargetMarker ||
            !hasExtinguishV2MoveTargetMarker)
        {
            return;
        }

        float scale = Mathf.Max(0.1f, extinguishV2MoveTargetMarkerScale);
        Vector3 center = extinguishV2MoveTargetMarkerPosition + Vector3.up * 0.05f;
        Color color = extinguishV2MoveTargetMarkerColor;

        Debug.DrawLine(center + Vector3.left * scale, center + Vector3.right * scale, color, 0f, false);
        Debug.DrawLine(center + Vector3.forward * scale, center + Vector3.back * scale, color, 0f, false);
        Debug.DrawLine(center + (Vector3.left + Vector3.forward) * (scale * 0.7f), center + (Vector3.right + Vector3.back) * (scale * 0.7f), color, 0f, false);
        Debug.DrawLine(center + (Vector3.left + Vector3.back) * (scale * 0.7f), center + (Vector3.right + Vector3.forward) * (scale * 0.7f), color, 0f, false);
        Debug.DrawLine(transform.position + Vector3.up * 0.15f, center, color, 0f, false);
    }

    private void LogExtinguishV2Activity(string detail)
    {
        activityDebug?.LogExtinguishV2(this, detail);
    }

    private void MoveToExtinguishV2ActivityDestination(Vector3 destination)
    {
        bool previousSuppressPathFlowLogging = suppressPathFlowLogging;
        suppressPathFlowLogging = true;
        MoveTo(destination);
        suppressPathFlowLogging = previousSuppressPathFlowLogging;
    }

}
