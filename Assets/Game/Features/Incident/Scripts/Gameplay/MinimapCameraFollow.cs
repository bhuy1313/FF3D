using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class MinimapCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoResolvePlayerOnStart = true;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 25f, 0f);
    [SerializeField] private bool lockHeight = true;
    [SerializeField] private float fixedHeight = 25f;
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] [Min(0.01f)] private float followSmoothTime = 0.12f;

    [Header("Rotation")]
    [SerializeField] private bool followTargetYaw;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(90f, 0f, 0f);

    private Quaternion initialRotation;
    private Vector3 followVelocity;

    private void Awake()
    {
        initialRotation = transform.rotation;
        ResolveTarget();
        SnapToTarget();
    }

    private void Start()
    {
        ResolveTarget();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + worldOffset;
        if (lockHeight)
        {
            desiredPosition.y = fixedHeight;
        }

        if (smoothFollow)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                followSmoothTime);
        }
        else
        {
            transform.position = desiredPosition;
        }

        if (followTargetYaw)
        {
            Vector3 euler = rotationOffsetEuler;
            euler.y += target.eulerAngles.y;
            transform.rotation = Quaternion.Euler(euler);
            return;
        }

        transform.rotation = initialRotation;
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
        SnapToTarget();
    }

    private void ResolveTarget()
    {
        if (target != null || !autoResolvePlayerOnStart)
        {
            return;
        }

        FirstPersonController playerController = FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (playerController != null)
        {
            target = playerController.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + worldOffset;
        if (lockHeight)
        {
            desiredPosition.y = fixedHeight;
        }

        transform.position = desiredPosition;

        if (followTargetYaw)
        {
            Vector3 euler = rotationOffsetEuler;
            euler.y += target.eulerAngles.y;
            transform.rotation = Quaternion.Euler(euler);
            return;
        }

        transform.rotation = initialRotation;
    }
}
