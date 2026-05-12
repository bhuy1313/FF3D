using UnityEngine;

[DisallowMultipleComponent]
public class SimpleFireHoseConnectPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private SimpleFireHosePathSystem pathSystem;
    [SerializeField] private Transform connectAnchor;
    [SerializeField] private float normalOffset = 0.05f;
    [SerializeField] private bool occupied;

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
        if (occupied)
        {
            return;
        }

        pathSystem?.TryCompleteSession(this, interactor);
    }

    public bool TryResolveConnectPose(out Vector3 worldPosition, out Vector3 worldNormal)
    {
        Transform anchor = connectAnchor != null ? connectAnchor : transform;
        if (pathSystem != null && pathSystem.TryProjectPointToGround(anchor.position, out RaycastHit hit))
        {
            worldNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            worldPosition = hit.point + worldNormal * Mathf.Max(0f, normalOffset);
            return true;
        }

        worldPosition = anchor.position;
        worldNormal = Vector3.up;
        return true;
    }

    public Transform ResolveAnchorTransform()
    {
        return connectAnchor != null ? connectAnchor : transform;
    }

    public bool IsOccupied => occupied;

    public void SetOccupied(bool value)
    {
        occupied = value;
    }

    private void ResolveReferences()
    {
        pathSystem ??= GetComponentInParent<SimpleFireHosePathSystem>();
        pathSystem ??= FindAnyObjectByType<SimpleFireHosePathSystem>(FindObjectsInactive.Include);
        connectAnchor ??= transform;
    }
}
