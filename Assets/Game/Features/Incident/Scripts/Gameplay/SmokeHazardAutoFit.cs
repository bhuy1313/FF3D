using UnityEngine;
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(SmokeHazard))]
public class SmokeHazardAutoFit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private BoxCollider triggerZone;
    [SerializeField] private Transform cornerMarkerA;
    [SerializeField] private Transform cornerMarkerB;

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
    }

    private bool TryCalculateCornerBounds(out Bounds localBounds)
    {
        localBounds = default;
        if (cornerMarkerA == null || cornerMarkerB == null)
        {
            return false;
        }

        bool foundAny = false;
        float minX = 0f;
        float maxX = 0f;
        float minZ = 0f;
        float maxZ = 0f;
        float floorY = 0f;

        Transform[] corners = { cornerMarkerA, cornerMarkerB };
        for (int i = 0; i < corners.Length; i++)
        {
            Transform corner = corners[i];
            Vector3 cornerPosition = transform.InverseTransformPoint(corner.position);
            float cornerFloorY = ResolveCornerFloorLocalY(corner, cornerPosition.y);

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
        localBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, bottomY + (resolvedHeight * 0.5f), (minZ + maxZ) * 0.5f),
            new Vector3(
                Mathf.Max(minimumSize, maxX - minX),
                resolvedHeight,
                Mathf.Max(minimumSize, maxZ - minZ)));
        return true;
    }

    private float ResolveCornerFloorLocalY(Transform corner, float fallbackLocalY)
    {
        if (!TryResolveCornerFloorWorldY(corner, out float floorWorldY))
        {
            return fallbackLocalY;
        }

        Vector3 floorWorldPoint = corner.position;
        floorWorldPoint.y = floorWorldY;
        return transform.InverseTransformPoint(floorWorldPoint).y;
    }

    private static bool TryResolveCornerFloorWorldY(Transform corner, out float floorWorldY)
    {
        floorWorldY = 0f;
        if (corner == null)
        {
            return false;
        }

        Collider cornerCollider = corner.GetComponent<Collider>();
        if (cornerCollider != null)
        {
            floorWorldY = cornerCollider.bounds.min.y;
            return true;
        }

        Renderer cornerRenderer = corner.GetComponent<Renderer>();
        if (cornerRenderer != null)
        {
            floorWorldY = cornerRenderer.bounds.min.y;
            return true;
        }

        cornerCollider = corner.GetComponentInChildren<Collider>(true);
        if (cornerCollider != null)
        {
            floorWorldY = cornerCollider.bounds.min.y;
            return true;
        }

        cornerRenderer = corner.GetComponentInChildren<Renderer>(true);
        if (cornerRenderer != null)
        {
            floorWorldY = cornerRenderer.bounds.min.y;
            return true;
        }

        return false;
    }
}
