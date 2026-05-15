using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class FireHoseHeadPickup : MonoBehaviour,
    IInteractable,
    IPickupable,
    IUsable,
    IMovementWeightSource,
    IHandOccupyingObject,
    IInventoryStowBlocker,
    IInventorySelectionBlocker,
    IJumpActionBlocker
{
    [SerializeField] private FireHoseAssembly assembly;
    [SerializeField] private Transform rotationAnchor;
    [SerializeField] private GameObject nozzleConnectedHiddenTarget;
    [SerializeField] private float movementWeightKg = 12f;
    [SerializeField] private bool isConnected;
    [SerializeField] private float connectAttachDuration = 0.12f;
    [SerializeField] private Vector3 connectedRotationOffsetEuler;
    private Rigidbody cachedRigidbody;
    private Collider[] childColliders = System.Array.Empty<Collider>();
    private bool[] childColliderDefaultStates = System.Array.Empty<bool>();
    private bool nozzleConnectedHiddenTargetDefaultActive = true;

    public Rigidbody Rigidbody => cachedRigidbody;
    public Transform RotationAnchor => ResolveRotationAnchor();
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public bool IsHeld => assembly != null && assembly.IsHeadHeld;
    public bool IsConnected => isConnected;
    public float ConnectAttachDuration => Mathf.Max(0f, connectAttachDuration);
    public Quaternion ConnectedRotationOffset => Quaternion.Euler(connectedRotationOffsetEuler);
    public GameObject CurrentHolder => assembly != null ? assembly.CurrentHolder : null;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        ResolveRotationAnchor();
        CacheNozzleConnectedVisualTarget();
        CacheChildColliders();
        ApplyConnectedState();
        if (assembly == null)
        {
            assembly = GetComponentInParent<FireHoseAssembly>() ?? FindAnyObjectByType<FireHoseAssembly>();
        }
    }

    void Reset()
    {
        assembly = GetComponentInParent<FireHoseAssembly>() ?? FindAnyObjectByType<FireHoseAssembly>();
        cachedRigidbody = GetComponent<Rigidbody>();
        ResolveRotationAnchor();
        CacheNozzleConnectedVisualTarget();
        CacheChildColliders();
        ApplyConnectedState();
    }

    void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        ResolveRotationAnchor();
        CacheNozzleConnectedVisualTarget();
        CacheChildColliders();
        ApplyConnectedState();
    }

    void LateUpdate()
    {
        SyncRotationAnchorWhenFree();
    }

    public void Interact(GameObject interactor)
    {
        if (interactor == null || assembly == null || !interactor.TryGetComponent(out FPSInventorySystem inventory))
        {
            return;
        }

        GameObject heldObject = inventory.HeldObject;
        if (heldObject == null)
        {
            return;
        }

        FireHose heldNozzle = heldObject.GetComponent<FireHose>() ?? heldObject.GetComponentInParent<FireHose>();
        if (heldNozzle == null)
        {
            return;
        }

        assembly.TryAttachHeadToMount(
            heldNozzle.transform,
            interactor,
            inventory,
            heldNozzle,
            snapPosition: true,
            snapRotation: true);
    }

    public void Use(GameObject user)
    {
        FireHose shooter = assembly != null ? assembly.Shooter : null;
        if (shooter == null)
        {
            return;
        }

        shooter.Use(user);
    }

    public void OnPickup(GameObject picker)
    {
        assembly?.HandleHeadPickedUp(picker);
    }

    public void OnDrop(GameObject dropper)
    {
        assembly?.HandleHeadDropped(dropper);
    }

    public void ConfigureAssembly(FireHoseAssembly owner)
    {
        assembly = owner;
        ApplyConnectedState();
    }

    public void SetConnected(bool value)
    {
        isConnected = value;
        ApplyConnectedState();
    }

    public FireHoseAssembly Assembly => assembly;

    public bool BlocksInventoryStow(GameObject owner)
    {
        return owner != null && CurrentHolder == owner;
    }

    public bool BlocksInventorySelectionChange(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }

    public bool BlocksJumpAction(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }

    private Transform ResolveRotationAnchor()
    {
        if (rotationAnchor == null)
        {
            Transform found = transform.Find("RotationAnchor");
            rotationAnchor = found != null ? found : transform;
        }

        return rotationAnchor;
    }

    private void SyncRotationAnchorWhenFree()
    {
        Transform anchor = ResolveRotationAnchor();
        if (anchor == null || anchor == transform || assembly == null)
        {
            return;
        }

        if (assembly.IsHeadHeld || assembly.IsAttached)
        {
            return;
        }

        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
    }

    private void CacheChildColliders()
    {
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        int childColliderCount = 0;

        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null && allColliders[i].transform != transform)
            {
                childColliderCount++;
            }
        }

        childColliders = new Collider[childColliderCount];
        childColliderDefaultStates = new bool[childColliderCount];

        int index = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider collider = allColliders[i];
            if (collider == null || collider.transform == transform)
            {
                continue;
            }

            childColliders[index] = collider;
            childColliderDefaultStates[index] = collider.enabled;
            index++;
        }
    }

    private void CacheNozzleConnectedVisualTarget()
    {
        if (nozzleConnectedHiddenTarget == null)
        {
            Transform defaultTarget = transform.Find("model_normal");
            if (defaultTarget != null)
            {
                nozzleConnectedHiddenTarget = defaultTarget.gameObject;
            }
        }

        if (nozzleConnectedHiddenTarget != null)
        {
            nozzleConnectedHiddenTargetDefaultActive = nozzleConnectedHiddenTarget.activeSelf;
        }
    }

    private void ApplyConnectedState()
    {
        ApplyConnectedColliderState();
        ApplyNozzleConnectedVisualState();
    }

    private void ApplyConnectedColliderState()
    {
        if (childColliders == null || childColliderDefaultStates == null || childColliders.Length != childColliderDefaultStates.Length)
        {
            CacheChildColliders();
        }

        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider collider = childColliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.enabled = isConnected ? false : childColliderDefaultStates[i];
        }
    }

    private void ApplyNozzleConnectedVisualState()
    {
        if (nozzleConnectedHiddenTarget == null)
        {
            return;
        }

        bool hideForNozzleConnection = isConnected && assembly != null && assembly.IsConnectedToNozzle;
        nozzleConnectedHiddenTarget.SetActive(hideForNozzleConnection ? false : nozzleConnectedHiddenTargetDefaultActive);
    }
}
