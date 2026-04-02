using System.Collections.Generic;
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
    [Range(0f, 1f)]
    [SerializeField] private float startSmokeDensity;
    [SerializeField] private float smokePerBurningFire = 0.3f;
    [SerializeField] private float smokePerFireIntensity = 0.7f;
    [SerializeField] private float passiveVentilationRelief = 0.05f;
    [SerializeField] private float smokeAccumulationRate = 0.75f;
    [SerializeField] private float smokeDissipationRate = 1f;
    [SerializeField] private float fireDraftBoostPerSecond = 0.2f;

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

    private readonly HashSet<int> processedTargets = new HashSet<int>();
    private int processedFrame = -1;

    public float CurrentSmokeDensity => currentSmokeDensity;
    public float CurrentVisibilityPenalty => currentSmokeDensity * maxVisibilityPenalty;

    private void Awake()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveLinkedObjects();
    }

    private void Reset()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveLinkedObjects();
        currentSmokeDensity = startSmokeDensity;
    }

    private void OnValidate()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        ResolveLinkedObjects();
        startSmokeDensity = Mathf.Clamp01(startSmokeDensity);
        smokePerBurningFire = Mathf.Max(0f, smokePerBurningFire);
        smokePerFireIntensity = Mathf.Max(0f, smokePerFireIntensity);
        passiveVentilationRelief = Mathf.Max(0f, passiveVentilationRelief);
        smokeAccumulationRate = Mathf.Max(0f, smokeAccumulationRate);
        smokeDissipationRate = Mathf.Max(0f, smokeDissipationRate);
        fireDraftBoostPerSecond = Mathf.Max(0f, fireDraftBoostPerSecond);
        minimumDangerousDensity = Mathf.Clamp01(minimumDangerousDensity);
        oxygenDrainPerSecond = Mathf.Max(0f, oxygenDrainPerSecond);
        victimConditionDamagePerSecond = Mathf.Max(0f, victimConditionDamagePerSecond);
        maxVisibilityPenalty = Mathf.Clamp01(maxVisibilityPenalty);
        currentSmokeDensity = Mathf.Clamp01(currentSmokeDensity);
    }

    private void OnEnable()
    {
        currentSmokeDensity = Mathf.Clamp01(startSmokeDensity);
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
        density -= GetVentilationRelief();
        return Mathf.Clamp01(density);
    }

    private void ApplySmokeEffects(Collider other, float deltaTime)
    {
        if (currentSmokeDensity < minimumDangerousDensity)
            return;

        float scaledDeltaTime = Mathf.Max(0f, deltaTime);
        float effectScale = currentSmokeDensity;

        if (affectPlayers)
        {
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null && processedTargets.Add(vitals.GetInstanceID()))
                vitals.ConsumeOxygen(oxygenDrainPerSecond * effectScale * scaledDeltaTime);
        }

        if (affectVictims)
        {
            VictimCondition victim = other.GetComponentInParent<VictimCondition>();
            if (victim != null && processedTargets.Add(victim.GetInstanceID()))
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

    private void ResolveSupportComponents()
    {
        if (triggerBody == null)
            triggerBody = GetComponent<Rigidbody>();

        if (triggerBody == null)
            triggerBody = gameObject.AddComponent<Rigidbody>();

        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
    }

    private void ResolveLinkedObjects()
    {
        if (autoCollectChildFires && (linkedFires == null || linkedFires.Length == 0))
            linkedFires = CollectAutoFires();

        if (autoCollectChildVentPoints && (linkedVentPoints == null || linkedVentPoints.Length == 0))
            linkedVentPoints = CollectAutoVentPoints();
    }

    private void ApplyVentilationDraftToLinkedFires(float deltaTime)
    {
        if (linkedFires == null || linkedFires.Length == 0 || fireDraftBoostPerSecond <= 0f)
            return;

        float draftRisk = GetVentilationDraftRisk();
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

    private float GetVentilationRelief()
    {
        float relief = 0f;
        if (linkedVentPoints == null)
            return relief;

        for (int i = 0; i < linkedVentPoints.Length; i++)
        {
            if (linkedVentPoints[i] is ISmokeVentPoint ventPoint)
                relief += Mathf.Max(0f, ventPoint.SmokeVentilationRelief);
        }

        return relief;
    }

    private float GetVentilationDraftRisk()
    {
        float risk = 0f;
        if (linkedVentPoints == null)
            return risk;

        for (int i = 0; i < linkedVentPoints.Length; i++)
        {
            if (linkedVentPoints[i] is ISmokeVentPoint ventPoint)
                risk += Mathf.Max(0f, ventPoint.FireDraftRisk);
        }

        return risk;
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
        Fire[] fires = FindObjectsByType<Fire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
