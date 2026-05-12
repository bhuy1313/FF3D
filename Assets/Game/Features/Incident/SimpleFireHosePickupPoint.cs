using UnityEngine;

[DisallowMultipleComponent]
public class SimpleFireHosePickupPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private SimpleFireHosePathSystem pathSystem;
    [SerializeField] private Transform startAnchor;
    [SerializeField] private float startNormalOffset = 0.05f;
    [SerializeField] private bool singleUse;

    [SerializeField] private bool hasBeenUsed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void Interact(GameObject interactor)
    {
        if (singleUse && hasBeenUsed)
        {
            return;
        }

        if (pathSystem != null && pathSystem.TryBeginSession(this, interactor))
        {
            hasBeenUsed = true;
        }
    }

    public bool TryResolveStartPose(out Vector3 worldPosition, out Vector3 worldNormal)
    {
        Transform anchor = startAnchor != null ? startAnchor : transform;
        if (pathSystem != null && pathSystem.TryProjectPointToGround(anchor.position, out RaycastHit hit))
        {
            worldNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            worldPosition = hit.point + worldNormal * Mathf.Max(0f, startNormalOffset);
            return true;
        }

        worldPosition = anchor.position;
        worldNormal = Vector3.up;
        return true;
    }

    public Transform ResolveAnchorTransform()
    {
        return startAnchor != null ? startAnchor : transform;
    }

    private void ResolveReferences()
    {
        pathSystem ??= GetComponentInParent<SimpleFireHosePathSystem>();
        pathSystem ??= FindAnyObjectByType<SimpleFireHosePathSystem>(FindObjectsInactive.Include);
        startAnchor ??= transform;
    }
}
