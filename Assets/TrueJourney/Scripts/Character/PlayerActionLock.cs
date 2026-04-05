using System.Collections;
using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerActionLock : MonoBehaviour
{
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private FPSInteractionSystem interactionSystem;
    [SerializeField] private FPSInventorySystem inventorySystem;
    [Header("Pose Snap")]
    [SerializeField] private bool smoothPoseSnap = true;
    [SerializeField] private float poseSnapDuration = 0.12f;
    [SerializeField] private float poseSnapArcHeight = 0.03f;

    private int fullLockCount;
    private int carryRestrictionCount;
    private bool restoreFirstPersonController;
    private bool restoreInteractionSystem;
    private bool restoreInventorySystem;
    private Coroutine poseSnapRoutine;
    private bool poseSnapRestoreCharacterController;

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
        CancelPoseSnap();

        bool shouldSmooth =
            smoothPoseSnap &&
            poseSnapDuration > 0.001f &&
            isActiveAndEnabled &&
            (transform.position - position).sqrMagnitude > 0.0001f;

        if (!shouldSmooth)
        {
            SetPoseImmediately(position, rotation);
            return;
        }

        poseSnapRoutine = StartCoroutine(SmoothSnapToPoseRoutine(position, rotation));
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

    private void SetPoseImmediately(Vector3 position, Quaternion rotation)
    {
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

    private IEnumerator SmoothSnapToPoseRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        poseSnapRestoreCharacterController = characterController != null && characterController.enabled;
        if (poseSnapRestoreCharacterController)
        {
            characterController.enabled = false;
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float duration = Mathf.Max(0.01f, poseSnapDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(startPosition, targetPosition, easedT);
            if (poseSnapArcHeight > 0f)
            {
                position += Vector3.up * (Mathf.Sin(easedT * Mathf.PI) * poseSnapArcHeight);
            }

            Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
            transform.SetPositionAndRotation(position, rotation);
            yield return null;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (poseSnapRestoreCharacterController && characterController != null)
        {
            characterController.enabled = true;
        }

        poseSnapRestoreCharacterController = false;
        poseSnapRoutine = null;
    }

    private void CancelPoseSnap()
    {
        if (poseSnapRoutine != null)
        {
            StopCoroutine(poseSnapRoutine);
            poseSnapRoutine = null;
        }

        if (poseSnapRestoreCharacterController && characterController != null)
        {
            characterController.enabled = true;
            poseSnapRestoreCharacterController = false;
        }
    }
}
