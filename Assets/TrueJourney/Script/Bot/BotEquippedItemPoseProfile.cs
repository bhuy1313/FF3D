using UnityEngine.Serialization;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public class BotEquippedItemPoseProfile : MonoBehaviour, IBotEquippedItemPoseSource
    {
        [SerializeField] private bool overrideBotEquippedPose = true;
        [SerializeField] private Vector3 equippedLocalPosition;
        [SerializeField] private Vector3 equippedLocalEulerAngles;
        [FormerlySerializedAs("useRightHandIk")]
        [SerializeField] private bool useRightHandIkTarget = true;
        [SerializeField, Range(0f, 1f)] private float rightHandIkWeight = 1f;
        [SerializeField] private Vector3 rightHandIkLocalPosition;
        [SerializeField] private Vector3 rightHandIkLocalEulerAngles;
        [SerializeField] private bool useRightHandIkHint = true;
        [SerializeField] private Vector3 rightHandIkHintLocalPosition;
        [SerializeField] private Vector3 rightHandIkHintLocalEulerAngles;

        public bool TryGetBotEquippedItemPose(out BotEquippedItemPose pose)
        {
            pose = default;
            if (!overrideBotEquippedPose)
            {
                return false;
            }

            pose.equippedLocalPosition = equippedLocalPosition;
            pose.equippedLocalEulerAngles = equippedLocalEulerAngles;
            pose.useRightHandIkTarget = useRightHandIkTarget;
            pose.rightHandIkWeight = rightHandIkWeight;
            pose.rightHandIkLocalPosition = rightHandIkLocalPosition;
            pose.rightHandIkLocalEulerAngles = rightHandIkLocalEulerAngles;
            pose.useRightHandIkHint = useRightHandIkHint;
            pose.rightHandIkHintLocalPosition = rightHandIkHintLocalPosition;
            pose.rightHandIkHintLocalEulerAngles = rightHandIkHintLocalEulerAngles;
            return true;
        }
    }
}
