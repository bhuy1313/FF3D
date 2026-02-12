using UnityEngine;

public class ChildRotationLag : MonoBehaviour
{
    [Tooltip("Cha mà ViewPoint sẽ đuổi theo (CameraRoot)")]
    public Transform parentToFollow;

    [Tooltip("Độ nhanh khi đuổi theo. Càng nhỏ = càng trễ.")]
    public float followSpeed = 10f;

    [Tooltip("Giới hạn độ lệch tối đa (độ).")]
    public float maxAngle = 12f;

    Quaternion _defaultLocalRot;

    void Awake()
    {
        _defaultLocalRot = transform.localRotation;
        if (!parentToFollow) parentToFollow = transform.parent;
    }

    void LateUpdate()
    {
        if (!parentToFollow) return;

        // Ta muốn ViewPoint cuối cùng trùng hướng với cha (trong world),
        // nhưng giới hạn độ lệch + tạo trễ.
        Quaternion targetWorldRot = parentToFollow.rotation * _defaultLocalRot;

        // Đuổi theo mượt (time-constant, không phụ thuộc FPS)
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Quaternion newWorldRot = Quaternion.Slerp(transform.rotation, targetWorldRot, t);

        // Clamp độ lệch so với target để không văng quá xa
        float ang = Quaternion.Angle(targetWorldRot, newWorldRot);
        if (ang > maxAngle)
            newWorldRot = Quaternion.Slerp(targetWorldRot, newWorldRot, maxAngle / ang);

        transform.rotation = newWorldRot;
    }
}
