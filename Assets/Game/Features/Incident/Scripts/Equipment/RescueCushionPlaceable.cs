using UnityEngine;

[DisallowMultipleComponent]
public class RescueCushionPlaceable : PlaceableItem
{
    [Header("Pickup Presentation")]
    [SerializeField] private Collider pickupCollider;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float pickupVisualWidth = 1.2f;
    [SerializeField] private float pickupVisualHeight = 0.2f;

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float placementDistance = 6f;
    [SerializeField] private float surfacePadding;
    [SerializeField] private bool alignToSurfaceNormal;
    [SerializeField] private bool keepUprightOnFloorPlacement = true;

    [Header("Cushion")]
    [SerializeField] private RescueCushionDeployed deployedCushionPrefab;

    protected override void Awake()
    {
        base.Awake();
        EnsureFallbackSetup();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        pickupVisualWidth = Mathf.Max(0.25f, pickupVisualWidth);
        pickupVisualHeight = Mathf.Max(0.05f, pickupVisualHeight);
        placementDistance = Mathf.Max(0.1f, placementDistance);
        surfacePadding = Mathf.Max(0f, surfacePadding);
        RefreshFallbackGeometry();
    }

    protected override bool TryPlace(GameObject user)
    {
        if (user == null)
        {
            return false;
        }

        Transform aimTransform = ResolveAimTransform(user);
        if (aimTransform == null)
        {
            return false;
        }

        Ray ray = new Ray(aimTransform.position, aimTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0.1f, placementDistance), placementMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Quaternion rotation = ResolvePlacementRotation(aimTransform, hit.normal);
        Vector3 position = hit.point + hit.normal * Mathf.Max(0f, surfacePadding);

        RescueCushionDeployed deployedCushion = deployedCushionPrefab != null
            ? Instantiate(deployedCushionPrefab, position, rotation)
            : new GameObject("RescueCushion_Deployed").AddComponent<RescueCushionDeployed>();

        if (deployedCushion == null)
        {
            return false;
        }

        deployedCushion.name = deployedCushionPrefab != null
            ? deployedCushionPrefab.name
            : "RescueCushion_Deployed";
        deployedCushion.transform.SetPositionAndRotation(position, rotation);
        return true;
    }

    private void EnsureFallbackSetup()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider>();
        }

        if (pickupCollider == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            pickupCollider = boxCollider;
        }

        if (visualRoot == null)
        {
            Transform existingVisual = transform.Find("PickupVisual");
            if (existingVisual != null)
            {
                visualRoot = existingVisual;
            }
        }

        if (visualRoot == null)
        {
            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualObject.name = "PickupVisual";
            visualObject.transform.SetParent(transform, false);
            visualRoot = visualObject.transform;

            Collider visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null)
            {
                DestroyRuntimeSafe(visualCollider);
            }
        }

        RefreshFallbackGeometry();
    }

    private void RefreshFallbackGeometry()
    {
        if (pickupCollider is BoxCollider boxCollider)
        {
            boxCollider.size = new Vector3(pickupVisualWidth, pickupVisualHeight, pickupVisualWidth);
            boxCollider.center = new Vector3(0f, pickupVisualHeight * 0.5f, 0f);
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = new Vector3(0f, pickupVisualHeight * 0.5f, 0f);
            visualRoot.localScale = new Vector3(pickupVisualWidth, pickupVisualHeight, pickupVisualWidth);
        }
    }

    private Transform ResolveAimTransform(GameObject user)
    {
        Transform cameraRoot = user.transform.Find("PlayerCameraRoot");
        if (cameraRoot != null)
        {
            return cameraRoot;
        }

        Camera cameraComponent = user.GetComponentInChildren<Camera>();
        if (cameraComponent != null)
        {
            return cameraComponent.transform;
        }

        return user.transform;
    }

    private Quaternion ResolvePlacementRotation(Transform aimTransform, Vector3 surfaceNormal)
    {
        Vector3 up = alignToSurfaceNormal && surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized
            : Vector3.up;

        Vector3 forward = Vector3.ProjectOnPlane(aimTransform.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(aimTransform.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        forward.Normalize();
        Quaternion rotation = Quaternion.LookRotation(forward, up);

        if (!alignToSurfaceNormal && keepUprightOnFloorPlacement)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            }
        }

        return rotation;
    }

    private static void DestroyRuntimeSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
