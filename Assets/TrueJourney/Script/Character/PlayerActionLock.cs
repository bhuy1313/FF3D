using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerActionLock : MonoBehaviour
{
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private FPSInteractionSystem interactionSystem;
    [SerializeField] private FPSInventorySystem inventorySystem;

    private int fullLockCount;
    private int carryRestrictionCount;
    private bool restoreFirstPersonController;
    private bool restoreInteractionSystem;
    private bool restoreInventorySystem;

    public bool IsFullyLocked => fullLockCount > 0;
    public bool HasCarryRestriction => carryRestrictionCount > 0;
    public bool AllowsGeneralInteraction => !IsFullyLocked && !HasCarryRestriction;
    public bool AllowsSafeZoneInteractionOnly => !IsFullyLocked && HasCarryRestriction;
    public bool AllowsInventoryActions => !IsFullyLocked && !HasCarryRestriction;

    public static PlayerActionLock GetOrCreate(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (!target.TryGetComponent(out PlayerActionLock actionLock))
        {
            actionLock = target.AddComponent<PlayerActionLock>();
        }

        actionLock.ResolveReferences();
        return actionLock;
    }

    public void AcquireFullLock()
    {
        ResolveReferences();
        if (fullLockCount == 0)
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

        fullLockCount++;
    }

    public void ReleaseFullLock()
    {
        if (fullLockCount <= 0)
        {
            return;
        }

        fullLockCount--;
        if (fullLockCount > 0)
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

    public void AcquireCarryRestriction()
    {
        ResolveReferences();
        carryRestrictionCount++;
    }

    public void ReleaseCarryRestriction()
    {
        if (carryRestrictionCount <= 0)
        {
            return;
        }

        carryRestrictionCount--;
    }

    public void ReleaseAllLocks()
    {
        bool hadFullLock = fullLockCount > 0;
        fullLockCount = 0;
        carryRestrictionCount = 0;

        if (!hadFullLock)
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

    public void SnapToPose(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();

        bool restoreCharacterController = characterController != null && characterController.enabled;
        if (restoreCharacterController)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (restoreCharacterController && characterController != null)
        {
            characterController.enabled = true;
        }
    }

    protected virtual void ResolveReferences()
    {
        if (firstPersonController == null)
        {
            firstPersonController = GetComponent<FirstPersonController>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
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
