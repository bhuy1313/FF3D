using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FireHose : MonoBehaviour, IInteractable, IPickupable, IUsable, IBotExtinguisherItem, IMovementWeightSource
{
    private enum SprayPattern
    {
        StraightStream,
        Fog
    }

    private struct SprayPatternConfig
    {
        public float rangeMultiplier;
        public float radiusMultiplier;
        public float effectivenessMultiplier;
        public float vfxSpeedMultiplier;
        public float vfxSpreadMultiplier;
    }

    [Header("Water Supply")]
    [SerializeField] private float maxWater = 0f;
    [SerializeField] private float rechargePerSecond = 0f;
    [SerializeField] private float minWaterToUse = 0.05f;
    [SerializeField] private bool toggleUse = true;
    [SerializeField] private bool requiresConnectionToSpray;
    [SerializeField] private bool keepConnectionOnDrop;
    [SerializeField] private float movementWeightKg = 18f;

    [Header("Suppression")]
    [SerializeField] private Transform sprayOrigin;
    [SerializeField] private float sprayRange = 12f;
    [SerializeField] private float sprayRadius = 0.35f;
    [SerializeField] private LayerMask sprayMask = ~0;
    [SerializeField] private float playerApplyWaterPerSecond = 1.5f;
    [SerializeField] private float botApplyWaterPerSecond = 1.5f;

    [Header("Spray Control")]
    [SerializeField] private SprayPattern sprayPattern = SprayPattern.StraightStream;
    [SerializeField] private float pressureMultiplier = 1f;
    [SerializeField] private float minPressureMultiplier = 0.5f;
    [SerializeField] private float maxPressureMultiplier = 1.75f;
    [SerializeField] private float pressureStep = 0.25f;
    [SerializeField] private float localSupplyPressureMultiplier = 1f;
    [SerializeField] private float minimumEffectivePressureToSpray = 0.15f;
    [SerializeField] private bool allowRuntimeTuning = true;
    [SerializeField] private bool tuningOnlyWhileSpraying = true;
    [SerializeField] private bool showTuningLogs = false;

    [Header("Straight Stream")]
    [FormerlySerializedAs("concentratedRangeMultiplier")]
    [SerializeField] private float straightStreamRangeMultiplier = 1.25f;
    [FormerlySerializedAs("concentratedRadiusMultiplier")]
    [SerializeField] private float straightStreamRadiusMultiplier = 0.6f;
    [FormerlySerializedAs("concentratedEffectivenessMultiplier")]
    [SerializeField] private float straightStreamEffectivenessMultiplier = 1.35f;
    [FormerlySerializedAs("concentratedVfxSpeedMultiplier")]
    [SerializeField] private float straightStreamVfxSpeedMultiplier = 1.2f;
    [FormerlySerializedAs("concentratedVfxSpreadMultiplier")]
    [SerializeField] private float straightStreamVfxSpreadMultiplier = 0.5f;

    [Header("Fog Pattern")]
    [FormerlySerializedAs("wideRangeMultiplier")]
    [SerializeField] private float fogRangeMultiplier = 0.8f;
    [FormerlySerializedAs("wideRadiusMultiplier")]
    [SerializeField] private float fogRadiusMultiplier = 1.7f;
    [FormerlySerializedAs("wideEffectivenessMultiplier")]
    [SerializeField] private float fogEffectivenessMultiplier = 0.75f;
    [FormerlySerializedAs("wideVfxSpeedMultiplier")]
    [SerializeField] private float fogVfxSpeedMultiplier = 0.9f;
    [FormerlySerializedAs("wideVfxSpreadMultiplier")]
    [SerializeField] private float fogVfxSpreadMultiplier = 1.45f;

    [Header("References")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private AudioSource sprayAudio;
    [SerializeField] private bool enableParticle = true;
    [SerializeField] private float particleGravityModifier = 0.35f;
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private bool setWaterTag = true;
    [SerializeField] private bool ignoreBotLayerInParticleCollision = true;
    [SerializeField] private bool removeParticleBounce = true;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentWater;
    [SerializeField] private bool isSpraying;
    [SerializeField] private float currentApplyWaterRate;
    [SerializeField] private float currentSprayRange;
    [SerializeField] private float currentSprayRadius;
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private GameObject currentUser;
    [SerializeField] private bool currentUserIsBot;
    [SerializeField] private GameObject claimOwner;
    [SerializeField] private FireHoseConnectionPoint currentConnectionPoint;

    [Header("Ballistics")]
    [Tooltip("Initial velocity of the water stream")]
    [SerializeField] private float arcVelocity = 15f;
    [Tooltip("Gravity drop multiplier for the water stream")]
    [SerializeField] private float arcGravityMultiplier = 1f;
    [Tooltip("How long the water travels in seconds before disappearing")]
    [SerializeField] private float arcLifetime = 1.0f;
    [Tooltip("Number of SphereCast segments along the parabolic curve")]
    [SerializeField] private int arcSegments = 8;

    [Header("Bot AI")]
    [Tooltip("Optional stand-off distance override for bots. Set to 0 to derive automatically from current spray range.")]
    [FormerlySerializedAs("botPreferredSprayDistance")]
    [SerializeField] private float botStandDistanceOverride = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawArcGizmo = true;
    [FormerlySerializedAs("drawOnlyWhenSelected")]
    [SerializeField] private bool drawGizmoOnlyWhenSelected = true;

    public float CurrentApplyWaterRate => currentApplyWaterRate;
    public float ApplyWaterPerSecond => currentApplyWaterRate;
    public FireSuppressionAgent SuppressionAgent => FireSuppressionAgent.Water;
    public bool IsSpraying => isSpraying;
    public float PreferredSprayDistance => botStandDistanceOverride > 0f
        ? Mathf.Min(botStandDistanceOverride, MaxSprayDistance)
        : Mathf.Max(4f, currentSprayRange * 0.75f);
    public float MaxSprayDistance => Mathf.Max(0.1f, currentSprayRange);
    public float MaxVerticalReach => Mathf.Max(2f, currentSprayRange);
    public float BallisticLaunchSpeed => arcVelocity;
    public float BallisticGravityMultiplier => arcGravityMultiplier;
    public bool RequiresPreciseAim => true;
    public bool HasUsableCharge => CanStartSpraying();
    public bool IsHeld => currentHolder != null;
    public GameObject ClaimOwner => claimOwner;
    public bool IsConnectedToSupply => connectionState.IsConnected;

    private Rigidbody cachedRigidbody;
    private readonly FireHoseConnectionState connectionState = new FireHoseConnectionState();
    private bool particleDefaultsCached;
    private float baseParticleStartSpeedMultiplier = 1f;
    private float baseParticleShapeAngle = 25f;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (waterParticles == null && sprayOrigin != null)
        {
            waterParticles = sprayOrigin.GetComponentInChildren<ParticleSystem>(true);
        }

        if (waterParticles == null)
        {
            waterParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (sprayOrigin == null && waterParticles != null)
        {
            sprayOrigin = waterParticles.transform;
        }

        AlignWaterVfxToSprayOrigin();
        if (waterParticles == null)
        {
            Debug.LogWarning("FireHose has no water ParticleSystem assigned/found.", this);
        }
        else
        {
            ConfigureWaterParticleCollision();
        }

        if (sprayAudio == null)
        {
            sprayAudio = GetComponentInChildren<AudioSource>();
        }

        if (maxWater > 0f)
        {
            currentWater = Mathf.Clamp(currentWater <= 0f ? maxWater : currentWater, 0f, maxWater);
        }

        ClampSpraySettings();
        RecalculateSprayRuntimeValues();
        CacheWaterParticleDefaults();
        ApplySprayTuningToVfx();

        SetSprayState(false);
        TryApplyWaterTag();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterExtinguisherItem(this);
    }

    private void Update()
    {
        HandleRuntimeTuningInput();
        RecalculateSprayRuntimeValues();

        if (!enableParticle && waterParticles != null && waterParticles.isPlaying)
        {
            waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (isSpraying)
        {
            if (!CanStartSpraying())
            {
                SetSprayState(false);
                return;
            }

            if (maxWater > 0f && !HasConnectedPressurizedSupply())
            {
                currentWater = Mathf.Max(0f, currentWater - currentApplyWaterRate * Time.deltaTime);
                if (currentWater <= 0f)
                {
                    SetSprayState(false);
                    return;
                }
            }

            if (!currentUserIsBot)
            {
                SprayWaterArc();
            }
        }
        else if (maxWater > 0f && currentWater < maxWater)
        {
            float passiveRechargeRate = rechargePerSecond;
            if (currentConnectionPoint != null)
            {
                passiveRechargeRate += currentConnectionPoint.RefillInternalTankPerSecond;
            }

            if (passiveRechargeRate > 0f)
            {
                currentWater = Mathf.Min(maxWater, currentWater + passiveRechargeRate * Time.deltaTime);
            }
        }
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
        claimOwner = picker;
        currentUser = null;
        currentUserIsBot = false;
        AlignWaterVfxToSprayOrigin();
        ConfigureWaterParticleCollision();
        CacheWaterParticleDefaults();
        ApplySprayTuningToVfx();
    }

    public void OnDrop(GameObject dropper)
    {
        currentHolder = null;
        if (claimOwner == dropper)
        {
            claimOwner = null;
        }

        currentUser = null;
        currentUserIsBot = false;
        SetSprayState(false);
        if (!keepConnectionOnDrop)
        {
            DisconnectFromSupply();
        }
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
                currentUserIsBot = false;
                SetSprayState(false);
                return;
            }

            if (CanStartSpraying())
            {
                SetCurrentUser(user);
                SetSprayState(true);
            }

            return;
        }

        if (CanStartSpraying())
        {
            SetCurrentUser(user);
            SetSprayState(true);
        }
    }

    public void SetExternalSprayState(bool enable, GameObject user)
    {
        if (!enable)
        {
            currentUser = null;
            currentUserIsBot = false;
            ClearExternalAimDirection(user);
            SetSprayState(false);
            return;
        }

        if (!CanStartSpraying())
        {
            currentUser = null;
            currentUserIsBot = false;
            SetSprayState(false);
            return;
        }

        SetCurrentUser(user);
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
        DisconnectFromSupply();
        currentUser = null;
        currentUserIsBot = false;
        SetSprayState(false);
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        ClampSpraySettings();
        RecalculateSprayRuntimeValues();

        if (!Application.isPlaying)
        {
            return;
        }

        if (!particleDefaultsCached)
        {
            CacheWaterParticleDefaults();
        }

        ApplySprayTuningToVfx();
    }

    public bool TryConnectToSupply(FireHoseConnectionPoint connectionPoint)
    {
        if (connectionPoint == null)
        {
            return false;
        }

        if (ReferenceEquals(currentConnectionPoint, connectionPoint))
        {
            return true;
        }

        if (!connectionPoint.TryRegisterConnection(this))
        {
            return false;
        }

        FireHoseConnectionPoint previousConnectionPoint = currentConnectionPoint;
        currentConnectionPoint = connectionPoint;
        connectionState.TryConnect(connectionPoint);
        previousConnectionPoint?.ClearConnection(this);
        RecalculateSprayRuntimeValues();
        return true;
    }

    public bool DisconnectFromSupply(FireHoseConnectionPoint expectedConnectionPoint = null)
    {
        if (!connectionState.IsConnected || currentConnectionPoint == null)
        {
            return false;
        }

        if (expectedConnectionPoint != null && !ReferenceEquals(currentConnectionPoint, expectedConnectionPoint))
        {
            return false;
        }

        FireHoseConnectionPoint previousConnectionPoint = currentConnectionPoint;
        currentConnectionPoint = null;
        connectionState.TryDisconnect(previousConnectionPoint);
        previousConnectionPoint.ClearConnection(this);
        RecalculateSprayRuntimeValues();

        if (isSpraying && !CanStartSpraying())
        {
            currentUser = null;
            currentUserIsBot = false;
            SetSprayState(false);
        }

        return true;
    }

    private bool HasLocalUsableWater()
    {
        if (maxWater <= 0f)
        {
            return !requiresConnectionToSpray;
        }

        return currentWater >= minWaterToUse;
    }

    private bool CanStartSpraying()
    {
        return EvaluateSprayAvailability(out _);
    }

    private bool HasConnectedPressurizedSupply()
    {
        return currentConnectionPoint != null && currentConnectionPoint.ProvidesPressurizedWater;
    }

    private float GetLocalSupplyPressureMultiplier()
    {
        if (!HasLocalUsableWater())
        {
            return 0f;
        }

        return Mathf.Max(0f, localSupplyPressureMultiplier);
    }

    private float GetConnectedSupplyPressureMultiplier()
    {
        return currentConnectionPoint != null
            ? currentConnectionPoint.SupplyPressureMultiplier
            : 0f;
    }

    private float GetActiveSourcePressureMultiplier()
    {
        if (HasConnectedPressurizedSupply())
        {
            return GetConnectedSupplyPressureMultiplier();
        }

        if (!requiresConnectionToSpray)
        {
            return GetLocalSupplyPressureMultiplier();
        }

        return 0f;
    }

    private float GetEffectivePressureMultiplier(float sourcePressureMultiplier)
    {
        return Mathf.Max(0f, pressureMultiplier) * Mathf.Max(0f, sourcePressureMultiplier);
    }

    private bool EvaluateSprayAvailability(out string reason)
    {
        float sourcePressureMultiplier = GetActiveSourcePressureMultiplier();
        float effectivePressureMultiplier = GetEffectivePressureMultiplier(sourcePressureMultiplier);

        if (sourcePressureMultiplier > 0f)
        {
            if (effectivePressureMultiplier >= minimumEffectivePressureToSpray)
            {
                reason = HasConnectedPressurizedSupply()
                    ? "Connected to pressurized water"
                    : maxWater > 0f
                        ? "Using internal water reserve"
                        : "Using local water supply";
                return true;
            }

            reason = "Supply pressure too low";
            return false;
        }

        if (currentConnectionPoint != null && !currentConnectionPoint.ProvidesPressurizedWater)
        {
            reason = "Connected source has no pressurized water";
            return false;
        }

        if (requiresConnectionToSpray)
        {
            reason = "No hose connection";
            return false;
        }

        if (maxWater > 0f)
        {
            reason = "Internal water depleted";
            return false;
        }

        reason = "No local water supply";
        return false;
    }

    private void SetSprayState(bool enable)
    {
        if (isSpraying == enable)
        {
            return;
        }

        isSpraying = enable;

        if (waterParticles != null)
        {
            if (enable && enableParticle)
            {
                AlignWaterVfxToSprayOrigin();
                ApplySprayTuningToVfx();
                waterParticles.Play();
            }
            else
            {
                waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (sprayAudio != null)
        {
            if (enable)
            {
                if (!sprayAudio.isPlaying)
                {
                    sprayAudio.Play();
                }
            }
            else
            {
                sprayAudio.Stop();
            }
        }
    }



    private void SprayWaterArc()
    {
        if (currentApplyWaterRate <= 0f) return;

        Transform origin = sprayOrigin != null ? sprayOrigin : transform;
        Vector3 startPos = origin.position;
        Vector3 initialVelocity = origin.forward * arcVelocity;
        Vector3 gravity = Physics.gravity * arcGravityMultiplier;
        float effectiveLifetime = GetEffectiveArcLifetime(startPos, initialVelocity, gravity);
        int segments = GetEffectiveArcSegments(effectiveLifetime);

        if (effectiveLifetime <= 0f || segments <= 0)
        {
            return;
        }

        float amount = currentApplyWaterRate * Time.deltaTime;
        float timeStep = effectiveLifetime / segments;

        System.Collections.Generic.HashSet<FireGroup> processedGroups = new System.Collections.Generic.HashSet<FireGroup>();
        System.Collections.Generic.HashSet<Fire> processedFires = new System.Collections.Generic.HashSet<Fire>();

        Vector3 currentPos = startPos;

        for (int step = 0; step < segments; step++)
        {
            float t = step * timeStep;
            float nextT = t + timeStep;

            // Calculate next position using kinematic equation
            Vector3 nextPos = startPos + initialVelocity * nextT + 0.5f * gravity * nextT * nextT;

            // Expanding radius over time
            float progress = t / effectiveLifetime;
            float currentRadius = Mathf.Lerp(0.1f, currentSprayRadius, progress);

            Vector3 segmentDir = nextPos - currentPos;
            float segmentDistance = segmentDir.magnitude;

            if (segmentDistance > 0.001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(
                    currentPos,
                    currentRadius,
                    segmentDir.normalized,
                    segmentDistance,
                    sprayMask,
                    QueryTriggerInteraction.Collide);

                for (int i = 0; i < hits.Length; i++)
                {
                    ApplyWaterToColliderSafe(hits[i].collider, amount, processedGroups, processedFires);
                }
            }

            currentPos = nextPos;
        }
    }

    private int GetEffectiveArcSegments(float effectiveLifetime)
    {
        if (effectiveLifetime <= 0f)
        {
            return 0;
        }

        float lifetimeRatio = arcLifetime > 0f ? effectiveLifetime / arcLifetime : 1f;
        return Mathf.Max(1, Mathf.CeilToInt(arcSegments * lifetimeRatio));
    }

    private float GetEffectiveArcLifetime(Vector3 startPos, Vector3 initialVelocity, Vector3 gravity)
    {
        float maxLifetime = Mathf.Max(0.01f, arcLifetime);
        float desiredRange = Mathf.Max(0.01f, currentSprayRange);
        int samples = Mathf.Max(8, arcSegments * 4);

        Vector3 previousPos = startPos;
        float travelledDistance = 0f;

        for (int sample = 1; sample <= samples; sample++)
        {
            float sampleT = maxLifetime * sample / samples;
            Vector3 samplePos = startPos + initialVelocity * sampleT + 0.5f * gravity * sampleT * sampleT;
            float segmentDistance = Vector3.Distance(previousPos, samplePos);

            if (travelledDistance + segmentDistance >= desiredRange)
            {
                if (segmentDistance <= Mathf.Epsilon)
                {
                    return sampleT;
                }

                float overshootRatio = (desiredRange - travelledDistance) / segmentDistance;
                float previousT = maxLifetime * (sample - 1) / samples;
                return Mathf.Lerp(previousT, sampleT, Mathf.Clamp01(overshootRatio));
            }

            travelledDistance += segmentDistance;
            previousPos = samplePos;
        }

        return maxLifetime;
    }

    private static void ApplyWaterToColliderSafe(
        Collider collider,
        float amount,
        System.Collections.Generic.HashSet<FireGroup> processedGroups,
        System.Collections.Generic.HashSet<Fire> processedFires)
    {
        if (collider == null)
            return;

        FireGroup fireGroup = FindFireGroup(collider);
        if (fireGroup != null && processedGroups.Add(fireGroup))
        {
            fireGroup.ApplyWater(amount);
            return;
        }

        Fire fire = FindFire(collider);
        if (fire != null && processedFires.Add(fire))
            fire.ApplySuppression(amount, FireSuppressionAgent.Water);
    }

    private static FireGroup FindFireGroup(Collider collider)
    {
        if (collider.TryGetComponent(out FireGroup direct)) return direct;
        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out FireGroup rigidbodyOwner)) return rigidbodyOwner;
        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out FireGroup parentGroup)) return parentGroup;
        return null;
    }

    private static Fire FindFire(Collider collider)
    {
        if (collider.TryGetComponent(out Fire direct))
            return direct;

        if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent(out Fire rigidbodyOwner))
            return rigidbodyOwner;

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out Fire parentFire))
            return parentFire;

        return null;
    }

    private void AlignWaterVfxToSprayOrigin()
    {
        if (sprayOrigin == null || waterParticles == null)
        {
            return;
        }

        Transform particleTransform = waterParticles.transform;
        if (particleTransform.parent != sprayOrigin)
        {
            particleTransform.SetParent(sprayOrigin, false);
        }

        particleTransform.localPosition = Vector3.zero;
        particleTransform.localRotation = Quaternion.identity;
    }

    private void ConfigureWaterParticleCollision()
    {
        if (waterParticles == null)
        {
            return;
        }

        ParticleSystem.CollisionModule collision = waterParticles.collision;
        if (!collision.enabled)
        {
            return;
        }

        if (ignoreBotLayerInParticleCollision)
        {
            int botLayer = LayerMask.NameToLayer("Bot");
            if (botLayer >= 0)
            {
                collision.collidesWith &= ~(1 << botLayer);
            }
        }

        if (!removeParticleBounce)
        {
            return;
        }

        collision.bounce = 0f;
        collision.dampen = 0f;
        collision.lifetimeLoss = 1f;
    }

    private void ClampSpraySettings()
    {
        sprayRange = Mathf.Max(0.1f, sprayRange);
        sprayRadius = Mathf.Max(0.01f, sprayRadius);
        playerApplyWaterPerSecond = Mathf.Max(0f, playerApplyWaterPerSecond);
        botApplyWaterPerSecond = Mathf.Max(0f, botApplyWaterPerSecond);
        particleGravityModifier = Mathf.Max(0f, particleGravityModifier);
        pressureStep = Mathf.Max(0.01f, pressureStep);
        minPressureMultiplier = Mathf.Max(0.1f, minPressureMultiplier);
        maxPressureMultiplier = Mathf.Max(minPressureMultiplier, maxPressureMultiplier);
        pressureMultiplier = Mathf.Clamp(pressureMultiplier, minPressureMultiplier, maxPressureMultiplier);
        localSupplyPressureMultiplier = Mathf.Max(0f, localSupplyPressureMultiplier);
        minimumEffectivePressureToSpray = Mathf.Max(0.01f, minimumEffectivePressureToSpray);

        straightStreamRangeMultiplier = Mathf.Max(0.1f, straightStreamRangeMultiplier);
        straightStreamRadiusMultiplier = Mathf.Max(0.05f, straightStreamRadiusMultiplier);
        straightStreamEffectivenessMultiplier = Mathf.Max(0f, straightStreamEffectivenessMultiplier);
        straightStreamVfxSpeedMultiplier = Mathf.Max(0.1f, straightStreamVfxSpeedMultiplier);
        straightStreamVfxSpreadMultiplier = Mathf.Max(0.05f, straightStreamVfxSpreadMultiplier);

        fogRangeMultiplier = Mathf.Max(0.1f, fogRangeMultiplier);
        fogRadiusMultiplier = Mathf.Max(0.05f, fogRadiusMultiplier);
        fogEffectivenessMultiplier = Mathf.Max(0f, fogEffectivenessMultiplier);
        fogVfxSpeedMultiplier = Mathf.Max(0.1f, fogVfxSpeedMultiplier);
        fogVfxSpreadMultiplier = Mathf.Max(0.05f, fogVfxSpreadMultiplier);

        arcVelocity = Mathf.Max(0.01f, arcVelocity);
        arcGravityMultiplier = Mathf.Max(0f, arcGravityMultiplier);
        arcLifetime = Mathf.Max(0.01f, arcLifetime);
        arcSegments = Mathf.Max(1, arcSegments);
    }

    private void RecalculateSprayRuntimeValues()
    {
        ClampSpraySettings();
        float sourcePressureMultiplier = GetActiveSourcePressureMultiplier();
        float effectivePressureMultiplier = GetEffectivePressureMultiplier(sourcePressureMultiplier);
        bool canSpray = EvaluateSprayAvailability(out _);

        SprayPatternConfig config = GetCurrentPatternConfig();
        float pressureT = GetPressure01(effectivePressureMultiplier);
        float pressureRangeMultiplier = Mathf.Lerp(0.85f, 1.2f, pressureT);
        float pressureRadiusMultiplier = Mathf.Lerp(1.15f, 0.85f, pressureT);
        float baseApplyWaterPerSecond = currentUserIsBot
            ? botApplyWaterPerSecond
            : playerApplyWaterPerSecond;

        currentApplyWaterRate = canSpray
            ? baseApplyWaterPerSecond * config.effectivenessMultiplier * effectivePressureMultiplier
            : 0f;
        currentSprayRange = sprayRange * config.rangeMultiplier * pressureRangeMultiplier;
        currentSprayRadius = sprayRadius * config.radiusMultiplier * pressureRadiusMultiplier;
    }

    private void SetCurrentUser(GameObject user)
    {
        currentUser = user;
        currentUserIsBot = user != null && user.GetComponentInParent<BotBehaviorContext>() != null;
    }

    private SprayPatternConfig GetCurrentPatternConfig()
    {
        if (sprayPattern == SprayPattern.Fog)
        {
            return new SprayPatternConfig
            {
                rangeMultiplier = fogRangeMultiplier,
                radiusMultiplier = fogRadiusMultiplier,
                effectivenessMultiplier = fogEffectivenessMultiplier,
                vfxSpeedMultiplier = fogVfxSpeedMultiplier,
                vfxSpreadMultiplier = fogVfxSpreadMultiplier
            };
        }

        return new SprayPatternConfig
        {
            rangeMultiplier = straightStreamRangeMultiplier,
            radiusMultiplier = straightStreamRadiusMultiplier,
            effectivenessMultiplier = straightStreamEffectivenessMultiplier,
            vfxSpeedMultiplier = straightStreamVfxSpeedMultiplier,
            vfxSpreadMultiplier = straightStreamVfxSpreadMultiplier
        };
    }

    private float GetPressure01(float effectivePressureMultiplier)
    {
        if (Mathf.Approximately(minPressureMultiplier, maxPressureMultiplier))
        {
            return effectivePressureMultiplier > 0f ? 1f : 0f;
        }

        return Mathf.InverseLerp(minPressureMultiplier, maxPressureMultiplier, effectivePressureMultiplier);
    }

    private void HandleRuntimeTuningInput()
    {
        if (!allowRuntimeTuning)
        {
            return;
        }

        if (tuningOnlyWhileSpraying && !isSpraying)
        {
            return;
        }

        bool changed = false;

        if (WasIncreasePressurePressedThisFrame())
        {
            pressureMultiplier = Mathf.Clamp(
                pressureMultiplier + pressureStep,
                minPressureMultiplier,
                maxPressureMultiplier);
            changed = true;
        }

        if (WasDecreasePressurePressedThisFrame())
        {
            pressureMultiplier = Mathf.Clamp(
                pressureMultiplier - pressureStep,
                minPressureMultiplier,
                maxPressureMultiplier);
            changed = true;
        }

        if (WasTogglePatternPressedThisFrame())
        {
            sprayPattern = sprayPattern == SprayPattern.StraightStream
                ? SprayPattern.Fog
                : SprayPattern.StraightStream;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RecalculateSprayRuntimeValues();
        ApplySprayTuningToVfx();

        if (showTuningLogs)
        {
            Debug.Log(
                $"FireHose tune -> Pattern: {sprayPattern}, Pressure: {pressureMultiplier:0.00}x",
                this);
        }
    }

    private bool WasTogglePatternPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (WasInputActionPressedThisFrame("ToggleSprayPattern"))
        {
            return true;
        }

        if (!HasInputActionBinding("ToggleSprayPattern"))
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.V))
        {
            return true;
        }
#endif

        return false;
    }

    private bool WasIncreasePressurePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (WasInputActionPressedThisFrame("IncreasePressure"))
        {
            return true;
        }

        if (!HasInputActionBinding("IncreasePressure"))
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.rightBracketKey.wasPressedThisFrame || keyboard.equalsKey.wasPressedThisFrame))
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals))
        {
            return true;
        }
#endif

        return false;
    }

    private bool WasDecreasePressurePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (WasInputActionPressedThisFrame("DecreasePressure"))
        {
            return true;
        }

        if (!HasInputActionBinding("DecreasePressure"))
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.leftBracketKey.wasPressedThisFrame || keyboard.minusKey.wasPressedThisFrame))
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus))
        {
            return true;
        }
#endif

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private bool WasInputActionPressedThisFrame(string actionName)
    {
        PlayerInput input = ResolveRuntimePlayerInput();
        if (input == null || input.actions == null)
        {
            return false;
        }

        InputAction action = input.actions.FindAction(actionName, throwIfNotFound: false);
        return action != null && action.WasPressedThisFrame();
    }

    private bool HasInputActionBinding(string actionName)
    {
        PlayerInput input = ResolveRuntimePlayerInput();
        if (input == null || input.actions == null)
        {
            return false;
        }

        return input.actions.FindAction(actionName, throwIfNotFound: false) != null;
    }

    private PlayerInput ResolveRuntimePlayerInput()
    {
        if (currentUser != null)
        {
            PlayerInput userInput = currentUser.GetComponentInParent<PlayerInput>();
            if (userInput != null)
            {
                return userInput;
            }
        }

        if (currentHolder != null)
        {
            PlayerInput holderInput = currentHolder.GetComponentInParent<PlayerInput>();
            if (holderInput != null)
            {
                return holderInput;
            }
        }

        return null;
    }
#endif

    private void CacheWaterParticleDefaults()
    {
        particleDefaultsCached = false;
        if (waterParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = waterParticles.main;
        baseParticleStartSpeedMultiplier = Mathf.Max(0.01f, main.startSpeedMultiplier);

        ParticleSystem.ShapeModule shape = waterParticles.shape;
        baseParticleShapeAngle = Mathf.Max(0.01f, shape.angle);
        particleDefaultsCached = true;
    }

    private void ApplySprayTuningToVfx()
    {
        if (waterParticles == null || !particleDefaultsCached)
        {
            return;
        }

        SprayPatternConfig config = GetCurrentPatternConfig();
        float effectivePressureMultiplier = GetEffectivePressureMultiplier(GetActiveSourcePressureMultiplier());
        float pressureT = GetPressure01(effectivePressureMultiplier);
        float pressureSpeedMultiplier = Mathf.Lerp(0.8f, 1.25f, pressureT);
        float pressureSpreadMultiplier = Mathf.Lerp(1.1f, 0.85f, pressureT);

        ParticleSystem.MainModule main = waterParticles.main;
        main.startSpeedMultiplier =
            baseParticleStartSpeedMultiplier * config.vfxSpeedMultiplier * pressureSpeedMultiplier;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(particleGravityModifier);

        ParticleSystem.ShapeModule shape = waterParticles.shape;
        shape.angle = baseParticleShapeAngle * config.vfxSpreadMultiplier * pressureSpreadMultiplier;
    }



    private void TryApplyWaterTag()
    {
        if (!setWaterTag || waterParticles == null || string.IsNullOrEmpty(waterTag))
        {
            return;
        }

        if (waterParticles.CompareTag(waterTag))
        {
            return;
        }

        try
        {
            waterParticles.gameObject.tag = waterTag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{waterTag}' not found. Add it in Tag Manager to enable water detection.", this);
        }
    }

#if UNITY_EDITOR
private void OnDrawGizmos()
{
    if (!drawArcGizmo || drawGizmoOnlyWhenSelected) return;
    DrawSprayArcGizmoDetailed();
}

private void OnDrawGizmosSelected()
{
    if (!drawArcGizmo) return;
    DrawSprayArcGizmoDetailed();
}
#endif

    private void DrawSprayArcGizmoDetailed()
    {
        Transform origin = sprayOrigin != null ? sprayOrigin : transform;
        if (origin == null) return;

        Vector3 startPos = origin.position;
        Vector3 initialVelocity = origin.forward * arcVelocity;
        Vector3 gravity = Physics.gravity * arcGravityMultiplier;
        float lifetime = GetEffectiveArcLifetime(startPos, initialVelocity, gravity);
        int segments = GetEffectiveArcSegments(lifetime);

        if (lifetime <= 0f || segments <= 0)
        {
            return;
        }

        float timeStep = lifetime / segments;
        Vector3 currentPos = startPos;

        for (int step = 0; step < segments; step++)
        {
            float t = step * timeStep;
            float nextT = t + timeStep;

            Vector3 nextPos = startPos
                            + initialVelocity * nextT
                            + 0.5f * gravity * nextT * nextT;

            float progress = t / lifetime;
            float radius = Mathf.Lerp(0.1f, currentSprayRadius > 0f ? currentSprayRadius : sprayRadius, progress);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(currentPos, nextPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentPos, radius);
            Gizmos.DrawWireSphere(nextPos, radius);

            currentPos = nextPos;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPos, 0.05f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(currentPos, 0.06f);
    }
}
