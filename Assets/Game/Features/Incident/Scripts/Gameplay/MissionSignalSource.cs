using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class MissionSignalSource : MonoBehaviour, IMissionSignalResettable
{
    [Header("Signal")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string signalKey = "signal";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string requiredTag = "Player";
    [SerializeField] private bool isTriggered;

    [Header("Trigger")]
    [SerializeField] private Collider triggerZone;

    [Header("Events")]
    [SerializeField] private UnityEvent onSignalRaised;

    public string SignalKey => signalKey;
    public bool IsTriggered => isTriggered;

    private void Awake()
    {
        ResolveTriggerZone();
        ResolveMissionSystem();
    }

    private void Reset()
    {
        triggerZone = GetComponent<Collider>();
        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        ResolveTriggerZone();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        RaiseSignal(other.gameObject);
    }

    public void RaiseSignal()
    {
        RaiseSignal(null);
    }

    public void RaiseSignal(GameObject instigator)
    {
        if (triggerOnce && isTriggered)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(requiredTag) && instigator != null && !instigator.CompareTag(requiredTag))
        {
            return;
        }

        ResolveMissionSystem();
        if (missionSystem == null || string.IsNullOrWhiteSpace(signalKey))
        {
            return;
        }

        if (!missionSystem.NotifySignal(signalKey))
        {
            if (triggerOnce)
            {
                isTriggered = true;
            }

            return;
        }

        isTriggered = true;
        onSignalRaised?.Invoke();
    }

    public void ResetMissionSignalState()
    {
        isTriggered = false;
    }

    private void ResolveTriggerZone()
    {
        if (triggerZone == null)
        {
            triggerZone = GetComponent<Collider>();
        }

        if (triggerZone != null && !triggerZone.isTrigger)
        {
            triggerZone.isTrigger = true;
        }
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }
}
