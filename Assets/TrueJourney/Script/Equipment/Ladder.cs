using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class Ladder : MonoBehaviour, IInteractable
{
    [SerializeField] private string requiredTag = "Player";
    [Tooltip("Additional height added to ladder top bound to stop climbing")]
    [SerializeField] private float topHeightOffset = 0.0f;

    public Vector3 GetClimbDirection()
    {
        Vector3 climbDirection = transform.up;
        if (climbDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }

        return climbDirection.normalized;
    }

    public Vector3 GetClimbBottomWorld()
    {
        return GetClimbEndpoint(isTop: false);
    }

    public Vector3 GetClimbTopWorld()
    {
        return GetClimbEndpoint(isTop: true) + GetClimbDirection() * topHeightOffset;
    }

    public float GetClimbExtent()
    {
        Vector3 climbDirection = GetClimbDirection();
        Vector3 bottom = GetClimbBottomWorld();
        Vector3 top = GetClimbTopWorld();
        return Mathf.Max(0f, Vector3.Dot(top - bottom, climbDirection));
    }

    public Vector3 GetClosestPointOnClimbLine(Vector3 worldPoint)
    {
        Vector3 climbDirection = GetClimbDirection();
        Vector3 bottom = GetClimbBottomWorld();
        float climbDistance = Mathf.Clamp(
            Vector3.Dot(worldPoint - bottom, climbDirection),
            0f,
            GetClimbExtent());
        return bottom + climbDirection * climbDistance;
    }

    public Vector3 GetClimbAttachPoint(Vector3 worldPoint, float clearance)
    {
        Vector3 linePoint = GetClosestPointOnClimbLine(worldPoint);
        Vector3 depthDirection = GetClimbDepthDirection();
        float sideSign = Mathf.Sign(Vector3.Dot(worldPoint - linePoint, depthDirection));
        if (Mathf.Abs(sideSign) <= 0.001f)
        {
            sideSign = 1f;
        }

        float offset = GetClimbHalfDepth() + Mathf.Max(0f, clearance);
        return linePoint + depthDirection * (sideSign * offset);
    }

    public bool IsValidClimber(GameObject other)
    {
        if (string.IsNullOrWhiteSpace(requiredTag))
        {
            return true;
        }

        return other != null && other.CompareTag(requiredTag);
    }

    public float GetClimbMaxY()
    {
        return GetClimbTopWorld().y;
    }

    public void SetTopHeightOffset(float value)
    {
        topHeightOffset = Mathf.Max(0f, value);
    }

    public void Interact(GameObject interactor)
    {
        if (!IsValidClimber(interactor))
        {
            return;
        }

        FirstPersonController controller = interactor.GetComponentInParent<FirstPersonController>();
        if (controller != null)
        {
            controller.TryStartClimbFromInteract(this);
        }
    }

    private Vector3 GetClimbEndpoint(bool isTop)
    {
        if (TryGetBoxColliderEndpoint(isTop, out Vector3 endpoint))
        {
            return endpoint;
        }

        float direction = isTop ? 0.5f : -0.5f;
        return transform.position + GetClimbDirection() * direction;
    }

    private Vector3 GetClimbDepthDirection()
    {
        Vector3 depthDirection = transform.forward;
        if (depthDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return depthDirection.normalized;
    }

    private float GetClimbHalfDepth()
    {
        if (!TryGetComponent(out BoxCollider boxCollider))
        {
            return 0f;
        }

        return transform.TransformVector(Vector3.forward * (boxCollider.size.z * 0.5f)).magnitude;
    }

    private bool TryGetBoxColliderEndpoint(bool isTop, out Vector3 endpoint)
    {
        endpoint = default;
        if (!TryGetComponent(out BoxCollider boxCollider))
        {
            return false;
        }

        float verticalSign = isTop ? 0.5f : -0.5f;
        Vector3 localPoint = boxCollider.center + Vector3.up * (boxCollider.size.y * verticalSign);
        endpoint = transform.TransformPoint(localPoint);
        return true;
    }
}
