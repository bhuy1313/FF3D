using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

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

        [Header("Carry Pose")]
        [SerializeField] private bool enableCarryPose = true;
        [SerializeField] private float carryPoseWeightLerpSpeed = 12f;
        [SerializeField] private MultiAimConstraint carrySpineConstraint;
        [SerializeField] private MultiAimConstraint carrySpine1Constraint;
        [SerializeField] private MultiAimConstraint carrySpine2Constraint;
        [SerializeField] private TwoBoneIKConstraint carryRightHandIkConstraint;
        [SerializeField] private TwoBoneIKConstraint carryLeftHandIkConstraint;
        [SerializeField] private Transform carryRightHandIkTarget;
        [SerializeField] private Transform carryLeftHandIkTarget;

        [Header("Runtime Info")]
        [SerializeField] private bool hasEquippedItem;
        [SerializeField] private bool isUsingTool;
        [SerializeField] private bool isAimingTool;
        [SerializeField] private bool isCrouching;
        [SerializeField] private bool isMoving;
        [SerializeField] private bool isCarryingVictim;
        [SerializeField] private BotEquippedItemPoseKey resolvedPoseKey;

        private bool carryPoseWeightsCached;
        private float carrySpineConfiguredWeight;
        private float carrySpine1ConfiguredWeight;
        private float carrySpine2ConfiguredWeight;
        private float carryRightHandIkConfiguredWeight;
        private float carryLeftHandIkConfiguredWeight;
        private Vector3 defaultCarryRightHandIkTargetLocalPosition;
        private Quaternion defaultCarryRightHandIkTargetLocalRotation = Quaternion.identity;
        private Vector3 defaultCarryLeftHandIkTargetLocalPosition;
        private Quaternion defaultCarryLeftHandIkTargetLocalRotation = Quaternion.identity;
        private bool hasDefaultCarryRightHandIkTargetPose;
        private bool hasDefaultCarryLeftHandIkTargetPose;

        private void Awake()
        {
            AutoAssignReferences();
            CacheCarryPoseConfiguredWeights(true);
            CacheCarryPoseTargetDefaults(true);
            UpdateCarryPoseWeights(false, true);
            ResetCarryPoseTargets();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateCarryPoseWeights(false, true);
            ResetCarryPoseTargets();
        }

        private void LateUpdate()
        {
            UpdateEquippedPoseKey();
            UpdateCarryPoseState();
            UpdateCarryPoseTargets();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
            CacheCarryPoseConfiguredWeights(true);
            CacheCarryPoseTargetDefaults(true);
        }

        private void AutoAssignReferences()
        {
            inventorySystem ??= GetComponent<BotInventorySystem>();
            behaviorContext ??= GetComponent<BotBehaviorContext>();
            commandAgent ??= GetComponent<BotCommandAgent>();
            navMeshAgent ??= GetComponent<NavMeshAgent>();
            ResolveCarryPoseReferences();
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
            float extinguishStance = behaviorContext != null ? behaviorContext.ExtinguishStance : -1f;

            return BotEquippedPoseKeyUtility.Resolve(
                hasEquippedItem,
                isUsingTool,
                isAimingTool,
                isCrouching,
                isMoving,
                extinguishStance);
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

        private void ResolveCarryPoseReferences()
        {
            if (carrySpineConstraint != null &&
                carrySpine1Constraint != null &&
                carrySpine2Constraint != null &&
                carryRightHandIkConstraint != null &&
                carryLeftHandIkConstraint != null)
            {
                return;
            }

            MultiAimConstraint[] aimConstraints = GetComponentsInChildren<MultiAimConstraint>(true);
            for (int i = 0; i < aimConstraints.Length; i++)
            {
                MultiAimConstraint candidate = aimConstraints[i];
                if (candidate == null)
                {
                    continue;
                }

                if (carrySpineConstraint == null && candidate.gameObject.name == "CarrySpine")
                {
                    carrySpineConstraint = candidate;
                }
                else if (carrySpine1Constraint == null && candidate.gameObject.name == "CarrySpine1")
                {
                    carrySpine1Constraint = candidate;
                }
                else if (carrySpine2Constraint == null && candidate.gameObject.name == "CarrySpine2")
                {
                    carrySpine2Constraint = candidate;
                }
            }

            TwoBoneIKConstraint[] ikConstraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);
            for (int i = 0; i < ikConstraints.Length; i++)
            {
                TwoBoneIKConstraint candidate = ikConstraints[i];
                if (candidate == null)
                {
                    continue;
                }

                if (carryRightHandIkConstraint == null && candidate.gameObject.name == "CarryRightHandIK")
                {
                    carryRightHandIkConstraint = candidate;
                }
                else if (carryLeftHandIkConstraint == null && candidate.gameObject.name == "CarryLeftHandIK")
                {
                    carryLeftHandIkConstraint = candidate;
                }
            }
        }

        private void CacheCarryPoseConfiguredWeights(bool force)
        {
            if (!force && carryPoseWeightsCached)
            {
                return;
            }

            carrySpineConfiguredWeight = carrySpineConstraint != null ? Mathf.Clamp01(carrySpineConstraint.weight) : 0f;
            carrySpine1ConfiguredWeight = carrySpine1Constraint != null ? Mathf.Clamp01(carrySpine1Constraint.weight) : 0f;
            carrySpine2ConfiguredWeight = carrySpine2Constraint != null ? Mathf.Clamp01(carrySpine2Constraint.weight) : 0f;
            carryRightHandIkConfiguredWeight = carryRightHandIkConstraint != null ? Mathf.Clamp01(carryRightHandIkConstraint.weight) : 0f;
            carryLeftHandIkConfiguredWeight = carryLeftHandIkConstraint != null ? Mathf.Clamp01(carryLeftHandIkConstraint.weight) : 0f;
            carryPoseWeightsCached = true;
        }

        private void CacheCarryPoseTargetDefaults(bool force)
        {
            if (!force && hasDefaultCarryRightHandIkTargetPose && hasDefaultCarryLeftHandIkTargetPose)
            {
                return;
            }

            if (carryRightHandIkTarget != null)
            {
                defaultCarryRightHandIkTargetLocalPosition = carryRightHandIkTarget.localPosition;
                defaultCarryRightHandIkTargetLocalRotation = carryRightHandIkTarget.localRotation;
                hasDefaultCarryRightHandIkTargetPose = true;
            }
            else
            {
                hasDefaultCarryRightHandIkTargetPose = false;
            }

            if (carryLeftHandIkTarget != null)
            {
                defaultCarryLeftHandIkTargetLocalPosition = carryLeftHandIkTarget.localPosition;
                defaultCarryLeftHandIkTargetLocalRotation = carryLeftHandIkTarget.localRotation;
                hasDefaultCarryLeftHandIkTargetPose = true;
            }
            else
            {
                hasDefaultCarryLeftHandIkTargetPose = false;
            }
        }

        private void UpdateCarryPoseState()
        {
            isCarryingVictim = ShouldUseCarryPose();
            UpdateCarryPoseWeights(isCarryingVictim, false);
        }

        private bool ShouldUseCarryPose()
        {
            if (!enableCarryPose || commandAgent == null)
            {
                return false;
            }

            IRescuableTarget rescueTarget = commandAgent.CurrentCarriedRescueTarget;
            return rescueTarget != null &&
                   rescueTarget.IsCarried &&
                   rescueTarget.ActiveRescuer == gameObject;
        }

        private void UpdateCarryPoseTargets()
        {
            if (!enableCarryPose)
            {
                ResetCarryPoseTargets();
                return;
            }

            IRescuableTarget rescueTarget = commandAgent != null ? commandAgent.CurrentCarriedRescueTarget : null;
            if (rescueTarget == null)
            {
                ResetCarryPoseTargets();
                return;
            }

            UpdateCarryRightHandTarget(rescueTarget.GetCarryRightHandHoldPoint());
            UpdateCarryLeftHandTarget(rescueTarget.GetCarryLeftHandHoldPoint());
        }

        private void ResetCarryPoseTargets()
        {
            ResetCarryRightHandTargetPose();
            ResetCarryLeftHandTargetPose();
        }

        private void UpdateCarryRightHandTarget(Transform holdPoint)
        {
            if (holdPoint != null)
            {
                MatchCarryRightHandTargetTo(holdPoint);
                return;
            }

            ResetCarryRightHandTargetPose();
        }

        private void UpdateCarryLeftHandTarget(Transform holdPoint)
        {
            if (holdPoint != null)
            {
                MatchCarryLeftHandTargetTo(holdPoint);
                return;
            }

            ResetCarryLeftHandTargetPose();
        }

        private void ResetCarryRightHandTargetPose()
        {
            if (!hasDefaultCarryRightHandIkTargetPose || carryRightHandIkTarget == null)
            {
                return;
            }

            carryRightHandIkTarget.localPosition = defaultCarryRightHandIkTargetLocalPosition;
            carryRightHandIkTarget.localRotation = defaultCarryRightHandIkTargetLocalRotation;
        }

        private void ResetCarryLeftHandTargetPose()
        {
            if (!hasDefaultCarryLeftHandIkTargetPose || carryLeftHandIkTarget == null)
            {
                return;
            }

            carryLeftHandIkTarget.localPosition = defaultCarryLeftHandIkTargetLocalPosition;
            carryLeftHandIkTarget.localRotation = defaultCarryLeftHandIkTargetLocalRotation;
        }

        private void MatchCarryRightHandTargetTo(Transform holdPoint)
        {
            if (carryRightHandIkTarget == null || holdPoint == null)
            {
                return;
            }

            carryRightHandIkTarget.position = holdPoint.position;
        }

        private void MatchCarryLeftHandTargetTo(Transform holdPoint)
        {
            if (carryLeftHandIkTarget == null || holdPoint == null)
            {
                return;
            }

            carryLeftHandIkTarget.position = holdPoint.position;
        }

        private void UpdateCarryPoseWeights(bool active, bool instant)
        {
            CacheCarryPoseConfiguredWeights(false);

            ApplyCarryConstraintWeight(carrySpineConstraint, active ? carrySpineConfiguredWeight : 0f, instant);
            ApplyCarryConstraintWeight(carrySpine1Constraint, active ? carrySpine1ConfiguredWeight : 0f, instant);
            ApplyCarryConstraintWeight(carrySpine2Constraint, active ? carrySpine2ConfiguredWeight : 0f, instant);
            ApplyCarryConstraintWeight(carryRightHandIkConstraint, active ? carryRightHandIkConfiguredWeight : 0f, instant);
            ApplyCarryConstraintWeight(carryLeftHandIkConstraint, active ? carryLeftHandIkConfiguredWeight : 0f, instant);
        }

        private void ApplyCarryConstraintWeight(MultiAimConstraint constraint, float targetWeight, bool instant)
        {
            if (constraint == null)
            {
                return;
            }

            constraint.weight = ResolveCarryConstraintWeight(constraint.weight, targetWeight, instant);
        }

        private void ApplyCarryConstraintWeight(TwoBoneIKConstraint constraint, float targetWeight, bool instant)
        {
            if (constraint == null)
            {
                return;
            }

            constraint.weight = ResolveCarryConstraintWeight(constraint.weight, targetWeight, instant);
        }

        private float ResolveCarryConstraintWeight(float currentWeight, float targetWeight, bool instant)
        {
            targetWeight = Mathf.Clamp01(targetWeight);
            if (!Application.isPlaying || instant || carryPoseWeightLerpSpeed <= 0f)
            {
                return targetWeight;
            }

            float blend = 1f - Mathf.Exp(-carryPoseWeightLerpSpeed * Time.deltaTime);
            return Mathf.Lerp(currentWeight, targetWeight, blend);
        }
    }
}
