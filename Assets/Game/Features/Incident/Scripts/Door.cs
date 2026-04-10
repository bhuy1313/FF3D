using System.Collections;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Door : MonoBehaviour, IInteractable, IOpenable, IPryOpenable, ISmokeVentPoint, IBotPryTarget
{
    public enum DoorLockMode
    {
        None = 0,
        SoftLockedCrowbar = 1,
        Blocked = 2
    }

    [SerializeField] private string doorChildName = "Door";
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;
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
    [Header("Runtime")]
    [SerializeField] private bool isLocked;
    [SerializeField] private bool isPryInProgress;
    [SerializeField] private GameObject activePryer;

    private Transform doorTransform;
    private Quaternion closedLocalRotation;
    private Vector3 closedLocalEulerAngles;
    private Quaternion targetLocalRotation;
    private bool isOpen;
    private bool initialized;
    private int currentOpenDirection = -1;
    private Coroutine pryRoutine;
    private PlayerActionLock activePlayerLock;

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
        CancelActivePry();
        BotRuntimeRegistry.UnregisterPryTarget(this);
    }

    private void Update()
    {
        if (!initialized || doorTransform == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
        doorTransform.localRotation = Quaternion.Slerp(doorTransform.localRotation, targetLocalRotation, t);
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
        {
            InitializeDoorState();
        }

        if (doorTransform == null)
        {
            return;
        }

        if (isPryInProgress)
        {
            return;
        }

        if (isOpen)
        {
            CloseDoor();
            return;
        }

        if (isLocked)
        {
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

        if (doorTransform == null || isOpen || !isLocked || lockMode != DoorLockMode.SoftLockedCrowbar)
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

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    private void InitializeDoorState()
    {
        ApplyLegacyLockMigration();
        doorTransform = FindDoorTransform();
        if (doorTransform == null)
        {
            initialized = false;
            return;
        }

        closedLocalRotation = doorTransform.localRotation;
        closedLocalEulerAngles = doorTransform.localEulerAngles;
        currentOpenDirection = GetDefaultOpenDirection();
        isOpen = startsOpen;
        isLocked = lockMode != DoorLockMode.None && startsLocked;
        targetLocalRotation = isOpen ? GetOpenLocalRotation(currentOpenDirection) : closedLocalRotation;
        doorTransform.localRotation = targetLocalRotation;
        initialized = true;
    }

    private Transform FindDoorTransform()
    {
        if (!string.IsNullOrWhiteSpace(doorChildName))
        {
            Transform namedChild = transform.Find(doorChildName);
            if (namedChild != null)
            {
                return namedChild;
            }
        }

        return transform;
    }

    private Quaternion GetOpenLocalRotation(int direction)
    {
        float targetY = closedLocalEulerAngles.y + Mathf.Abs(openAngle) * direction;
        return Quaternion.Euler(closedLocalEulerAngles.x, targetY, closedLocalEulerAngles.z);
    }

    private void OpenDoor(GameObject interactor)
    {
        currentOpenDirection = DetermineOpenDirection(interactor);
        isOpen = true;
        targetLocalRotation = GetOpenLocalRotation(currentOpenDirection);
    }

    private void CloseDoor()
    {
        isOpen = false;
        targetLocalRotation = closedLocalRotation;
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
        yield return new WaitForSeconds(duration);

        pryRoutine = null;
        if (!isActiveAndEnabled || doorTransform == null)
        {
            isPryInProgress = false;
            activePryer = null;
            ReleasePryLock();
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
        ReleasePryLock();
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
