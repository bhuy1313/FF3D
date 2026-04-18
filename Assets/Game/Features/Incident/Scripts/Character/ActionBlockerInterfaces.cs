using UnityEngine;

public interface IInventorySelectionBlocker
{
    bool BlocksInventorySelectionChange(GameObject owner);
}

public interface IJumpActionBlocker
{
    bool BlocksJumpAction(GameObject owner);
}
