using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEquippedItemPoseProfile : MonoBehaviour
{
    [SerializeField] private bool overridePlayerEquippedPose = true;
    [SerializeField] private Vector3 equippedLocalPosition;
    [SerializeField] private Vector3 equippedLocalEulerAngles;

    public bool TryGetEquippedPose(out Vector3 localPosition, out Vector3 localEulerAngles)
    {
        localPosition = equippedLocalPosition;
        localEulerAngles = equippedLocalEulerAngles;
        return overridePlayerEquippedPose;
    }
}
