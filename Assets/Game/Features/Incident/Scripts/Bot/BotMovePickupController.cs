using System;
using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

internal sealed class BotMovePickupOptions
{
    public Transform BotTransform;
    public NavMeshAgent NavMeshAgent;
    public BotBehaviorContext BehaviorContext;
    public BotInventorySystem InventorySystem;
    public float PickupDistance;
    public float NavMeshSampleDistance;
    public Action<BotCommandType> PrepareForIssuedCommand;
    public Action<string, string> LogPathFlow;
    public Func<IPickupable, string> GetPickupableName;
    public Func<Vector3, bool> MoveToDestination;
    public Func<bool> ShouldRefreshPathCheck;
    public Action<bool, IPickupable> SetPickupWindow;
    public Func<IBotExtinguisherItem, bool> TryEnsureExtinguisherEquipped;
    public Func<IBotBreakTool, bool> TryEnsureBreakToolEquipped;
    public bool EnablePickupDebug;
    public Action<string> LogPickupDebug;
}

internal sealed class BotMovePickupController
{
    private const float PickupPathReissueDistanceSq = 0.04f;
    private const float PickupDebugRepeatWindowSeconds = 1f;

    private IPickupable currentMovePickupTarget;
    private IPickupable cachedDistanceTarget;
    private Collider[] cachedDistanceColliders;
    private bool hasLastPickupPathDestination;
    private Vector3 lastPickupPathDestination;
    private IPickupable debugLastIssueTarget;
    private float debugLastIssueWindowStartTime;
    private int debugIssueCountInWindow;

    internal bool HasTarget => currentMovePickupTarget != null;
    internal IPickupable CurrentTarget => currentMovePickupTarget;

    internal void SetTarget(IPickupable target)
    {
        currentMovePickupTarget = target;
        ClearPickupRuntimeCache();
    }

    internal void Reset()
    {
        currentMovePickupTarget = null;
        ClearPickupRuntimeCache();
    }

    internal bool TryIssueMoveToPickup(IPickupable pickupTarget, BotMovePickupOptions options, out Vector3 destination)
    {
        destination = default;
        if (pickupTarget == null || pickupTarget.Rigidbody == null || options?.NavMeshAgent == null)
        {
            LogPickupDebug(options, $"issue-rejected invalid target={(pickupTarget == null ? "null" : GetPickupableDebugName(pickupTarget))} hasRigidbody={pickupTarget?.Rigidbody != null} hasAgent={options?.NavMeshAgent != null}");
            return false;
        }

        if (!options.NavMeshAgent.enabled || !options.NavMeshAgent.isOnNavMesh)
        {
            LogPickupDebug(options, $"issue-rejected agent unavailable target={GetPickupableDebugName(pickupTarget)} enabled={options.NavMeshAgent.enabled} onNavMesh={options.NavMeshAgent.isOnNavMesh}");
            return false;
        }

        if (currentMovePickupTarget == pickupTarget)
        {
            destination = hasLastPickupPathDestination
                ? lastPickupPathDestination
                : ResolvePickupDestination(pickupTarget, options);
            LogPickupDebug(options, $"issue-ignored same-target target={GetPickupableDebugName(pickupTarget)} destination={FormatVector(destination)} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending} status={options.NavMeshAgent.pathStatus}");
            return true;
        }

        destination = ResolvePickupDestination(pickupTarget, options);
        RecordPickupIssue(options, pickupTarget, destination);

        options.PrepareForIssuedCommand?.Invoke(BotCommandType.Move);
        currentMovePickupTarget = pickupTarget;

        string pickupName = options.GetPickupableName != null
            ? options.GetPickupableName(pickupTarget)
            : "(unknown item)";
        options.LogPathFlow?.Invoke($"move-pickup-order:{pickupName}", $"Moving to pick up {pickupName}.");

        hasLastPickupPathDestination = true;
        lastPickupPathDestination = destination;

        bool accepted;
        if (options.BehaviorContext != null && options.BehaviorContext.UseMoveOrdersAsBehaviorInput)
        {
            options.BehaviorContext.SetMoveOrder(destination);
            accepted = true;
        }
        else
        {
            options.NavMeshAgent.isStopped = false;
            accepted = options.MoveToDestination != null
                ? options.MoveToDestination(destination)
                : options.NavMeshAgent.SetDestination(destination);
        }

        if (!accepted)
        {
            LogPickupDebug(options, $"issue-move-failed target={pickupName} destination={FormatVector(destination)} stopped={options.NavMeshAgent.isStopped} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending} status={options.NavMeshAgent.pathStatus}");
            return true;
        }

        LogPickupDebug(options, $"issue-move-accepted target={pickupName} destination={FormatVector(destination)} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending} status={options.NavMeshAgent.pathStatus}");
        return true;
    }

    internal bool TryCompleteMovePickupTarget(BotMovePickupOptions options)
    {
        if (currentMovePickupTarget == null || options?.InventorySystem == null)
        {
            LogPickupDebug(options, $"complete-rejected target={(currentMovePickupTarget == null ? "null" : GetPickupableDebugName(currentMovePickupTarget))} hasInventory={options?.InventorySystem != null}");
            currentMovePickupTarget = null;
            ClearPickupRuntimeCache();
            return false;
        }

        string pickupName = options.GetPickupableName != null
            ? options.GetPickupableName(currentMovePickupTarget)
            : "(unknown item)";

        if (currentMovePickupTarget is IBotExtinguisherItem extinguisherItem)
        {
            float extinguisherDistance = GetPickupableHorizontalDistance(currentMovePickupTarget, options.BotTransform.position);
            if (extinguisherDistance > options.PickupDistance)
            {
                options.SetPickupWindow?.Invoke(true, currentMovePickupTarget);
                options.LogPathFlow?.Invoke($"move-pickup-approach:{pickupName}", "Moving.");
                LogPickupDebug(options, $"complete-wait extinguisher target={pickupName} distance={extinguisherDistance:F2} pickupDistance={options.PickupDistance:F2}");
                RefreshPickupPathIfNeeded(options);
                return false;
            }

            if (options.TryEnsureExtinguisherEquipped != null && options.TryEnsureExtinguisherEquipped(extinguisherItem))
            {
                options.SetPickupWindow?.Invoke(false, null);
                options.LogPathFlow?.Invoke($"move-pickup-success:{pickupName}", "Picked up item.");
                LogPickupDebug(options, $"complete-success extinguisher target={pickupName}");
                currentMovePickupTarget = null;
                ClearPickupRuntimeCache();
                return true;
            }

            LogPickupDebug(options, $"complete-equip-pending extinguisher target={pickupName}");
            return false;
        }

        if (currentMovePickupTarget is IBotBreakTool breakTool)
        {
            float breakToolDistance = GetPickupableHorizontalDistance(currentMovePickupTarget, options.BotTransform.position);
            if (breakToolDistance > options.PickupDistance)
            {
                options.SetPickupWindow?.Invoke(true, currentMovePickupTarget);
                options.LogPathFlow?.Invoke($"move-pickup-approach:{pickupName}", "Moving.");
                LogPickupDebug(options, $"complete-wait breaktool target={pickupName} distance={breakToolDistance:F2} pickupDistance={options.PickupDistance:F2}");
                RefreshPickupPathIfNeeded(options);
                return false;
            }

            if (options.TryEnsureBreakToolEquipped != null && options.TryEnsureBreakToolEquipped(breakTool))
            {
                options.SetPickupWindow?.Invoke(false, null);
                options.LogPathFlow?.Invoke($"move-pickup-success:{pickupName}", "Picked up item.");
                LogPickupDebug(options, $"complete-success breaktool target={pickupName}");
                currentMovePickupTarget = null;
                ClearPickupRuntimeCache();
                return true;
            }

            LogPickupDebug(options, $"complete-equip-pending breaktool target={pickupName}");
            return false;
        }

        if (!(currentMovePickupTarget is Component) || currentMovePickupTarget.Rigidbody == null)
        {
            options.SetPickupWindow?.Invoke(false, null);
            options.LogPathFlow?.Invoke("move-pickup-invalid", "No item available to pick up.");
            LogPickupDebug(options, $"complete-invalid target={pickupName}");
            currentMovePickupTarget = null;
            ClearPickupRuntimeCache();
            return false;
        }

        float horizontalDistance = GetPickupableHorizontalDistance(currentMovePickupTarget, options.BotTransform.position);
        if (horizontalDistance > options.PickupDistance)
        {
            options.SetPickupWindow?.Invoke(true, currentMovePickupTarget);
            options.LogPathFlow?.Invoke($"move-pickup-approach:{pickupName}", "Moving.");
            RefreshPickupPathIfNeeded(options);
            return false;
        }

        options.SetPickupWindow?.Invoke(true, currentMovePickupTarget);
        bool pickedUp = options.InventorySystem.TryPickup(currentMovePickupTarget);
        options.SetPickupWindow?.Invoke(false, null);
        options.LogPathFlow?.Invoke(
            pickedUp ? $"move-pickup-success:{pickupName}" : $"move-pickup-fail:{pickupName}",
            pickedUp ? "Picked up item." : "Failed to pick up item.");
        LogPickupDebug(options, $"complete-generic target={pickupName} pickedUp={pickedUp}");
        currentMovePickupTarget = null;
        ClearPickupRuntimeCache();
        return pickedUp;
    }

    private void RefreshPickupPathIfNeeded(BotMovePickupOptions options)
    {
        if (options?.MoveToDestination == null ||
            options.ShouldRefreshPathCheck == null ||
            !options.ShouldRefreshPathCheck() ||
            currentMovePickupTarget?.Rigidbody == null)
        {
            return;
        }

        Vector3 destination = ResolvePickupDestination(currentMovePickupTarget, options);

        if (hasLastPickupPathDestination &&
            (destination - lastPickupPathDestination).sqrMagnitude <= PickupPathReissueDistanceSq &&
            options.NavMeshAgent != null &&
            (options.NavMeshAgent.hasPath || options.NavMeshAgent.pathPending))
        {
            LogPickupDebug(options, $"refresh-skipped same-destination target={GetPickupableDebugName(currentMovePickupTarget)} destination={FormatVector(destination)} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending}");
            return;
        }

        if (options.MoveToDestination(destination))
        {
            hasLastPickupPathDestination = true;
            lastPickupPathDestination = destination;
            LogPickupDebug(options, $"refresh-move-accepted target={GetPickupableDebugName(currentMovePickupTarget)} destination={FormatVector(destination)} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending} status={options.NavMeshAgent.pathStatus}");
        }
        else
        {
            LogPickupDebug(options, $"refresh-move-failed target={GetPickupableDebugName(currentMovePickupTarget)} destination={FormatVector(destination)} hasPath={options.NavMeshAgent.hasPath} pending={options.NavMeshAgent.pathPending} status={options.NavMeshAgent.pathStatus}");
        }
    }

    private float GetPickupableHorizontalDistance(IPickupable pickupable, Vector3 fromPosition)
    {
        if (pickupable == null)
        {
            return float.PositiveInfinity;
        }

        Rigidbody rigidbody = pickupable.Rigidbody;
        if (rigidbody == null)
        {
            return float.PositiveInfinity;
        }

        Collider[] colliders = GetCachedDistanceColliders(pickupable, rigidbody);
        float bestDistance = float.PositiveInfinity;
        bool foundCollider = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Vector3 closestPoint = collider.ClosestPoint(fromPosition);
            Vector2 from2 = new Vector2(fromPosition.x, fromPosition.z);
            Vector2 target2 = new Vector2(closestPoint.x, closestPoint.z);
            float distance = Vector2.Distance(from2, target2);
            if (distance < bestDistance)
            {
                bestDistance = distance;
            }

            foundCollider = true;
        }

        if (foundCollider)
        {
            return bestDistance;
        }

        Vector3 position = rigidbody.transform.position;
        return Vector2.Distance(new Vector2(fromPosition.x, fromPosition.z), new Vector2(position.x, position.z));
    }

    private static Vector3 ResolvePickupDestination(IPickupable pickupTarget, BotMovePickupOptions options)
    {
        Vector3 destination = pickupTarget.Rigidbody.transform.position;
        if (options.NavMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, options.NavMeshSampleDistance, options.NavMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        return destination;
    }

    private Collider[] GetCachedDistanceColliders(IPickupable pickupable, Rigidbody rigidbody)
    {
        if (cachedDistanceTarget != pickupable || cachedDistanceColliders == null)
        {
            cachedDistanceTarget = pickupable;
            cachedDistanceColliders = rigidbody.GetComponentsInChildren<Collider>(true);
        }

        return cachedDistanceColliders;
    }

    private void ClearPickupRuntimeCache()
    {
        cachedDistanceTarget = null;
        cachedDistanceColliders = null;
        hasLastPickupPathDestination = false;
        lastPickupPathDestination = default;
    }

    private void RecordPickupIssue(BotMovePickupOptions options, IPickupable pickupTarget, Vector3 destination)
    {
        if (options == null || !options.EnablePickupDebug)
        {
            return;
        }

        float now = Time.time;
        if (debugLastIssueTarget != pickupTarget ||
            now - debugLastIssueWindowStartTime > PickupDebugRepeatWindowSeconds)
        {
            debugLastIssueTarget = pickupTarget;
            debugLastIssueWindowStartTime = now;
            debugIssueCountInWindow = 0;
        }

        debugIssueCountInWindow++;
        LogPickupDebug(options, $"issue-start target={GetPickupableDebugName(pickupTarget)} count1s={debugIssueCountInWindow} destination={FormatVector(destination)} current={(currentMovePickupTarget == null ? "null" : GetPickupableDebugName(currentMovePickupTarget))}");
        if (debugIssueCountInWindow >= 3)
        {
            LogPickupDebug(options, $"issue-repeat-warning target={GetPickupableDebugName(pickupTarget)} count1s={debugIssueCountInWindow}");
        }
    }

    private static void LogPickupDebug(BotMovePickupOptions options, string detail)
    {
        if (options == null || !options.EnablePickupDebug || string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        options.LogPickupDebug?.Invoke(detail);
    }

    private static string GetPickupableDebugName(IPickupable pickupable)
    {
        return pickupable is Component component && component != null
            ? component.name
            : "(unknown pickup)";
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }
}
