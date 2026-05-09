using UnityEngine;

[DisallowMultipleComponent]
public sealed class PerfectHandsRotationLag : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;

    [Header("Lag")]
    [SerializeField] private bool enableLag = true;
    [SerializeField, Min(0.01f)] private float followSpeed = 12f;
    [SerializeField, Range(0f, 45f)] private float maxAngle = 10f;

    [Header("Runtime")]
    [SerializeField] private Quaternion defaultLocalRotation = Quaternion.identity;
    [SerializeField] private Quaternion currentWorldRotation = Quaternion.identity;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnValidate()
    {
        followSpeed = Mathf.Max(0.01f, followSpeed);
        maxAngle = Mathf.Clamp(maxAngle, 0f, 45f);
    }

    private void LateUpdate()
    {
        if (!enableLag || followTarget == null)
        {
            return;
        }

        Quaternion targetWorldRotation = followTarget.rotation * defaultLocalRotation;
        float interpolation = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        currentWorldRotation = Quaternion.Slerp(currentWorldRotation, targetWorldRotation, interpolation);

        float angle = Quaternion.Angle(targetWorldRotation, currentWorldRotation);
        if (angle > maxAngle && angle > 0.001f)
        {
            currentWorldRotation = Quaternion.Slerp(targetWorldRotation, currentWorldRotation, maxAngle / angle);
        }

        transform.localRotation = Quaternion.Inverse(followTarget.rotation) * currentWorldRotation;
    }

    [ContextMenu("Snap To Follow Target")]
    public void SnapToFollowTarget()
    {
        Initialize();
        if (followTarget == null)
        {
            return;
        }

        transform.localRotation = defaultLocalRotation;
    }

    private void Initialize()
    {
        if (followTarget == null)
        {
            followTarget = transform.parent;
        }

        defaultLocalRotation = transform.localRotation;
        currentWorldRotation = followTarget != null
            ? followTarget.rotation * defaultLocalRotation
            : transform.rotation;
    }
}
