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
    [SerializeField] private bool autoCollectChildFires = true;
    [SerializeField] private Fire[] linkedFires = System.Array.Empty<Fire>();
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

    [Header("Runtime")]
    [Range(0f, 1f)]
    [SerializeField] private float currentSmokeDensity;

    private readonly HashSet<Component> processedTargets = new HashSet<Component>();
    private int processedFrame = -1;

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

    private void Awake()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
    }

    private void Start()
    {
        ResolveLinkedObjects(forceRefresh: true);
    }

    private void Reset()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        currentSmokeDensity = startSmokeDensity;
    }

    private void OnValidate()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        startSmokeDensity = Mathf.Clamp01(startSmokeDensity);
        smokePerBurningFire = Mathf.Max(0f, smokePerBurningFire);
        smokePerFireIntensity = Mathf.Max(0f, smokePerFireIntensity);
        passiveVentilationRelief = Mathf.Max(0f, passiveVentilationRelief);
        smokeAccumulationRate = Mathf.Max(0f, smokeAccumulationRate);
        smokeDissipationRate = Mathf.Max(0f, smokeDissipationRate);
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
        currentSmokeDensity = Mathf.Clamp01(currentSmokeDensity);

        if (forceMaximumSmokeDensity)
        {
            currentSmokeDensity = 1f;
        }
    }

    private void OnEnable()
    {
        currentSmokeDensity = forceMaximumSmokeDensity
            ? 1f
            : Mathf.Clamp01(startSmokeDensity);
    }

    private void Update()
    {
        ApplyVentilationDraftToLinkedFires(Time.deltaTime);
        UpdateSmokeDensity(Time.deltaTime);
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
        float rate = targetDensity >= currentSmokeDensity ? smokeAccumulationRate : smokeDissipationRate;
        currentSmokeDensity = Mathf.MoveTowards(
            currentSmokeDensity,
            targetDensity,
            Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));
    }

    private float CalculateTargetSmokeDensity()
    {
        float density = 0f;
        VentilationResponse ventilation = GetVentilationResponse();

        if (linkedFires != null)
        {
            for (int i = 0; i < linkedFires.Length; i++)
            {
                Fire fire = linkedFires[i];
                if (fire == null || !fire.IsBurning)
                    continue;

                density += smokePerBurningFire + fire.NormalizedHp * smokePerFireIntensity;
            }
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
        if (exposure != null && processedTargets.Add(exposure))
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
        if (autoCollectChildFires && (forceRefresh || linkedFires == null || linkedFires.Length == 0))
            linkedFires = CollectAutoFires();

        if (autoCollectChildVentPoints && (forceRefresh || linkedVentPoints == null || linkedVentPoints.Length == 0))
            linkedVentPoints = CollectAutoVentPoints();
    }

    private void ApplyVentilationDraftToLinkedFires(float deltaTime)
    {
        if (linkedFires == null || linkedFires.Length == 0 || fireDraftBoostPerSecond <= 0f)
            return;

        float draftRisk = GetVentilationResponse().DraftRisk;
        if (draftRisk <= 0f)
            return;

        float delta = Mathf.Max(0f, deltaTime);
        if (delta <= 0f)
            return;

        for (int i = 0; i < linkedFires.Length; i++)
        {
            Fire fire = linkedFires[i];
            if (fire == null || !fire.IsBurning)
                continue;

            float intensityScale = Mathf.Max(0.35f, fire.NormalizedHp);
            fire.Ignite(draftRisk * fireDraftBoostPerSecond * intensityScale * delta);
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

    private Fire[] CollectAutoFires()
    {
        if (autoCollectMode == AutoCollectMode.TriggerVolume)
            return CollectVolumeFires();

        return GetComponentsInChildren<Fire>(true);
    }

    private MonoBehaviour[] CollectAutoVentPoints()
    {
        if (autoCollectMode == AutoCollectMode.TriggerVolume)
            return CollectVolumeVentPoints();

        return CollectChildVentPoints();
    }

    private Fire[] CollectVolumeFires()
    {
        Fire[] fires = FindObjectsByType<Fire>(FindObjectsInactive.Include);
        List<Fire> results = new List<Fire>();
        for (int i = 0; i < fires.Length; i++)
        {
            Fire fire = fires[i];
            if (fire != null && IsComponentWithinTriggerVolume(fire))
                results.Add(fire);
        }

        return results.ToArray();
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
}
