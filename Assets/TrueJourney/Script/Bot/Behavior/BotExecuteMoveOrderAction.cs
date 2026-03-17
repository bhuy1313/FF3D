using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using BehaviorAction = Unity.Behavior.Action;
using Status = Unity.Behavior.Node.Status;

namespace TrueJourney.BotBehavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Execute Move Order",
        description: "Consumes the current move order from BotBehaviorContext and navigates to it.",
        category: "Action/FF3D Bot",
        story: "[Agent] executes queued move order",
        id: "4a7fbc1e334a4464b4e4fbab6fdb9f21")]
    public partial class BotExecuteMoveOrderAction : BehaviorAction
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        [CreateProperty] private Vector3 currentDestination;
        [CreateProperty] private float originalStoppingDistance = -1f;

        private BotBehaviorContext context;
        private NavMeshAgent navMeshAgent;

        protected override Status OnStart()
        {
            if (!TryInitialize(out Vector3 destination))
            {
                return Status.Failure;
            }

            currentDestination = destination;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (context == null || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                return Status.Failure;
            }

            if (!context.TryGetMoveOrder(out Vector3 destination))
            {
                return Status.Success;
            }

            if ((destination - currentDestination).sqrMagnitude > 0.01f)
            {
                currentDestination = destination;
                if (!navMeshAgent.SetDestination(currentDestination))
                {
                    return Status.Failure;
                }
            }

            if (navMeshAgent.pathPending)
            {
                return Status.Running;
            }

            if (navMeshAgent.remainingDistance > context.ArrivalDistance)
            {
                return Status.Running;
            }

            context.ClearMoveOrder();
            navMeshAgent.ResetPath();
            return Status.Success;
        }

        protected override void OnEnd()
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && originalStoppingDistance >= 0f)
            {
                navMeshAgent.stoppingDistance = originalStoppingDistance;
            }
        }

        private bool TryInitialize(out Vector3 destination)
        {
            destination = default;
            if (!Agent?.Value)
            {
                return false;
            }

            if (!BotBehaviorActionUtility.TryGetContext(Agent.Value, out context, out navMeshAgent))
            {
                return false;
            }

            if (!context.TryGetMoveOrder(out destination))
            {
                return false;
            }

            if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            originalStoppingDistance = navMeshAgent.stoppingDistance;
            navMeshAgent.stoppingDistance = context.ArrivalDistance;
            navMeshAgent.isStopped = false;

            BotBehaviorActionUtility.TrySampleDestination(navMeshAgent, destination, 2f, out destination);
            return navMeshAgent.SetDestination(destination);
        }
    }
}
