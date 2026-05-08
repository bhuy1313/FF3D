using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SmokeHazard : MonoBehaviour
{
    private enum AutoCollectMode
    {
        ChildHierarchy = 0,
        TriggerVolume = 1
    }

    [Header("Trigger")]
    [SerializeField] private Collider triggerZone;
    [SerializeField] private Rigidbody triggerBody;

    [Header("Smoke Sources")]
    [SerializeField] private AutoCollectMode autoCollectMode = AutoCollectMode.ChildHierarchy;
    [SerializeField] private float autoCollectDelay = 0f;
    [SerializeField] private FireSimulationManager fireSimulationManager;
    [FormerlySerializedAs("autoCollectChildDoors")]
    [FormerlySerializedAs("autoCollectChildVents")]
    [SerializeField] private bool autoCollectChildVentPoints;
    [FormerlySerializedAs("linkedDoors")]
    [FormerlySerializedAs("linkedVents")]
    [SerializeField] private MonoBehaviour[] linkedVentPoints = System.Array.Empty<MonoBehaviour>();

    [Header("Smoke Simulation")]
    [SerializeField] private bool forceMaximumSmokeDensity;
    [Range(0f, 1f)]
    [SerializeField] private float startSmokeDensity;
    [SerializeField] private float runtimeSmokeAccumulationMultiplier = 1f;
    [SerializeField] private float smokePerBurningFire = 0.3f;
    [SerializeField] private float smokePerFireIntensity = 0.7f;
    [SerializeField] private float passiveVentilationRelief = 0.05f;
    [SerializeField] private float smokeAccumulationRate = 0.75f;
    [SerializeField] private float smokeDissipationRate = 1f;
    [SerializeField] private float fireDraftBoostPerSecond = 0.2f;

    [Header("Crouch Relief")]
    [SerializeField] private bool reduceSmokeWhileCrouching = true;
    [Range(0f, 1f)]
    [SerializeField] private float crouchReliefUntilDensity = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float crouchedExposureMultiplier = 0.55f;

    [Header("Ventilation Response")]
    [SerializeField] private bool enhanceCrossVentilation = true;
    [SerializeField] private float extraOpeningReliefBonus = 0.12f;
    [SerializeField] private float extraOpeningDraftBonus = 0.18f;
    [SerializeField] private float maxCrossVentilationBonus = 0.6f;
    [SerializeField] private float doorVentilationMultiplier = 1f;
    [SerializeField] private float windowVentilationMultiplier = 1.15f;
    [SerializeField] private float ventVentilationMultiplier = 1.25f;
    [SerializeField] private float doorDraftMultiplier = 1.15f;
    [SerializeField] private float windowDraftMultiplier = 1.35f;
    [SerializeField] private float ventDraftMultiplier = 0.85f;

    [Header("Effects")]
    [SerializeField] private bool affectPlayers = true;
    [SerializeField] private bool affectVictims = true;
    [Range(0f, 1f)]
    [SerializeField] private float minimumDangerousDensity = 0.05f;
    [SerializeField] private float oxygenDrainPerSecond = 12f;
    [SerializeField] private float victimConditionDamagePerSecond = 10f;
    [Range(0f, 1f)]
    [SerializeField] private float maxVisibilityPenalty = 0.8f;

    [Header("Smoke VFX")]
    [SerializeField] private bool autoConfigureSmokeVfx = true;
    [SerializeField] private ParticleSystem smokeParticleSystem;
    [SerializeField] private Vector3 particleShapePadding = new Vector3(0.15f, 0.05f, 0.15f);
    [SerializeField] private float minimumParticleShapeSize = 0.25f;
    [SerializeField] private float smokeVfxActiveThreshold = 0.0001f;
    [SerializeField] private float smokeVfxDensityExponent = 0.5f;
    [SerializeField] private float smokeVfxMinDensityScaleWhenActive = 0.18f;
    [SerializeField] private int smokeVfxMinMaxParticles = 12;
    [SerializeField] private float smokeVfxMinRateOverTime = 1.5f;

    [Header("Runtime")]
    [Range(0f, 1f)]
    [SerializeField] private float currentSmokeDensity;

    private readonly HashSet<Component> processedTargets = new HashSet<Component>();
    private int processedFrame = -1;
    private bool lastSmokeVfxActiveState;
    private bool smokeVfxParticleBaselineCaptured;
    private int smokeVfxBaseMaxParticles;
    private ParticleSystem.MinMaxCurve smokeVfxBaseRateOverTime;
    private ParticleSystem.MinMaxCurve smokeVfxBaseRateOverDistance;

    private readonly struct VentilationResponse
    {
        public VentilationResponse(float relief, float draftRisk)
        {
            Relief = relief;
            DraftRisk = draftRisk;
        }

        public float Relief { get; }
        public float DraftRisk { get; }
    }

    public float CurrentSmokeDensity => currentSmokeDensity;
    public float CurrentVisibilityPenalty => currentSmokeDensity * maxVisibilityPenalty;
    public Collider TriggerZone => triggerZone;
    public IReadOnlyList<MonoBehaviour> LinkedVentPoints => linkedVentPoints;

    public void SetStartSmokeDensity(float density01, bool applyImmediately)
    {
        startSmokeDensity = Mathf.Clamp01(density01);
        if (forceMaximumSmokeDensity)
        {
            currentSmokeDensity = 1f;
        }
        else if (applyImmediately)
        {
            currentSmokeDensity = startSmokeDensity;
        }

        ApplySmokeVfxParticleDensity();
        UpdateSmokeVfxActiveState();
    }

    public void SetSmokeAccumulationMultiplier(float multiplier)
    {
        runtimeSmokeAccumulationMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetFireSimulationManager(FireSimulationManager manager)
    {
        fireSimulationManager = manager;
    }

    public void RefreshLinkedVentPoints()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveLinkedObjects(forceRefresh: true);
    }

    private void Awake()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveSmokeVfxReferences();
        ApplySmokeVfxConfiguration();
    }

    private void Start()
    {
        if (autoCollectDelay > 0f)
        {
            Invoke(nameof(DelayedAutoCollect), autoCollectDelay);
        }
        else
        {
            DelayedAutoCollect();
        }
    }

    private void DelayedAutoCollect()
    {
        ResolveLinkedObjects(forceRefresh: true);
    }

    private void Reset()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveSmokeVfxReferences();
        currentSmokeDensity = startSmokeDensity;
        ApplySmokeVfxConfiguration();
    }

    private void OnValidate()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveSmokeVfxReferences();
        autoCollectDelay = Mathf.Max(0f, autoCollectDelay);
        startSmokeDensity = Mathf.Clamp01(startSmokeDensity);
        smokePerBurningFire = Mathf.Max(0f, smokePerBurningFire);
        smokePerFireIntensity = Mathf.Max(0f, smokePerFireIntensity);
        passiveVentilationRelief = Mathf.Max(0f, passiveVentilationRelief);
        smokeAccumulationRate = Mathf.Max(0f, smokeAccumulationRate);
        smokeDissipationRate = Mathf.Max(0f, smokeDissipationRate);
        runtimeSmokeAccumulationMultiplier = Mathf.Max(0f, runtimeSmokeAccumulationMultiplier);
        fireDraftBoostPerSecond = Mathf.Max(0f, fireDraftBoostPerSecond);
        crouchReliefUntilDensity = Mathf.Clamp01(crouchReliefUntilDensity);
        crouchedExposureMultiplier = Mathf.Clamp01(crouchedExposureMultiplier);
        extraOpeningReliefBonus = Mathf.Max(0f, extraOpeningReliefBonus);
        extraOpeningDraftBonus = Mathf.Max(0f, extraOpeningDraftBonus);
        maxCrossVentilationBonus = Mathf.Max(0f, maxCrossVentilationBonus);
        doorVentilationMultiplier = Mathf.Max(0f, doorVentilationMultiplier);
        windowVentilationMultiplier = Mathf.Max(0f, windowVentilationMultiplier);
        ventVentilationMultiplier = Mathf.Max(0f, ventVentilationMultiplier);
        doorDraftMultiplier = Mathf.Max(0f, doorDraftMultiplier);
        windowDraftMultiplier = Mathf.Max(0f, windowDraftMultiplier);
        ventDraftMultiplier = Mathf.Max(0f, ventDraftMultiplier);
        minimumDangerousDensity = Mathf.Clamp01(minimumDangerousDensity);
        oxygenDrainPerSecond = Mathf.Max(0f, oxygenDrainPerSecond);
        victimConditionDamagePerSecond = Mathf.Max(0f, victimConditionDamagePerSecond);
        maxVisibilityPenalty = Mathf.Clamp01(maxVisibilityPenalty);
        particleShapePadding = Vector3.Max(Vector3.zero, particleShapePadding);
        minimumParticleShapeSize = Mathf.Max(0.01f, minimumParticleShapeSize);
        currentSmokeDensity = Mathf.Clamp01(currentSmokeDensity);
        smokeVfxDensityExponent = Mathf.Max(0.01f, smokeVfxDensityExponent);
        smokeVfxMinDensityScaleWhenActive = Mathf.Clamp01(smokeVfxMinDensityScaleWhenActive);
        smokeVfxMinRateOverTime = Mathf.Max(0f, smokeVfxMinRateOverTime);

        if (forceMaximumSmokeDensity)
        {
            currentSmokeDensity = 1f;
        }

        smokeVfxActiveThreshold = Mathf.Max(0f, smokeVfxActiveThreshold);
        smokeVfxMinMaxParticles = Mathf.Max(1, smokeVfxMinMaxParticles);
        ApplySmokeVfxConfiguration();
    }

    private void OnEnable()
    {
        currentSmokeDensity = forceMaximumSmokeDensity
            ? 1f
            : Mathf.Clamp01(startSmokeDensity);

        ApplySmokeVfxConfiguration();
        ApplySmokeVfxParticleDensity();
        UpdateSmokeVfxActiveState();
    }

    private void Update()
    {
        ApplyVentilationDraftToLinkedFires(Time.deltaTime);
        UpdateSmokeDensity(Time.deltaTime);
        ApplySmokeVfxParticleDensity();
        UpdateSmokeVfxActiveState();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null)
            return;

        BeginFrameProcessingIfNeeded();
        ApplySmokeEffects(other, Time.deltaTime);
    }

    private void UpdateSmokeDensity(float deltaTime)
    {
        if (forceMaximumSmokeDensity)
        {
            currentSmokeDensity = 1f;
            return;
        }

        float targetDensity = CalculateTargetSmokeDensity();
        float riseRate = smokeAccumulationRate * Mathf.Max(0f, runtimeSmokeAccumulationMultiplier);
        float rate = targetDensity >= currentSmokeDensity ? riseRate : smokeDissipationRate;
        currentSmokeDensity = Mathf.MoveTowards(
            currentSmokeDensity,
            targetDensity,
            Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));
    }

    private float CalculateTargetSmokeDensity()
    {
        float density = 0f;
        VentilationResponse ventilation = GetVentilationResponse();

        if (fireSimulationManager != null && fireSimulationManager.IsInitialized && triggerZone != null)
        {
            Bounds bounds = triggerZone.bounds;
            fireSimulationManager.GetBurningTrackedStats(bounds, out int burningCount, out float intensitySum);
            density += burningCount * smokePerBurningFire;
            density += intensitySum * smokePerFireIntensity;
        }
        density -= passiveVentilationRelief;
        density -= ventilation.Relief;
        return Mathf.Clamp01(density);
    }

    private void ApplySmokeEffects(Collider other, float deltaTime)
    {
        float scaledDeltaTime = Mathf.Max(0f, deltaTime);
        float effectScale = CalculateLocalSmokeEffectScale(other);

        PlayerHazardExposure exposure = other.GetComponentInParent<PlayerHazardExposure>();
        if (exposure != null && effectScale > 0f && processedTargets.Add(exposure))
        {
            exposure.ReportSmokeExposure(effectScale);
        }

        if (effectScale < minimumDangerousDensity)
            return;

        if (affectPlayers)
        {
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null && processedTargets.Add(vitals))
                vitals.ConsumeOxygen(oxygenDrainPerSecond * effectScale * scaledDeltaTime);
        }

        if (affectVictims)
        {
            VictimCondition victim = other.GetComponentInParent<VictimCondition>();
            if (victim != null && processedTargets.Add(victim))
                victim.ApplySmokeExposure(victimConditionDamagePerSecond * effectScale * scaledDeltaTime);
        }
    }

    private void BeginFrameProcessingIfNeeded()
    {
        int frame = Time.frameCount;
        if (processedFrame == frame)
            return;

        processedFrame = frame;
        processedTargets.Clear();
    }

    private void ResolveTriggerZone()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<Collider>();

        if (triggerZone == null)
            triggerZone = gameObject.AddComponent<BoxCollider>();

        if (triggerZone != null && !triggerZone.isTrigger)
            triggerZone.isTrigger = true;
    }

    public void SetTriggerZone(Collider newTriggerZone)
    {
        triggerZone = newTriggerZone;
        ResolveTriggerZone();
        ResolveSupportComponents();
        ApplySmokeVfxConfiguration();
    }

    private void ResolveSupportComponents()
    {
        if (triggerBody == null)
            triggerBody = GetComponent<Rigidbody>();

        if (triggerBody == null)
            triggerBody = gameObject.AddComponent<Rigidbody>();

        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
    }

    [ContextMenu("Refresh Smoke VFX")]
    private void RefreshSmokeVfx()
    {
        ResolveTriggerZone();
        ResolveSmokeVfxReferences();
        ApplySmokeVfxConfiguration();
    }

    [ContextMenu("Refresh Linked Smoke Objects")]
    private void RefreshLinkedObjects()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveLinkedObjects(forceRefresh: true);
    }

    private void ResolveLinkedObjects()
    {
        ResolveLinkedObjects(forceRefresh: false);
    }

    private void ResolveLinkedObjects(bool forceRefresh)
    {
        if (fireSimulationManager == null)
            fireSimulationManager = GetComponentInParent<FireSimulationManager>(true);

        if (fireSimulationManager == null)
        {
            Transform root = transform.root;
            if (root != null)
                fireSimulationManager = root.GetComponentInChildren<FireSimulationManager>(true);
        }

        if (autoCollectChildVentPoints && (forceRefresh || linkedVentPoints == null || linkedVentPoints.Length == 0))
            linkedVentPoints = CollectAutoVentPoints();
    }

    private void ApplyVentilationDraftToLinkedFires(float deltaTime)
    {
        if (fireDraftBoostPerSecond <= 0f)
            return;

        float draftRisk = GetVentilationResponse().DraftRisk;
        if (draftRisk <= 0f)
            return;

        float delta = Mathf.Max(0f, deltaTime);
        if (delta <= 0f)
            return;

        if (fireSimulationManager != null && fireSimulationManager.IsInitialized && triggerZone != null)
        {
            fireSimulationManager.ApplyDraftHeatInBounds(triggerZone.bounds, draftRisk * fireDraftBoostPerSecond * delta);
        }
    }

    private VentilationResponse GetVentilationResponse()
    {
        float relief = 0f;
        float risk = 0f;
        int openingCount = 0;
        if (linkedVentPoints == null)
            return new VentilationResponse(relief, risk);

        for (int i = 0; i < linkedVentPoints.Length; i++)
        {
            if (!(linkedVentPoints[i] is ISmokeVentPoint ventPoint))
                continue;

            float ventRelief = Mathf.Max(0f, ventPoint.SmokeVentilationRelief);
            float ventRisk = Mathf.Max(0f, ventPoint.FireDraftRisk);
            if (ventRelief <= 0f && ventRisk <= 0f)
                continue;

            openingCount++;
            GetVentilationTypeMultipliers(ventPoint, out float reliefMultiplier, out float riskMultiplier);
            relief += ventRelief * reliefMultiplier;
            risk += ventRisk * riskMultiplier;
        }

        if (enhanceCrossVentilation && openingCount > 1)
        {
            float additionalOpenings = openingCount - 1;
            float reliefBonus = Mathf.Min(maxCrossVentilationBonus, additionalOpenings * extraOpeningReliefBonus);
            float draftBonus = Mathf.Min(maxCrossVentilationBonus, additionalOpenings * extraOpeningDraftBonus);
            relief *= 1f + reliefBonus;
            risk *= 1f + draftBonus;
        }

        return new VentilationResponse(relief, risk);
    }

    private void GetVentilationTypeMultipliers(ISmokeVentPoint ventPoint, out float reliefMultiplier, out float riskMultiplier)
    {
        if (ventPoint is Window)
        {
            reliefMultiplier = windowVentilationMultiplier;
            riskMultiplier = windowDraftMultiplier;
            return;
        }

        if (ventPoint is Vent)
        {
            reliefMultiplier = ventVentilationMultiplier;
            riskMultiplier = ventDraftMultiplier;
            return;
        }

        if (ventPoint is Door)
        {
            reliefMultiplier = doorVentilationMultiplier;
            riskMultiplier = doorDraftMultiplier;
            return;
        }

        reliefMultiplier = 1f;
        riskMultiplier = 1f;
    }

    private float CalculateLocalSmokeEffectScale(Collider other)
    {
        if (other == null)
            return 0f;

        float effectScale = currentSmokeDensity;
        if (reduceSmokeWhileCrouching &&
            currentSmokeDensity < crouchReliefUntilDensity &&
            TryGetCrouchingController(other, out _))
        {
            effectScale *= crouchedExposureMultiplier;
        }

        return Mathf.Clamp01(effectScale);
    }

    private bool TryGetCrouchingController(Collider other, out FirstPersonController controller)
    {
        controller = other != null ? other.GetComponentInParent<FirstPersonController>() : null;
        return controller != null && controller.IsCrouching;
    }

    private MonoBehaviour[] CollectAutoVentPoints()
    {
        if (autoCollectMode == AutoCollectMode.TriggerVolume)
            return CollectVolumeVentPoints();

        return CollectChildVentPoints();
    }

    private MonoBehaviour[] CollectVolumeVentPoints()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        List<MonoBehaviour> results = new List<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is ISmokeVentPoint && IsComponentWithinTriggerVolume(behaviour))
                results.Add(behaviour);
        }

        return results.ToArray();
    }

    private MonoBehaviour[] CollectChildVentPoints()
    {
        MonoBehaviour[] childBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
        List<MonoBehaviour> results = new List<MonoBehaviour>();
        for (int i = 0; i < childBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = childBehaviours[i];
            if (behaviour is ISmokeVentPoint)
                results.Add(behaviour);
        }

        return results.ToArray();
    }

    private bool IsComponentWithinTriggerVolume(Component component)
    {
        if (component == null || triggerZone == null)
            return false;

        Collider componentCollider = component.GetComponent<Collider>();
        if (componentCollider != null &&
            componentCollider.enabled &&
            triggerZone.enabled &&
            Physics.ComputePenetration(
                triggerZone,
                triggerZone.transform.position,
                triggerZone.transform.rotation,
                componentCollider,
                componentCollider.transform.position,
                componentCollider.transform.rotation,
                out _,
                out _))
        {
            return true;
        }

        if (IsPointWithinTriggerVolume(component.transform.position))
            return true;

        Renderer renderer = component.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            if (IsPointWithinTriggerVolume(bounds.center))
                return true;
        }

        return false;
    }

    private bool IsPointWithinTriggerVolume(Vector3 point)
    {
        if (triggerZone == null || !triggerZone.bounds.Contains(point))
            return false;

        Vector3 closestPoint = triggerZone.ClosestPoint(point);
        return (closestPoint - point).sqrMagnitude <= 0.0001f;
    }

    private void ResolveSmokeVfxReferences()
    {
        if (smokeParticleSystem == null)
        {
            smokeParticleSystem = GetComponentInChildren<ParticleSystem>(true);
        }

        CaptureSmokeVfxParticleBaseline();
    }

    private void ApplySmokeVfxConfiguration()
    {
        if (!autoConfigureSmokeVfx)
            return;

        ResolveTriggerZone();
        ResolveSmokeVfxReferences();

        if (triggerZone == null || smokeParticleSystem == null)
            return;

        if (!(triggerZone is BoxCollider boxCollider))
            return;

        ConfigureSmokeShape(boxCollider);
    }

    private void UpdateSmokeVfxActiveState()
    {
        if (!Application.isPlaying)
            return;

        ResolveSmokeVfxReferences();
        if (smokeParticleSystem == null)
            return;

        bool shouldBeActive = forceMaximumSmokeDensity || currentSmokeDensity > smokeVfxActiveThreshold;
        if (lastSmokeVfxActiveState == shouldBeActive && smokeParticleSystem.isPlaying == shouldBeActive)
            return;

        lastSmokeVfxActiveState = shouldBeActive;

        if (shouldBeActive)
        {
            if (!smokeParticleSystem.isPlaying)
            {
                smokeParticleSystem.Play(true);
            }

            return;
        }

        smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplySmokeVfxParticleDensity()
    {
        if (!Application.isPlaying)
            return;

        ResolveSmokeVfxReferences();
        if (smokeParticleSystem == null || !smokeVfxParticleBaselineCaptured)
            return;

        bool isActive = forceMaximumSmokeDensity || currentSmokeDensity > smokeVfxActiveThreshold;
        float normalizedDensity = forceMaximumSmokeDensity
            ? 1f
            : Mathf.Pow(Mathf.Clamp01(currentSmokeDensity), smokeVfxDensityExponent);
        if (isActive)
        {
            normalizedDensity = Mathf.Max(smokeVfxMinDensityScaleWhenActive, normalizedDensity);
        }

        ParticleSystem.MainModule main = smokeParticleSystem.main;
        ParticleSystem.EmissionModule emission = smokeParticleSystem.emission;
        int targetMaxParticles = forceMaximumSmokeDensity
            ? smokeVfxBaseMaxParticles
            : Mathf.RoundToInt(Mathf.Lerp(smokeVfxMinMaxParticles, smokeVfxBaseMaxParticles, normalizedDensity));
        main.maxParticles = Mathf.Max(smokeVfxMinMaxParticles, targetMaxParticles);

        float targetRateOverTime = forceMaximumSmokeDensity
            ? GetRepresentativeCurveValue(smokeVfxBaseRateOverTime)
            : Mathf.Lerp(smokeVfxMinRateOverTime, GetRepresentativeCurveValue(smokeVfxBaseRateOverTime), normalizedDensity);
        float baseRateOverTime = Mathf.Max(0.0001f, GetRepresentativeCurveValue(smokeVfxBaseRateOverTime));
        float emissionScale = targetRateOverTime / baseRateOverTime;
        emission.rateOverTime = ScaleMinMaxCurve(smokeVfxBaseRateOverTime, emissionScale);
        emission.rateOverDistance = ScaleMinMaxCurve(smokeVfxBaseRateOverDistance, emissionScale);
    }

    private void CaptureSmokeVfxParticleBaseline()
    {
        if (smokeVfxParticleBaselineCaptured || smokeParticleSystem == null)
            return;

        ParticleSystem.MainModule main = smokeParticleSystem.main;
        ParticleSystem.EmissionModule emission = smokeParticleSystem.emission;
        smokeVfxBaseMaxParticles = Mathf.Max(smokeVfxMinMaxParticles, main.maxParticles);
        smokeVfxBaseRateOverTime = emission.rateOverTime;
        smokeVfxBaseRateOverDistance = emission.rateOverDistance;
        smokeVfxParticleBaselineCaptured = true;
    }

    private static float GetRepresentativeCurveValue(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => (curve.constantMin + curve.constantMax) * 0.5f,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => curve.constant
        };
    }

    private static ParticleSystem.MinMaxCurve ScaleMinMaxCurve(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        source.constant *= multiplier;
        source.constantMin *= multiplier;
        source.constantMax *= multiplier;
        source.curveMultiplier *= multiplier;
        return source;
    }

    private void ConfigureSmokeShape(BoxCollider boxCollider)
    {
        ParticleSystem.ShapeModule shape = smokeParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        Bounds particleLocalBounds = GetColliderBoundsInParticleLocalSpace(boxCollider, smokeParticleSystem.transform);
        Vector3 paddedSize = Vector3.Max(
            particleLocalBounds.size + particleShapePadding,
            Vector3.one * minimumParticleShapeSize);

        shape.position = particleLocalBounds.center;
        shape.scale = paddedSize;
    }

    private static Bounds GetColliderBoundsInParticleLocalSpace(BoxCollider boxCollider, Transform particleTransform)
    {
        Vector3[] corners = GetBoxColliderWorldCorners(boxCollider);
        Vector3 firstPoint = particleTransform.InverseTransformPoint(corners[0]);
        Bounds localBounds = new Bounds(firstPoint, Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(particleTransform.InverseTransformPoint(corners[i]));
        }

        return localBounds;
    }

    private static Vector3[] GetBoxColliderWorldCorners(BoxCollider boxCollider)
    {
        Vector3 center = boxCollider.center;
        Vector3 extents = boxCollider.size * 0.5f;
        Transform boxTransform = boxCollider.transform;

        return new[]
        {
            boxTransform.TransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)),
            boxTransform.TransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z)),
            boxTransform.TransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z)),
            boxTransform.TransformPoint(center + new Vector3(extents.x, extents.y, -extents.z)),
            boxTransform.TransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z)),
            boxTransform.TransformPoint(center + new Vector3(extents.x, -extents.y, extents.z)),
            boxTransform.TransformPoint(center + new Vector3(-extents.x, extents.y, extents.z)),
            boxTransform.TransformPoint(center + new Vector3(extents.x, extents.y, extents.z))
        };
    }

}
