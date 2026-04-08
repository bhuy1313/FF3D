using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(SmokeHazard))]
public class SmokeHazardAutoFit : MonoBehaviour
{
    private const int RequiredCornerCount = 4;

    [Header("References")]
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private BoxCollider triggerZone;
    [SerializeField] private Transform[] cornerMarkers = new Transform[RequiredCornerCount];

    [Header("Fit")]
    [SerializeField] private float roomHeight = 3.2f;
    [SerializeField] private float bottomOffset = 0f;
    [SerializeField] private Vector3 sizePadding = Vector3.zero;
    [SerializeField] private float minimumSize = 0.05f;

    [Header("Update")]
    [SerializeField] private bool fitOnEnable = false;
    [SerializeField] private bool fitOnValidate = false;

    private void Reset()
    {
        smokeHazard = GetComponent<SmokeHazard>();
        triggerZone = GetComponent<BoxCollider>();
        EnsureCornerArraySize();
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
        ResolveReferences();
        roomHeight = Mathf.Max(minimumSize, roomHeight);
        minimumSize = Mathf.Max(0.001f, minimumSize);
        sizePadding = Vector3.Max(Vector3.zero, sizePadding);

        if (fitOnValidate)
        {
            FitNow();
        }
    }

    [ContextMenu("Fit Smoke Hazard")]
    public void FitNow()
    {
        ResolveReferences();
        if (smokeHazard == null || triggerZone == null)
        {
            return;
        }

        if (!TryCalculateCornerBounds(out Bounds localBounds))
        {
            return;
        }

        triggerZone.center = localBounds.center;
        triggerZone.size = Vector3.Max(localBounds.size + sizePadding, Vector3.one * minimumSize);
        triggerZone.isTrigger = true;
        smokeHazard.SetTriggerZone(triggerZone);
    }

    private void ResolveReferences()
    {
        if (smokeHazard == null)
        {
            smokeHazard = GetComponent<SmokeHazard>();
        }

        if (triggerZone == null)
        {
            triggerZone = GetComponent<BoxCollider>();
        }

        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }

        EnsureCornerArraySize();
    }

    private void EnsureCornerArraySize()
    {
        if (cornerMarkers != null && cornerMarkers.Length == RequiredCornerCount)
        {
            return;
        }

        Transform[] resized = new Transform[RequiredCornerCount];
        if (cornerMarkers != null)
        {
            int copyLength = Mathf.Min(cornerMarkers.Length, resized.Length);
            for (int i = 0; i < copyLength; i++)
            {
                resized[i] = cornerMarkers[i];
            }
        }

        cornerMarkers = resized;
    }

    private bool TryCalculateCornerBounds(out Bounds localBounds)
    {
        localBounds = default;
        if (cornerMarkers == null || cornerMarkers.Length < RequiredCornerCount)
        {
            return false;
        }

        bool foundAny = false;
        float minX = 0f;
        float maxX = 0f;
        float minZ = 0f;
        float maxZ = 0f;
        float floorY = 0f;

        for (int i = 0; i < cornerMarkers.Length; i++)
        {
            Transform corner = cornerMarkers[i];
            if (corner == null)
            {
                continue;
            }

            Vector3 cornerPosition = corner.position;
            float cornerFloorY = ResolveCornerFloorY(corner, cornerPosition.y);

            if (!foundAny)
            {
                foundAny = true;
                minX = maxX = cornerPosition.x;
                minZ = maxZ = cornerPosition.z;
                floorY = cornerFloorY;
                continue;
            }

            minX = Mathf.Min(minX, cornerPosition.x);
            maxX = Mathf.Max(maxX, cornerPosition.x);
            minZ = Mathf.Min(minZ, cornerPosition.z);
            maxZ = Mathf.Max(maxZ, cornerPosition.z);
            floorY = Mathf.Min(floorY, cornerFloorY);
        }

        if (!foundAny)
        {
            return false;
        }

        float resolvedHeight = Mathf.Max(minimumSize, roomHeight);
        float bottomY = floorY + bottomOffset;
        Bounds worldBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, bottomY + (resolvedHeight * 0.5f), (minZ + maxZ) * 0.5f),
            new Vector3(
                Mathf.Max(minimumSize, maxX - minX),
                resolvedHeight,
                Mathf.Max(minimumSize, maxZ - minZ)));

        localBounds = CreateLocalBounds(worldBounds);
        return true;
    }

    private float ResolveCornerFloorY(Transform corner, float fallbackY)
    {
        if (corner == null)
        {
            return fallbackY;
        }

        Collider cornerCollider = corner.GetComponent<Collider>();
        if (cornerCollider != null)
        {
            return cornerCollider.bounds.min.y;
        }

        Renderer cornerRenderer = corner.GetComponent<Renderer>();
        if (cornerRenderer != null)
        {
            return cornerRenderer.bounds.min.y;
        }

        cornerCollider = corner.GetComponentInChildren<Collider>(true);
        if (cornerCollider != null)
        {
            return cornerCollider.bounds.min.y;
        }

        cornerRenderer = corner.GetComponentInChildren<Renderer>(true);
        if (cornerRenderer != null)
        {
            return cornerRenderer.bounds.min.y;
        }

        return fallbackY;
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
