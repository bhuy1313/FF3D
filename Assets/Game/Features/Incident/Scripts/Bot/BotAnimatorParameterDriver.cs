using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace TrueJourney.BotBehavior
{
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(BotCommandAgent))]
    public sealed class BotAnimatorParameterDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private BotCommandAgent commandAgent;
        [SerializeField] private BotBehaviorContext behaviorContext;
        [SerializeField] private BotInventorySystem inventorySystem;
        [Tooltip("Velocity is converted into this local space before being sent to the animator.")]
        [SerializeField] private Transform velocityReference;

        [Header("Thresholds")]
        [SerializeField] private float movingThreshold = 0.1f;
        [SerializeField] private float directionalMoveThreshold = 0.05f;
        [SerializeField] private float parameterDampTime = 0.1f;

        [Header("Bool Parameters")]
        [SerializeField] private string isGroundedParameter = "IsGrounded";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string isIdleParameter = "IsIdle";
        [SerializeField] private string isStrafingParameter = "IsStrafing";
        [SerializeField] private string isMovingForwardParameter = "IsMovingForward";
        [SerializeField] private string isMovingBackwardParameter = "IsMovingBackward";
        [SerializeField] private string isMovingRightParameter = "IsMovingRight";
        [SerializeField] private string isMovingLeftParameter = "IsMovingLeft";
        [SerializeField] private string isCrouchingParameter = "IsCrouching";
        [SerializeField] private string isHoldingParameter = "IsHolding";
        [SerializeField] private string hasItemParameter = "HasItem";
        [SerializeField] private string isAimingToolParameter = "IsAimingTool";
        [SerializeField] private string isUsingToolParameter = "IsUsingTool";
        [SerializeField] private string isCarryingVictimParameter = "IsCarryingVictim";
        [SerializeField] private string hasCommandParameter = "HasCommand";
        [SerializeField] private string isFollowingParameter = "IsFollowing";
        [SerializeField] private string isExtinguishingParameter = "IsExtinguishing";
        [SerializeField] private string isRescuingParameter = "IsRescuing";

        [Header("Int Parameters")]
        [SerializeField] private string itemCountParameter = "ItemCount";
        [SerializeField] private string heldItemNameHashParameter = "HeldItemNameHash";
        [SerializeField] private string heldItemTypeHashParameter = "HeldItemTypeHash";
        [SerializeField] private string activeCommandTypeParameter = "ActiveCommandType";

        [Header("Dynamic Parameter Prefixes")]
        [SerializeField] private string holdingNamePrefix = "IsHolding_";
        [SerializeField] private string holdingItemNamePrefix = "IsHoldingItem_";
        [SerializeField] private string holdingTypePrefix = "IsHoldingType_";
        [SerializeField] private string commandTypePrefix = "IsCommand_";

        [Header("Runtime Debug")]
        [SerializeField] private Vector3 worldVelocity;
        [SerializeField] private Vector3 localVelocity;
        [SerializeField] private Vector3 desiredWorldVelocity;
        [SerializeField] private Vector3 desiredLocalVelocity;
        [SerializeField] private float speed;
        [SerializeField] private float horizontalSpeed;
        [SerializeField] private float desiredSpeed;
        [SerializeField] private float turnAmount;
        [SerializeField] private float movementBurden;
        [SerializeField] private float extinguishStance;
        [SerializeField] private bool isGrounded;
        [SerializeField] private bool isMoving;
        [SerializeField] private bool isIdle;
        [SerializeField] private bool isStrafing;
        [SerializeField] private bool isMovingForward;
        [SerializeField] private bool isMovingBackward;
        [SerializeField] private bool isMovingRight;
        [SerializeField] private bool isMovingLeft;
        [SerializeField] private bool isCrouching;
        [SerializeField] private bool isHolding;
        [SerializeField] private bool isAimingTool;
        [SerializeField] private bool isUsingTool;
        [SerializeField] private bool isCarryingVictim;
        [SerializeField] private bool hasCommand;
        [SerializeField] private BotCommandType activeCommandType;
        [SerializeField] private int itemCount;
        [SerializeField] private string heldItemName;
        [SerializeField] private string heldItemDisplayName;
        [SerializeField] private string heldItemTypeName;

        private readonly Dictionary<string, int> parameterHashes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> dynamicParameterHashes = new Dictionary<string, int>();
        private readonly HashSet<int> floatParameterHashes = new HashSet<int>();
        private readonly HashSet<int> boolParameterHashes = new HashSet<int>();
        private readonly HashSet<int> intParameterHashes = new HashSet<int>();
        private readonly HashSet<int> activeDynamicHashes = new HashSet<int>();
        private readonly List<int> dynamicHashesToDisable = new List<int>();
        private readonly Dictionary<int, bool> boolParameterValues = new Dictionary<int, bool>();
        private readonly Dictionary<int, int> intParameterValues = new Dictionary<int, int>();

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
            UpdateMovementState();
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
            navMeshAgent ??= GetComponent<NavMeshAgent>();
            commandAgent ??= GetComponent<BotCommandAgent>();
            behaviorContext ??= GetComponent<BotBehaviorContext>();
            inventorySystem ??= GetComponent<BotInventorySystem>();

            if (animator == null)
            {
                Animator[] animators = GetComponentsInChildren<Animator>(true);
                Animator fallbackAnimator = null;
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator candidate = animators[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    fallbackAnimator ??= candidate;
                    if (candidate.runtimeAnimatorController != null)
                    {
                        animator = candidate;
                        break;
                    }
                }

                animator ??= fallbackAnimator;
            }

            velocityReference ??= transform;
        }

        private void CacheParameterHashes()
        {
            parameterHashes.Clear();

            CacheHash(isGroundedParameter);
            CacheHash(isMovingParameter);
            CacheHash(isIdleParameter);
            CacheHash(isStrafingParameter);
            CacheHash(isMovingForwardParameter);
            CacheHash(isMovingBackwardParameter);
            CacheHash(isMovingRightParameter);
            CacheHash(isMovingLeftParameter);
            CacheHash(isCrouchingParameter);
            CacheHash(isHoldingParameter);
            CacheHash(hasItemParameter);
            CacheHash(isAimingToolParameter);
            CacheHash(isUsingToolParameter);
            CacheHash(isCarryingVictimParameter);
            CacheHash(hasCommandParameter);
            CacheHash(isFollowingParameter);
            CacheHash(isExtinguishingParameter);
            CacheHash(isRescuingParameter);

            CacheHash(itemCountParameter);
            CacheHash(heldItemNameHashParameter);
            CacheHash(heldItemTypeHashParameter);
            CacheHash(activeCommandTypeParameter);

            CacheAnimatorParameters();
        }

        private void CacheAnimatorParameters()
        {
            floatParameterHashes.Clear();
            boolParameterHashes.Clear();
            intParameterHashes.Clear();
            boolParameterValues.Clear();
            intParameterValues.Clear();

            if (animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        floatParameterHashes.Add(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        boolParameterHashes.Add(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Int:
                        intParameterHashes.Add(parameter.nameHash);
                        break;
                }
            }
        }

        private void UpdateMovementState()
        {
            if (navMeshAgent == null || !navMeshAgent.enabled)
            {
                worldVelocity = Vector3.zero;
                localVelocity = Vector3.zero;
                desiredWorldVelocity = Vector3.zero;
                desiredLocalVelocity = Vector3.zero;
                speed = 0f;
                horizontalSpeed = 0f;
                desiredSpeed = 0f;
                turnAmount = 0f;
                return;
            }

            worldVelocity = navMeshAgent.velocity;
            desiredWorldVelocity = navMeshAgent.desiredVelocity;

            Transform reference = velocityReference != null ? velocityReference : transform;
            localVelocity = reference.InverseTransformDirection(worldVelocity);
            desiredLocalVelocity = reference.InverseTransformDirection(desiredWorldVelocity);

            speed = worldVelocity.magnitude;
            horizontalSpeed = new Vector3(worldVelocity.x, 0f, worldVelocity.z).magnitude;
            desiredSpeed = new Vector3(desiredWorldVelocity.x, 0f, desiredWorldVelocity.z).magnitude;
            turnAmount = Mathf.Clamp(localVelocity.x, -1f, 1f);
        }

        private void UpdateAnimationState()
        {
            isGrounded = navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
            isMoving = BotMovementAnimationUtility.ShouldUseMoveAnimation(
                navMeshAgent != null && navMeshAgent.enabled,
                navMeshAgent != null && navMeshAgent.isOnNavMesh,
                navMeshAgent != null && navMeshAgent.isStopped,
                navMeshAgent != null && navMeshAgent.pathPending,
                navMeshAgent != null && navMeshAgent.hasPath,
                navMeshAgent != null ? navMeshAgent.remainingDistance : 0f,
                navMeshAgent != null ? navMeshAgent.stoppingDistance : 0f,
                navMeshAgent != null ? navMeshAgent.velocity : Vector3.zero,
                navMeshAgent != null ? navMeshAgent.desiredVelocity : Vector3.zero,
                movingThreshold);

            isIdle = !isMoving && isGrounded;
            isStrafing = Mathf.Abs(localVelocity.x) > directionalMoveThreshold;
            isMovingForward = localVelocity.z > directionalMoveThreshold;
            isMovingBackward = localVelocity.z < -directionalMoveThreshold;
            isMovingRight = localVelocity.x > directionalMoveThreshold;
            isMovingLeft = localVelocity.x < -directionalMoveThreshold;

            isCrouching = behaviorContext != null && behaviorContext.IsCrouchAnimationActive;
            isHolding = inventorySystem != null && inventorySystem.HasItem;
            itemCount = inventorySystem != null ? inventorySystem.ItemCount : 0;
            isAimingTool = commandAgent != null && commandAgent.IsAimingEquippedItemPose;
            isUsingTool = commandAgent != null && commandAgent.IsUsingEquippedItemPose;
            isCarryingVictim = commandAgent != null && commandAgent.IsCarryingRescueTarget;
            movementBurden = commandAgent != null ? commandAgent.CurrentCarryWeightKg : 0f;
            extinguishStance = behaviorContext != null ? behaviorContext.ExtinguishStance : -1f;
            hasCommand = behaviorContext != null && behaviorContext.HasCommandIntent;
            activeCommandType = behaviorContext != null ? behaviorContext.ActiveCommandType : BotCommandType.None;

            ResolveHeldItemInfo(out heldItemName, out heldItemTypeName);
        }

        private void PushAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            SetBool(isGroundedParameter, isGrounded);
            SetBool(isMovingParameter, isMoving);
            SetBool(isIdleParameter, isIdle);
            SetBool(isStrafingParameter, isStrafing);
            SetBool(isMovingForwardParameter, isMovingForward);
            SetBool(isMovingBackwardParameter, isMovingBackward);
            SetBool(isMovingRightParameter, isMovingRight);
            SetBool(isMovingLeftParameter, isMovingLeft);
            SetBool(isCrouchingParameter, isCrouching);
            SetBool(isHoldingParameter, isHolding);
            SetBool(hasItemParameter, itemCount > 0);
            SetBool(isAimingToolParameter, isAimingTool);
            SetBool(isUsingToolParameter, isUsingTool);
            SetBool(isCarryingVictimParameter, isCarryingVictim);
            SetBool(hasCommandParameter, hasCommand);
            SetBool(isFollowingParameter, BotCommandTypeUtility.UsesFollowOrder(activeCommandType));
            SetBool(isExtinguishingParameter, activeCommandType == BotCommandType.Extinguish);
            SetBool(isRescuingParameter, activeCommandType == BotCommandType.Rescue);

            SetInt(itemCountParameter, itemCount);
            SetInt(heldItemNameHashParameter, string.IsNullOrWhiteSpace(heldItemName) ? 0 : Animator.StringToHash(heldItemName));
            SetInt(heldItemTypeHashParameter, string.IsNullOrWhiteSpace(heldItemTypeName) ? 0 : Animator.StringToHash(heldItemTypeName));
            SetInt(activeCommandTypeParameter, (int)activeCommandType);

            UpdateDynamicParameters();
        }

        private void UpdateDynamicParameters()
        {
            dynamicHashesToDisable.Clear();
            foreach (int hash in activeDynamicHashes)
            {
                dynamicHashesToDisable.Add(hash);
            }

            activeDynamicHashes.Clear();

            if (isHolding)
            {
                EnableDynamicBoolParameter(holdingNamePrefix, heldItemName);
                EnableDynamicBoolParameter(holdingItemNamePrefix, heldItemDisplayName);
                EnableDynamicBoolParameter(holdingTypePrefix, heldItemTypeName);
            }

            if (hasCommand && activeCommandType != BotCommandType.None)
            {
                EnableDynamicBoolParameter(commandTypePrefix, activeCommandType.ToString());
            }

            DisableUnusedDynamicParameters();
        }

        private void EnableDynamicBoolParameter(string prefix, string value)
        {
            string parameterName = BuildDynamicParameterName(prefix, value);
            if (string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            int parameterHash = GetDynamicHash(parameterName);
            if (!HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            SetBool(parameterHash, true);
            activeDynamicHashes.Add(parameterHash);
            dynamicHashesToDisable.Remove(parameterHash);
        }

        private void DisableUnusedDynamicParameters()
        {
            for (int i = 0; i < dynamicHashesToDisable.Count; i++)
            {
                SetBool(dynamicHashesToDisable[i], false);
            }

            dynamicHashesToDisable.Clear();
        }

        private int GetDynamicHash(string parameterName)
        {
            if (dynamicParameterHashes.TryGetValue(parameterName, out int hash))
            {
                return hash;
            }

            hash = Animator.StringToHash(parameterName);
            dynamicParameterHashes[parameterName] = hash;
            return hash;
        }

        private void ResolveHeldItemInfo(out string itemName, out string itemTypeName)
        {
            itemName = string.Empty;
            heldItemDisplayName = string.Empty;
            itemTypeName = string.Empty;

            IPickupable activeItem = inventorySystem != null ? inventorySystem.ActiveItem : null;
            if (activeItem?.Rigidbody == null)
            {
                return;
            }

            itemName = activeItem.Rigidbody.gameObject.name;
            heldItemDisplayName = ResolveItemDisplayName(activeItem.Rigidbody.gameObject);
            MonoBehaviour[] components = activeItem.Rigidbody.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component is not IPickupable)
                {
                    continue;
                }

                itemName = component.gameObject.name;
                heldItemDisplayName = ResolveItemDisplayName(component.gameObject);
                itemTypeName = component.GetType().Name;
                return;
            }

            itemTypeName = activeItem.GetType().Name;
        }

        private static string ResolveItemDisplayName(GameObject itemObject)
        {
            if (itemObject == null)
            {
                return string.Empty;
            }

            Component itemComponent = FindComponentInParents(itemObject.transform, TypeCache.ItemComponentTypeName);
            if (itemComponent != null &&
                itemComponent.GetType().GetProperty(TypeCache.ItemNamePropertyName, BindingFlags.Instance | BindingFlags.Public) is PropertyInfo itemNameProperty &&
                itemNameProperty.PropertyType == typeof(string))
            {
                string itemName = itemNameProperty.GetValue(itemComponent) as string;
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    return itemName;
                }
            }

            return itemObject.name;
        }

        private static Component FindComponentInParents(Transform start, string componentTypeName)
        {
            Transform current = start;
            while (current != null)
            {
                Component component = current.GetComponent(componentTypeName);
                if (component != null)
                {
                    return component;
                }

                current = current.parent;
            }

            return null;
        }

        private static class TypeCache
        {
            public const string ItemComponentTypeName = "Item";
            public const string ItemNamePropertyName = "ItemName";
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

            SetBool(parameterHash, value);
        }

        private void SetInt(string parameterName, int value)
        {
            if (!TryGetHash(parameterName, out int parameterHash) ||
                !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Int))
            {
                return;
            }

            if (intParameterValues.TryGetValue(parameterHash, out int currentValue) && currentValue == value)
            {
                return;
            }

            animator.SetInteger(parameterHash, value);
            intParameterValues[parameterHash] = value;
        }

        private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType type)
        {
            if (animator == null || parameterHash == 0)
            {
                return false;
            }

            switch (type)
            {
                case AnimatorControllerParameterType.Float:
                    return floatParameterHashes.Contains(parameterHash);
                case AnimatorControllerParameterType.Bool:
                    return boolParameterHashes.Contains(parameterHash);
                case AnimatorControllerParameterType.Int:
                    return intParameterHashes.Contains(parameterHash);
                default:
                    return false;
            }
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

        private void SetBool(int parameterHash, bool value)
        {
            if (boolParameterValues.TryGetValue(parameterHash, out bool currentValue) && currentValue == value)
            {
                return;
            }

            animator.SetBool(parameterHash, value);
            boolParameterValues[parameterHash] = value;
        }

        private static string BuildDynamicParameterName(string prefix, string value)
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
