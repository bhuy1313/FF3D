using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SmokeHazard : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Collider triggerZone;
    [SerializeField] private Rigidbody triggerBody;

    [Header("Effects")]
    [SerializeField] private bool affectPlayers = true;
    [SerializeField] private bool affectVictims = true;
    [SerializeField] private float oxygenDrainPerSecond = 12f;
    [SerializeField] private float victimConditionDamagePerSecond = 10f;

    private readonly HashSet<int> processedTargets = new HashSet<int>();
    private int processedFrame = -1;

    private void Awake()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
    }

    private void Reset()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
    }

    private void OnValidate()
    {
        ResolveSupportComponents();
        ResolveTriggerZone();
        oxygenDrainPerSecond = Mathf.Max(0f, oxygenDrainPerSecond);
        victimConditionDamagePerSecond = Mathf.Max(0f, victimConditionDamagePerSecond);
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null)
            return;

        BeginFrameProcessingIfNeeded();

        if (affectPlayers)
        {
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null && processedTargets.Add(vitals.GetInstanceID()))
                vitals.ConsumeOxygen(oxygenDrainPerSecond * Time.deltaTime);
        }

        if (affectVictims)
        {
            VictimCondition victim = other.GetComponentInParent<VictimCondition>();
            if (victim != null && processedTargets.Add(victim.GetInstanceID()))
                victim.ApplySmokeExposure(victimConditionDamagePerSecond * Time.deltaTime);
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
}
