using UnityEngine;

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

    [Header("Carry")]
    [SerializeField] private bool lockHeadPhysicsWhileHeld = true;
    [SerializeField] private bool reenableHeadPhysicsOnDrop = true;
    [SerializeField] private bool autoDisablePrototypeInputMovement = true;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private bool isHeadHeld;
    [SerializeField] private FireHoseHeadSocket currentSocket;

    private Rigidbody headRigidbody;
    private bool cachedHeadWasKinematic;
    private bool cachedHeadDetectCollisions = true;

    public bool IsHeadHeld => isHeadHeld;
    public GameObject CurrentHolder => currentHolder;
    public bool IsAttached => currentSocket != null;
    public FireHose Shooter => shooter;
    public FireHoseRig Rig => rig;
    public Transform HeadTransform => headTransform;

    void Awake()
    {
        ResolveReferences();
        CacheHeadRigidbodyState();
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
            headTransform = deployable != null ? deployable.head : null;
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

        if (deployable != null && headTransform != null)
        {
            deployable.head = headTransform;
        }

        if (staticHose != null && deployable != null)
        {
            staticHose.source = deployable;
        }
    }

    void CacheHeadRigidbodyState()
    {
        headRigidbody = headPickup != null ? headPickup.Rigidbody : null;
        if (headRigidbody == null)
        {
            return;
        }

        cachedHeadWasKinematic = headRigidbody.isKinematic;
        cachedHeadDetectCollisions = headRigidbody.detectCollisions;
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
    }

    public void HandleHeadPickedUp(GameObject picker)
    {
        if (currentSocket != null)
        {
            currentSocket = null;
        }

        if (headTransform != null && headTransform.parent != transform)
        {
            headTransform.SetParent(transform, true);
        }

        currentHolder = picker;
        isHeadHeld = true;

        if (deployable != null && autoDisablePrototypeInputMovement)
        {
            deployable.useInputMovement = false;
        }

        if (headRigidbody == null)
        {
            CacheHeadRigidbodyState();
        }

        if (headRigidbody != null && lockHeadPhysicsWhileHeld)
        {
            headRigidbody.linearVelocity = Vector3.zero;
            headRigidbody.angularVelocity = Vector3.zero;
            headRigidbody.isKinematic = true;
            headRigidbody.detectCollisions = false;
        }
    }

    public void HandleHeadDropped(GameObject dropper)
    {
        if (currentHolder == dropper)
        {
            currentHolder = null;
        }

        isHeadHeld = false;

        if (headRigidbody == null)
        {
            CacheHeadRigidbodyState();
        }

        if (headRigidbody != null && reenableHeadPhysicsOnDrop)
        {
            headRigidbody.isKinematic = cachedHeadWasKinematic;
            headRigidbody.detectCollisions = cachedHeadDetectCollisions;
        }
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

        currentSocket = socket;

        if (inventory.HasItem)
        {
            inventory.Drop(interactor);
        }

        Transform attachPoint = socket.AttachPoint;
        if (attachPoint == null)
        {
            attachPoint = socket.transform;
        }

        headTransform.SetParent(attachPoint, true);

        if (snapPosition)
        {
            headTransform.position = attachPoint.position;
        }

        if (snapRotation)
        {
            headTransform.rotation = attachPoint.rotation;
        }

        if (headRigidbody == null)
        {
            CacheHeadRigidbodyState();
        }

        if (headRigidbody != null)
        {
            headRigidbody.linearVelocity = Vector3.zero;
            headRigidbody.angularVelocity = Vector3.zero;
            headRigidbody.isKinematic = true;
            headRigidbody.detectCollisions = false;
        }

        currentHolder = null;
        isHeadHeld = false;
        return true;
    }
}
