using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class FireHoseAssembly : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireHoseRig rig;
    [SerializeField] private FireHoseDeployable deployable;
    [SerializeField] private FireHoseDeployed staticHose;
    [SerializeField] private FireHoseTailVisual dynamicTail;
    [SerializeField] private FireHoseHeadPickup headPickup;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform hoseOrigin;
    [SerializeField] private Transform hoseExit;
    [SerializeField] private FireHose shooter;
    [SerializeField] private FireTruckHosePickupPointLite ownerPickupPointLite;

    [Header("Carry")]
    [SerializeField] private bool autoDisablePrototypeInputMovement = true;
    [SerializeField] private float headSnapHeightOffset = 0f;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private bool isHeadHeld;
    [SerializeField] private FireHoseHeadSocket currentSocket;
    [SerializeField] private Transform currentAttachPoint;
    [SerializeField] private FireHose currentAttachedNozzle;

    private Coroutine attachSnapRoutine;
    private Transform tailEndVisualAnchor;
    private bool hasTailEndVisualTarget;
    private Vector3 tailEndVisualTargetPosition;
    private const float TailEndProbeHeight = 2f;
    private const float TailEndProbeDistance = 8f;
    private const float TailEndFallSpeed = 4f;

    public bool IsHeadHeld => isHeadHeld;
    public GameObject CurrentHolder => currentHolder;
    public bool IsAttached => currentSocket != null || currentAttachPoint != null;
    public FireHose Shooter => shooter;
    public FireHoseRig Rig => rig;
    public Transform HeadTransform => headTransform;
    public Transform HeadRotationAnchor => headPickup != null ? headPickup.RotationAnchor : headTransform;
    public float HeadSnapHeightOffset => headSnapHeightOffset;
    public FireHose CurrentAttachedNozzle => ownerPickupPointLite != null && ownerPickupPointLite.PumpSystem != null
        ? ownerPickupPointLite.PumpSystem.GetConnectedNozzle(ownerPickupPointLite)
        : currentAttachedNozzle;
    public bool IsConnectedToNozzle => CurrentAttachedNozzle != null;
    public FireTruckHosePickupPointLite OwnerPickupPointLite => ownerPickupPointLite;
    public FireApparatusPumpSystem OwnerPumpSystem => ownerPickupPointLite != null ? ownerPickupPointLite.PumpSystem : null;

    void Awake()
    {
        ResolveReferences();
        ApplyAuthoringDefaults();
        WireComponents();
    }

    void Reset()
    {
        ResolveReferences();
        WireComponents();
    }

    void OnValidate()
    {
        ResolveReferences();
        WireComponents();
    }

    void LateUpdate()
    {
        UpdateTailEndVisualAnchor();
    }

    void OnDestroy()
    {
        if (tailEndVisualAnchor != null)
        {
            Destroy(tailEndVisualAnchor.gameObject);
        }
    }

    void ResolveReferences()
    {
        rig ??= GetComponent<FireHoseRig>();
        deployable ??= GetComponentInChildren<FireHoseDeployable>(true);
        staticHose ??= GetComponentInChildren<FireHoseDeployed>(true);
        dynamicTail ??= GetComponentInChildren<FireHoseTailVisual>(true);
        headPickup ??= GetComponentInChildren<FireHoseHeadPickup>(true);
        shooter ??= GetComponentInChildren<FireHose>(true);

        if (headTransform == null)
        {
            headTransform = headPickup != null ? headPickup.transform : deployable != null ? deployable.head : null;
        }

        if (hoseOrigin == null)
        {
            hoseOrigin = staticHose != null ? staticHose.transform : null;
        }

        if (hoseExit == null && transform != null)
        {
            Transform found = transform.Find("End/HoseExit");
            if (found != null)
            {
                hoseExit = found;
            }
        }
    }

    void WireComponents()
    {
        if (headPickup != null)
        {
            headPickup.ConfigureAssembly(this);
        }

        if (deployable != null && HeadRotationAnchor != null)
        {
            deployable.head = HeadRotationAnchor;
        }

        if (staticHose != null && deployable != null)
        {
            staticHose.source = deployable;
        }

        BindTailVisual();
    }

    void ApplyAuthoringDefaults()
    {
        if (deployable != null && autoDisablePrototypeInputMovement)
        {
            deployable.useInputMovement = false;
        }

        if (shooter != null)
        {
            shooter.gameObject.SetActive(false);
        }
    }

    public void ConfigureRig(FireHoseRig owner)
    {
        rig = owner;
        ResolveReferences();
        WireComponents();
    }

    public void ConfigureLiteOwner(FireTruckHosePickupPointLite owner)
    {
        ownerPickupPointLite = owner;
        BindTailVisual();
    }

    public void SetHeadSnapHeightOffset(float offset)
    {
        headSnapHeightOffset = offset;
    }

    public void ConfigureTailVisual(Vector3 tailEndPosition, Vector3 tailEndNormal)
    {
        EnsureTailEndVisualAnchor();

        Vector3 normal = tailEndNormal.sqrMagnitude > 0.0001f ? tailEndNormal.normalized : Vector3.up;
        tailEndVisualAnchor.SetPositionAndRotation(tailEndPosition, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, normal), normal));
        tailEndVisualTargetPosition = tailEndPosition;
        hasTailEndVisualTarget = true;
        BindTailVisual();
    }

    public void HandleHeadPickedUp(GameObject picker)
    {
        ClearAttachedNozzle();
        ClearAttachmentState();

        if (headTransform != null && headTransform.parent != transform)
        {
            StopAttachSnapRoutine();
            headTransform.SetParent(transform, true);
        }

        currentHolder = picker;
        isHeadHeld = true;
        headPickup?.SetConnected(false);

        if (deployable != null && autoDisablePrototypeInputMovement)
        {
            deployable.useInputMovement = false;
        }
    }

    public void HandleHeadDropped(GameObject dropper)
    {
        if (currentHolder == dropper)
        {
            currentHolder = null;
        }

        isHeadHeld = false;
        headPickup?.SetConnected(false);

        SnapHeadToLatestKnot();
    }

    public bool TryAttachHeadToSocket(
        FireHoseHeadSocket socket,
        GameObject interactor,
        FPSInventorySystem inventory,
        bool snapPosition,
        bool snapRotation)
    {
        if (socket == null || inventory == null || headPickup == null || headTransform == null)
        {
            return false;
        }

        if (inventory.HeldObject != headPickup.gameObject)
        {
            return false;
        }

        if (inventory.HasItem)
        {
            inventory.Drop(interactor);
        }

        Transform attachPoint = socket.AttachPoint;
        if (attachPoint == null)
        {
            attachPoint = socket.transform;
        }

        ClearAttachedNozzle();
        AttachHeadToMount(attachPoint, snapPosition, snapRotation);
        currentSocket = socket;

        currentHolder = null;
        isHeadHeld = false;
        headPickup?.SetConnected(true);
        return true;
    }

    public bool TryAttachHeadToMount(
        Transform attachPoint,
        GameObject interactor,
        FPSInventorySystem inventory,
        FireHose nozzle,
        bool snapPosition,
        bool snapRotation)
    {
        if (attachPoint == null || inventory == null || headPickup == null || headTransform == null)
        {
            return false;
        }

        if (inventory.HeldObject == headPickup.gameObject)
        {
            inventory.Drop(interactor);
        }

        ClearAttachedNozzle();
        AttachHeadToMount(attachPoint, snapPosition, snapRotation);
        currentAttachedNozzle = nozzle;
        currentAttachedNozzle?.ConfigureConnectedAssembly(this);
        ownerPickupPointLite?.SetConnectedNozzle(nozzle);
        SyncAttachedNozzleSupply();

        currentHolder = null;
        isHeadHeld = false;
        headPickup?.SetConnected(true);
        return true;
    }

    public void SyncAttachedNozzleSupply()
    {
        if (currentAttachedNozzle == null)
        {
            return;
        }

        FireHose attachedNozzle = CurrentAttachedNozzle;
        if (attachedNozzle == null)
        {
            return;
        }

        FireHoseConnectionPoint hydrant =
            OwnerPumpSystem != null ? OwnerPumpSystem.GetSupplyHydrant() :
            rig != null ? rig.ConnectedHydrant :
            null;

        if (hydrant != null)
        {
            attachedNozzle.TryConnectToSupply(hydrant);
            return;
        }

        attachedNozzle.DisconnectFromSupply();
    }

    private void AttachHeadToMount(Transform attachPoint, bool snapPosition, bool snapRotation)
    {
        if (attachPoint == null || headTransform == null)
        {
            return;
        }

        ClearAttachmentState();
        StopAttachSnapRoutine();
        headTransform.SetParent(attachPoint, true);

        currentAttachPoint = attachPoint;

        Quaternion rotationOffset = headPickup != null ? headPickup.ConnectedRotationOffset : Quaternion.identity;
        Vector3 targetLocalPosition = snapPosition ? Vector3.zero : headTransform.localPosition;
        Quaternion targetLocalRotation = snapRotation ? rotationOffset : headTransform.localRotation;
        float attachDuration = headPickup != null ? headPickup.ConnectAttachDuration : 0f;

        if (attachDuration <= 0.0001f || (!snapPosition && !snapRotation))
        {
            if (snapPosition)
            {
                headTransform.localPosition = targetLocalPosition;
            }

            if (snapRotation)
            {
                headTransform.localRotation = targetLocalRotation;
            }

            return;
        }

        attachSnapRoutine = StartCoroutine(SmoothAttachHeadRoutine(targetLocalPosition, targetLocalRotation, snapPosition, snapRotation, attachDuration));
    }

    private void ClearAttachmentState()
    {
        currentSocket = null;
        currentAttachPoint = null;
        headPickup?.SetConnected(false);
    }

    private IEnumerator SmoothAttachHeadRoutine(
        Vector3 targetLocalPosition,
        Quaternion targetLocalRotation,
        bool snapPosition,
        bool snapRotation,
        float duration)
    {
        Vector3 startLocalPosition = headTransform.localPosition;
        Quaternion startLocalRotation = headTransform.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (headTransform == null)
            {
                attachSnapRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            if (snapPosition)
            {
                headTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, easedT);
            }

            if (snapRotation)
            {
                headTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, easedT);
            }

            yield return null;
        }

        if (headTransform != null)
        {
            if (snapPosition)
            {
                headTransform.localPosition = targetLocalPosition;
            }

            if (snapRotation)
            {
                headTransform.localRotation = targetLocalRotation;
            }
        }

        attachSnapRoutine = null;
    }

    private void StopAttachSnapRoutine()
    {
        if (attachSnapRoutine == null)
        {
            return;
        }

        StopCoroutine(attachSnapRoutine);
        attachSnapRoutine = null;
    }

    private void ClearAttachedNozzle()
    {
        if (currentAttachedNozzle == null)
        {
            ownerPickupPointLite?.ClearConnectedNozzle(null);
            return;
        }

        ownerPickupPointLite?.ClearConnectedNozzle(currentAttachedNozzle);
        currentAttachedNozzle.ConfigureConnectedAssembly(null);
        currentAttachedNozzle.DisconnectFromSupply(rig != null ? rig.ConnectedHydrant : null);
        currentAttachedNozzle = null;
    }

    public bool TryGetLatestHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = headTransform != null ? headTransform.position : transform.position;
        rotation = headTransform != null ? headTransform.rotation : transform.rotation;

        if (deployable == null)
        {
            return false;
        }

        FireHosePath path = deployable.Path;
        if (path == null || path.Knots == null || path.Knots.Count == 0)
        {
            return false;
        }

        Knot latestKnot = path.Knots[path.Knots.Count - 1];
        Vector3 up = latestKnot.Rotation * Vector3.up;
        if (up.sqrMagnitude <= 0.0001f)
        {
            up = latestKnot.Normal.sqrMagnitude > 0.0001f ? latestKnot.Normal.normalized : Vector3.up;
        }

        position = latestKnot.Position + up.normalized * headSnapHeightOffset;
        rotation = latestKnot.Rotation;
        return true;
    }

    public void SnapHeadToLatestKnot()
    {
        if (headTransform == null)
        {
            return;
        }

        if (!TryGetLatestHeadPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        headTransform.SetPositionAndRotation(position, rotation);
    }

    private void BindTailVisual()
    {
        if (dynamicTail == null)
        {
            return;
        }

        Transform tailStart = hoseExit;
        if (tailStart == null && ownerPickupPointLite != null)
        {
            tailStart = ownerPickupPointLite.TailAnchor;
        }

        if (tailStart == null)
        {
            tailStart = hoseOrigin;
        }

        dynamicTail.SetEndpoints(tailStart, tailEndVisualAnchor);
    }

    private void EnsureTailEndVisualAnchor()
    {
        if (tailEndVisualAnchor != null)
        {
            return;
        }

        GameObject anchorObject = new GameObject("FireHoseTail_EndPoint");
        anchorObject.transform.SetParent(transform, true);
        tailEndVisualAnchor = anchorObject.transform;
    }

    private void UpdateTailEndVisualAnchor()
    {
        if (!hasTailEndVisualTarget || tailEndVisualAnchor == null)
        {
            return;
        }

        Vector3 currentPosition = tailEndVisualAnchor.position;
        Vector3 probeOrigin = new Vector3(
            tailEndVisualTargetPosition.x,
            tailEndVisualTargetPosition.y + TailEndProbeHeight,
            tailEndVisualTargetPosition.z);

        int groundMask = ownerPickupPointLite != null ? ownerPickupPointLite.GroundMask : Physics.DefaultRaycastLayers;
        if (!Physics.Raycast(
            probeOrigin,
            Vector3.down,
            out RaycastHit groundHit,
            TailEndProbeDistance + TailEndProbeHeight,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float nextY = Mathf.MoveTowards(currentPosition.y, groundHit.point.y, TailEndFallSpeed * Time.deltaTime);
        tailEndVisualAnchor.position = new Vector3(tailEndVisualTargetPosition.x, nextY, tailEndVisualTargetPosition.z);
    }
}
