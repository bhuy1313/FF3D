using UnityEngine;
using UnityEngine.AI;

namespace TrueJourney.BotBehavior
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BotInventorySystem))]
    public class BotEquippedItemPoseDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BotInventorySystem inventorySystem;
        [SerializeField] private BotBehaviorContext behaviorContext;
        [SerializeField] private BotCommandAgent commandAgent;
        [SerializeField] private NavMeshAgent navMeshAgent;

        [Header("Movement")]
        [SerializeField] private float fallbackMovementThreshold = 0.1f;

        [Header("Runtime Info")]
        [SerializeField] private bool hasEquippedItem;
        [SerializeField] private bool isUsingTool;
        [SerializeField] private bool isAimingTool;
        [SerializeField] private bool isCrouching;
        [SerializeField] private bool isMoving;
        [SerializeField] private BotEquippedItemPoseKey resolvedPoseKey;

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void LateUpdate()
        {
            UpdateEquippedPoseKey();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void AutoAssignReferences()
        {
            inventorySystem ??= GetComponent<BotInventorySystem>();
            behaviorContext ??= GetComponent<BotBehaviorContext>();
            commandAgent ??= GetComponent<BotCommandAgent>();
            navMeshAgent ??= GetComponent<NavMeshAgent>();
        }

        private void UpdateEquippedPoseKey()
        {
            if (inventorySystem == null)
            {
                return;
            }

            resolvedPoseKey = ResolveEquippedPoseKey();
            inventorySystem.SetEquippedPoseKey(resolvedPoseKey);
        }

        private BotEquippedItemPoseKey ResolveEquippedPoseKey()
        {
            hasEquippedItem = inventorySystem != null && inventorySystem.HasItem;
            isUsingTool = commandAgent != null && commandAgent.IsUsingEquippedItemPose;
            isAimingTool = commandAgent != null && commandAgent.IsAimingEquippedItemPose;
            isCrouching = behaviorContext != null && behaviorContext.IsCrouchAnimationActive;
            isMoving = ShouldUseMovePose();

            return BotEquippedPoseKeyUtility.Resolve(
                hasEquippedItem,
                isUsingTool,
                isAimingTool,
                isCrouching,
                isMoving);
        }

        private bool ShouldUseMovePose()
        {
            if (navMeshAgent == null)
            {
                return false;
            }

            return BotMovementAnimationUtility.ShouldUseMoveAnimation(
                navMeshAgent.enabled,
                navMeshAgent.isOnNavMesh,
                navMeshAgent.isStopped,
                navMeshAgent.pathPending,
                navMeshAgent.hasPath,
                navMeshAgent.remainingDistance,
                navMeshAgent.stoppingDistance,
                navMeshAgent.velocity,
                navMeshAgent.desiredVelocity,
                ResolveMovementThreshold());
        }

        private float ResolveMovementThreshold()
        {
            if (behaviorContext != null)
            {
                return behaviorContext.MovementAnimationThreshold;
            }

            return Mathf.Max(0f, fallbackMovementThreshold);
        }
    }
}
