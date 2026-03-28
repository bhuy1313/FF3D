using System;
using System.Collections;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Rescuable : MonoBehaviour, IInteractable, IRescuableTarget
{
    [Header("Pickup")]
    [FormerlySerializedAs("rescueDuration")]
    [SerializeField] private float pickupDuration = 0.5f;
    [SerializeField] private Vector3 carriedLocalPosition = new Vector3(0f, 1.1f, 0.6f);
    [SerializeField] private Vector3 carriedLocalEulerAngles = Vector3.zero;
    [FormerlySerializedAs("disableCollidersOnRescue")]
    [SerializeField] private bool disableCollidersWhileCarried = true;

    [Header("Medical")]
    [SerializeField] private float stabilizeDuration = 1.25f;
    [SerializeField] private float stabilizeRestoreAmount = 15f;

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
    [SerializeField] private GameObject activeRescuer;

    public bool NeedsRescue => !isRescued;
    public bool IsRescueInProgress => isRescueInProgress;
    public GameObject ActiveRescuer => activeRescuer;
    public bool IsCarried => isCarried;
    public bool IsRescued => isRescued;
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

    private Coroutine rescueRoutine;
    private Coroutine stabilizationRoutine;
    private Transform activeCarryAnchor;
    private Transform originalParent;
    private Quaternion originalRotation;
    private Rigidbody cachedRigidbody;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private Collider[] managedColliders;
    private bool[] managedColliderStates;
    private VictimCondition victimCondition;

    private void OnEnable()
    {
        CacheVictimCondition();
        BotRuntimeRegistry.RegisterRescuableTarget(this);
        isRescueInProgress = false;
        isCarried = false;
        activeRescuer = null;
        rescueRoutine = null;
        stabilizationRoutine = null;
        activeCarryAnchor = null;
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

        isRescueInProgress = false;
        isCarried = false;
        activeRescuer = null;
        activeCarryAnchor = null;
        RestoreRigidbodyState();
        RestoreColliderStates();
        BotRuntimeRegistry.UnregisterRescuableTarget(this);
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
        onRescueStarted?.Invoke();
        RescueStarted?.Invoke();
        rescueRoutine = StartCoroutine(BeginCarryAfterDelay(Mathf.Max(0.01f, pickupDuration)));
        return true;
    }

    public bool TryStabilize(GameObject rescuer)
    {
        CacheVictimCondition();
        if (isRescued || isCarried || victimCondition == null || !victimCondition.CanBeStabilized)
        {
            return false;
        }

        if (!victimCondition.RequiresStabilization)
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
        stabilizationRoutine = StartCoroutine(StabilizeAfterDelay(Mathf.Max(0.01f, stabilizeDuration)));
        return true;
    }

    public void CompleteRescueAt(Vector3 dropPosition)
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

        transform.SetParent(originalParent, true);
        transform.position = dropPosition;
        transform.rotation = originalRotation;

        isRescued = true;
        isRescueInProgress = false;
        isCarried = false;
        activeRescuer = null;
        activeCarryAnchor = null;

        RestoreRigidbodyState();
        RestoreColliderStates();

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

    private IEnumerator BeginCarryAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        rescueRoutine = null;
        if (isRescued || activeCarryAnchor == null)
        {
            isRescueInProgress = false;
            activeRescuer = null;
            yield break;
        }

        isRescueInProgress = false;
        isCarried = true;
        PrepareRigidbodyForCarry();
        transform.SetParent(activeCarryAnchor, false);
        transform.localPosition = carriedLocalPosition;
        transform.localRotation = Quaternion.Euler(carriedLocalEulerAngles);
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
            yield break;
        }

        victimCondition.Stabilize(stabilizeRestoreAmount);
        isRescueInProgress = false;
        activeRescuer = null;
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
            activeCarryAnchor.TransformPoint(carriedLocalPosition),
            activeCarryAnchor.rotation * Quaternion.Euler(carriedLocalEulerAngles));
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
}
