using UnityEngine;

[DisallowMultipleComponent]
public class FireTruckHosePickupPointLite : MonoBehaviour, IInteractable
{
    [SerializeField] private FireApparatusPumpSystem pumpSystem;
    [SerializeField] private FireHoseConnectionSystemLite connectionSystem;
    [SerializeField] private GameObject hoseBodyPrefab;
    [SerializeField] private GameObject headPrefab;
    [SerializeField] private Transform rigParent;
    [SerializeField] private Transform tailAnchor;
    [SerializeField] private int headLayer;
    [SerializeField] private bool isSingleUse = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float forwardOffset = 1.1f;
    [SerializeField] private float lateralJitter = 0.35f;
    [SerializeField] private float playerSideBias = 0.3f;
    [SerializeField] private float downwardBias = 0.7f;
    [SerializeField] private float raycastHeight = 2.5f;
    [SerializeField] private float raycastDistance = 6f;
    [SerializeField] private float fallbackVerticalProbeDistance = 8f;
    [SerializeField] private float spawnNormalOffset = 0.08f;
    [SerializeField] private float headSnapHeightOffset = 0f;
    [SerializeField] private float tailEndVerticalOffset = 0.06f;
    [SerializeField] private bool drawDebug;

    void Awake()
    {
        ResolveReferences();
    }

    void Reset()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    public void Interact(GameObject interactor)
    {
        if (connectionSystem == null)
        {
            return;
        }

        if (!TryResolveSpawnPose(
            interactor,
            out Vector3 headSpawnPosition,
            out Vector3 startKnotPosition,
            out Vector3 startKnotNormal,
            out Vector3 tailEndPosition))
        {
            return;
        }

        connectionSystem.TryDeployRig(this, interactor, headSpawnPosition, startKnotPosition, startKnotNormal, tailEndPosition);
    }

    private void ResolveReferences()
    {
        pumpSystem ??= GetComponentInParent<FireApparatusPumpSystem>();
        connectionSystem ??= GetComponentInParent<FireHoseConnectionSystemLite>();
        connectionSystem ??= FindAnyObjectByType<FireHoseConnectionSystemLite>();
        rigParent ??= transform;
    }

    private bool TryResolveSpawnPose(
        GameObject interactor,
        out Vector3 headSpawnPosition,
        out Vector3 startKnotPosition,
        out Vector3 startKnotNormal,
        out Vector3 tailEndPosition)
    {
        Vector3 preferredForward = ResolvePreferredSpawnForward(interactor);
        Vector3 baseOrigin = transform.position + preferredForward * Mathf.Max(0f, forwardOffset);
        Vector3 lateral = Vector3.Cross(Vector3.up, preferredForward).normalized;
        float randomSide = Random.Range(-Mathf.Max(0f, lateralJitter), Mathf.Max(0f, lateralJitter));
        Vector3 rayOrigin = baseOrigin + lateral * randomSide + Vector3.up * Mathf.Max(0.1f, raycastHeight);
        Vector3 rayDirection = (preferredForward + Vector3.down * Mathf.Max(0.01f, downwardBias)).normalized;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit angledHit, Mathf.Max(0.1f, raycastDistance), groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 normal = angledHit.normal.sqrMagnitude > 0.0001f ? angledHit.normal.normalized : Vector3.up;
            startKnotNormal = normal;
            startKnotPosition = angledHit.point;
            headSpawnPosition = angledHit.point + normal * Mathf.Max(0f, spawnNormalOffset);
            tailEndPosition = startKnotPosition + Vector3.up * Mathf.Max(0f, tailEndVerticalOffset);
            DrawDebugRay(rayOrigin, rayDirection, angledHit.distance, Color.green);
            return true;
        }

        Vector3 fallbackOrigin = baseOrigin + lateral * randomSide + Vector3.up * Mathf.Max(0.1f, raycastHeight);
        if (Physics.Raycast(fallbackOrigin, Vector3.down, out RaycastHit downwardHit, Mathf.Max(0.1f, fallbackVerticalProbeDistance), groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 normal = downwardHit.normal.sqrMagnitude > 0.0001f ? downwardHit.normal.normalized : Vector3.up;
            startKnotNormal = normal;
            startKnotPosition = downwardHit.point;
            headSpawnPosition = downwardHit.point + normal * Mathf.Max(0f, spawnNormalOffset);
            tailEndPosition = startKnotPosition + Vector3.up * Mathf.Max(0f, tailEndVerticalOffset);
            DrawDebugRay(fallbackOrigin, Vector3.down, downwardHit.distance, Color.yellow);
            return true;
        }

        startKnotNormal = Vector3.up;
        startKnotPosition = baseOrigin;
        headSpawnPosition = baseOrigin + Vector3.up * Mathf.Max(0f, spawnNormalOffset);
        tailEndPosition = startKnotPosition + Vector3.up * Mathf.Max(0f, tailEndVerticalOffset);
        DrawDebugRay(rayOrigin, rayDirection, Mathf.Max(0.1f, raycastDistance), Color.red);
        return true;
    }

    private Vector3 ResolvePreferredSpawnForward(GameObject interactor)
    {
        Vector3 fallbackForward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        if (interactor == null)
        {
            return fallbackForward;
        }

        Vector3 towardInteractor = interactor.transform.position - transform.position;
        towardInteractor.y = 0f;
        if (towardInteractor.sqrMagnitude <= 0.0001f)
        {
            return fallbackForward;
        }

        Vector3 preferredForward = towardInteractor.normalized;
        Vector3 sideways = Vector3.Cross(Vector3.up, preferredForward);
        if (sideways.sqrMagnitude > 0.0001f)
        {
            sideways.Normalize();
            float sideAmount = Mathf.Max(0f, playerSideBias);
            float sideSign = Random.value < 0.5f ? -1f : 1f;
            preferredForward = (preferredForward + sideways * sideSign * sideAmount).normalized;
        }

        if (Vector3.Dot(preferredForward, fallbackForward) < -0.95f)
        {
            return fallbackForward;
        }

        return preferredForward;
    }

    private void DrawDebugRay(Vector3 origin, Vector3 direction, float distance, Color color)
    {
        if (!drawDebug)
        {
            return;
        }

        Debug.DrawRay(origin, direction.normalized * distance, color, 2f);
    }

    public bool IsSingleUse => isSingleUse;
    public GameObject HoseBodyPrefab => hoseBodyPrefab;
    public GameObject HeadPrefab => headPrefab;
    public LayerMask GroundMask => groundMask;
    public int HeadLayer => headLayer;
    public float HeadSnapHeightOffset => headSnapHeightOffset;
    public float TailEndVerticalOffset => tailEndVerticalOffset;
    public FireApparatusPumpSystem PumpSystem => pumpSystem;
    public Transform TailAnchor => tailAnchor != null ? tailAnchor : transform;

    public Transform ResolveRigParent()
    {
        return rigParent != null ? rigParent : transform;
    }

    internal void RegisterRuntimeRig(FireHoseRig rig)
    {
        pumpSystem?.RegisterRuntimeRig(this, rig);
    }

    internal void ClearRuntimeRig(FireHoseRig rig)
    {
        pumpSystem?.ClearRuntimeRig(this, rig);
    }

    internal bool TryConnectHydrant(FireHoseConnectionPoint connectionPoint, FireHoseRig rig)
    {
        return pumpSystem != null && pumpSystem.TryConnectHydrant(this, connectionPoint, rig);
    }

    internal bool DisconnectHydrant(FireHoseConnectionPoint expectedConnectionPoint, FireHoseRig rig)
    {
        return pumpSystem != null && pumpSystem.DisconnectHydrant(this, expectedConnectionPoint, rig);
    }

    internal void SetConnectedNozzle(FireHose nozzle)
    {
        pumpSystem?.SetConnectedNozzle(this, nozzle);
    }

    internal void ClearConnectedNozzle(FireHose nozzle)
    {
        pumpSystem?.ClearConnectedNozzle(this, nozzle);
    }

    internal void SyncConnectedNozzleSupply()
    {
        pumpSystem?.SyncConnectedNozzleSupply(this);
    }
}
