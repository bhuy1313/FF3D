using UnityEngine;

[DisallowMultipleComponent]
public class PortableWorkLightPlaceable : PlaceableItem
{
    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float placementDistance = 4f;
    [SerializeField] private float surfacePadding = 0.02f;
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private bool keepUprightOnFloorPlacement = true;

    [Header("Light")]
    [SerializeField] private PortableWorkLightDeployed deployedLightPrefab;
    [SerializeField] private bool overrideDeployedLightMode;
    [SerializeField] private PortableWorkLightDeployed.PortableWorkLightMode deployedLightMode = PortableWorkLightDeployed.PortableWorkLightMode.Omni;

    protected override bool TryPlace(GameObject user)
    {
        if (deployedLightPrefab == null || user == null)
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

        PortableWorkLightDeployed deployedLight = Instantiate(deployedLightPrefab, position, rotation);
        deployedLight.name = deployedLightPrefab.name;

        if (overrideDeployedLightMode && deployedLight != null)
        {
            deployedLight.SetLightMode(deployedLightMode);
        }

        return deployedLight != null;
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
}
