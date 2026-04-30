using System;
using System.Collections;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Rescuable : MonoBehaviour, IInteractable, IRescuableTarget, IMovementWeightSource
{
    [Header("Pickup")]
    [FormerlySerializedAs("rescueDuration")]
    [SerializeField] private float pickupDuration = 0.5f;
    [SerializeField] private float movementWeightKg = 75f;
    [FormerlySerializedAs("carriedLocalPosition")]
    [SerializeField] private Vector3 botCarriedLocalPosition = new Vector3(0f, 1.1f, 0.6f);
    [FormerlySerializedAs("carriedLocalEulerAngles")]
    [SerializeField] private Vector3 botCarriedLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 playerCarriedLocalPosition = new Vector3(0f, 1.1f, 0.6f);
    [SerializeField] private Vector3 playerCarriedLocalEulerAngles = Vector3.zero;
    [FormerlySerializedAs("disableCollidersOnRescue")]
    [SerializeField] private bool disableCollidersWhileCarried = true;
    [FormerlySerializedAs("carryRightHandTargetRoot")]
    [SerializeField] private Transform carryRightHandHoldPoint;
    [SerializeField] private Transform carryLeftHandHoldPoint;

    [Header("Medical")]
    [SerializeField] private float stabilizeDuration = 1.25f;
    [SerializeField] private float stabilizeRestoreAmount = 15f;
    [SerializeField] private float urgentStabilizeDurationMultiplier = 0.9f;
    [SerializeField] private float criticalStabilizeDurationMultiplier = 1.25f;
    [SerializeField] private float urgentStabilizeRestoreMultiplier = 0.8f;
    [SerializeField] private float criticalStabilizeRestoreMultiplier = 1.35f;

    [Header("Extraction")]
    [SerializeField] private float extractionDuration = 1.5f;
    [SerializeField] private float extractionRestoreAmount = 5f;

    [Header("Player Locking")]
    [SerializeField] private bool lockPlayerWhilePickingUp = true;
    [SerializeField] private bool lockPlayerWhileStabilizing = true;
    [SerializeField] private bool restrictPlayerActionsWhileCarried = true;

    [Header("Completion")]
    [SerializeField] private bool deactivateOnRescue = false;
    [SerializeField] private bool disableRenderersOnRescue = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onRescueStarted;
    [SerializeField] private UnityEvent onRescued;

    [Header("Runtime")]
    [SerializeField] private bool isRescued;
    [SerializeField] private bool isRescueInProgress;
    [SerializeField] private bool isCarried;
    [SerializeField] private bool isExtractionInProgress;
    [SerializeField] private GameObject activeRescuer;

    public bool NeedsRescue => !isRescued;
    public bool IsRescueInProgress => isRescueInProgress;
    public GameObject ActiveRescuer => activeRescuer;
    public bool IsCarried => isCarried;
    public bool IsRescued => isRescued;
    public bool IsExtractionInProgress => isExtractionInProgress;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public bool RequiresStabilization
    {
        get
        {
            CacheVictimCondition();
            return victimCondition != null && victimCondition.RequiresStabilization;
        }
    }
    public float RescuePriority
    {
        get
        {
            CacheVictimCondition();
            return victimCondition != null ? victimCondition.GetRescuePriorityScore() : 0f;
        }
    }

    public event Action RescueStarted;
    public event Action RescueCompleted;
    public event Action<bool> CarryStateChanged;

    private Coroutine rescueRoutine;
    private Coroutine stabilizationRoutine;
    private Coroutine extractionRoutine;
    private Transform activeCarryAnchor;
    private Transform originalParent;
    private Quaternion originalRotation;
    private Rigidbody cachedRigidbody;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private Collider[] managedColliders;
    private bool[] managedColliderStates;
    private VictimCondition victimCondition;
    private PlayerActionLock activePlayerActionLock;
    private bool hasActiveProgressLock;
    private bool hasActiveCarryRestriction;

    private void OnEnable()
    {
        CacheVictimCondition();
        ResolveCarryHoldPoints();
        BotRuntimeRegistry.RegisterRescuableTarget(this);
        isRescueInProgress = false;
        SetCarryState(false);
        activeRescuer = null;
        rescueRoutine = null;
        stabilizationRoutine = null;
        extractionRoutine = null;
        activeCarryAnchor = null;
        activePlayerActionLock = null;
        hasActiveProgressLock = false;
        hasActiveCarryRestriction = false;
        isExtractionInProgress = false;
        if (!isRescued)
        {
            originalParent = transform.parent;
            originalRotation = transform.rotation;
        }

        CacheRigidbodyState();
        RestoreRigidbodyState();
        CacheColliderStates();
        RestoreColliderStates();
    }

    private void OnDisable()
    {
        if (rescueRoutine != null)
        {
            StopCoroutine(rescueRoutine);
            rescueRoutine = null;
        }

        if (stabilizationRoutine != null)
        {
            StopCoroutine(stabilizationRoutine);
            stabilizationRoutine = null;
        }

        if (extractionRoutine != null)
        {
            StopCoroutine(extractionRoutine);
            extractionRoutine = null;
        }

        isRescueInProgress = false;
        SetCarryState(false);
        isExtractionInProgress = false;
        activeRescuer = null;
        activeCarryAnchor = null;
        ReleasePlayerLocks();
        RestoreRigidbodyState();
        RestoreColliderStates();
        BotRuntimeRegistry.UnregisterRescuableTarget(this);
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        pickupDuration = Mathf.Max(0.01f, pickupDuration);
        stabilizeDuration = Mathf.Max(0.01f, stabilizeDuration);
        extractionDuration = Mathf.Max(0f, extractionDuration);
        stabilizeRestoreAmount = Mathf.Max(0f, stabilizeRestoreAmount);
        extractionRestoreAmount = Mathf.Max(0f, extractionRestoreAmount);
        urgentStabilizeDurationMultiplier = Mathf.Max(0.1f, urgentStabilizeDurationMultiplier);
        criticalStabilizeDurationMultiplier = Mathf.Max(0.1f, criticalStabilizeDurationMultiplier);
        urgentStabilizeRestoreMultiplier = Mathf.Max(0f, urgentStabilizeRestoreMultiplier);
        criticalStabilizeRestoreMultiplier = Mathf.Max(0f, criticalStabilizeRestoreMultiplier);
        ResolveCarryHoldPoints();
    }

    public void Interact(GameObject interactor)
    {
        if (TryStabilize(interactor))
        {
            return;
        }

        TryBeginCarry(interactor, interactor != null ? interactor.transform : null);
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public Transform GetCarryRightHandHoldPoint()
    {
        ResolveCarryHoldPoints();
        return carryRightHandHoldPoint;
    }

    public Transform GetCarryLeftHandHoldPoint()
    {
        ResolveCarryHoldPoints();
        return carryLeftHandHoldPoint;
    }

    public bool TryBeginCarry(GameObject rescuer, Transform carryAnchor)
    {
        CacheVictimCondition();
        if (isRescued)
        {
            return false;
        }

        if (victimCondition != null && !victimCondition.CanBeginCarry)
        {
            return false;
        }

        if (isCarried)
        {
            return activeRescuer == rescuer;
        }

        if (isRescueInProgress)
        {
            return rescueRoutine != null && activeRescuer == rescuer;
        }

        isRescueInProgress = true;
        activeRescuer = rescuer;
        activeCarryAnchor = carryAnchor;
        AcquirePlayerProgressLock(rescuer, lockPlayerWhilePickingUp);
        onRescueStarted?.Invoke();
        RescueStarted?.Invoke();
        rescueRoutine = StartCoroutine(BeginCarryAfterDelay(Mathf.Max(0.01f, pickupDuration)));
        return true;
    }

    public bool TryStabilize(GameObject rescuer)
    {
        return TryStabilize(rescuer, 1f);
    }

    public bool TryStabilize(GameObject rescuer, float durationMultiplier)
    {
        CacheVictimCondition();
        if (isRescued || isCarried || victimCondition == null || !victimCondition.CanReceiveStabilizationTreatment)
        {
            return false;
        }

        if (isRescueInProgress)
        {
            return stabilizationRoutine != null && activeRescuer == rescuer;
        }

        isRescueInProgress = true;
        activeRescuer = rescuer;
        activeCarryAnchor = null;
        AcquirePlayerProgressLock(rescuer, lockPlayerWhileStabilizing);
        float adjustedDurationMultiplier = Mathf.Max(0.05f, durationMultiplier);
        stabilizationRoutine = StartCoroutine(StabilizeAfterDelay(GetAdjustedStabilizeDuration() * adjustedDurationMultiplier));
        return true;
    }

    public void CompleteRescueAt(Vector3 dropPosition, Quaternion rotation)
    {
        if (isRescued)
        {
            return;
        }

        if (rescueRoutine != null)
        {
            StopCoroutine(rescueRoutine);
            rescueRoutine = null;
        }

        if (stabilizationRoutine != null)
        {
            StopCoroutine(stabilizationRoutine);
            stabilizationRoutine = null;
        }

        if (extractionRoutine != null)
        {
            StopCoroutine(extractionRoutine);
            extractionRoutine = null;
        }

        transform.SetParent(originalParent, true);
        transform.position = dropPosition;
        transform.rotation = rotation;
        SetCarryState(false);
        activeCarryAnchor = null;
        ReleasePlayerLocks();

        RestoreRigidbodyState();
        RestoreColliderStates();
        activeRescuer = null;

        if (extractionDuration <= 0f)
        {
            FinalizeRescue();
            return;
        }

        isRescueInProgress = true;
        isExtractionInProgress = true;
        extractionRoutine = StartCoroutine(FinishExtractionAfterDelay(extractionDuration));
    }

    public void CompleteRescueAt(Vector3 dropPosition)
    {
        CompleteRescueAt(dropPosition, originalRotation);
    }

    private IEnumerator BeginCarryAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        rescueRoutine = null;
        if (isRescued || activeCarryAnchor == null)
        {
            isRescueInProgress = false;
            activeRescuer = null;
            ReleasePlayerProgressLock();
            yield break;
        }

        ReleasePlayerProgressLock();
        isRescueInProgress = false;
        SetCarryState(true);
        PrepareRigidbodyForCarry();
        transform.SetParent(activeCarryAnchor, false);
        transform.localPosition = ResolveCarryLocalPosition();
        transform.localRotation = Quaternion.Euler(ResolveCarryLocalEulerAngles());
        AcquirePlayerCarryRestriction();
        if (disableCollidersWhileCarried)
        {
            SetColliderStates(false);
        }
    }

    private IEnumerator StabilizeAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        stabilizationRoutine = null;
        if (isRescued || isCarried || victimCondition == null)
        {
            isRescueInProgress = false;
            activeRescuer = null;
            ReleasePlayerProgressLock();
            yield break;
        }

        victimCondition.Stabilize(GetAdjustedStabilizeRestoreAmount());
        isRescueInProgress = false;
        activeRescuer = null;
        ReleasePlayerProgressLock();
    }

    private IEnumerator FinishExtractionAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        extractionRoutine = null;
        isExtractionInProgress = false;
        isRescueInProgress = false;

        CacheVictimCondition();
        if (victimCondition != null && !victimCondition.IsAlive)
        {
            activeRescuer = null;
            yield break;
        }

        if (victimCondition != null && extractionRestoreAmount > 0f)
        {
            victimCondition.Stabilize(extractionRestoreAmount);
        }

        FinalizeRescue();
    }

    private void CacheRigidbodyState()
    {
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        if (cachedRigidbody == null)
        {
            return;
        }

        originalUseGravity = cachedRigidbody.useGravity;
        originalIsKinematic = cachedRigidbody.isKinematic;
    }

    private void PrepareRigidbodyForCarry()
    {
        if (cachedRigidbody == null)
        {
            CacheRigidbodyState();
        }

        if (cachedRigidbody == null)
        {
            return;
        }

        cachedRigidbody.linearVelocity = Vector3.zero;
        cachedRigidbody.angularVelocity = Vector3.zero;
        cachedRigidbody.useGravity = false;
        cachedRigidbody.isKinematic = true;
    }

    private void RestoreRigidbodyState()
    {
        if (cachedRigidbody == null)
        {
            CacheRigidbodyState();
        }

        if (cachedRigidbody == null)
        {
            return;
        }

        cachedRigidbody.isKinematic = originalIsKinematic;
        cachedRigidbody.useGravity = originalUseGravity;
        if (!cachedRigidbody.isKinematic)
        {
            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (!isCarried || activeCarryAnchor == null)
        {
            return;
        }

        transform.SetPositionAndRotation(
            activeCarryAnchor.TransformPoint(ResolveCarryLocalPosition()),
            activeCarryAnchor.rotation * Quaternion.Euler(ResolveCarryLocalEulerAngles()));
    }

    private void CacheColliderStates()
    {
        managedColliders = GetComponentsInChildren<Collider>(true);
        managedColliderStates = new bool[managedColliders.Length];
        for (int i = 0; i < managedColliders.Length; i++)
        {
            managedColliderStates[i] = managedColliders[i] != null && managedColliders[i].enabled;
        }
    }

    private void RestoreColliderStates()
    {
        if (managedColliders == null || managedColliderStates == null || managedColliders.Length != managedColliderStates.Length)
        {
            CacheColliderStates();
        }

        for (int i = 0; i < managedColliders.Length; i++)
        {
            if (managedColliders[i] != null)
            {
                managedColliders[i].enabled = managedColliderStates[i];
            }
        }
    }

    private void SetColliderStates(bool enabled)
    {
        if (managedColliders == null || managedColliders.Length == 0)
        {
            CacheColliderStates();
        }

        for (int i = 0; i < managedColliders.Length; i++)
        {
            if (managedColliders[i] != null)
            {
                managedColliders[i].enabled = enabled;
            }
        }
    }

    private void CacheVictimCondition()
    {
        if (victimCondition == null)
        {
            victimCondition = GetComponent<VictimCondition>();
        }
    }

    private void ResolveCarryHoldPoints()
    {
        if (carryRightHandHoldPoint != null && carryLeftHandHoldPoint != null)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform candidate = childTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name == "CarryRightHandHoldPoint" ||
                candidate.name == "CarryRightHandTargetRoot")
            {
                carryRightHandHoldPoint = candidate;
            }
            else if (candidate.name == "CarryLeftHandHoldPoint" ||
                     candidate.name == "CarryLeftHandTargetRoot")
            {
                carryLeftHandHoldPoint = candidate;
            }

            if (carryRightHandHoldPoint != null && carryLeftHandHoldPoint != null)
            {
                return;
            }
        }
    }

    private void AcquirePlayerProgressLock(GameObject rescuer, bool shouldLock)
    {
        if (!shouldLock || !IsPlayerRescuer(rescuer) || hasActiveProgressLock)
        {
            return;
        }

        activePlayerActionLock = PlayerActionLock.GetOrCreate(rescuer);
        activePlayerActionLock?.AcquireFullLock();
        hasActiveProgressLock = activePlayerActionLock != null;
    }

    private void ReleasePlayerProgressLock()
    {
        if (!hasActiveProgressLock || activePlayerActionLock == null)
        {
            hasActiveProgressLock = false;
            return;
        }

        activePlayerActionLock.ReleaseFullLock();
        hasActiveProgressLock = false;
    }

    private void AcquirePlayerCarryRestriction()
    {
        if (!restrictPlayerActionsWhileCarried || !IsPlayerRescuer(activeRescuer) || hasActiveCarryRestriction)
        {
            return;
        }

        activePlayerActionLock = PlayerActionLock.GetOrCreate(activeRescuer);
        activePlayerActionLock?.AcquireCarryRestriction();
        hasActiveCarryRestriction = activePlayerActionLock != null;
    }

    private void ReleasePlayerCarryRestriction()
    {
        if (!hasActiveCarryRestriction || activePlayerActionLock == null)
        {
            hasActiveCarryRestriction = false;
            return;
        }

        activePlayerActionLock.ReleaseCarryRestriction();
        hasActiveCarryRestriction = false;
    }

    private void ReleasePlayerLocks()
    {
        ReleasePlayerProgressLock();
        ReleasePlayerCarryRestriction();
        if (!hasActiveProgressLock && !hasActiveCarryRestriction)
        {
            activePlayerActionLock = null;
        }
    }

    private Vector3 ResolveCarryLocalPosition()
    {
        return IsPlayerRescuer(activeRescuer)
            ? playerCarriedLocalPosition
            : botCarriedLocalPosition;
    }

    private Vector3 ResolveCarryLocalEulerAngles()
    {
        return IsPlayerRescuer(activeRescuer)
            ? playerCarriedLocalEulerAngles
            : botCarriedLocalEulerAngles;
    }

    private static bool IsPlayerRescuer(GameObject rescuer)
    {
        return rescuer != null && rescuer.GetComponent<BotCommandAgent>() == null;
    }

    private void SetCarryState(bool carried)
    {
        if (isCarried == carried)
        {
            return;
        }

        isCarried = carried;
        CarryStateChanged?.Invoke(isCarried);
    }

    private float GetAdjustedStabilizeDuration()
    {
        CacheVictimCondition();
        if (victimCondition == null)
        {
            return Mathf.Max(0.01f, stabilizeDuration);
        }

        float multiplier = 1f;
        switch (victimCondition.CurrentTriageState)
        {
            case VictimCondition.TriageState.Critical:
                multiplier = criticalStabilizeDurationMultiplier;
                break;
            case VictimCondition.TriageState.Urgent:
                multiplier = urgentStabilizeDurationMultiplier;
                break;
        }

        return Mathf.Max(0.01f, stabilizeDuration * multiplier);
    }

    private float GetAdjustedStabilizeRestoreAmount()
    {
        CacheVictimCondition();
        if (victimCondition == null)
        {
            return Mathf.Max(0f, stabilizeRestoreAmount);
        }

        float multiplier = 1f;
        switch (victimCondition.CurrentTriageState)
        {
            case VictimCondition.TriageState.Critical:
                multiplier = criticalStabilizeRestoreMultiplier;
                break;
            case VictimCondition.TriageState.Urgent:
                multiplier = urgentStabilizeRestoreMultiplier;
                break;
        }

        return Mathf.Max(0f, stabilizeRestoreAmount * multiplier);
    }

    private void FinalizeRescue()
    {
        isRescued = true;
        isRescueInProgress = false;
        SetCarryState(false);
        isExtractionInProgress = false;
        activeRescuer = null;
        activeCarryAnchor = null;

        onRescued?.Invoke();
        RescueCompleted?.Invoke();

        if (disableRenderersOnRescue)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        if (deactivateOnRescue)
        {
            gameObject.SetActive(false);
        }
    }
}
