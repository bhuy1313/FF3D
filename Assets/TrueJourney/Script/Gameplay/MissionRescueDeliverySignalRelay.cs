using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class MissionRescueDeliverySignalRelay : MonoBehaviour, IMissionSignalResettable
{
    [SerializeField] private Rescuable rescuable;
    [SerializeField] private SafeZone safeZone;
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string signalKey = "deliver-target";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool hasRaised;

    private void OnEnable()
    {
        ResolveReferences();
        if (rescuable != null)
        {
            rescuable.RescueCompleted += HandleRescueCompleted;
        }
    }

    private void OnDisable()
    {
        if (rescuable != null)
        {
            rescuable.RescueCompleted -= HandleRescueCompleted;
        }
    }

    private void HandleRescueCompleted()
    {
        if (triggerOnce && hasRaised)
        {
            return;
        }

        ResolveReferences();
        if (rescuable == null || safeZone == null || missionSystem == null || string.IsNullOrWhiteSpace(signalKey))
        {
            return;
        }

        if (!safeZone.ContainsPoint(rescuable.transform.position))
        {
            return;
        }

        if (missionSystem.NotifySignal(signalKey))
        {
            hasRaised = true;
        }
    }

    private void ResolveReferences()
    {
        if (rescuable == null)
        {
            rescuable = GetComponent<Rescuable>();
        }

        if (safeZone == null)
        {
            safeZone = FindFirstObjectByType<SafeZone>();
        }

        if (missionSystem == null)
        {
            missionSystem = FindFirstObjectByType<IncidentMissionSystem>();
        }
    }

    public void ResetMissionSignalState()
    {
        hasRaised = false;
    }
}
