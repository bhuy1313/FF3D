using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private enum BotPathClearingV2Step
    {
        None = 0,
        ResolveTarget = 1,
        SelectTool = 2,
        PrepareHeldTool = 3,
        MoveToTool = 4,
        PickupTool = 5,
        MoveToBlocker = 6,
        ClearBlocker = 7,
        Complete = 8,
        Failed = 9
    }

    private enum BotPathClearingV2Caller
    {
        None = 0,
        ExtinguishV2 = 1,
        RescueV2 = 2
    }

    private sealed class BotPathClearingV2State
    {
        public BotPathClearingV2Step Step;
        public BotPathClearingV2Caller Caller;
        public string FailureReason;
        public Vector3 OriginalDestination;
        public IBotBreakableTarget BlockedBreakable;
        public IBotPryTarget BlockedPryTarget;
        public IBotBreakTool Tool;
        public IPickupable PickupTarget;
        public bool HasIssuedToolMove;
        public Vector3 IssuedToolDestination;
        public bool HasIssuedBlockerMove;
        public Vector3 IssuedBlockerDestination;
        public float StepReadyTime;

        public void Reset()
        {
            Step = BotPathClearingV2Step.None;
            Caller = BotPathClearingV2Caller.None;
            FailureReason = string.Empty;
            OriginalDestination = default;
            BlockedBreakable = null;
            BlockedPryTarget = null;
            Tool = null;
            PickupTarget = null;
            HasIssuedToolMove = false;
            IssuedToolDestination = default;
            HasIssuedBlockerMove = false;
            IssuedBlockerDestination = default;
            StepReadyTime = 0f;
        }
    }

    private readonly BotPathClearingV2State pathClearingV2State = new BotPathClearingV2State();

    private bool IsPathClearingV2Active => pathClearingV2State.Step != BotPathClearingV2Step.None;

    private void BeginPathClearingV2(
        BotPathClearingV2Caller caller,
        Vector3 originalDestination,
        IBotBreakableTarget blockedBreakable,
        IBotPryTarget blockedPryTarget)
    {
        pathClearingV2State.Reset();
        pathClearingV2State.Step = BotPathClearingV2Step.ResolveTarget;
        pathClearingV2State.Caller = caller;
        pathClearingV2State.OriginalDestination = originalDestination;
        pathClearingV2State.BlockedBreakable = blockedBreakable;
        pathClearingV2State.BlockedPryTarget = blockedPryTarget;
        pathClearingV2State.StepReadyTime = 0f;
    }

    private void TickPathClearingV2()
    {
        if (Time.time < pathClearingV2State.StepReadyTime)
        {
            return;
        }

        switch (pathClearingV2State.Step)
        {
            case BotPathClearingV2Step.ResolveTarget:
                TryResolvePathClearingV2Target();
                break;
            case BotPathClearingV2Step.SelectTool:
                TrySelectPathClearingV2Tool();
                break;
            case BotPathClearingV2Step.PrepareHeldTool:
                TryPreparePathClearingV2HeldTool();
                break;
            case BotPathClearingV2Step.MoveToTool:
                TryIssuePathClearingV2MoveToTool();
                break;
            case BotPathClearingV2Step.PickupTool:
                TryCompletePathClearingV2ToolPickup();
                break;
            case BotPathClearingV2Step.MoveToBlocker:
                TryIssuePathClearingV2MoveToBlocker();
                break;
            case BotPathClearingV2Step.ClearBlocker:
                TryApplyPathClearingV2ClearBlocker();
                break;
            case BotPathClearingV2Step.Complete:
                CompletePathClearingV2();
                break;
            case BotPathClearingV2Step.Failed:
                FailPathClearingV2();
                break;
        }
    }

    private void SetPathClearingV2Step(BotPathClearingV2Step nextStep, bool applyDelay = true)
    {
        if (pathClearingV2State.Step == nextStep)
        {
            return;
        }

        pathClearingV2State.Step = nextStep;
        bool shouldDelay =
            applyDelay &&
            nextStep != BotPathClearingV2Step.None &&
            nextStep != BotPathClearingV2Step.Complete &&
            nextStep != BotPathClearingV2Step.Failed;
        pathClearingV2State.StepReadyTime = shouldDelay
            ? Time.time + Mathf.Max(0f, v2FlowStepTransitionDelay)
            : 0f;
    }

    private bool TryResolvePathClearingV2Target()
    {
        if (IsPathClearingV2PryTargetValid(pathClearingV2State.BlockedPryTarget))
        {
            pathClearingV2State.BlockedBreakable = null;
            SetPathClearingV2Step(BotPathClearingV2Step.SelectTool);
            return true;
        }

        if (IsPathClearingV2BreakableValid(pathClearingV2State.BlockedBreakable))
        {
            pathClearingV2State.BlockedPryTarget = null;
            SetPathClearingV2Step(BotPathClearingV2Step.SelectTool);
            return true;
        }

        pathClearingV2State.FailureReason = "Path Clearing V2 blocker is no longer valid.";
        SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
        return false;
    }

    private string GetPathClearingV2TaskDetail()
    {
        return pathClearingV2State.Step switch
        {
            BotPathClearingV2Step.ResolveTarget => "Resolving Path Clearing V2 blocker.",
            BotPathClearingV2Step.SelectTool => "Selecting Path Clearing V2 tool.",
            BotPathClearingV2Step.PrepareHeldTool => "Preparing held tool for Path Clearing V2.",
            BotPathClearingV2Step.MoveToTool => "Moving to Path Clearing V2 tool.",
            BotPathClearingV2Step.PickupTool => "Picking up Path Clearing V2 tool.",
            BotPathClearingV2Step.MoveToBlocker => "Moving to Path Clearing V2 blocker.",
            BotPathClearingV2Step.ClearBlocker => "Clearing Path Clearing V2 blocker.",
            BotPathClearingV2Step.Failed => pathClearingV2State.FailureReason,
            _ => "Executing Path Clearing V2."
        };
    }

    private static bool IsPathClearingV2PryTargetValid(IBotPryTarget pryTarget)
    {
        return pryTarget != null &&
               !pryTarget.IsBreached &&
               (pryTarget.CanBePriedOpen || pryTarget.IsPryInProgress);
    }

    private static bool IsPathClearingV2BreakableValid(IBotBreakableTarget breakableTarget)
    {
        return breakableTarget != null &&
               !breakableTarget.IsBroken &&
               breakableTarget.CanBeClearedByBot;
    }

    private bool TrySelectPathClearingV2Tool()
    {
        IBotBreakTool tool = null;

        if (pathClearingV2State.BlockedPryTarget != null)
        {
            tool = ResolvePreferredPryTool();
            if (tool == null)
            {
                pathClearingV2State.FailureReason = $"No usable crowbar found for '{GetDebugTargetName(pathClearingV2State.BlockedPryTarget)}'.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }
        }
        else if (pathClearingV2State.BlockedBreakable != null)
        {
            IBotBreakableTarget previousBlockedBreakable = currentBlockedBreakable;
            currentBlockedBreakable = pathClearingV2State.BlockedBreakable;
            tool = ResolveCommittedBreakTool();
            currentBlockedBreakable = previousBlockedBreakable;

            if (tool == null)
            {
                pathClearingV2State.FailureReason = $"No usable break tool found for '{GetDebugTargetName(pathClearingV2State.BlockedBreakable)}'.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }
        }
        else
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 has no blocker to choose a tool for.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        pathClearingV2State.Tool = tool;
        pathClearingV2State.PickupTarget = tool as IPickupable;
        SetPathClearingV2Step(BotPathClearingV2Step.PrepareHeldTool);
        return true;
    }

    private bool TryPreparePathClearingV2HeldTool()
    {
        if (pathClearingV2State.Tool == null)
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 has no selected tool to prepare.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        if (!TryPrepareActiveItemForPathClearingV2(pathClearingV2State.Tool))
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 could not prepare the currently held item for tool switching.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        if (pathClearingV2State.Tool.IsHeldBy(gameObject))
        {
            if (!TryEquipPathClearingV2Tool(pathClearingV2State.Tool))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 could not equip the selected break tool after switching items.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            SetPathClearingV2Step(BotPathClearingV2Step.MoveToBlocker);
            return true;
        }

        SetPathClearingV2Step(BotPathClearingV2Step.MoveToTool);
        return true;
    }

    private bool TryPrepareActiveItemForPathClearingV2(IBotBreakTool desiredTool)
    {
        if (desiredTool == null || inventorySystem == null)
        {
            return false;
        }

        IPickupable activeItem = inventorySystem.ActiveItem;
        if (activeItem == null)
        {
            return true;
        }

        if (ReferenceEquals(activeItem, desiredTool as IPickupable))
        {
            return true;
        }

        Rigidbody activeBody = activeItem.Rigidbody;
        if (activeBody == null)
        {
            return inventorySystem.StowActiveItem();
        }

        GameObject heldObject = activeBody.gameObject;
        if (!DoesPathClearingV2HeldObjectBlockStow(heldObject))
        {
            bool stowed = inventorySystem.StowActiveItem();
            if (stowed)
            {
                ClearPathClearingV2DisplacedToolReferences(activeItem);
            }

            return stowed;
        }

        Quaternion dropRotation;
        Vector3 dropPosition = ResolveBulkyToolDropPosition(activeBody.transform, out dropRotation);
        bool dropped = inventorySystem.DropItem(activeItem, dropPosition, dropRotation);
        if (dropped)
        {
            ClearPathClearingV2DisplacedToolReferences(activeItem);
        }

        return dropped;
    }

    private bool DoesPathClearingV2HeldObjectBlockStow(GameObject heldObject)
    {
        if (heldObject == null)
        {
            return false;
        }

        MonoBehaviour[] components = heldObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is IInventoryStowBlocker blocker && blocker.BlocksInventoryStow(gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPathClearingV2DisplacedToolReferences(IPickupable displacedItem)
    {
        if (displacedItem == null)
        {
            return;
        }

        if (displacedItem is IBotExtinguisherItem extinguisherItem)
        {
            if (ReferenceEquals(activeExtinguisher, extinguisherItem))
            {
                StopExtinguisher();
                activeExtinguisher = null;
            }

            if (ReferenceEquals(preferredExtinguishTool, extinguisherItem))
            {
                preferredExtinguishTool = null;
            }
        }

        if (displacedItem is IBotBreakTool breakTool && ReferenceEquals(activeBreakTool, breakTool))
        {
            activeBreakTool = null;
        }

        SetPickupWindow(false, null);
    }

    private bool TryIssuePathClearingV2MoveToTool()
    {
        if (pathClearingV2State.Tool == null ||
            pathClearingV2State.PickupTarget == null ||
            pathClearingV2State.PickupTarget.Rigidbody == null)
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 selected tool cannot be picked up.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        Vector3 destination = ResolveExtinguishV2NavDestination(pathClearingV2State.PickupTarget.Rigidbody.transform.position);
        if (pathClearingV2State.HasIssuedToolMove &&
            (destination - pathClearingV2State.IssuedToolDestination).sqrMagnitude <= 0.04f)
        {
            SetPathClearingV2Step(BotPathClearingV2Step.PickupTool);
            return true;
        }

        pathClearingV2State.HasIssuedToolMove = true;
        pathClearingV2State.IssuedToolDestination = destination;
        if (!TrySetDestinationDirect(destination))
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 failed to move to the selected tool.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        SetPathClearingV2Step(BotPathClearingV2Step.PickupTool);
        return true;
    }

    private bool TryCompletePathClearingV2ToolPickup()
    {
        if (pathClearingV2State.Tool == null)
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 has no selected tool to pick up.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        if (TryEquipPathClearingV2Tool(pathClearingV2State.Tool))
        {
            SetPathClearingV2Step(BotPathClearingV2Step.MoveToBlocker);
            return true;
        }

        if (pathClearingV2State.PickupTarget == null || pathClearingV2State.PickupTarget.Rigidbody == null)
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 pickup target disappeared.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        float distance = GetExtinguishV2HorizontalPickupDistance(pathClearingV2State.PickupTarget, transform.position);
        if (distance > pickupDistance)
        {
            Vector3 destination = ResolveExtinguishV2NavDestination(pathClearingV2State.PickupTarget.Rigidbody.transform.position);
            if (!TrySetDestinationDirect(destination))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 failed to close distance to the selected tool.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            return true;
        }

        if (inventorySystem == null)
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 has no inventory system for tool pickup.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        SetPickupWindow(true, pathClearingV2State.PickupTarget);
        bool pickedUp = inventorySystem.TryPickup(pathClearingV2State.PickupTarget);
        SetPickupWindow(false, null);
        if (!pickedUp || !TryEquipPathClearingV2Tool(pathClearingV2State.Tool))
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 failed to pick up the selected tool.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        SetPathClearingV2Step(BotPathClearingV2Step.MoveToBlocker);
        return true;
    }

    private bool TryEquipPathClearingV2Tool(IBotBreakTool tool)
    {
        if (tool == null || inventorySystem == null)
        {
            return false;
        }

        if (tool is not IPickupable pickupable)
        {
            return false;
        }

        bool equipped = inventorySystem.TryEquipItem(pickupable);
        if (!equipped && tool.IsHeldBy(gameObject))
        {
            if (inventorySystem.TryPickup(pickupable))
            {
                equipped = inventorySystem.TryEquipItem(pickupable);
            }
        }

        if (!equipped)
        {
            return false;
        }

        activeBreakTool = tool;
        return true;
    }

    private bool TryIssuePathClearingV2MoveToBlocker()
    {
        if (!TryResolvePathClearingV2BlockerMoveDestination(out Vector3 destination))
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 blocker no longer has a valid move destination.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        if (pathClearingV2State.HasIssuedBlockerMove &&
            (destination - pathClearingV2State.IssuedBlockerDestination).sqrMagnitude <= 0.04f)
        {
            SetPathClearingV2Step(BotPathClearingV2Step.ClearBlocker);
            return true;
        }

        pathClearingV2State.HasIssuedBlockerMove = true;
        pathClearingV2State.IssuedBlockerDestination = destination;
        if (!TrySetDestinationDirect(destination))
        {
            pathClearingV2State.FailureReason = "Path Clearing V2 failed to move to the blocker.";
            SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
            return false;
        }

        SetPathClearingV2Step(BotPathClearingV2Step.ClearBlocker);
        return true;
    }

    private bool TryResolvePathClearingV2BlockerMoveDestination(out Vector3 destination)
    {
        destination = default;

        if (IsPathClearingV2PryTargetValid(pathClearingV2State.BlockedPryTarget))
        {
            destination = ResolveExtinguishV2NavDestination(pathClearingV2State.BlockedPryTarget.GetWorldPosition());
            return true;
        }

        if (IsPathClearingV2BreakableValid(pathClearingV2State.BlockedBreakable))
        {
            Vector3 targetPosition = pathClearingV2State.BlockedBreakable.GetWorldPosition();
            if (pathClearingV2State.BlockedBreakable.TryGetBreakStandPose(transform.position, out Vector3 standPosition, out _))
            {
                destination = ResolveExtinguishV2NavDestination(standPosition);
                return true;
            }

            IBotBreakTool tool = activeBreakTool ?? pathClearingV2State.Tool;
            float preferredDistance = tool != null
                ? Mathf.Clamp(tool.PreferredBreakDistance, 0.5f, tool.MaxBreakDistance)
                : 1f;
            destination = ResolveStandPositionAroundPoint(transform.position, targetPosition, preferredDistance);
            return true;
        }

        return false;
    }

    private bool TryApplyPathClearingV2ClearBlocker()
    {
        if (pathClearingV2State.BlockedPryTarget != null)
        {
            if (pathClearingV2State.BlockedPryTarget.IsBreached)
            {
                SetPathClearingV2Step(BotPathClearingV2Step.Complete, false);
                return true;
            }

            if (activeBreakTool == null ||
                activeBreakTool.ToolKind != BreakToolKind.Crowbar ||
                !activeBreakTool.IsAvailableTo(gameObject))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 lost the crowbar required for the pry target.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            if (!HandleEquippedPryToolAgainstTarget(activeBreakTool, pathClearingV2State.BlockedPryTarget))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 failed while prying the blocker.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            if (pathClearingV2State.BlockedPryTarget.IsBreached)
            {
                SetPathClearingV2Step(BotPathClearingV2Step.Complete, false);
                return true;
            }

            return true;
        }

        if (pathClearingV2State.BlockedBreakable != null)
        {
            if (pathClearingV2State.BlockedBreakable.IsBroken || !pathClearingV2State.BlockedBreakable.CanBeClearedByBot)
            {
                SetPathClearingV2Step(BotPathClearingV2Step.Complete, false);
                return true;
            }

            if (activeBreakTool == null || !activeBreakTool.IsAvailableTo(gameObject))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 lost the break tool required for the blocker.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            if (!pathClearingV2State.BlockedBreakable.SupportsBreakTool(activeBreakTool.ToolKind))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 equipped tool is not valid for the blocker.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            if (!HandleEquippedBreakToolAgainstTarget(activeBreakTool, pathClearingV2State.BlockedBreakable))
            {
                pathClearingV2State.FailureReason = "Path Clearing V2 failed while breaking the blocker.";
                SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
                return false;
            }

            if (pathClearingV2State.BlockedBreakable.IsBroken)
            {
                SetPathClearingV2Step(BotPathClearingV2Step.Complete, false);
                return true;
            }

            return true;
        }

        pathClearingV2State.FailureReason = "Path Clearing V2 has no blocker to clear.";
        SetPathClearingV2Step(BotPathClearingV2Step.Failed, false);
        return false;
    }

    private void CompletePathClearingV2()
    {
        BotPathClearingV2Caller caller = pathClearingV2State.Caller;
        pathClearingV2State.Reset();
        activeBreakTool = null;

        if (caller == BotPathClearingV2Caller.ExtinguishV2)
        {
            TryResumeExtinguishV2();
        }
        else if (caller == BotPathClearingV2Caller.RescueV2)
        {
            TryResumeRescueV2();
        }
    }

    private void FailPathClearingV2()
    {
        BotPathClearingV2Caller caller = pathClearingV2State.Caller;
        string failureReason = string.IsNullOrWhiteSpace(pathClearingV2State.FailureReason)
            ? "Path Clearing V2 failed."
            : pathClearingV2State.FailureReason;
        pathClearingV2State.Reset();
        activeBreakTool = null;

        if (caller == BotPathClearingV2Caller.ExtinguishV2 && IsExtinguishV2Active)
        {
            extinguishV2State.FailureReason = failureReason;
            extinguishV2State.IsPaused = false;
            extinguishV2State.PauseReason = BotExtinguishV2PauseReason.None;
            extinguishV2State.PauseDetail = string.Empty;
            extinguishV2State.PausedAtTime = 0f;
            extinguishV2State.Step = BotExtinguishV2Step.Failed;
        }
        else if (caller == BotPathClearingV2Caller.RescueV2 && IsRescueV2Active)
        {
            rescueV2State.FailureReason = failureReason;
            rescueV2State.IsPaused = false;
            rescueV2State.PauseReason = BotRescueV2PauseReason.None;
            rescueV2State.PauseDetail = string.Empty;
            rescueV2State.PausedAtTime = 0f;
            rescueV2State.Step = BotRescueV2Step.Failed;
        }
    }
}
