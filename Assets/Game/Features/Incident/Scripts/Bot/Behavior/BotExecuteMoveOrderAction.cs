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
        private global::BotCommandAgent commandAgent;

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

            if (context.HasExtinguishOrder || context.HasFollowOrder || context.HasRescueOrder)
            {
                return Status.Failure;
            }

            if (!context.TryGetMoveOrder(out Vector3 destination))
            {
                return Status.Success;
            }

            if (commandAgent != null && commandAgent.HasMovePickupTarget)
            {
                if (commandAgent.TryCompleteMovePickupTarget())
                {
                    navMeshAgent.ResetPath();

                    currentDestination = destination;
                    if (!(commandAgent.TryNavigateTo(currentDestination)))
                    {
                        return Status.Failure;
                    }

                    return Status.Running;
                }

                return Status.Running;
            }

            bool destinationChanged = (destination - currentDestination).sqrMagnitude > 0.01f;
            bool requiresNavigationRefresh =
                !navMeshAgent.pathPending &&
                (!navMeshAgent.hasPath ||
                 navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete ||
                 (commandAgent != null && commandAgent.ShouldRefreshPathClearingCheck()));
            if (destinationChanged || requiresNavigationRefresh)
            {
                currentDestination = destination;
                if (!(commandAgent != null
                        ? commandAgent.TryNavigateTo(currentDestination)
                        : navMeshAgent.SetDestination(currentDestination)))
                {
                    return Status.Failure;
                }
            }

            if (navMeshAgent.pathPending)
            {
                return Status.Running;
            }

            if (commandAgent != null && commandAgent.IsPathClearingActive)
            {
                return Status.Running;
            }

            if (navMeshAgent.remainingDistance > context.ArrivalDistance)
            {
                return Status.Running;
            }

            context.ClearMoveOrder();
            navMeshAgent.ResetPath();
            commandAgent?.ResetMoveActivityDebug();
            return Status.Success;
        }

        protected override void OnEnd()
        {
            commandAgent?.ResetMoveActivityDebug();

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

            commandAgent = Agent.Value.GetComponent<global::BotCommandAgent>();

            if (context.HasExtinguishOrder || context.HasFollowOrder || context.HasRescueOrder || !context.TryGetMoveOrder(out destination))
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
            return commandAgent != null
                ? commandAgent.TryNavigateTo(destination)
                : navMeshAgent.SetDestination(destination);
        }
    }
}
