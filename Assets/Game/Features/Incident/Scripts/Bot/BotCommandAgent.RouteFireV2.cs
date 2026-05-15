using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private enum BotRouteFireV2Step
    {
        None = 0,
        ResolveTarget = 1,
        SelectTool = 2,
        PrepareHeldTool = 3,
        MoveToTool = 4,
        PickupTool = 5,
        ReadyToSuppress = 6,
        SuppressFire = 7,
        Complete = 8,
        Failed = 9
    }

    private enum BotRouteFireV2Caller
    {
        None = 0,
        ExtinguishV2 = 1,
        RescueV2 = 2
    }

    private sealed class BotRouteFireV2State
    {
        public BotRouteFireV2Step Step;
        public BotRouteFireV2Caller Caller;
        public string FailureReason;
        public Vector3 OriginalDestination;
        public IFireTarget FireTarget;
        public IFireGroupTarget FireGroup;
        public Vector3 FirePosition;
        public IBotExtinguisherItem Tool;
        public IPickupable PickupTarget;
        public bool HasIssuedToolMove;
        public Vector3 IssuedToolDestination;
        public float StepReadyTime;

        public void Reset()
        {
            Step = BotRouteFireV2Step.None;
            Caller = BotRouteFireV2Caller.None;
            FailureReason = string.Empty;
            OriginalDestination = default;
            FireTarget = null;
            FireGroup = null;
            FirePosition = default;
            Tool = null;
            PickupTarget = null;
            HasIssuedToolMove = false;
            IssuedToolDestination = default;
            StepReadyTime = 0f;
        }
    }

    private readonly BotRouteFireV2State routeFireV2State = new BotRouteFireV2State();

    private bool IsRouteFireV2Active => routeFireV2State.Step != BotRouteFireV2Step.None;

    private void BeginRouteFireV2(
        BotRouteFireV2Caller caller,
        Vector3 originalDestination,
        IFireTarget fireTarget,
        IFireGroupTarget fireGroup)
    {
        routeFireV2State.Reset();
        SetRouteFireV2Step(BotRouteFireV2Step.ResolveTarget, false);
        routeFireV2State.Caller = caller;
        routeFireV2State.OriginalDestination = originalDestination;
        routeFireV2State.FireTarget = fireTarget;
        routeFireV2State.FireGroup = fireGroup;
        routeFireV2State.StepReadyTime = 0f;
    }

    private void TickRouteFireV2()
    {
        if (Time.time < routeFireV2State.StepReadyTime)
        {
            return;
        }

        switch (routeFireV2State.Step)
        {
            case BotRouteFireV2Step.ResolveTarget:
                TryResolveRouteFireV2Target();
                break;
            case BotRouteFireV2Step.SelectTool:
                TrySelectRouteFireV2Tool();
                break;
            case BotRouteFireV2Step.PrepareHeldTool:
                TryPrepareRouteFireV2HeldTool();
                break;
            case BotRouteFireV2Step.MoveToTool:
                TryIssueRouteFireV2MoveToTool();
                break;
            case BotRouteFireV2Step.PickupTool:
                TryCompleteRouteFireV2ToolPickup();
                break;
            case BotRouteFireV2Step.ReadyToSuppress:
                TryPrepareRouteFireV2Suppression();
                break;
            case BotRouteFireV2Step.SuppressFire:
                TryApplyRouteFireV2Suppression();
                break;
            case BotRouteFireV2Step.Complete:
                CompleteRouteFireV2();
                break;
            case BotRouteFireV2Step.Failed:
                FailRouteFireV2();
                break;
        }
    }

    private void SetRouteFireV2Step(BotRouteFireV2Step nextStep, bool applyDelay = true)
    {
        if (routeFireV2State.Step == nextStep)
        {
            return;
        }

        routeFireV2State.Step = nextStep;
        bool shouldDelay =
            applyDelay &&
            nextStep != BotRouteFireV2Step.None &&
            nextStep != BotRouteFireV2Step.Complete &&
            nextStep != BotRouteFireV2Step.Failed;
        routeFireV2State.StepReadyTime = shouldDelay
            ? Time.time + Mathf.Max(0f, v2FlowStepTransitionDelay)
            : 0f;
    }

    private string GetRouteFireV2TaskDetail()
    {
        return routeFireV2State.Step switch
        {
            BotRouteFireV2Step.ResolveTarget => "Resolving RouteFireV2 target.",
            BotRouteFireV2Step.SelectTool => "Selecting RouteFireV2 tool.",
            BotRouteFireV2Step.PrepareHeldTool => "Preparing held tool for RouteFireV2.",
            BotRouteFireV2Step.MoveToTool => "Moving to RouteFireV2 tool.",
            BotRouteFireV2Step.PickupTool => "Picking up RouteFireV2 tool.",
            BotRouteFireV2Step.ReadyToSuppress => "Ready to suppress RouteFireV2 fire.",
            BotRouteFireV2Step.SuppressFire => "Suppressing RouteFireV2 fire.",
            BotRouteFireV2Step.Failed => routeFireV2State.FailureReason,
            _ => "Executing RouteFireV2."
        };
    }

    private bool TryResolveRouteFireV2Target()
    {
        IFireTarget fireTarget = routeFireV2State.FireTarget;
        if ((fireTarget == null || !fireTarget.IsBurning) &&
            routeFireV2State.FireGroup != null &&
            routeFireV2State.FireGroup.HasActiveFires)
        {
            fireTarget = ResolveRepresentativeFireTarget(routeFireV2State.FireGroup, transform.position);
        }

        if (fireTarget == null || !fireTarget.IsBurning)
        {
            routeFireV2State.FailureReason = "RouteFireV2 fire target is no longer valid.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        routeFireV2State.FireTarget = fireTarget;
        routeFireV2State.FirePosition = fireTarget.GetWorldPosition();
        SetRouteFireV2Step(BotRouteFireV2Step.SelectTool);
        return true;
    }

    private bool TrySelectRouteFireV2Tool()
    {
        IFireTarget fireTarget = routeFireV2State.FireTarget;
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            routeFireV2State.FailureReason = "RouteFireV2 fire target disappeared before tool selection.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        if (!TryResolveSuppressionTool(
            routeFireV2State.FirePosition,
            routeFireV2State.FirePosition,
            routeFireV2State.FireGroup,
            fireTarget,
            BotExtinguishCommandMode.PointFire,
            BotExtinguishEngagementMode.DirectBestTool,
            false,
            out IBotExtinguisherItem routeTool))
        {
            routeFireV2State.FailureReason = $"No usable RouteFireV2 suppression tool found for '{GetDebugTargetName(fireTarget)}'.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        routeFireV2State.Tool = routeTool;
        routeFireV2State.PickupTarget = routeTool as IPickupable;
        SetRouteFireV2Step(BotRouteFireV2Step.PrepareHeldTool);
        return true;
    }

    private bool TryPrepareRouteFireV2HeldTool()
    {
        if (routeFireV2State.Tool == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 has no selected tool to prepare.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        if (!TryPrepareActiveSuppressionItemForToolSwitch(routeFireV2State.Tool))
        {
            routeFireV2State.FailureReason = "RouteFireV2 could not prepare the currently held item for tool switching.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        if (routeFireV2State.Tool.CurrentHolder == gameObject)
        {
            if (!TryEquipRouteFireV2Tool(routeFireV2State.Tool))
            {
                routeFireV2State.FailureReason = "RouteFireV2 could not equip the selected suppression tool after switching items.";
                SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
                return false;
            }

            SetRouteFireV2Step(BotRouteFireV2Step.ReadyToSuppress);
            return true;
        }

        SetRouteFireV2Step(BotRouteFireV2Step.MoveToTool);
        return true;
    }

    private bool TryPrepareActiveSuppressionItemForToolSwitch(IBotExtinguisherItem desiredTool)
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
        if (!DoesHeldObjectBlockStowForSuppressionToolSwitch(heldObject))
        {
            bool stowed = inventorySystem.StowActiveItem();
            if (stowed)
            {
                ClearDisplacedSuppressionToolReferences(activeItem);
            }

            return stowed;
        }

        Quaternion dropRotation;
        Vector3 dropPosition = ResolveBulkyToolDropPosition(activeBody.transform, out dropRotation);
        bool dropped = inventorySystem.DropItem(activeItem, dropPosition, dropRotation);
        if (dropped)
        {
            ClearDisplacedSuppressionToolReferences(activeItem);
        }

        return dropped;
    }

    private bool DoesHeldObjectBlockStowForSuppressionToolSwitch(GameObject heldObject)
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

    private void ClearDisplacedSuppressionToolReferences(IPickupable displacedItem)
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

    private bool TryIssueRouteFireV2MoveToTool()
    {
        if (routeFireV2State.Tool == null ||
            routeFireV2State.PickupTarget == null ||
            routeFireV2State.PickupTarget.Rigidbody == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 selected tool cannot be picked up.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        Vector3 destination = ResolveExtinguishV2NavDestination(routeFireV2State.PickupTarget.Rigidbody.transform.position);
        if (routeFireV2State.HasIssuedToolMove &&
            (destination - routeFireV2State.IssuedToolDestination).sqrMagnitude <= 0.04f)
        {
            SetRouteFireV2Step(BotRouteFireV2Step.PickupTool);
            return true;
        }

        routeFireV2State.HasIssuedToolMove = true;
        routeFireV2State.IssuedToolDestination = destination;
        if (!TrySetDestinationDirect(destination))
        {
            routeFireV2State.FailureReason = "RouteFireV2 failed to move to the selected tool.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        SetRouteFireV2Step(BotRouteFireV2Step.PickupTool);
        return true;
    }

    private bool TryCompleteRouteFireV2ToolPickup()
    {
        if (routeFireV2State.Tool == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 has no selected tool to pick up.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        if (TryEquipRouteFireV2Tool(routeFireV2State.Tool))
        {
            SetRouteFireV2Step(BotRouteFireV2Step.ReadyToSuppress);
            return true;
        }

        if (routeFireV2State.PickupTarget == null || routeFireV2State.PickupTarget.Rigidbody == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 pickup target disappeared.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        float distance = GetExtinguishV2HorizontalPickupDistance(routeFireV2State.PickupTarget, transform.position);
        if (distance > pickupDistance)
        {
            Vector3 destination = ResolveExtinguishV2NavDestination(routeFireV2State.PickupTarget.Rigidbody.transform.position);
            if (!TrySetDestinationDirect(destination))
            {
                routeFireV2State.FailureReason = "RouteFireV2 failed to close distance to the selected tool.";
                SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
                return false;
            }

            return true;
        }

        if (inventorySystem == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 has no inventory system for tool pickup.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        SetPickupWindow(true, routeFireV2State.PickupTarget);
        bool pickedUp = inventorySystem.TryPickup(routeFireV2State.PickupTarget);
        SetPickupWindow(false, null);
        if (!pickedUp || !TryEquipRouteFireV2Tool(routeFireV2State.Tool))
        {
            routeFireV2State.FailureReason = "RouteFireV2 failed to pick up the selected tool.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        SetRouteFireV2Step(BotRouteFireV2Step.ReadyToSuppress);
        return true;
    }

    private bool TryEquipRouteFireV2Tool(IBotExtinguisherItem tool)
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
        if (!equipped && tool.CurrentHolder == gameObject)
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

        activeExtinguisher = tool;
        return true;
    }

    private bool TryPrepareRouteFireV2Suppression()
    {
        IFireTarget fireTarget = routeFireV2State.FireTarget;
        if (routeFireV2State.Tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            routeFireV2State.FailureReason = "RouteFireV2 fire target or tool is no longer valid.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        routeFireV2State.FirePosition = fireTarget.GetWorldPosition();
        StopNavMeshMovement();
        SetRouteFireV2Step(BotRouteFireV2Step.SuppressFire);
        return true;
    }

    private bool TryApplyRouteFireV2Suppression()
    {
        IFireTarget fireTarget = routeFireV2State.FireTarget;
        if (routeFireV2State.Tool == null || fireTarget == null)
        {
            routeFireV2State.FailureReason = "RouteFireV2 lost its fire target or tool during suppression.";
            SetRouteFireV2Step(BotRouteFireV2Step.Failed, false);
            return false;
        }

        if (!fireTarget.IsBurning)
        {
            SetRouteFireV2Step(BotRouteFireV2Step.Complete, false);
            return true;
        }

        routeFireV2State.FirePosition = fireTarget.GetWorldPosition();
        activeExtinguisher = routeFireV2State.Tool;

        PreparePointFireExtinguisherSuppression(fireTarget, routeFireV2State.FirePosition, transform.position);
        currentExtinguishLaunchDirection = Vector3.zero;
        hasCurrentExtinguishLaunchDirection = false;
        if (!IsAimSettled(activeExtinguisher, routeFireV2State.FirePosition))
        {
            StopExtinguisher();
            return true;
        }

        if (!IsPointFireExtinguisherSprayReady(routeFireV2State.FirePosition, false))
        {
            return true;
        }

        SprayPointFireExtinguisher(
            fireTarget,
            routeFireV2State.FirePosition,
            routeFireDetectionRadius,
            false,
            false,
            "Clearing fire from RouteFireV2.");

        if (!fireTarget.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            SetRouteFireV2Step(BotRouteFireV2Step.Complete, false);
        }

        return true;
    }

    private void CompleteRouteFireV2()
    {
        BotRouteFireV2Caller caller = routeFireV2State.Caller;
        routeFireV2State.Reset();
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        activeExtinguisher = null;
        sprayReadyTime = -1f;

        if (caller == BotRouteFireV2Caller.ExtinguishV2)
        {
            TryResumeExtinguishV2();
        }
        else if (caller == BotRouteFireV2Caller.RescueV2)
        {
            TryResumeRescueV2();
        }
    }

    private void FailRouteFireV2()
    {
        BotRouteFireV2Caller caller = routeFireV2State.Caller;
        string failureReason = string.IsNullOrWhiteSpace(routeFireV2State.FailureReason)
            ? "RouteFireV2 failed."
            : routeFireV2State.FailureReason;
        routeFireV2State.Reset();
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        activeExtinguisher = null;
        sprayReadyTime = -1f;

        if (caller == BotRouteFireV2Caller.ExtinguishV2 && IsExtinguishV2Active)
        {
            extinguishV2State.FailureReason = failureReason;
            extinguishV2State.IsPaused = false;
            extinguishV2State.PauseReason = BotExtinguishV2PauseReason.None;
            extinguishV2State.PauseDetail = string.Empty;
            extinguishV2State.PausedAtTime = 0f;
            extinguishV2State.Step = BotExtinguishV2Step.Failed;
        }
        else if (caller == BotRouteFireV2Caller.RescueV2 && IsRescueV2Active)
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
