using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonAnimatorParameterDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private FPSInventorySystem inventorySystem;
        [Tooltip("Velocity is converted into this local space before being sent to the animator.")]
        [SerializeField] private Transform velocityReference;

        [Header("Thresholds")]
        [SerializeField] private float movingThreshold = 0.1f;
        [SerializeField] private float directionalMoveThreshold = 0.05f;
        [SerializeField] private float jumpVerticalVelocityThreshold = 0.1f;
        [SerializeField] private float fallVerticalVelocityThreshold = -0.1f;
        [SerializeField] private float parameterDampTime = 0.1f;

        [Header("Float Parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string horizontalSpeedParameter = "HorizontalSpeed";
        [SerializeField] private string velocityXParameter = "VelocityX";
        [SerializeField] private string velocityYParameter = "VelocityY";
        [SerializeField] private string velocityZParameter = "VelocityZ";
        [SerializeField] private string moveXParameter = "MoveX";
        [SerializeField] private string moveYParameter = "MoveY";
        [SerializeField] private string moveForwardParameter = "MoveForward";
        [SerializeField] private string moveRightParameter = "MoveRight";
        [SerializeField] private string upwardSpeedParameter = "UpwardSpeed";
        [SerializeField] private string downwardSpeedParameter = "DownwardSpeed";
        [SerializeField] private string movementBurdenParameter = "MovementBurden";
        [SerializeField] private string fallDistanceParameter = "FallDistance";
        [SerializeField] private string landingSpeedParameter = "LandingSpeed";

        [Header("Bool Parameters")]
        [SerializeField] private string isGroundedParameter = "IsGrounded";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string isIdleParameter = "IsIdle";
        [SerializeField] private string isStrafingParameter = "IsStrafing";
        [SerializeField] private string isMovingForwardParameter = "IsMovingForward";
        [SerializeField] private string isMovingBackwardParameter = "IsMovingBackward";
        [SerializeField] private string isMovingRightParameter = "IsMovingRight";
        [SerializeField] private string isMovingLeftParameter = "IsMovingLeft";
        [SerializeField] private string isSprintingParameter = "IsSprinting";
        [SerializeField] private string isCrouchingParameter = "IsCrouching";
        [SerializeField] private string isJumpingParameter = "IsJumping";
        [SerializeField] private string isFallingParameter = "IsFalling";
        [SerializeField] private string isClimbingParameter = "IsClimbing";
        [SerializeField] private string isHoldingParameter = "IsHolding";
        [SerializeField] private string hasItemParameter = "HasItem";

        [Header("Int Parameters")]
        [SerializeField] private string itemCountParameter = "ItemCount";
        [SerializeField] private string heldItemNameHashParameter = "HeldItemNameHash";
        [SerializeField] private string heldItemTypeHashParameter = "HeldItemTypeHash";

        [Header("Holding Parameter Prefixes")]
        [SerializeField] private string holdingNamePrefix = "IsHolding_";
        [SerializeField] private string holdingTypePrefix = "IsHoldingType_";

        [Header("Runtime Debug")]
        [SerializeField] private Vector3 worldVelocity;
        [SerializeField] private Vector3 localVelocity;
        [SerializeField] private float speed;
        [SerializeField] private float horizontalSpeed;
        [SerializeField] private float upwardSpeed;
        [SerializeField] private float downwardSpeed;
        [SerializeField] private bool isGrounded;
        [SerializeField] private bool isMoving;
        [SerializeField] private bool isIdle;
        [SerializeField] private bool isStrafing;
        [SerializeField] private bool isMovingForward;
        [SerializeField] private bool isMovingBackward;
        [SerializeField] private bool isMovingRight;
        [SerializeField] private bool isMovingLeft;
        [SerializeField] private bool isSprinting;
        [SerializeField] private bool isCrouching;
        [SerializeField] private bool isJumping;
        [SerializeField] private bool isFalling;
        [SerializeField] private bool isClimbing;
        [SerializeField] private bool isHolding;
        [SerializeField] private int itemCount;
        [SerializeField] private string heldItemName;
        [SerializeField] private string heldItemTypeName;

        private readonly Dictionary<string, int> parameterHashes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> dynamicHoldingParameterHashes = new Dictionary<string, int>();
        private readonly HashSet<int> activeDynamicHoldingHashes = new HashSet<int>();
        private readonly List<int> dynamicHoldingHashesToDisable = new List<int>();

        public Vector3 WorldVelocity => worldVelocity;
        public Vector3 LocalVelocity => localVelocity;
        public float Speed => speed;
        public float HorizontalSpeed => horizontalSpeed;

        private void Reset()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void Awake()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void LateUpdate()
        {
            UpdateVelocityState();
            UpdateAnimationState();
            PushAnimatorParameters();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void AutoAssignReferences()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (inventorySystem == null)
            {
                inventorySystem = GetComponent<FPSInventorySystem>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (velocityReference == null)
            {
                velocityReference = transform;
            }
        }

        private void CacheParameterHashes()
        {
            parameterHashes.Clear();

            CacheHash(speedParameter);
            CacheHash(horizontalSpeedParameter);
            CacheHash(velocityXParameter);
            CacheHash(velocityYParameter);
            CacheHash(velocityZParameter);
            CacheHash(moveXParameter);
            CacheHash(moveYParameter);
            CacheHash(moveForwardParameter);
            CacheHash(moveRightParameter);
            CacheHash(upwardSpeedParameter);
            CacheHash(downwardSpeedParameter);
            CacheHash(movementBurdenParameter);
            CacheHash(fallDistanceParameter);
            CacheHash(landingSpeedParameter);

            CacheHash(isGroundedParameter);
            CacheHash(isMovingParameter);
            CacheHash(isIdleParameter);
            CacheHash(isStrafingParameter);
            CacheHash(isMovingForwardParameter);
            CacheHash(isMovingBackwardParameter);
            CacheHash(isMovingRightParameter);
            CacheHash(isMovingLeftParameter);
            CacheHash(isSprintingParameter);
            CacheHash(isCrouchingParameter);
            CacheHash(isJumpingParameter);
            CacheHash(isFallingParameter);
            CacheHash(isClimbingParameter);
            CacheHash(isHoldingParameter);
            CacheHash(hasItemParameter);

            CacheHash(itemCountParameter);
            CacheHash(heldItemNameHashParameter);
            CacheHash(heldItemTypeHashParameter);
        }

        private void UpdateVelocityState()
        {
            if (characterController == null)
            {
                worldVelocity = Vector3.zero;
                localVelocity = Vector3.zero;
                speed = 0f;
                horizontalSpeed = 0f;
                upwardSpeed = 0f;
                downwardSpeed = 0f;
                return;
            }

            worldVelocity = characterController.velocity;

            Transform reference = velocityReference != null ? velocityReference : transform;
            localVelocity = reference.InverseTransformDirection(worldVelocity);
            speed = worldVelocity.magnitude;
            horizontalSpeed = new Vector3(worldVelocity.x, 0f, worldVelocity.z).magnitude;
            upwardSpeed = Mathf.Max(0f, worldVelocity.y);
            downwardSpeed = Mathf.Max(0f, -worldVelocity.y);
        }

        private void UpdateAnimationState()
        {
            GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;

            isGrounded = firstPersonController != null ? firstPersonController.Grounded : characterController != null && characterController.isGrounded;
            isCrouching = firstPersonController != null && firstPersonController.IsCrouching;
            isClimbing = firstPersonController != null && firstPersonController.IsClimbing;
            isSprinting = firstPersonController != null && firstPersonController.WantsSprint && horizontalSpeed > movingThreshold;
            isHolding = heldObject != null;
            itemCount = inventorySystem != null ? inventorySystem.ItemCount : 0;

            isMoving = horizontalSpeed > movingThreshold;
            isIdle = !isMoving && isGrounded && !isClimbing;
            isStrafing = Mathf.Abs(localVelocity.x) > directionalMoveThreshold;
            isMovingForward = localVelocity.z > directionalMoveThreshold;
            isMovingBackward = localVelocity.z < -directionalMoveThreshold;
            isMovingRight = localVelocity.x > directionalMoveThreshold;
            isMovingLeft = localVelocity.x < -directionalMoveThreshold;
            isJumping = !isGrounded && !isClimbing && worldVelocity.y > jumpVerticalVelocityThreshold;
            isFalling = !isGrounded && !isClimbing && worldVelocity.y < fallVerticalVelocityThreshold;

            heldItemName = isHolding ? heldObject.name : string.Empty;
            heldItemTypeName = ResolveHeldItemTypeName(heldObject);
        }

        private void PushAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            SetFloat(speedParameter, speed);
            SetFloat(horizontalSpeedParameter, horizontalSpeed);
            SetFloat(velocityXParameter, localVelocity.x);
            SetFloat(velocityYParameter, localVelocity.y);
            SetFloat(velocityZParameter, localVelocity.z);
            SetFloat(moveXParameter, localVelocity.x);
            SetFloat(moveYParameter, localVelocity.z);
            SetFloat(moveForwardParameter, Mathf.Max(0f, localVelocity.z));
            SetFloat(moveRightParameter, localVelocity.x);
            SetFloat(upwardSpeedParameter, upwardSpeed);
            SetFloat(downwardSpeedParameter, downwardSpeed);
            SetFloat(movementBurdenParameter, firstPersonController != null ? firstPersonController.CurrentMovementBurdenKg : 0f);
            SetFloat(fallDistanceParameter, firstPersonController != null ? firstPersonController.CurrentTrackedFallDistance : 0f);
            SetFloat(landingSpeedParameter, firstPersonController != null ? firstPersonController.LastResolvedLandingSpeed : 0f);

            SetBool(isGroundedParameter, isGrounded);
            SetBool(isMovingParameter, isMoving);
            SetBool(isIdleParameter, isIdle);
            SetBool(isStrafingParameter, isStrafing);
            SetBool(isMovingForwardParameter, isMovingForward);
            SetBool(isMovingBackwardParameter, isMovingBackward);
            SetBool(isMovingRightParameter, isMovingRight);
            SetBool(isMovingLeftParameter, isMovingLeft);
            SetBool(isSprintingParameter, isSprinting);
            SetBool(isCrouchingParameter, isCrouching);
            SetBool(isJumpingParameter, isJumping);
            SetBool(isFallingParameter, isFalling);
            SetBool(isClimbingParameter, isClimbing);
            SetBool(isHoldingParameter, isHolding);
            SetBool(hasItemParameter, itemCount > 0);

            SetInt(itemCountParameter, itemCount);
            SetInt(heldItemNameHashParameter, string.IsNullOrWhiteSpace(heldItemName) ? 0 : Animator.StringToHash(heldItemName));
            SetInt(heldItemTypeHashParameter, string.IsNullOrWhiteSpace(heldItemTypeName) ? 0 : Animator.StringToHash(heldItemTypeName));

            UpdateDynamicHoldingParameters();
        }

        private void UpdateDynamicHoldingParameters()
        {
            dynamicHoldingHashesToDisable.Clear();
            foreach (int hash in activeDynamicHoldingHashes)
            {
                dynamicHoldingHashesToDisable.Add(hash);
            }

            activeDynamicHoldingHashes.Clear();

            if (!isHolding)
            {
                DisableDynamicHoldingParameters();
                return;
            }

            EnableDynamicHoldingParameter(holdingNamePrefix, heldItemName);
            EnableDynamicHoldingParameter(holdingTypePrefix, heldItemTypeName);
            DisableUnusedDynamicHoldingParameters();
        }

        private void EnableDynamicHoldingParameter(string prefix, string value)
        {
            string parameterName = BuildHoldingParameterName(prefix, value);
            if (string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            int parameterHash = GetDynamicHoldingHash(parameterName);
            if (!HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            animator.SetBool(parameterHash, true);
            activeDynamicHoldingHashes.Add(parameterHash);
            dynamicHoldingHashesToDisable.Remove(parameterHash);
        }

        private void DisableDynamicHoldingParameters()
        {
            DisableUnusedDynamicHoldingParameters();
        }

        private void DisableUnusedDynamicHoldingParameters()
        {
            for (int i = 0; i < dynamicHoldingHashesToDisable.Count; i++)
            {
                animator.SetBool(dynamicHoldingHashesToDisable[i], false);
            }

            dynamicHoldingHashesToDisable.Clear();
        }

        private int GetDynamicHoldingHash(string parameterName)
        {
            if (dynamicHoldingParameterHashes.TryGetValue(parameterName, out int hash))
            {
                return hash;
            }

            hash = Animator.StringToHash(parameterName);
            dynamicHoldingParameterHashes[parameterName] = hash;
            return hash;
        }

        private void SetFloat(string parameterName, float value)
        {
            if (!TryGetHash(parameterName, out int parameterHash) ||
                !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Float))
            {
                return;
            }

            animator.SetFloat(parameterHash, value, parameterDampTime, Time.deltaTime);
        }

        private void SetBool(string parameterName, bool value)
        {
            if (!TryGetHash(parameterName, out int parameterHash) ||
                !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            animator.SetBool(parameterHash, value);
        }

        private void SetInt(string parameterName, int value)
        {
            if (!TryGetHash(parameterName, out int parameterHash) ||
                !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Int))
            {
                return;
            }

            animator.SetInteger(parameterHash, value);
        }

        private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType type)
        {
            if (animator == null || parameterHash == 0)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == parameterHash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheHash(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            parameterHashes[parameterName] = Animator.StringToHash(parameterName);
        }

        private bool TryGetHash(string parameterName, out int parameterHash)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterHash = 0;
                return false;
            }

            return parameterHashes.TryGetValue(parameterName, out parameterHash) && parameterHash != 0;
        }

        private static string ResolveHeldItemTypeName(GameObject heldObject)
        {
            if (heldObject == null)
            {
                return string.Empty;
            }

            MonoBehaviour[] components = heldObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null || component is PlayerEquippedItemPoseProfile)
                {
                    continue;
                }

                return component.GetType().Name;
            }

            return string.Empty;
        }

        private static string BuildHoldingParameterName(string prefix, string value)
        {
            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return prefix + SanitizeParameterToken(value);
        }

        private static string SanitizeParameterToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    buffer[count++] = c;
                }
            }

            return count > 0 ? new string(buffer, 0, count) : string.Empty;
        }
    }
}
