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
        name: "Patrol Route",
        description: "Patrols waypoint transforms configured on BotBehaviorContext.",
        category: "Action/FF3D Bot",
        story: "[Agent] patrols its local route",
        id: "bc4fb7b9af564f6e9f4e2051020b6891")]
    public partial class BotPatrolRouteAction : BehaviorAction
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        [CreateProperty] private int patrolIndex;
        [CreateProperty] private float waitTimer;
        [CreateProperty] private float originalStoppingDistance = -1f;

        private BotBehaviorContext context;
        private NavMeshAgent navMeshAgent;
        private bool waitingForNextPoint;

        protected override Status OnStart()
        {
            if (!TryInitialize())
            {
                return Status.Failure;
            }

            return MoveToCurrentPatrolPoint() ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (context == null || navMeshAgent == null)
            {
                return Status.Failure;
            }

            if (context.HasMoveOrder || !context.PatrolMovementEnabled || !context.HasConfiguredPatrolRoute || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                return Status.Failure;
            }

            if (waitingForNextPoint)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer > 0f)
                {
                    return Status.Running;
                }

                waitingForNextPoint = false;
                AdvancePatrolIndex();
                return MoveToCurrentPatrolPoint() ? Status.Running : Status.Failure;
            }

            if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > context.ArrivalDistance)
            {
                return Status.Running;
            }

            float delay = context.PatrolWaitSeconds;
            if (delay <= 0f)
            {
                AdvancePatrolIndex();
                return MoveToCurrentPatrolPoint() ? Status.Running : Status.Failure;
            }

            waitingForNextPoint = true;
            waitTimer = delay;
            return Status.Running;
        }

        protected override void OnEnd()
        {
            waitingForNextPoint = false;
            waitTimer = 0f;

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && originalStoppingDistance >= 0f)
            {
                navMeshAgent.stoppingDistance = originalStoppingDistance;
            }
        }

        private bool TryInitialize()
        {
            if (!Agent?.Value)
            {
                return false;
            }

            if (!BotBehaviorActionUtility.TryGetContext(Agent.Value, out context, out navMeshAgent))
            {
                return false;
            }

            if (context.HasMoveOrder || !context.PatrolMovementEnabled || !context.HasConfiguredPatrolRoute || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            originalStoppingDistance = navMeshAgent.stoppingDistance;
            navMeshAgent.stoppingDistance = context.ArrivalDistance;
            navMeshAgent.isStopped = false;

            int patrolPointCount = context.GetPatrolPointCount();
            if (patrolPointCount <= 0)
            {
                return false;
            }

            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPointCount - 1);
            waitingForNextPoint = false;
            waitTimer = 0f;
            return true;
        }

        private void AdvancePatrolIndex()
        {
            int patrolPointCount = context.GetPatrolPointCount();
            patrolIndex = patrolPointCount <= 0 ? 0 : (patrolIndex + 1) % patrolPointCount;
        }

        private bool MoveToCurrentPatrolPoint()
        {
            if (!context.TryGetPatrolPointPosition(patrolIndex, out Vector3 destination))
            {
                return false;
            }

            BotBehaviorActionUtility.TrySampleDestination(navMeshAgent, destination, 2f, out destination);
            return navMeshAgent.SetDestination(destination);
        }
    }
}
