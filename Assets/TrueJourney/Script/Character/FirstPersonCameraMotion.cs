using UnityEngine;

namespace StarterAssets
{
    public static class FirstPersonCameraMotion
    {
        public static Vector3 EvaluateBob(float timer, float horizontalAmplitude, float verticalAmplitude)
        {
            return new Vector3(
                Mathf.Cos(timer) * horizontalAmplitude,
                Mathf.Sin(timer * 2f) * verticalAmplitude,
                0f
            );
        }

        public static float EvaluateStrafeTilt(float strafeInput, float maxTilt, float motionScale)
        {
            return -Mathf.Clamp(strafeInput, -1f, 1f) * maxTilt * Mathf.Clamp01(motionScale);
        }

        public static float EvaluateAirborneVerticalOffset(
            float verticalVelocity,
            float upwardOffset,
            float downwardOffset,
            float maxRiseSpeed,
            float maxFallSpeed)
        {
            if (verticalVelocity > 0f)
            {
                float riseSpeed = Mathf.Max(0.001f, maxRiseSpeed);
                return Mathf.Lerp(0f, upwardOffset, Mathf.Clamp01(verticalVelocity / riseSpeed));
            }

            if (verticalVelocity < 0f)
            {
                float fallSpeed = Mathf.Max(0.001f, maxFallSpeed);
                return Mathf.Lerp(0f, -downwardOffset, Mathf.Clamp01(-verticalVelocity / fallSpeed));
            }

            return 0f;
        }
    }
}
