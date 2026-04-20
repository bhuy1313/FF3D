using UnityEngine;

namespace StarterAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FirstPersonController firstPersonController;

        [Header("Audio")]
        [SerializeField] private AudioId walkingAudioId = AudioId.DefaultWalking;
        [SerializeField] private float stopFadeDuration = 0.08f;
        [SerializeField] private Vector3 sourceLocalOffset = new Vector3(0f, -0.9f, 0f);

        [Header("Detection")]
        [SerializeField] private float movementThreshold = 0.2f;
        [SerializeField] private float movementStopThreshold = 0.12f;
        [SerializeField] private float startDelay = 0.1f;
        [SerializeField] private float stopGraceTime = 0.12f;

        [Header("Loop Tuning")]
        [SerializeField] private float minLoopPitch = 0.96f;
        [SerializeField] private float maxLoopPitch = 1.05f;

        private AudioSource walkingLoopSource;
        private float startTimer;
        private float stopTimer;
        private bool isWalkingEligible;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
            movementThreshold = Mathf.Max(0.01f, movementThreshold);
            movementStopThreshold = Mathf.Clamp(movementStopThreshold, 0.01f, movementThreshold);
            stopFadeDuration = Mathf.Max(0f, stopFadeDuration);
            startDelay = Mathf.Max(0f, startDelay);
            stopGraceTime = Mathf.Max(0f, stopGraceTime);
            minLoopPitch = Mathf.Clamp(minLoopPitch, 0.01f, 3f);
            maxLoopPitch = Mathf.Clamp(maxLoopPitch, minLoopPitch, 3f);
        }

        private void Update()
        {
            bool shouldWalk = TryGetStepContext(out float horizontalSpeed);

            if (shouldWalk)
            {
                stopTimer = 0f;
                startTimer += Time.deltaTime;
                isWalkingEligible = true;

                if (walkingLoopSource == null && startTimer >= startDelay)
                {
                    EnsureWalkingLoop();
                }

                UpdateWalkingLoop(horizontalSpeed);
                return;
            }

            startTimer = 0f;
            isWalkingEligible = false;

            if (walkingLoopSource == null)
            {
                return;
            }

            stopTimer += Time.deltaTime;
            if (stopTimer >= stopGraceTime)
            {
                StopWalkingLoop();
            }
        }

        private void AutoAssignReferences()
        {
            characterController ??= GetComponent<CharacterController>();
            firstPersonController ??= GetComponent<FirstPersonController>();
        }

        private bool TryGetStepContext(out float horizontalSpeed)
        {
            horizontalSpeed = 0f;

            if (characterController == null)
            {
                return false;
            }

            if (firstPersonController != null)
            {
                if (firstPersonController.IsClimbing || firstPersonController.IsCrouching || firstPersonController.WantsSprint)
                {
                    return false;
                }
            }

            bool grounded = firstPersonController != null
                ? firstPersonController.Grounded
                : characterController.isGrounded;
            if (!grounded)
            {
                return false;
            }

            Vector3 velocity = characterController.velocity;
            horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float requiredThreshold = isWalkingEligible ? movementStopThreshold : movementThreshold;
            return horizontalSpeed >= requiredThreshold;
        }

        private void EnsureWalkingLoop()
        {
            if (walkingLoopSource != null)
            {
                return;
            }

            if (walkingAudioId == AudioId.None)
            {
                return;
            }

            walkingLoopSource = AudioService.PlayLoop(walkingAudioId, transform);
            if (walkingLoopSource != null)
            {
                walkingLoopSource.transform.localPosition = sourceLocalOffset;
            }
        }

        private void UpdateWalkingLoop(float horizontalSpeed)
        {
            if (walkingLoopSource == null)
            {
                return;
            }

            float referenceSpeed = 4f;
            if (firstPersonController != null)
            {
                referenceSpeed = Mathf.Max(firstPersonController.MoveSpeed, movementThreshold + 0.01f);
            }

            float normalizedSpeed = Mathf.InverseLerp(movementStopThreshold, referenceSpeed, horizontalSpeed);
            float targetPitch = Mathf.Lerp(minLoopPitch, maxLoopPitch, normalizedSpeed);
            walkingLoopSource.pitch = Mathf.Lerp(walkingLoopSource.pitch, targetPitch, Time.deltaTime * 8f);
            walkingLoopSource.transform.localPosition = sourceLocalOffset;
        }

        private void StopWalkingLoop()
        {
            if (walkingLoopSource == null)
            {
                return;
            }

            AudioService.Stop(walkingLoopSource, stopFadeDuration);
            walkingLoopSource = null;
            stopTimer = 0f;
        }

        private void OnDisable()
        {
            startTimer = 0f;
            stopTimer = 0f;
            isWalkingEligible = false;
            StopWalkingLoop();
        }

        private void OnDestroy()
        {
            StopWalkingLoop();
        }
    }
}
