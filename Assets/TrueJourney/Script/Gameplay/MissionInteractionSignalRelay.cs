using UnityEngine;

[DisallowMultipleComponent]
public class MissionInteractionSignalRelay : MonoBehaviour, IMissionSignalResettable
{
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string signalKey = "interact-target";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string requiredInteractorTag = "Player";
    [SerializeField] private bool hasRaised;

    public void NotifyInteracted(GameObject interactor)
    {
        if (triggerOnce && hasRaised)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(requiredInteractorTag) &&
            interactor != null &&
            !interactor.CompareTag(requiredInteractorTag))
        {
            return;
        }

        ResolveMissionSystem();
        if (missionSystem == null || string.IsNullOrWhiteSpace(signalKey))
        {
            return;
        }

        if (missionSystem.NotifySignal(signalKey))
        {
            hasRaised = true;
        }
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    public void ResetMissionSignalState()
    {
        hasRaised = false;
    }
}
