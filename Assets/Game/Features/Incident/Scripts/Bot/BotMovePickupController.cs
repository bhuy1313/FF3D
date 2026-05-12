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
}

internal sealed class BotMovePickupController
{
    private IPickupable currentMovePickupTarget;

    internal bool HasTarget => currentMovePickupTarget != null;
    internal IPickupable CurrentTarget => currentMovePickupTarget;

    internal void SetTarget(IPickupable target)
    {
        currentMovePickupTarget = target;
    }

    internal void Reset()
    {
        currentMovePickupTarget = null;
    }

    internal bool TryIssueMoveToPickup(IPickupable pickupTarget, BotMovePickupOptions options, out Vector3 destination)
    {
        destination = default;
        if (pickupTarget == null || pickupTarget.Rigidbody == null || options?.NavMeshAgent == null)
        {
            return false;
        }

        if (!options.NavMeshAgent.enabled || !options.NavMeshAgent.isOnNavMesh)
        {
            return false;
        }

        destination = pickupTarget.Rigidbody.transform.position;
        if (options.NavMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, options.NavMeshSampleDistance, options.NavMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        options.PrepareForIssuedCommand?.Invoke(BotCommandType.Move);
        currentMovePickupTarget = pickupTarget;

        string pickupName = options.GetPickupableName != null
            ? options.GetPickupableName(pickupTarget)
            : "(unknown item)";
        options.LogPathFlow?.Invoke($"move-pickup-order:{pickupName}", $"Moving to pick up {pickupName}.");

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
            currentMovePickupTarget = null;
            return false;
        }

        return true;
    }

    internal bool TryCompleteMovePickupTarget(BotMovePickupOptions options)
    {
        if (currentMovePickupTarget == null || options?.InventorySystem == null)
        {
            currentMovePickupTarget = null;
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
                RefreshPickupPathIfNeeded(options);
                return false;
            }

            if (options.TryEnsureExtinguisherEquipped != null && options.TryEnsureExtinguisherEquipped(extinguisherItem))
            {
                options.SetPickupWindow?.Invoke(false, null);
                options.LogPathFlow?.Invoke($"move-pickup-success:{pickupName}", "Picked up item.");
                currentMovePickupTarget = null;
                return true;
            }

            return false;
        }

        if (currentMovePickupTarget is IBotBreakTool breakTool)
        {
            float breakToolDistance = GetPickupableHorizontalDistance(currentMovePickupTarget, options.BotTransform.position);
            if (breakToolDistance > options.PickupDistance)
            {
                options.SetPickupWindow?.Invoke(true, currentMovePickupTarget);
                options.LogPathFlow?.Invoke($"move-pickup-approach:{pickupName}", "Moving.");
                RefreshPickupPathIfNeeded(options);
                return false;
            }

            if (options.TryEnsureBreakToolEquipped != null && options.TryEnsureBreakToolEquipped(breakTool))
            {
                options.SetPickupWindow?.Invoke(false, null);
                options.LogPathFlow?.Invoke($"move-pickup-success:{pickupName}", "Picked up item.");
                currentMovePickupTarget = null;
                return true;
            }

            return false;
        }

        if (!(currentMovePickupTarget is Component) || currentMovePickupTarget.Rigidbody == null)
        {
            options.SetPickupWindow?.Invoke(false, null);
            options.LogPathFlow?.Invoke("move-pickup-invalid", "No item available to pick up.");
            currentMovePickupTarget = null;
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
        currentMovePickupTarget = null;
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

        Vector3 destination = currentMovePickupTarget.Rigidbody.transform.position;
        if (options.NavMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, options.NavMeshSampleDistance, options.NavMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        options.MoveToDestination(destination);
    }

    private static float GetPickupableHorizontalDistance(IPickupable pickupable, Vector3 fromPosition)
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

        Collider[] colliders = rigidbody.GetComponentsInChildren<Collider>(true);
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
}
