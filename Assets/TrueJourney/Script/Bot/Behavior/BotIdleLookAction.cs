using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using BehaviorAction = Unity.Behavior.Action;
using Status = Unity.Behavior.Node.Status;

namespace TrueJourney.BotBehavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Idle Look",
        description: "Keeps the bot in a simple idle look-around loop until another branch should take over.",
        category: "Action/FF3D Bot",
        story: "[Agent] idles and scans around",
        id: "0d6a464d215c4ae38d8198f5f08ed630")]
    public partial class BotIdleLookAction : BehaviorAction
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        [CreateProperty] private float phaseTimer;
        [CreateProperty] private bool isPaused;
        [CreateProperty] private float turnDirection;

        private BotBehaviorContext context;
        private Transform agentTransform;

        protected override Status OnStart()
        {
            if (!TryInitialize())
            {
                return Status.Failure;
            }

            StartPausePhase();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (context == null || agentTransform == null)
            {
                return Status.Failure;
            }

            if (context.HasExtinguishOrder || context.HasFollowOrder || context.HasRescueOrder || context.HasMoveOrder || (context.PatrolMovementEnabled && context.HasConfiguredPatrolRoute))
            {
                return Status.Failure;
            }

            phaseTimer -= Time.deltaTime;
            if (!isPaused)
            {
                float angle = context.IdleTurnSpeed * turnDirection * Time.deltaTime;
                agentTransform.Rotate(0f, angle, 0f, Space.Self);
            }

            if (phaseTimer > 0f)
            {
                return Status.Running;
            }

            if (isPaused)
            {
                StartTurnPhase();
            }
            else
            {
                StartPausePhase();
            }

            return Status.Running;
        }

        private bool TryInitialize()
        {
            if (!Agent?.Value)
            {
                return false;
            }

            context = Agent.Value.GetComponent<BotBehaviorContext>();
            if (context == null || context.HasExtinguishOrder || context.HasFollowOrder || context.HasRescueOrder || context.HasMoveOrder || (context.PatrolMovementEnabled && context.HasConfiguredPatrolRoute))
            {
                return false;
            }

            agentTransform = Agent.Value.transform;
            return true;
        }

        private void StartPausePhase()
        {
            isPaused = true;
            phaseTimer = UnityEngine.Random.Range(context.IdlePauseDurationRange.x, context.IdlePauseDurationRange.y);
        }

        private void StartTurnPhase()
        {
            isPaused = false;
            turnDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            phaseTimer = UnityEngine.Random.Range(context.IdleTurnDurationRange.x, context.IdleTurnDurationRange.y);
        }
    }
}
