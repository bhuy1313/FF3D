using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class BreakActionLock : MonoBehaviour
{
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private FPSInteractionSystem interactionSystem;
    [SerializeField] private FPSInventorySystem inventorySystem;

    private int lockCount;
    private bool restoreFirstPersonController;
    private bool restoreInteractionSystem;
    private bool restoreInventorySystem;

    public static BreakActionLock GetOrCreate(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (!target.TryGetComponent(out BreakActionLock actionLock))
        {
            actionLock = target.AddComponent<BreakActionLock>();
        }

        actionLock.ResolveReferences();
        return actionLock;
    }

    public void Acquire()
    {
        ResolveReferences();
        if (lockCount == 0)
        {
            restoreFirstPersonController = firstPersonController != null && firstPersonController.enabled;
            restoreInteractionSystem = interactionSystem != null && interactionSystem.enabled;
            restoreInventorySystem = inventorySystem != null && inventorySystem.enabled;

            if (firstPersonController != null)
            {
                firstPersonController.enabled = false;
            }

            if (interactionSystem != null)
            {
                interactionSystem.enabled = false;
            }

            if (inventorySystem != null)
            {
                inventorySystem.enabled = false;
            }
        }

        lockCount++;
    }

    public void Release()
    {
        if (lockCount <= 0)
        {
            return;
        }

        lockCount--;
        if (lockCount > 0)
        {
            return;
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = restoreFirstPersonController;
        }

        if (interactionSystem != null)
        {
            interactionSystem.enabled = restoreInteractionSystem;
        }

        if (inventorySystem != null)
        {
            inventorySystem.enabled = restoreInventorySystem;
        }
    }

    private void ResolveReferences()
    {
        if (firstPersonController == null)
        {
            firstPersonController = GetComponent<FirstPersonController>();
        }

        if (interactionSystem == null)
        {
            interactionSystem = GetComponent<FPSInteractionSystem>();
        }

        if (inventorySystem == null)
        {
            inventorySystem = GetComponent<FPSInventorySystem>();
        }
    }
}
