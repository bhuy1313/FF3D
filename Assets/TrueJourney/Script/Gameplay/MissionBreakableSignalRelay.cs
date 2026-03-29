using UnityEngine;

[DisallowMultipleComponent]
public class MissionBreakableSignalRelay : MonoBehaviour, IMissionSignalResettable
{
    [SerializeField] private Breakable breakable;
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string signalKey = "break-target";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool hasRaised;

    private void OnEnable()
    {
        ResolveReferences();
        if (breakable != null)
        {
            breakable.BreakCompleted += HandleBreakCompleted;
        }
    }

    private void OnDisable()
    {
        if (breakable != null)
        {
            breakable.BreakCompleted -= HandleBreakCompleted;
        }
    }

    private void HandleBreakCompleted()
    {
        if (triggerOnce && hasRaised)
        {
            return;
        }

        ResolveReferences();
        if (missionSystem == null || string.IsNullOrWhiteSpace(signalKey))
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
        if (breakable == null)
        {
            breakable = GetComponent<Breakable>();
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
