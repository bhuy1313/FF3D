using UnityEngine;
using UnityEngine.AI;

namespace TrueJourney.BotBehavior
{
    internal static class BotBehaviorActionUtility
    {
        public static bool TryGetContext(GameObject agentObject, out BotBehaviorContext context, out NavMeshAgent navMeshAgent)
        {
            context = null;
            navMeshAgent = null;

            if (agentObject == null)
            {
                return false;
            }

            context = agentObject.GetComponent<BotBehaviorContext>();
            if (context == null)
            {
                return false;
            }

            navMeshAgent = context.NavMeshAgent != null
                ? context.NavMeshAgent
                : agentObject.GetComponent<NavMeshAgent>();

            return navMeshAgent != null;
        }

        public static bool TrySampleDestination(NavMeshAgent navMeshAgent, Vector3 worldPoint, float sampleDistance, out Vector3 destination)
        {
            destination = worldPoint;
            if (navMeshAgent == null || sampleDistance <= 0f)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, sampleDistance, navMeshAgent.areaMask))
            {
                return false;
            }

            destination = hit.position;
            return true;
        }
    }
}
