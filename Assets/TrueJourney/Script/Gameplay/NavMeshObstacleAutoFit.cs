using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshObstacle))]
public class NavMeshObstacleAutoFit : MonoBehaviour
{
    private enum BoundsSource
    {
        CollidersThenRenderers,
        CollidersOnly,
        RenderersOnly
    }

    [Header("References")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] private Transform boundsRoot;

    [Header("Source")]
    [SerializeField] private BoundsSource boundsSource = BoundsSource.CollidersThenRenderers;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool includeChildObjects = true;
    [SerializeField] private bool includeTriggerColliders = false;

    [Header("Fit")]
    [SerializeField] private Vector3 sizePadding = Vector3.zero;
    [SerializeField] private float radiusPadding = 0f;
    [SerializeField] private float minimumSize = 0.05f;

    [Header("Shape")]
    [SerializeField] private bool applyShapeSettings = true;
    [SerializeField] private NavMeshObstacleShape obstacleShape = NavMeshObstacleShape.Box;

    [Header("Carve")]
    [SerializeField] private bool applyCarveSettings = true;
    [SerializeField] private bool carve = true;
    [SerializeField] private bool carveOnlyStationary = true;
    [SerializeField] private float carvingMoveThreshold = 0.1f;
    [SerializeField] private float carvingTimeToStationary = 0.5f;

    [Header("Update")]
    [SerializeField] private bool fitOnEnable = true;
    [SerializeField] private bool fitOnValidate = true;

    private void Reset()
    {
        navMeshObstacle = GetComponent<NavMeshObstacle>();
        boundsRoot = transform;
    }

    private void OnEnable()
    {
        if (fitOnEnable)
        {
            FitNow();
        }
    }

    private void OnValidate()
    {
        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        }

        if (boundsRoot == null)
        {
            boundsRoot = transform;
        }

        sizePadding = Vector3.Max(Vector3.zero, sizePadding);
        radiusPadding = Mathf.Max(0f, radiusPadding);
        minimumSize = Mathf.Max(0.001f, minimumSize);
        carvingMoveThreshold = Mathf.Max(0f, carvingMoveThreshold);
        carvingTimeToStationary = Mathf.Max(0f, carvingTimeToStationary);

        if (fitOnValidate)
        {
            FitNow();
        }
    }

    [ContextMenu("Fit NavMeshObstacle")]
    public void FitNow()
    {
        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        }

        Transform root = boundsRoot != null ? boundsRoot : transform;
        if (navMeshObstacle == null || root == null)
        {
            return;
        }

        if (applyShapeSettings)
        {
            navMeshObstacle.shape = obstacleShape;
        }

        if (applyCarveSettings)
        {
            navMeshObstacle.carving = carve;
            navMeshObstacle.carveOnlyStationary = carveOnlyStationary;
            navMeshObstacle.carvingMoveThreshold = carvingMoveThreshold;
            navMeshObstacle.carvingTimeToStationary = carvingTimeToStationary;
        }

        if (!TryCalculateLocalBounds(root, out Bounds localBounds))
        {
            return;
        }

        navMeshObstacle.center = localBounds.center;

        if (navMeshObstacle.shape == NavMeshObstacleShape.Capsule)
        {
            Vector3 localSize = Vector3.Max(localBounds.size + sizePadding, Vector3.one * minimumSize);
            float radius = Mathf.Max(localSize.x, localSize.z) * 0.5f + radiusPadding;
            navMeshObstacle.radius = Mathf.Max(minimumSize * 0.5f, radius);
            navMeshObstacle.height = Mathf.Max(localSize.y, navMeshObstacle.radius * 2f, minimumSize);
            return;
        }

        navMeshObstacle.size = Vector3.Max(localBounds.size + sizePadding, Vector3.one * minimumSize);
    }

    private bool TryCalculateLocalBounds(Transform root, out Bounds localBounds)
    {
        switch (boundsSource)
        {
            case BoundsSource.CollidersOnly:
                return TryCollectColliderBounds(root, out localBounds);
            case BoundsSource.RenderersOnly:
                return TryCollectRendererBounds(root, out localBounds);
            default:
                if (TryCollectColliderBounds(root, out localBounds))
                {
                    return true;
                }

                return TryCollectRendererBounds(root, out localBounds);
        }
    }

    private bool TryCollectColliderBounds(Transform root, out Bounds localBounds)
    {
        localBounds = default;
        bool foundAny = false;
        Collider[] colliders = includeChildObjects
            ? root.GetComponentsInChildren<Collider>(includeInactiveChildren)
            : root.GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!includeTriggerColliders && collider.isTrigger)
            {
                continue;
            }

            if (!foundAny)
            {
                localBounds = CreateLocalBounds(collider.bounds);
                foundAny = true;
                continue;
            }

            EncapsulateWorldBounds(ref localBounds, collider.bounds);
        }

        return foundAny;
    }

    private bool TryCollectRendererBounds(Transform root, out Bounds localBounds)
    {
        localBounds = default;
        bool foundAny = false;
        Renderer[] renderers = includeChildObjects
            ? root.GetComponentsInChildren<Renderer>(includeInactiveChildren)
            : root.GetComponents<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!foundAny)
            {
                localBounds = CreateLocalBounds(renderer.bounds);
                foundAny = true;
                continue;
            }

            EncapsulateWorldBounds(ref localBounds, renderer.bounds);
        }

        return foundAny;
    }

    private Bounds CreateLocalBounds(Bounds worldBounds)
    {
        Vector3[] corners = GetWorldBoundsCorners(worldBounds);
        Vector3 firstPoint = transform.InverseTransformPoint(corners[0]);
        Bounds localBounds = new Bounds(firstPoint, Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(transform.InverseTransformPoint(corners[i]));
        }

        return localBounds;
    }

    private void EncapsulateWorldBounds(ref Bounds localBounds, Bounds worldBounds)
    {
        Vector3[] corners = GetWorldBoundsCorners(worldBounds);
        for (int i = 0; i < corners.Length; i++)
        {
            localBounds.Encapsulate(transform.InverseTransformPoint(corners[i]));
        }
    }

    private static Vector3[] GetWorldBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };
    }
}
