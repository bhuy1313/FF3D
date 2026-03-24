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
        [SerializeField] private LayerMask fireMask = ~0;
        [SerializeField] private int fireBufferSize = 32;
        [SerializeField] private float fireVerticalTolerance = 1.25f;
        
        [Header("Cooldown")]
        [SerializeField] private float interactCooldown = 2.0f;
        
        [Header("Debug")]
        [SerializeField] private bool drawDebugPath = true;
        [SerializeField] private bool debugBreakableSensorLogs = false;
        [SerializeField] private bool debugBreakableSensorMisses = false;

        private NavMeshAgent agent;
        private BotInventorySystem inventory;
        private float lastInteractTime;
        private bool pickupWindowEnabled;
        private IPickupable pickupTarget;
        private string lastBreakableSensorLogKey;
        private Collider[] fireBuffer;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            inventory = GetComponent<BotInventorySystem>();
            EnsureFireBuffer();
        }

        private void Update()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            // Only check for interaction if moving
            if (!TryGetProbeDirection(true, out _))
                return;

            if (Time.time < lastInteractTime + interactCooldown)
                return;

            CheckForInteractables();
        }

        private void CheckForInteractables()
        {
            Vector3 origin = transform.position + Vector3.up * sensorYOffset; 
            if (!TryGetProbeDirection(true, out Vector3 direction))
            {
                return;
            }

            if (Physics.SphereCast(origin, sensorRadius, direction, out RaycastHit hit, sensorRange, interactMask, QueryTriggerInteraction.Ignore))
            {
                // Special behavior for Items: Try to pick it up if Inventory exists
                IPickupable pickupable = FindPickupable(hit.collider);
                if (pickupable != null)
                {
                    if (pickupWindowEnabled &&
                        inventory != null &&
                        !inventory.IsFull &&
                        (pickupTarget == null || pickupTarget == pickupable) &&
                        inventory.TryPickup(pickupable))
                    {
                        pickupWindowEnabled = false;
                        pickupTarget = null;
                        lastInteractTime = Time.time;
                    }

                    return; // Done processing this frame
                }

                // Normal Interaction
                IInteractable interactable = FindInteractable(hit.collider);
                
                if (interactable != null)
                {
                    // Rescue pickup is handled explicitly by the rescue controller.
                    if (FindRescuableTarget(hit.collider) != null)
                    {
                        return;
                    }

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

        public void SetPickupWindow(bool enabled, IPickupable target = null)
        {
            pickupWindowEnabled = enabled;
            pickupTarget = enabled ? target : null;
        }

        public bool TryFindNearbyFire(float radius, out IFireTarget fireTarget)
        {
            Vector3 center = transform.position + Vector3.up * sensorYOffset;
            return TryFindFireNear(center, radius, transform.position, transform.position.y, out fireTarget);
        }

        public bool TryFindFireNearPoint(Vector3 worldPoint, float radius, out IFireTarget fireTarget)
        {
            Vector3 center = worldPoint + Vector3.up * sensorYOffset;
            return TryFindFireNear(center, radius, transform.position, worldPoint.y, out fireTarget);
        }

        public bool TryFindBreakableAhead(out IBotBreakableTarget breakableTarget)
        {
            breakableTarget = null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                LogBreakableSensor("ahead:no-agent", "Ahead: sensor unavailable.");
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * sensorYOffset;
            if (!TryGetProbeDirection(false, out Vector3 direction))
            {
                LogBreakableSensor("ahead:no-direction", "Ahead: no probe direction.", true);
                return false;
            }

            if (!Physics.SphereCast(origin, sensorRadius, direction, out RaycastHit hit, sensorRange, interactMask, QueryTriggerInteraction.Ignore))
            {
                LogBreakableSensor(
                    $"ahead:no-hit:{FormatVectorKey(origin)}:{FormatVectorKey(direction)}",
                    $"Ahead: no blocker. origin={origin}, direction={direction}, range={sensorRange:F2}.",
                    true);
                return false;
            }

            IBotBreakableTarget breakable = FindBreakableTarget(hit.collider);
            if (breakable == null || breakable.IsBroken || !breakable.CanBeClearedByBot)
            {
                string hitName = hit.collider != null ? hit.collider.name : "(null)";
                LogBreakableSensor(
                    $"ahead:non-breakable:{hitName}",
                    $"Ahead: hit '{hitName}' but it is not a valid blocker.",
                    true);
                return false;
            }

            LogBreakableSensor(
                $"ahead:blocker:{GetBreakableDebugName(breakable)}",
                $"Ahead: detected blocker '{GetBreakableDebugName(breakable)}'.");
            breakableTarget = breakable;
            return true;
        }

        public bool TryFindBreakableTowards(Vector3 worldPosition, out IBotBreakableTarget breakableTarget)
        {
            breakableTarget = null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                LogBreakableSensor("towards:no-agent", $"Towards {worldPosition}: sensor unavailable.");
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * sensorYOffset;
            Vector3 direction = worldPosition - origin;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
            {
                LogBreakableSensor(
                    $"towards:short:{FormatVectorKey(worldPosition)}",
                    $"Towards {worldPosition}: distance too short.",
                    true);
                return false;
            }

            direction /= distance;
            if (!Physics.SphereCast(origin, sensorRadius, direction, out RaycastHit hit, distance, interactMask, QueryTriggerInteraction.Ignore))
            {
                LogBreakableSensor(
                    $"towards:no-hit:{FormatVectorKey(worldPosition)}",
                    $"Towards {worldPosition}: no blocker on probe.",
                    true);
                return false;
            }

            IBotBreakableTarget breakable = FindBreakableTarget(hit.collider);
            if (breakable == null || breakable.IsBroken || !breakable.CanBeClearedByBot)
            {
                string hitName = hit.collider != null ? hit.collider.name : "(null)";
                LogBreakableSensor(
                    $"towards:non-breakable:{hitName}:{FormatVectorKey(worldPosition)}",
                    $"Towards {worldPosition}: hit '{hitName}' but it is not a valid blocker.",
                    true);
                return false;
            }

            LogBreakableSensor(
                $"towards:blocker:{GetBreakableDebugName(breakable)}:{FormatVectorKey(worldPosition)}",
                $"Towards {worldPosition}: detected blocker '{GetBreakableDebugName(breakable)}'.");
            breakableTarget = breakable;
            return true;
        }

        public bool TryFindBreakableBetween(Vector3 worldStart, Vector3 worldEnd, out IBotBreakableTarget breakableTarget)
        {
            breakableTarget = null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                LogBreakableSensor(
                    $"between:no-agent:{FormatVectorKey(worldStart)}:{FormatVectorKey(worldEnd)}",
                    $"Between {worldStart} -> {worldEnd}: sensor unavailable.");
                return false;
            }

            Vector3 origin = worldStart + Vector3.up * sensorYOffset;
            Vector3 target = worldEnd + Vector3.up * sensorYOffset;
            Vector3 direction = target - origin;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
            {
                LogBreakableSensor(
                    $"between:short:{FormatVectorKey(worldStart)}:{FormatVectorKey(worldEnd)}",
                    $"Between {worldStart} -> {worldEnd}: distance too short.",
                    true);
                return false;
            }

            direction /= distance;
            if (!Physics.SphereCast(origin, sensorRadius, direction, out RaycastHit hit, distance, interactMask, QueryTriggerInteraction.Ignore))
            {
                LogBreakableSensor(
                    $"between:no-hit:{FormatVectorKey(worldStart)}:{FormatVectorKey(worldEnd)}",
                    $"Between {worldStart} -> {worldEnd}: no blocker on segment.",
                    true);
                return false;
            }

            IBotBreakableTarget breakable = FindBreakableTarget(hit.collider);
            if (breakable == null || breakable.IsBroken || !breakable.CanBeClearedByBot)
            {
                string hitName = hit.collider != null ? hit.collider.name : "(null)";
                LogBreakableSensor(
                    $"between:non-breakable:{hitName}:{FormatVectorKey(worldStart)}:{FormatVectorKey(worldEnd)}",
                    $"Between {worldStart} -> {worldEnd}: hit '{hitName}' but it is not a valid blocker.",
                    true);
                return false;
            }

            LogBreakableSensor(
                $"between:blocker:{GetBreakableDebugName(breakable)}:{FormatVectorKey(worldStart)}:{FormatVectorKey(worldEnd)}",
                $"Between {worldStart} -> {worldEnd}: detected blocker '{GetBreakableDebugName(breakable)}'.");
            breakableTarget = breakable;
            return true;
        }

        private bool TryGetProbeDirection(bool requireMovementIntent, out Vector3 direction)
        {
            direction = default;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            Vector3 probeDirection = agent.velocity;
            probeDirection.y = 0f;

            if (probeDirection.sqrMagnitude < 0.01f)
            {
                probeDirection = agent.desiredVelocity;
                probeDirection.y = 0f;
            }

            if (probeDirection.sqrMagnitude < 0.01f && agent.hasPath)
            {
                probeDirection = agent.steeringTarget - transform.position;
                probeDirection.y = 0f;
            }

            if (probeDirection.sqrMagnitude < 0.01f)
            {
                probeDirection = transform.forward;
                probeDirection.y = 0f;
            }

            if (probeDirection.sqrMagnitude < 0.01f)
            {
                return false;
            }

            if (requireMovementIntent && agent.remainingDistance <= agent.stoppingDistance + 0.05f && agent.desiredVelocity.sqrMagnitude < 0.01f && agent.velocity.sqrMagnitude < 0.01f)
            {
                return false;
            }

            direction = probeDirection.normalized;
            return true;
        }

        private bool TryFindFireNear(Vector3 center, float radius, Vector3 fromPosition, float referenceHeight, out IFireTarget fireTarget)
        {
            fireTarget = null;
            EnsureFireBuffer();

            float effectiveRadius = Mathf.Max(0.05f, radius);
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                effectiveRadius,
                fireBuffer,
                fireMask,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = fireBuffer[i];
                fireBuffer[i] = null;
                IFireTarget candidate = FindFireTarget(hit);
                if (candidate == null || !candidate.IsBurning)
                {
                    continue;
                }

                if (Mathf.Abs(candidate.GetWorldPosition().y - referenceHeight) > Mathf.Max(0.05f, fireVerticalTolerance))
                {
                    continue;
                }

                float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                fireTarget = candidate;
            }

            return fireTarget != null;
        }

        private void EnsureFireBuffer()
        {
            int desiredSize = Mathf.Max(8, fireBufferSize);
            if (fireBuffer == null || fireBuffer.Length != desiredSize)
            {
                fireBuffer = new Collider[desiredSize];
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

        private static IBotBreakableTarget FindBreakableTarget(Collider collider)
        {
            if (collider.TryGetComponent(out IBotBreakableTarget direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IBotBreakableTarget rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IBotBreakableTarget parentBreakable))
                {
                    return parentBreakable;
                }

                parent = parent.parent;
            }

            return null;
        }

        private static IFireTarget FindFireTarget(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.TryGetComponent(out IFireTarget direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IFireTarget rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IFireTarget parentFire))
                {
                    return parentFire;
                }

                parent = parent.parent;
            }

            return null;
        }

        private static IRescuableTarget FindRescuableTarget(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.TryGetComponent(out IRescuableTarget direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IRescuableTarget rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IRescuableTarget parentRescuable))
                {
                    return parentRescuable;
                }

                parent = parent.parent;
            }

            return null;
        }

        private void LogBreakableSensor(string key, string detail, bool isMiss = false)
        {
            if (!debugBreakableSensorLogs)
            {
                return;
            }

            if (isMiss && !debugBreakableSensorMisses)
            {
                return;
            }

            if (lastBreakableSensorLogKey == key)
            {
                return;
            }

            lastBreakableSensorLogKey = key;
            Debug.Log($"[BotSensor] [{name}] {detail}", this);
        }

        private static string GetBreakableDebugName(IBotBreakableTarget breakable)
        {
            return breakable is Component component && component != null
                ? component.name
                : "(unknown breakable)";
        }

        private static string FormatVectorKey(Vector3 value)
        {
            return $"{Mathf.RoundToInt(value.x * 10f)}:{Mathf.RoundToInt(value.y * 10f)}:{Mathf.RoundToInt(value.z * 10f)}";
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
