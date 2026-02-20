using UnityEngine;

public class ChildRotationLag : MonoBehaviour
{
    [Tooltip("Cha mà ViewPoint sẽ đuổi theo (CameraRoot)")]
    public Transform parentToFollow;

    [Tooltip("Độ nhanh khi đuổi theo. Càng nhỏ = càng trễ.")]
    public float followSpeed = 10f;

    [Tooltip("Giới hạn độ lệch tối đa (độ).")]
    public float maxAngle = 12f;

    private Quaternion _defaultLocalRot;
    private Quaternion _currentWorldRot;

    private void Awake()
    {
        _defaultLocalRot = transform.localRotation;
        if (!parentToFollow) parentToFollow = transform.parent;

        _currentWorldRot = parentToFollow != null
            ? parentToFollow.rotation * _defaultLocalRot
            : transform.rotation;
    }

    private void LateUpdate()
    {
        if (!parentToFollow) return;

        Quaternion targetWorldRot = parentToFollow.rotation * _defaultLocalRot;

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        _currentWorldRot = Quaternion.Slerp(_currentWorldRot, targetWorldRot, t);

        float ang = Quaternion.Angle(targetWorldRot, _currentWorldRot);
        if (ang > maxAngle)
        {
            _currentWorldRot = Quaternion.Slerp(targetWorldRot, _currentWorldRot, maxAngle / ang);
        }

        // Quan trọng: object là child nên phải bù ngược parent rotation vào local.
        transform.localRotation = Quaternion.Inverse(parentToFollow.rotation) * _currentWorldRot;
    }
}
