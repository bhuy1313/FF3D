using UnityEngine;

[DisallowMultipleComponent]
public class FireTruckHosePickupPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private FireHoseConnectionSystem connectionSystem;
    [SerializeField] private GameObject hoseBodyPrefab;
    [SerializeField] private Transform rigParent;
    [SerializeField] private bool isSingleUse = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float forwardOffset = 1.1f;
    [SerializeField] private float lateralJitter = 0.35f;
    [SerializeField] private float downwardBias = 0.7f;
    [SerializeField] private float raycastHeight = 2.5f;
    [SerializeField] private float raycastDistance = 6f;
    [SerializeField] private float fallbackVerticalProbeDistance = 8f;
    [SerializeField] private float spawnNormalOffset = 0.08f;
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

        if (!TryResolveSpawnPose(out Vector3 headSpawnPosition, out Vector3 startKnotPosition, out Vector3 startKnotNormal))
        {
            return;
        }

        connectionSystem.TryDeployRig(this, interactor, headSpawnPosition, startKnotPosition, startKnotNormal);
    }

    private void ResolveReferences()
    {
        connectionSystem ??= GetComponentInParent<FireHoseConnectionSystem>();
        connectionSystem ??= FindFirstObjectByType<FireHoseConnectionSystem>();
        rigParent ??= transform;
    }

    private bool TryResolveSpawnPose(out Vector3 headSpawnPosition, out Vector3 startKnotPosition, out Vector3 startKnotNormal)
    {
        Vector3 baseOrigin = transform.position + transform.forward * Mathf.Max(0f, forwardOffset);
        Vector3 lateral = Vector3.Cross(Vector3.up, transform.forward).normalized;
        float randomSide = Random.Range(-Mathf.Max(0f, lateralJitter), Mathf.Max(0f, lateralJitter));
        Vector3 rayOrigin = baseOrigin + lateral * randomSide + Vector3.up * Mathf.Max(0.1f, raycastHeight);
        Vector3 rayDirection = (transform.forward + Vector3.down * Mathf.Max(0.01f, downwardBias)).normalized;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit angledHit, Mathf.Max(0.1f, raycastDistance), groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 normal = angledHit.normal.sqrMagnitude > 0.0001f ? angledHit.normal.normalized : Vector3.up;
            startKnotNormal = normal;
            startKnotPosition = angledHit.point;
            headSpawnPosition = angledHit.point + normal * Mathf.Max(0f, spawnNormalOffset);
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
            DrawDebugRay(fallbackOrigin, Vector3.down, downwardHit.distance, Color.yellow);
            return true;
        }

        startKnotNormal = Vector3.up;
        startKnotPosition = baseOrigin;
        headSpawnPosition = baseOrigin + Vector3.up * Mathf.Max(0f, spawnNormalOffset);
        DrawDebugRay(rayOrigin, rayDirection, Mathf.Max(0.1f, raycastDistance), Color.red);
        return true;
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

    public Transform ResolveRigParent()
    {
        return rigParent != null ? rigParent : transform;
    }
}
