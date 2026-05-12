using System.Collections;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Door : MonoBehaviour, IInteractable, IOpenable, IPryOpenable, ISmokeVentPoint, IBotPryTarget
{
    public enum DoorOpenMode
    {
        Hinged = 0,
        DoubleSliding = 1
    }

    public enum DoorLockMode
    {
        None = 0,
        SoftLockedCrowbar = 1,
        Blocked = 2
    }

    [Header("Motion")]
    [SerializeField] private DoorOpenMode openMode = DoorOpenMode.Hinged;
    [Header("Hinged Door")]
    [SerializeField] private Transform hingedDoorTransform;
    [SerializeField] private float openAngle = -90f;
    [Header("Double Sliding Door")]
    [SerializeField] private Transform leftSlidingDoorTransform;
    [SerializeField] private Transform rightSlidingDoorTransform;
    [SerializeField] private Vector3 slidingLocalAxis = Vector3.right;
    [SerializeField] private float slidingOpenDistance = 1f;
    [Header("Motion Shared")]
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private float interactOpenDelay = 0f;
    [Header("Forced Entry")]
    [FormerlySerializedAs("requiresCrowbarToOpenWhenClosed")]
    [SerializeField] private bool legacyRequiresCrowbarToOpenWhenClosed;
    [SerializeField] private DoorLockMode lockMode = DoorLockMode.None;
    [SerializeField] private bool startsLocked;
    [SerializeField] private float pryOpenDuration = 0.75f;
    [FormerlySerializedAs("clearCrowbarRequirementAfterPry")]
    [SerializeField] private bool unlockOnSuccessfulPry = true;
    [SerializeField] private bool lockPlayerWhilePrying = true;
    [Header("Smoke")]
    [SerializeField] private float smokeVentilationReliefWhenOpen = 0.2f;
    [SerializeField] private float fireDraftRiskWhenOpen;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Header("Runtime")]
    [SerializeField] private bool isLocked;
    [SerializeField] private bool isPryInProgress;
    [SerializeField] private GameObject activePryer;
    [Header("Locked Shake Effect")]
    [SerializeField] private float lockedShakeDuration = 0.3f;
    [SerializeField] private float lockedShakeIntensity = 4.5f;
    [SerializeField] private float lockedShakeSpeed = 60f;

    private float lockedShakeTimer;

    private Transform doorTransform;
    private Quaternion closedLocalRotation;
    private Vector3 closedLocalEulerAngles;
    private Quaternion targetLocalRotation;
    private Vector3 leftClosedLocalPosition;
    private Vector3 rightClosedLocalPosition;
    private Vector3 leftTargetLocalPosition;
    private Vector3 rightTargetLocalPosition;
    private bool isOpen;
    private bool initialized;
    private int currentOpenDirection = -1;
    private Coroutine pryRoutine;
    private Coroutine pendingOpenRoutine;
    private PlayerActionLock activePlayerLock;
    private PlayerInteractionAnimationState activeAnimationState;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public DoorLockMode LockMode => lockMode;
    public bool CanBePriedOpen => !isOpen && isLocked && lockMode == DoorLockMode.SoftLockedCrowbar && !isPryInProgress;
    public bool IsPryInProgress => isPryInProgress;
    public bool IsBreached => isOpen || !isLocked || lockMode != DoorLockMode.SoftLockedCrowbar;
    public float SmokeVentilationRelief => isOpen ? smokeVentilationReliefWhenOpen : 0f;
    public float FireDraftRisk => isOpen ? fireDraftRiskWhenOpen : 0f;

    private void Awake()
    {
        InitializeDoorState();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterPryTarget(this);
    }

    private void OnValidate()
    {
        ApplyLegacyLockMigration();
        slidingOpenDistance = Mathf.Max(0f, slidingOpenDistance);
        if (slidingLocalAxis.sqrMagnitude <= 0.0001f)
        {
            slidingLocalAxis = Vector3.right;
        }

        pryOpenDuration = Mathf.Max(0.01f, pryOpenDuration);
        smokeVentilationReliefWhenOpen = Mathf.Max(0f, smokeVentilationReliefWhenOpen);
        fireDraftRiskWhenOpen = Mathf.Max(0f, fireDraftRiskWhenOpen);
        if (lockMode == DoorLockMode.None)
        {
            startsLocked = false;
        }
    }

    private void OnDisable()
    {
        CancelPendingOpen();
        CancelActivePry();
        BotRuntimeRegistry.UnregisterPryTarget(this);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);

        float shakeAngle = 0f;
        if (lockedShakeTimer > 0f)
        {
            lockedShakeTimer -= Time.deltaTime;
            float intensity = lockedShakeTimer / lockedShakeDuration;
            shakeAngle = Mathf.Sin(lockedShakeTimer * lockedShakeSpeed) * lockedShakeIntensity * intensity;
        }

        if (openMode == DoorOpenMode.DoubleSliding)
        {
            leftSlidingDoorTransform.localPosition = Vector3.Lerp(leftSlidingDoorTransform.localPosition, leftTargetLocalPosition, t);
            rightSlidingDoorTransform.localPosition = Vector3.Lerp(rightSlidingDoorTransform.localPosition, rightTargetLocalPosition, t);
            return;
        }

        Quaternion actualTargetRot = targetLocalRotation;
        if (shakeAngle != 0f && !isOpen)
        {
            actualTargetRot *= Quaternion.Euler(0f, shakeAngle, 0f);
        }

        doorTransform.localRotation = Quaternion.Slerp(doorTransform.localRotation, actualTargetRot, t);
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
        {
            InitializeDoorState();
        }

        if (!HasRequiredDoorGeometry())
        {
            return;
        }

        if (isPryInProgress)
        {
            return;
        }

        if (pendingOpenRoutine != null)
        {
            return;
        }

        if (isOpen)
        {
            TriggerPlayerOpenDoorAnimation(interactor);
            CloseDoor();
            return;
        }

        if (isLocked)
        {
            lockedShakeTimer = lockedShakeDuration;
            return;
        }

        if (interactOpenDelay > 0f)
        {
            TriggerPlayerOpenDoorAnimation(interactor);
            pendingOpenRoutine = StartCoroutine(OpenDoorAfterDelay(interactOpenDelay, interactor));
            return;
        }

        OpenDoor(interactor);
    }

    public bool TryPryOpen(GameObject interactor)
    {
        if (!initialized)
        {
            InitializeDoorState();
        }

        if (!HasRequiredDoorGeometry() || isOpen || !isLocked || lockMode != DoorLockMode.SoftLockedCrowbar)
        {
            return false;
        }

        if (isPryInProgress)
        {
            return activePryer == interactor;
        }

        isPryInProgress = true;
        activePryer = interactor;
        AcquirePryLock(interactor);
        pryRoutine = StartCoroutine(PryOpenAfterDelay(Mathf.Max(0.01f, pryOpenDuration), interactor));
        return true;
    }

    public void SetOpenState(bool open, bool forceUnlockWhenOpening = true)
    {
        if (!initialized)
        {
            InitializeDoorState();
        }

        if (!HasRequiredDoorGeometry())
        {
            return;
        }

        CancelActivePry();
        CancelPendingOpen();

        if (open)
        {
            if (forceUnlockWhenOpening)
            {
                isLocked = false;
            }

            OpenDoor(null);
            return;
        }

        CloseDoor();
    }

    public void SetLockState(DoorLockMode mode, bool locked)
    {
        lockMode = mode;
        startsLocked = locked;
        isLocked = locked;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    private void InitializeDoorState()
    {
        ApplyLegacyLockMigration();
        ResolveDoorGeometry();
        if (!HasRequiredDoorGeometry())
        {
            initialized = false;
            return;
        }

        currentOpenDirection = GetDefaultOpenDirection();
        if (openMode == DoorOpenMode.DoubleSliding)
        {
            leftClosedLocalPosition = leftSlidingDoorTransform.localPosition;
            rightClosedLocalPosition = rightSlidingDoorTransform.localPosition;
            leftTargetLocalPosition = startsOpen ? GetSlidingOpenLocalPosition(leftClosedLocalPosition, -1) : leftClosedLocalPosition;
            rightTargetLocalPosition = startsOpen ? GetSlidingOpenLocalPosition(rightClosedLocalPosition, 1) : rightClosedLocalPosition;
            leftSlidingDoorTransform.localPosition = leftTargetLocalPosition;
            rightSlidingDoorTransform.localPosition = rightTargetLocalPosition;
        }
        else
        {
            closedLocalRotation = doorTransform.localRotation;
            closedLocalEulerAngles = doorTransform.localEulerAngles;
            targetLocalRotation = startsOpen ? GetOpenLocalRotation(currentOpenDirection) : closedLocalRotation;
            doorTransform.localRotation = targetLocalRotation;
        }

        isOpen = startsOpen;
        isLocked = lockMode != DoorLockMode.None && startsLocked;
        initialized = true;
    }

    private void ResolveDoorGeometry()
    {
        if (openMode == DoorOpenMode.DoubleSliding)
        {
            doorTransform = null;
            return;
        }

        doorTransform = hingedDoorTransform != null ? hingedDoorTransform : transform;
    }

    private bool HasRequiredDoorGeometry()
    {
        if (openMode == DoorOpenMode.DoubleSliding)
        {
            return leftSlidingDoorTransform != null && rightSlidingDoorTransform != null;
        }

        return doorTransform != null;
    }

    private Quaternion GetOpenLocalRotation(int direction)
    {
        float targetY = closedLocalEulerAngles.y + Mathf.Abs(openAngle) * direction;
        return Quaternion.Euler(closedLocalEulerAngles.x, targetY, closedLocalEulerAngles.z);
    }

    private Vector3 GetSlidingOpenLocalPosition(Vector3 closedLocalPosition, int direction)
    {
        return closedLocalPosition + slidingLocalAxis.normalized * slidingOpenDistance * direction;
    }

    private void OpenDoor(GameObject interactor)
    {
        TriggerPlayerOpenDoorAnimation(interactor);
        OpenDoorImmediate(interactor);
    }

    private void OpenDoorImmediate(GameObject interactor)
    {
        CancelPendingOpen();

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        currentOpenDirection = DetermineOpenDirection(interactor);
        isOpen = true;
        if (openMode == DoorOpenMode.DoubleSliding)
        {
            leftTargetLocalPosition = GetSlidingOpenLocalPosition(leftClosedLocalPosition, -1);
            rightTargetLocalPosition = GetSlidingOpenLocalPosition(rightClosedLocalPosition, 1);
            return;
        }

        targetLocalRotation = GetOpenLocalRotation(currentOpenDirection);
    }

    private void CloseDoor()
    {
        CancelPendingOpen();

        if (audioSource != null && closeSound != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        isOpen = false;
        if (openMode == DoorOpenMode.DoubleSliding)
        {
            leftTargetLocalPosition = leftClosedLocalPosition;
            rightTargetLocalPosition = rightClosedLocalPosition;
            return;
        }

        targetLocalRotation = closedLocalRotation;
    }

    private IEnumerator OpenDoorAfterDelay(float duration, GameObject interactor)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        pendingOpenRoutine = null;
        OpenDoorImmediate(interactor);
    }

    private void CancelPendingOpen()
    {
        if (pendingOpenRoutine == null)
        {
            return;
        }

        StopCoroutine(pendingOpenRoutine);
        pendingOpenRoutine = null;
    }

    private int DetermineOpenDirection(GameObject interactor)
    {
        if (interactor == null)
        {
            return GetDefaultOpenDirection();
        }

        Vector3 lookDirection = Vector3.ProjectOnPlane(interactor.transform.forward, transform.up);
        if (TryGetOpenDirection(lookDirection, out int lookSign))
        {
            return lookSign;
        }

        Vector3 toInteractor = Vector3.ProjectOnPlane(interactor.transform.position - transform.position, transform.up);
        if (TryGetOpenDirection(toInteractor, out int positionSign))
        {
            return positionSign;
        }

        return GetDefaultOpenDirection();
    }

    private bool TryGetOpenDirection(Vector3 direction, out int sign)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            sign = 0;
            return false;
        }

        float facing = Vector3.Dot(transform.forward, direction.normalized);
        if (Mathf.Abs(facing) <= 0.001f)
        {
            sign = 0;
            return false;
        }

        sign = GetDefaultOpenDirection() * (facing >= 0f ? -1 : 1);
        return true;
    }

    private int GetDefaultOpenDirection()
    {
        return openAngle >= 0f ? 1 : -1;
    }

    private IEnumerator PryOpenAfterDelay(float duration, GameObject interactor)
    {
        bool isPlayerPryer = interactor != null && interactor.GetComponent<BotCommandAgent>() == null;
        if (isPlayerPryer)
        {
            PlayerContinuousActionBus.StartAction();
        }

        float endTime = Time.time + Mathf.Max(0.01f, duration);
        while (Time.time < endTime)
        {
            if (!isActiveAndEnabled || !HasRequiredDoorGeometry() || !isPryInProgress)
            {
                if (isPlayerPryer)
                {
                    PlayerContinuousActionBus.EndAction(false);
                }

                pryRoutine = null;
                yield break;
            }

            if (isPlayerPryer)
            {
                float progress = 1f - ((endTime - Time.time) / Mathf.Max(0.01f, duration));
                PlayerContinuousActionBus.UpdateProgress(progress);
            }

            yield return null;
        }

        pryRoutine = null;
        if (!isActiveAndEnabled || !HasRequiredDoorGeometry())
        {
            isPryInProgress = false;
            activePryer = null;
            ReleasePryLock();
            if (isPlayerPryer)
            {
                PlayerContinuousActionBus.EndAction(false);
            }
            yield break;
        }

        if (unlockOnSuccessfulPry)
        {
            isLocked = false;
        }

        OpenDoor(interactor);
        isPryInProgress = false;
        activePryer = null;
        ReleasePryLock();
        if (isPlayerPryer)
        {
            PlayerContinuousActionBus.EndAction(true);
        }
    }

    private void ApplyLegacyLockMigration()
    {
        if (!legacyRequiresCrowbarToOpenWhenClosed)
        {
            return;
        }

        if (lockMode == DoorLockMode.None)
        {
            lockMode = DoorLockMode.SoftLockedCrowbar;
        }

        startsLocked = true;
    }

    private void CancelActivePry()
    {
        if (pryRoutine != null)
        {
            StopCoroutine(pryRoutine);
            pryRoutine = null;
        }

        isPryInProgress = false;
        activePryer = null;
        activeAnimationState?.EndAction(PlayerInteractionAnimationAction.BreakingObject, this, force: true);
        activeAnimationState = null;
        ReleasePryLock();
    }

    private void TriggerPlayerOpenDoorAnimation(GameObject interactor)
    {
        if (interactor == null || interactor.GetComponent<BotCommandAgent>() != null)
        {
            return;
        }

        PlayerInteractionAnimationState state = PlayerInteractionAnimationState.GetOrCreate(interactor);
        state?.PulseAction(PlayerInteractionAnimationAction.OpeningDoor, 0.2f, this);
    }

    private void AcquirePryLock(GameObject interactor)
    {
        if (!lockPlayerWhilePrying || interactor == null || interactor.GetComponent<BotCommandAgent>() != null)
        {
            return;
        }

        activePlayerLock = PlayerActionLock.GetOrCreate(interactor);
        activePlayerLock?.AcquireFullLock();
    }

    private void ReleasePryLock()
    {
        if (activePlayerLock == null)
        {
            return;
        }

        activePlayerLock.ReleaseFullLock();
        activePlayerLock = null;
    }
}
