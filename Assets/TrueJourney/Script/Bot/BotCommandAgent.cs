using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class BotCommandAgent : MonoBehaviour, ICommandable, IInteractable
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private BotBehaviorContext behaviorContext;
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private bool drawDestinationGizmo = true;

    private Vector3 lastIssuedDestination;
    private bool hasIssuedDestination;

    public Vector3 LastIssuedDestination => lastIssuedDestination;
    public bool HasIssuedDestination => hasIssuedDestination;

    private void Awake()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (behaviorContext == null)
        {
            behaviorContext = GetComponent<BotBehaviorContext>();
        }
    }

    public bool CanAcceptCommand(BotCommandType commandType)
    {
        return commandType == BotCommandType.Move &&
               navMeshAgent != null &&
               navMeshAgent.enabled &&
               isActiveAndEnabled;
    }

    public bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint)
    {
        if (!CanAcceptCommand(commandType))
        {
            return false;
        }

        if (!navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 destination = worldPoint;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(worldPoint, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        bool accepted;
        if (behaviorContext != null && behaviorContext.UseMoveOrdersAsBehaviorInput)
        {
            behaviorContext.SetMoveOrder(destination);
            accepted = true;
        }
        else
        {
            navMeshAgent.isStopped = false;
            accepted = navMeshAgent.SetDestination(destination);
        }

        if (!accepted)
        {
            return false;
        }

        lastIssuedDestination = destination;
        hasIssuedDestination = true;
        return true;
    }

    public void Interact(GameObject interactor)
    {
        // Intentionally empty. This lets bots participate in the focus/outline pipeline.
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDestinationGizmo || !hasIssuedDestination)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lastIssuedDestination, 0.3f);
        Gizmos.DrawLine(transform.position, lastIssuedDestination);
    }
}
