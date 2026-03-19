using UnityEngine;
using UnityEngine.AI;

namespace TrueJourney.BotBehavior
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class BotInteractionSensor : MonoBehaviour
    {
        [Header("Sensor Settings")]
        [SerializeField] private float sensorRange = 1.5f;
        [SerializeField] private float sensorRadius = 0.5f;
        [SerializeField] private float sensorYOffset = 1.0f;
        [SerializeField] private LayerMask interactMask = ~0;
        
        [Header("Cooldown")]
        [SerializeField] private float interactCooldown = 2.0f;
        
        [Header("Debug")]
        [SerializeField] private bool drawDebugPath = true;

        private NavMeshAgent agent;
        private BotInventorySystem inventory;
        private float lastInteractTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            inventory = GetComponent<BotInventorySystem>();
        }

        private void Update()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            // Only check for interaction if moving
            if (agent.velocity.sqrMagnitude < 0.1f)
                return;

            if (Time.time < lastInteractTime + interactCooldown)
                return;

            CheckForInteractables();
        }

        private void CheckForInteractables()
        {
            Vector3 origin = transform.position + Vector3.up * sensorYOffset; 
            Vector3 direction = agent.velocity.normalized;

            if (Physics.SphereCast(origin, sensorRadius, direction, out RaycastHit hit, sensorRange, interactMask, QueryTriggerInteraction.Ignore))
            {
                // Special behavior for Items: Try to pick it up if Inventory exists
                IPickupable pickupable = FindPickupable(hit.collider);
                if (pickupable != null)
                {
                    if (inventory != null && !inventory.IsFull)
                    {
                        inventory.TryPickup(pickupable);
                    }
                    return; // Done processing this frame
                }

                // Normal Interaction
                IInteractable interactable = FindInteractable(hit.collider);
                
                if (interactable != null)
                {
                    // Special behavior for Door: Don't interact if already open
                    if (interactable is IOpenable openable && openable.IsOpen)
                    {
                        return; // Door is already open, do not interact/close it
                    }

                    // Otherwise, regular interact (buttons, etc.)
                    interactable.Interact(gameObject);
                    lastInteractTime = Time.time;
                }
            }
        }

        private static IInteractable FindInteractable(Collider collider)
        {
            if (collider.TryGetComponent(out IInteractable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IInteractable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IInteractable parentInteractable))
                {
                    return parentInteractable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private static IPickupable FindPickupable(Collider collider)
        {
            if (collider.TryGetComponent(out IPickupable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IPickupable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IPickupable parentPickupable))
                {
                    return parentPickupable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugPath) return;

            Vector3 origin = transform.position + Vector3.up * sensorYOffset;
            Vector3 direction = Application.isPlaying && agent != null && agent.velocity.sqrMagnitude > 0.1f ? agent.velocity.normalized : transform.forward;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, direction * sensorRange);
            Gizmos.DrawWireSphere(origin + direction * sensorRange, sensorRadius);
        }
    }
}
