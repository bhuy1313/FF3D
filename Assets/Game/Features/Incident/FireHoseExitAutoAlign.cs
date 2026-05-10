using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(550)]
public class FireHoseExitAutoAlign : MonoBehaviour
{
    [SerializeField] private Transform groundReference;
    [SerializeField] private FireHoseDeployable deployable;
    [SerializeField] private FireHoseDeployed staticHose;
    [SerializeField] private float fallbackSurfaceOffset = 0.03f;
    [SerializeField] private bool keepLocalPlanarOffsetZero = true;

    void Reset()
    {
        groundReference = transform.parent;
        deployable = GetComponentInParent<FireHoseDeployable>();
        staticHose = FindAnyObjectByType<FireHoseDeployed>();
    }

    void LateUpdate()
    {
        Transform reference = groundReference != null ? groundReference : transform.parent;
        if (reference == null)
        {
            return;
        }

        float raycastHeight = deployable != null ? Mathf.Max(0.01f, deployable.raycastHeight) : 2f;
        LayerMask groundMask = deployable != null ? deployable.groundMask : Physics.DefaultRaycastLayers;
        Vector3 origin = reference.position + Vector3.up * raycastHeight;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask))
        {
            return;
        }

        Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        float surfaceOffset = staticHose != null ? staticHose.surfaceOffset : fallbackSurfaceOffset;
        transform.position = hit.point + normal * Mathf.Max(0f, surfaceOffset);

        if (keepLocalPlanarOffsetZero)
        {
            transform.localPosition = new Vector3(0f, transform.localPosition.y, 0f);
        }
    }
}
