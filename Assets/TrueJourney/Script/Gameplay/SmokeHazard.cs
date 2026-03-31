using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SmokeHazard : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Collider triggerZone;
    [SerializeField] private Rigidbody triggerBody;

    [Header("Smoke Sources")]
    [SerializeField] private bool autoCollectChildFires = true;
    [SerializeField] private Fire[] linkedFires = System.Array.Empty<Fire>();
    [SerializeField] private bool autoCollectChildDoors;
    [SerializeField] private Door[] linkedDoors = System.Array.Empty<Door>();
    [SerializeField] private bool autoCollectChildVents;
    [SerializeField] private Vent[] linkedVents = System.Array.Empty<Vent>();

    [Header("Smoke Simulation")]
    [Range(0f, 1f)]
    [SerializeField] private float startSmokeDensity;
    [SerializeField] private float smokePerBurningFire = 0.3f;
    [SerializeField] private float smokePerFireIntensity = 0.7f;
    [SerializeField] private float passiveVentilationRelief = 0.05f;
    [SerializeField] private float smokeAccumulationRate = 0.75f;
    [SerializeField] private float smokeDissipationRate = 1f;

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
        density -= GetDoorVentilationRelief();
        density -= GetVentVentilationRelief();
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
            linkedFires = GetComponentsInChildren<Fire>(true);

        if (autoCollectChildDoors && (linkedDoors == null || linkedDoors.Length == 0))
            linkedDoors = GetComponentsInChildren<Door>(true);

        if (autoCollectChildVents && (linkedVents == null || linkedVents.Length == 0))
            linkedVents = GetComponentsInChildren<Vent>(true);
    }

    private float GetDoorVentilationRelief()
    {
        float relief = 0f;
        if (linkedDoors == null)
            return relief;

        for (int i = 0; i < linkedDoors.Length; i++)
        {
            Door door = linkedDoors[i];
            if (door != null)
                relief += door.SmokeVentilationRelief;
        }

        return relief;
    }

    private float GetVentVentilationRelief()
    {
        float relief = 0f;
        if (linkedVents == null)
            return relief;

        for (int i = 0; i < linkedVents.Length; i++)
        {
            Vent vent = linkedVents[i];
            if (vent != null)
                relief += vent.SmokeVentilationRelief;
        }

        return relief;
    }
}
