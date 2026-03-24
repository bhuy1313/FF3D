using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class BotBehaviorContext : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator animator;

    [Header("Orders")]
    [SerializeField] private bool useMoveOrdersAsBehaviorInput;
    [SerializeField] private float arrivalDistance = 0.35f;

    [Header("Patrol")]
    [SerializeField] private bool enablePatrolMovement = true;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitSeconds = 1.5f;

    [Header("Idle")]
    [SerializeField] private float idleTurnSpeed = 90f;
    [SerializeField] private Vector2 idleTurnDurationRange = new Vector2(1.25f, 2.5f);
    [SerializeField] private Vector2 idlePauseDurationRange = new Vector2(0.4f, 1.2f);

    [Header("Animation")]
    [SerializeField] private bool driveMovementAnimation = true;
    [SerializeField] private string moveAnimationParameter = "IsMoving";
    [SerializeField] private float movementAnimationThreshold = 0.1f;
    [SerializeField] private bool movementAnimationActive;

    private readonly BotMoveOrderState moveOrderState = new BotMoveOrderState();
    private readonly BotExtinguishOrderState extinguishOrderState = new BotExtinguishOrderState();
    private readonly BotFollowOrderState followOrderState = new BotFollowOrderState();
    private readonly BotRescueOrderState rescueOrderState = new BotRescueOrderState();
    private int moveAnimationParameterHash;

    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public bool UseMoveOrdersAsBehaviorInput => useMoveOrdersAsBehaviorInput;
    public bool HasMoveOrder => moveOrderState.HasMoveOrder;
    public bool HasExtinguishOrder => extinguishOrderState.HasExtinguishOrder;
    public bool HasFollowOrder => followOrderState.HasFollowOrder;
    public bool HasRescueOrder => rescueOrderState.HasRescueOrder;
    public float ArrivalDistance => Mathf.Max(0.05f, arrivalDistance);
    public float PatrolWaitSeconds => Mathf.Max(0f, patrolWaitSeconds);
    public bool PatrolMovementEnabled => enablePatrolMovement;
    public float IdleTurnSpeed => Mathf.Max(0f, idleTurnSpeed);
    public Vector2 IdleTurnDurationRange => SanitizeRange(idleTurnDurationRange, 0.1f);
    public Vector2 IdlePauseDurationRange => SanitizeRange(idlePauseDurationRange, 0f);
    public int PatrolPointCount => patrolPoints != null ? patrolPoints.Length : 0;
    public bool HasConfiguredPatrolRoute => GetPatrolPointCount() > 0;
    public bool HasPatrolRoute => enablePatrolMovement && HasConfiguredPatrolRoute;

    private void Awake()
    {
        AutoAssignReferences();
        CacheAnimationHashes();
    }

    private void LateUpdate()
    {
        UpdateMovementAnimation();
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

    public void SetFollowOrder()
    {
        followOrderState.SetActive();
    }

    public void ClearFollowOrder()
    {
        followOrderState.Clear();
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

        if (animator != null)
        {
            return;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate != null && candidate.runtimeAnimatorController != null)
            {
                animator = candidate;
                return;
            }
        }

        if (animators.Length > 0)
        {
            animator = animators[0];
        }
    }

    private void CacheAnimationHashes()
    {
        moveAnimationParameterHash = ToHash(moveAnimationParameter);
    }

    private void UpdateMovementAnimation()
    {
        if (!driveMovementAnimation || animator == null)
        {
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
        if (moveAnimationParameterHash == 0)
        {
            return;
        }

        animator.SetBool(moveAnimationParameterHash, useMoveAnimation);
    }

    private static int ToHash(string stateName)
    {
        return string.IsNullOrWhiteSpace(stateName) ? 0 : Animator.StringToHash(stateName);
    }
}
