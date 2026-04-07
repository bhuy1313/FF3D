using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class BotBehaviorContext : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator animator;
    [SerializeField] private BotCommandAgent commandAgent;

    [Header("Orders")]
    [SerializeField] private bool useMoveOrdersAsBehaviorInput;
    [SerializeField] private float arrivalDistance = 0.35f;

    [Header("Patrol")]
    [SerializeField] private bool enablePatrolMovement = true;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitSeconds = 1.5f;

    [Header("Idle")]
    [SerializeField] private bool enableIdleTurning = true;
    [SerializeField] private float idleTurnSpeed = 90f;
    [SerializeField] private Vector2 idleTurnDurationRange = new Vector2(1.25f, 2.5f);
    [SerializeField] private Vector2 idlePauseDurationRange = new Vector2(0.4f, 1.2f);

    [Header("Animation")]
    [SerializeField] private bool driveMovementAnimation = true;
    [SerializeField] private string moveAnimationParameter = "IsMoving";
    [SerializeField] private float movementAnimationThreshold = 0.1f;
    [SerializeField] private bool movementAnimationActive;
    [SerializeField] private bool driveCrouchAnimation = true;
    [SerializeField] private string crouchAnimationParameter = "IsCrouching";
    [SerializeField] private string crouchAnimationLayer = "Base Layer";
    [SerializeField] private string crouchAnimationState = "Idle Crouching";
    [SerializeField] private string uncrouchAnimationState = "Breathing Idle";
    [SerializeField] private bool crouchAnimationActive;
    [SerializeField] private bool driveLoadedLowerBodyLayer = true;
    [SerializeField] private string loadedLowerBodyLayer = "Lower Body Layer";
    [SerializeField] private float loadedLowerBodyLayerWeightLerpSpeed = 10f;
    [SerializeField] private bool loadedLowerBodyOnlyWhileMoving = true;
    [SerializeField] private float loadedLowerBodyMinCarryWeight = 1f;
    [SerializeField] private bool loadedLowerBodyLayerActive;

    private readonly BotMoveOrderState moveOrderState = new BotMoveOrderState();
    private readonly BotExtinguishOrderState extinguishOrderState = new BotExtinguishOrderState();
    private readonly BotFollowOrderState followOrderState = new BotFollowOrderState();
    private readonly BotRescueOrderState rescueOrderState = new BotRescueOrderState();
    private int moveAnimationParameterHash;
    private int crouchAnimationParameterHash;
    private int crouchAnimationLayerIndex = -1;
    private int crouchAnimationStateHash;
    private int uncrouchAnimationStateHash;
    private int loadedLowerBodyLayerIndex = -1;

    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public bool UseMoveOrdersAsBehaviorInput => useMoveOrdersAsBehaviorInput;
    public bool HasMoveOrder => moveOrderState.HasMoveOrder;
    public bool HasExtinguishOrder => extinguishOrderState.HasExtinguishOrder;
    public bool HasFollowOrder => followOrderState.HasFollowOrder;
    public bool HasRescueOrder => rescueOrderState.HasRescueOrder;
    public float ArrivalDistance => Mathf.Max(0.05f, arrivalDistance);
    public float PatrolWaitSeconds => Mathf.Max(0f, patrolWaitSeconds);
    public bool PatrolMovementEnabled => enablePatrolMovement;
    public bool IdleTurningEnabled => enableIdleTurning;
    public float IdleTurnSpeed => Mathf.Max(0f, idleTurnSpeed);
    public Vector2 IdleTurnDurationRange => SanitizeRange(idleTurnDurationRange, 0.1f);
    public Vector2 IdlePauseDurationRange => SanitizeRange(idlePauseDurationRange, 0f);
    public int PatrolPointCount => patrolPoints != null ? patrolPoints.Length : 0;
    public bool HasConfiguredPatrolRoute => GetPatrolPointCount() > 0;
    public bool HasPatrolRoute => enablePatrolMovement && HasConfiguredPatrolRoute;
    public bool IsMovementAnimationActive => movementAnimationActive;
    public bool IsCrouchAnimationActive => crouchAnimationActive;
    public float MovementAnimationThreshold => movementAnimationThreshold;

    private void Awake()
    {
        AutoAssignReferences();
        CacheAnimationHashes();
    }

    private void LateUpdate()
    {
        UpdateMovementAnimation();
        UpdateLoadedLowerBodyLayer();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        CacheAnimationHashes();
    }

    public void SetUseMoveOrdersAsBehaviorInput(bool value)
    {
        useMoveOrdersAsBehaviorInput = value;
    }

    public void SetPatrolMovementEnabled(bool value)
    {
        enablePatrolMovement = value;
    }

    public void SetIdleTurningEnabled(bool value)
    {
        enableIdleTurning = value;
    }

    public void SetMoveOrder(Vector3 destination)
    {
        moveOrderState.SetDestination(destination);
    }

    public bool TryGetMoveOrder(out Vector3 destination)
    {
        return moveOrderState.TryGetDestination(out destination);
    }

    public void ClearMoveOrder()
    {
        moveOrderState.Clear();
    }

    public void SetExtinguishOrder(Vector3 destination)
    {
        extinguishOrderState.SetDestination(destination);
    }

    public void SetExtinguishOrder(Vector3 destination, Vector3 scanOrigin, BotExtinguishCommandMode mode)
    {
        extinguishOrderState.SetDestination(destination, scanOrigin, mode);
    }

    public bool TryGetExtinguishOrder(out Vector3 destination)
    {
        return extinguishOrderState.TryGetDestination(out destination);
    }

    public bool TryGetExtinguishOrder(out Vector3 destination, out Vector3 scanOrigin, out BotExtinguishCommandMode mode)
    {
        return extinguishOrderState.TryGetDestination(out destination, out scanOrigin, out mode);
    }

    public void ClearExtinguishOrder()
    {
        extinguishOrderState.Clear();
    }

    public void SetFollowOrder(BotFollowOrder order)
    {
        followOrderState.SetActive(order);
    }

    public void ClearFollowOrder()
    {
        followOrderState.Clear();
    }

    public bool TryGetFollowOrder(out BotFollowOrder order)
    {
        return followOrderState.TryGetOrder(out order);
    }

    public void SetRescueOrder(Vector3 destination)
    {
        rescueOrderState.SetDestination(destination);
    }

    public bool TryGetRescueOrder(out Vector3 destination)
    {
        return rescueOrderState.TryGetDestination(out destination);
    }

    public void ClearRescueOrder()
    {
        rescueOrderState.Clear();
    }

    public void ClearAllOrders()
    {
        moveOrderState.Clear();
        extinguishOrderState.Clear();
        followOrderState.Clear();
        rescueOrderState.Clear();
    }

    public void ClearOrdersExcept(BotCommandType commandType)
    {
        if (commandType != BotCommandType.Move)
        {
            moveOrderState.Clear();
        }

        if (commandType != BotCommandType.Extinguish)
        {
            extinguishOrderState.Clear();
        }

        if (commandType != BotCommandType.Follow)
        {
            followOrderState.Clear();
        }

        if (commandType != BotCommandType.Rescue)
        {
            rescueOrderState.Clear();
        }
    }

    public int GetPatrolPointCount()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    public bool TryGetPatrolPointPosition(int index, out Vector3 position)
    {
        position = default;
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        int resolvedIndex = 0;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
            {
                continue;
            }

            if (resolvedIndex == index)
            {
                position = point.position;
                return true;
            }

            resolvedIndex++;
        }

        return false;
    }

    public void SetCrouchAnimation(bool active)
    {
        crouchAnimationActive = active;
        ApplyCrouchAnimation();
    }

    private static Vector2 SanitizeRange(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, range.x);
        float max = Mathf.Max(min, range.y);
        return new Vector2(min, max);
    }

    private void AutoAssignReferences()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (commandAgent == null)
        {
            commandAgent = GetComponent<BotCommandAgent>();
        }

        if (HasAnimatorController())
        {
            return;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        Animator fallbackAnimator = animator;
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
                return;
            }
        }

        animator = fallbackAnimator;
    }

    private void CacheAnimationHashes()
    {
        moveAnimationParameterHash = ToHash(moveAnimationParameter);
        crouchAnimationParameterHash = ToHash(crouchAnimationParameter);
        crouchAnimationStateHash = ToHash(crouchAnimationState);
        uncrouchAnimationStateHash = ToHash(uncrouchAnimationState);
        RefreshCrouchAnimationLayerIndex();
        RefreshLoadedLowerBodyLayerIndex();
    }

    private void UpdateMovementAnimation()
    {
        if (!driveMovementAnimation || !HasAnimatorController())
        {
            movementAnimationActive = false;
            return;
        }

        bool shouldUseMoveAnimation = BotMovementAnimationUtility.ShouldUseMoveAnimation(
            navMeshAgent != null && navMeshAgent.enabled,
            navMeshAgent != null && navMeshAgent.isOnNavMesh,
            navMeshAgent != null && navMeshAgent.isStopped,
            navMeshAgent != null && navMeshAgent.pathPending,
            navMeshAgent != null && navMeshAgent.hasPath,
            navMeshAgent != null ? navMeshAgent.remainingDistance : 0f,
            navMeshAgent != null ? navMeshAgent.stoppingDistance : 0f,
            navMeshAgent != null ? navMeshAgent.velocity : Vector3.zero,
            navMeshAgent != null ? navMeshAgent.desiredVelocity : Vector3.zero,
            movementAnimationThreshold);

        movementAnimationActive = shouldUseMoveAnimation;
        ApplyMovementAnimationParameter(shouldUseMoveAnimation);
    }

    private void ApplyMovementAnimationParameter(bool useMoveAnimation)
    {
        if (moveAnimationParameterHash == 0 || !CanDriveAnimator())
        {
            return;
        }

        animator.SetBool(moveAnimationParameterHash, useMoveAnimation);
    }

    private void ApplyCrouchAnimation()
    {
        if (!driveCrouchAnimation || !CanDriveAnimator())
        {
            return;
        }

        if (HasBoolParameter(crouchAnimationParameterHash))
        {
            animator.SetBool(crouchAnimationParameterHash, crouchAnimationActive);
            return;
        }

        if (crouchAnimationLayerIndex < 0)
        {
            RefreshCrouchAnimationLayerIndex();
        }

        if (crouchAnimationLayerIndex < 0)
        {
            return;
        }

        if (crouchAnimationActive)
        {
            if (crouchAnimationStateHash != 0 && animator.HasState(crouchAnimationLayerIndex, crouchAnimationStateHash))
            {
                animator.CrossFade(crouchAnimationStateHash, 0.1f, crouchAnimationLayerIndex);
            }

            return;
        }

        if (uncrouchAnimationStateHash != 0 && animator.HasState(crouchAnimationLayerIndex, uncrouchAnimationStateHash))
        {
            animator.CrossFade(uncrouchAnimationStateHash, 0.1f, crouchAnimationLayerIndex);
        }
    }

    private bool HasBoolParameter(int parameterHash)
    {
        if (parameterHash == 0 || !CanDriveAnimator())
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == parameterHash)
            {
                return true;
            }
        }

        return false;
    }

    private static int ToHash(string stateName)
    {
        return string.IsNullOrWhiteSpace(stateName) ? 0 : Animator.StringToHash(stateName);
    }

    private void UpdateLoadedLowerBodyLayer()
    {
        if (!driveLoadedLowerBodyLayer || !HasAnimatorController())
        {
            loadedLowerBodyLayerActive = false;
            ApplyLoadedLowerBodyLayerWeight(0f);
            return;
        }

        if (loadedLowerBodyLayerIndex < 0)
        {
            RefreshLoadedLowerBodyLayerIndex();
        }

        if (loadedLowerBodyLayerIndex < 0)
        {
            loadedLowerBodyLayerActive = false;
            return;
        }

        bool shouldActivate = ShouldUseLoadedLowerBodyLayer();
        loadedLowerBodyLayerActive = shouldActivate;
        ApplyLoadedLowerBodyLayerWeight(shouldActivate ? 1f : 0f);
    }

    private bool ShouldUseLoadedLowerBodyLayer()
    {
        if (commandAgent == null || !commandAgent.IsCarryingRescueTarget)
        {
            return false;
        }

        if (commandAgent.CurrentCarryWeightKg < Mathf.Max(0f, loadedLowerBodyMinCarryWeight))
        {
            return false;
        }

        return !loadedLowerBodyOnlyWhileMoving || movementAnimationActive;
    }

    private void ApplyLoadedLowerBodyLayerWeight(float targetWeight)
    {
        if (loadedLowerBodyLayerIndex < 0 || !CanDriveAnimator())
        {
            return;
        }

        targetWeight = Mathf.Clamp01(targetWeight);
        if (!Application.isPlaying || loadedLowerBodyLayerWeightLerpSpeed <= 0f)
        {
            animator.SetLayerWeight(loadedLowerBodyLayerIndex, targetWeight);
            return;
        }

        float currentWeight = animator.GetLayerWeight(loadedLowerBodyLayerIndex);
        float blend = 1f - Mathf.Exp(-loadedLowerBodyLayerWeightLerpSpeed * Time.deltaTime);
        animator.SetLayerWeight(loadedLowerBodyLayerIndex, Mathf.Lerp(currentWeight, targetWeight, blend));
    }

    private void RefreshCrouchAnimationLayerIndex()
    {
        crouchAnimationLayerIndex = -1;
        if (!CanQueryAnimatorLayers() || string.IsNullOrWhiteSpace(crouchAnimationLayer))
        {
            return;
        }

        crouchAnimationLayerIndex = animator.GetLayerIndex(crouchAnimationLayer);
    }

    private void RefreshLoadedLowerBodyLayerIndex()
    {
        loadedLowerBodyLayerIndex = -1;
        if (!CanQueryAnimatorLayers() || string.IsNullOrWhiteSpace(loadedLowerBodyLayer))
        {
            return;
        }

        loadedLowerBodyLayerIndex = animator.GetLayerIndex(loadedLowerBodyLayer);
    }

    private bool CanQueryAnimatorLayers()
    {
        return CanDriveAnimator();
    }

    private bool HasAnimatorController()
    {
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private bool CanDriveAnimator()
    {
        return HasAnimatorController() &&
               animator.isActiveAndEnabled &&
               animator.gameObject.activeInHierarchy &&
               animator.isInitialized;
    }
}
