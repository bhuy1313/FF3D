using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;
public class FireExtinguisher : MonoBehaviour, IInteractable, IPickupable, IUsable, IBotExtinguisherItem, IMovementWeightSource
{
    [Header("Extinguisher")]
    [SerializeField] private FireExtinguisherType extinguisherType = FireExtinguisherType.DryChemical;
    [SerializeField] private float movementWeightKg = 12f;

    [Header("Charge")]
    [SerializeField] private float maxCharge = 10f;
    [SerializeField] private float rechargePerSecond = 0f;
    [SerializeField] private float minChargeToUse = 0.05f;

    [Header("Usage")]
    [SerializeField] private bool toggleUse = true;

    [Header("Suppression")]
    [FormerlySerializedAs("playerApplyWaterPerSecond")]
    [SerializeField] private float playerDischargePerSecond = 1.5f;
    [FormerlySerializedAs("botApplyWaterPerSecond")]
    [SerializeField] private float botDischargePerSecond = 1.5f;
    [SerializeField] private float maxSprayDistance = 4.25f;
    [Range(0f, 1f)]
    [Tooltip("Bot stand distance as a ratio of Max Spray Distance.")]
    [SerializeField] private float botStandDistanceFactor = 0.6f;

    [Header("Cone Detection")]
    [SerializeField] private float coneHalfAngle = 28f;
    [SerializeField] private float coneBaseRadius = 0.15f;
    [SerializeField] private int coneSegments = 4;
    [SerializeField] private LayerMask sprayMask = ~0;
    [SerializeField] private LayerMask lineOfSightMask = 1; // Default layer

    [Header("References")]
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private FireExtinguisherAudioController audioController;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentCharge;
    [SerializeField] private bool isSpraying;
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private GameObject currentUser;
    [SerializeField] private GameObject claimOwner;

    [Header("Debug")]
    [SerializeField] private bool drawConeGizmo = true;
    [SerializeField] private bool drawGizmoOnlyWhenSelected = true;

    private readonly Collider[] hitBuffer = new Collider[64];
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public float ApplyWaterPerSecond => botDischargePerSecond;
    public FireSuppressionAgent SuppressionAgent => ResolveSuppressionAgent();
    public float PreferredSprayDistance => Mathf.Clamp(maxSprayDistance * Mathf.Clamp01(botStandDistanceFactor), 0f, maxSprayDistance);
    public float MaxSprayDistance => maxSprayDistance;
    public float MaxVerticalReach => maxSprayDistance;
    public float BallisticLaunchSpeed => 0f;
    public float BallisticGravityMultiplier => 1f;
    public bool RequiresPreciseAim => false;
    public bool IsSpraying => isSpraying;
    public bool HasUsableCharge => currentCharge >= minChargeToUse;
    public GameObject CurrentHolder => currentHolder;
    public GameObject CurrentUser => currentUser;
    public bool IsHeld => currentHolder != null;
    public GameObject ClaimOwner => claimOwner;
    public bool IsBotControlled => currentUser != null && currentUser.GetComponentInParent<BotBehaviorContext>() != null;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (sprayParticles == null)
        {
            sprayParticles = GetComponentInChildren<ParticleSystem>();
        }

        EnsureAudioController();

        currentCharge = Mathf.Clamp(currentCharge <= 0f ? maxCharge : currentCharge, 0f, maxCharge);
        SetSprayState(false);
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterExtinguisherItem(this);
    }

    private void Update()
    {
        if (isSpraying)
        {
            currentCharge = Mathf.Max(0f, currentCharge - GetActiveWaterPerSecond() * Time.deltaTime);
            if (!IsBotControlled)
            {
                ApplyExtinguishCone();
            }
            if (currentCharge <= 0f)
            {
                currentUser = null;
                SetSprayState(false);
            }
        }
        else if (rechargePerSecond > 0f && currentCharge < maxCharge)
        {
            currentCharge = Mathf.Min(maxCharge, currentCharge + rechargePerSecond * Time.deltaTime);
        }
    }

    private float GetActiveWaterPerSecond()
    {
        return Mathf.Max(0f, IsBotControlled ? botDischargePerSecond : playerDischargePerSecond);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
        claimOwner = picker;
        currentUser = null;
        SetSprayState(false);
    }

    public void OnDrop(GameObject dropper)
    {
        currentHolder = null;
        if (claimOwner == dropper)
        {
            claimOwner = null;
        }

        currentUser = null;
        SetSprayState(false);
    }

    public bool IsAvailableTo(GameObject requester)
    {
        if (requester == null)
        {
            return false;
        }

        return claimOwner == null || claimOwner == requester || currentHolder == requester;
    }

    public bool TryClaim(GameObject requester)
    {
        if (!IsAvailableTo(requester))
        {
            return false;
        }

        claimOwner = requester;
        return true;
    }

    public void ReleaseClaim(GameObject requester)
    {
        if (requester != null && claimOwner == requester && currentHolder != requester)
        {
            claimOwner = null;
        }
    }

    public void Use(GameObject user)
    {
        if (toggleUse)
        {
            if (isSpraying)
            {
                currentUser = null;
                SetSprayState(false);
                return;
            }

            if (currentCharge >= minChargeToUse)
            {
                currentUser = user;
                SetSprayState(true);
            }

            return;
        }

        if (currentCharge >= minChargeToUse)
        {
            currentUser = user;
            SetSprayState(true);
        }
    }

    public void SetExternalSprayState(bool enable, GameObject user)
    {
        if (!enable)
        {
            currentUser = null;
            SetSprayState(false);
            return;
        }

        if (currentCharge < minChargeToUse)
        {
            currentUser = null;
            SetSprayState(false);
            return;
        }

        currentUser = user;
        SetSprayState(true);
    }

    public void SetExternalAimDirection(Vector3 worldDirection, GameObject user)
    {
    }

    public void ClearExternalAimDirection(GameObject user)
    {
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterExtinguisherItem(this);
        currentUser = null;
        SetSprayState(false);
    }

    private void SetSprayState(bool enable)
    {
        if (isSpraying == enable)
        {
            return;
        }

        isSpraying = enable;

        if (sprayParticles != null)
        {
            if (enable)
            {
                sprayParticles.Play();
            }
            else
            {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        audioController?.RefreshState();
    }

    private void EnsureAudioController()
    {
        if (audioController == null)
        {
            audioController = GetComponent<FireExtinguisherAudioController>();
        }

        if (audioController == null)
        {
            audioController = gameObject.AddComponent<FireExtinguisherAudioController>();
        }

        audioController.Initialize(this);
    }

    private void ApplyExtinguishCone()
    {
        if (playerDischargePerSecond <= 0f || maxSprayDistance <= 0f)
        {
            return;
        }

        Transform origin = sprayParticles != null ? sprayParticles.transform : transform;
        Vector3 start = origin.position;
        Vector3 forward = origin.forward;
        if (!IsFinite(start) || !IsFinite(forward))
        {
            currentUser = null;
            SetSprayState(false);
            return;
        }

        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float amount = playerDischargePerSecond * Time.deltaTime;
        float segmentLength = maxSprayDistance / Mathf.Max(1, coneSegments);

        System.Collections.Generic.HashSet<Fire> processedFires = new System.Collections.Generic.HashSet<Fire>();

        for (int i = 0; i < coneSegments; i++)
        {
            float distance = segmentLength * (i + 1);
            float radius = coneBaseRadius + Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * distance;
            Vector3 center = start + forward * distance;
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, hitBuffer, sprayMask, QueryTriggerInteraction.Collide);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = hitBuffer[hitIndex];
                if (hit == null)
                {
                    continue;
                }

                Vector3 closestPoint = GetClosestPointSafe(hit, start);
                Vector3 toHit = closestPoint - start;
                float hitDistance = toHit.magnitude;
                if (hitDistance <= 0.001f || hitDistance > maxSprayDistance)
                {
                    continue;
                }

                float angle = Vector3.Angle(forward, toHit.normalized);
                if (angle > coneHalfAngle)
                {
                    continue;
                }

                if (Physics.Linecast(start, closestPoint, out RaycastHit lineHit, lineOfSightMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                ApplyWaterToColliderSafe(hit, amount, processedFires, SuppressionAgent, currentUser);
            }
        }
    }

    private static void ApplyWaterToColliderSafe(
        Collider collider,
        float amount,
        System.Collections.Generic.HashSet<Fire> processedFires,
        FireSuppressionAgent suppressionAgent,
        GameObject sourceUser)
    {
        if (collider == null)
        {
            return;
        }

        Fire fire = FindFire(collider);
        if (fire != null && processedFires.Add(fire))
        {
            fire.ApplySuppression(amount, suppressionAgent, sourceUser);
        }
    }

    private FireSuppressionAgent ResolveSuppressionAgent()
    {
        switch (extinguisherType)
        {
            case FireExtinguisherType.Water:
                return FireSuppressionAgent.Water;
            case FireExtinguisherType.CO2:
                return FireSuppressionAgent.CO2;
            default:
                return FireSuppressionAgent.DryChemical;
        }
    }

    private static Fire FindFire(Collider collider)
    {
        if (collider.TryGetComponent(out Fire direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out Fire rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out Fire parentFire))
        {
            return parentFire;
        }

        return null;
    }

    private static Vector3 GetClosestPointSafe(Collider collider, Vector3 position)
    {
        if (collider == null)
        {
            return position;
        }

        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            return collider.ClosestPoint(position);
        }

        if (collider is MeshCollider meshCollider && meshCollider.convex)
        {
            return collider.ClosestPoint(position);
        }

        return collider.bounds.ClosestPoint(position);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        maxCharge = Mathf.Max(0f, maxCharge);
        rechargePerSecond = Mathf.Max(0f, rechargePerSecond);
        minChargeToUse = Mathf.Clamp(minChargeToUse, 0f, maxCharge);
        playerDischargePerSecond = Mathf.Max(0f, playerDischargePerSecond);
        botDischargePerSecond = Mathf.Max(0f, botDischargePerSecond);
        maxSprayDistance = Mathf.Max(0f, maxSprayDistance);
        coneHalfAngle = Mathf.Clamp(coneHalfAngle, 0f, 89f);
        coneBaseRadius = Mathf.Max(0f, coneBaseRadius);
        coneSegments = Mathf.Max(1, coneSegments);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawConeGizmo || drawGizmoOnlyWhenSelected)
        {
            return;
        }

        DrawConeGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawConeGizmo)
        {
            return;
        }

        DrawConeGizmo();
    }
#endif

    private void DrawConeGizmo()
    {
        Transform origin = sprayParticles != null ? sprayParticles.transform : transform;
        if (origin == null || maxSprayDistance <= 0f)
        {
            return;
        }

        Vector3 start = origin.position;
        Vector3 forward = origin.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        int segments = Mathf.Max(1, coneSegments);
        float segmentLength = maxSprayDistance / segments;
        Gizmos.color = new Color(0.85f, 0.95f, 1f, 0.9f);

        Vector3 previousCenter = start;
        float previousRadius = coneBaseRadius;

        for (int i = 0; i < segments; i++)
        {
            float distance = segmentLength * (i + 1);
            float radius = coneBaseRadius + Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * distance;
            Vector3 center = start + forward * distance;

            Gizmos.DrawWireSphere(center, radius);
            Gizmos.DrawLine(previousCenter + origin.right * previousRadius, center + origin.right * radius);
            Gizmos.DrawLine(previousCenter - origin.right * previousRadius, center - origin.right * radius);
            Gizmos.DrawLine(previousCenter + origin.up * previousRadius, center + origin.up * radius);
            Gizmos.DrawLine(previousCenter - origin.up * previousRadius, center - origin.up * radius);

            previousCenter = center;
            previousRadius = radius;
        }

        Gizmos.DrawRay(start, forward * maxSprayDistance);
    }
}
