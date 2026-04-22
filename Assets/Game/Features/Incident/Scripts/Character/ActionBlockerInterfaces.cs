using UnityEngine;

public static class HandOccupancyUtility
{
    public static bool IsHandsOccupied(GameObject occupyingObject, GameObject owner)
    {
        return HasConstraint<IHandOccupyingObject>(occupyingObject, _ => true) ||
            BlocksInventoryStow(occupyingObject, owner) ||
            BlocksInventorySelectionChange(occupyingObject, owner) ||
            BlocksJumpAction(occupyingObject, owner);
    }

    public static bool BlocksInventoryStow(GameObject heldObject, GameObject owner)
    {
        return HasConstraint<IInventoryStowBlocker>(heldObject, blocker => blocker.BlocksInventoryStow(owner));
    }

    public static bool BlocksInventorySelectionChange(GameObject heldObject, GameObject owner)
    {
        return HasConstraint<IInventorySelectionBlocker>(heldObject, blocker => blocker.BlocksInventorySelectionChange(owner));
    }

    public static bool BlocksJumpAction(GameObject heldObject, GameObject owner)
    {
        return HasConstraint<IJumpActionBlocker>(heldObject, blocker => blocker.BlocksJumpAction(owner));
    }

    private static bool HasConstraint<TConstraint>(GameObject heldObject, System.Func<TConstraint, bool> evaluator)
        where TConstraint : class
    {
        if (heldObject == null || evaluator == null)
        {
            return false;
        }

        MonoBehaviour[] components = heldObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is TConstraint constraint && evaluator(constraint))
            {
                return true;
            }
        }

        return false;
    }
}
