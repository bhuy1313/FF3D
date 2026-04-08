using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HazardIsolationDevice : MonoBehaviour, IInteractable
{
    private enum IsolationHazardType
    {
        None = 0,
        Electrical = 1,
        Gas = 2
    }

    [Header("Isolation")]
    [SerializeField] private bool startsIsolated;
    [SerializeField] private bool allowToggleAfterIsolation;
    [SerializeField] private IsolationHazardType hazardType = IsolationHazardType.None;
    [SerializeField] private bool applyHazardTypeToLinkedFires = true;
    [SerializeField] private bool autoCollectChildFires = true;
    [SerializeField] private Fire[] linkedFires = new Fire[0];

    [Header("Mission Signals")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string isolatedSignalKey;
    [SerializeField] private string reactivatedSignalKey;

    [Header("Events")]
    [SerializeField] private UnityEvent onHazardIsolated;
    [SerializeField] private UnityEvent onHazardReactivated;

    [Header("Runtime")]
    [SerializeField] private bool isIsolated;

    public bool IsIsolated => isIsolated;

    private void Awake()
    {
        ResolveLinkedFires();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
    }

    private void OnEnable()
    {
        ResolveLinkedFires();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
    }

    private void OnValidate()
    {
        ResolveLinkedFires();
        ApplyLinkedFireConfiguration();
    }

    public void Interact(GameObject interactor)
    {
        if (!allowToggleAfterIsolation && isIsolated)
        {
            return;
        }

        ApplyIsolationState(!isIsolated, invokeEvents: true);
    }

    [ContextMenu("Isolate Hazard")]
    public void IsolateHazard()
    {
        ApplyIsolationState(true, invokeEvents: true);
    }

    [ContextMenu("Reactivate Hazard")]
    public void ReactivateHazard()
    {
        if (!allowToggleAfterIsolation)
        {
            return;
        }

        ApplyIsolationState(false, invokeEvents: true);
    }

    private void ApplyIsolationState(bool isolated, bool invokeEvents)
    {
        bool changed = isIsolated != isolated;
        isIsolated = isolated;
        ApplyLinkedFireConfiguration();

        if (linkedFires == null)
        {
            linkedFires = new Fire[0];
        }

        for (int i = 0; i < linkedFires.Length; i++)
        {
            Fire fire = linkedFires[i];
            if (fire != null)
            {
                fire.SetHazardSourceIsolated(isolated);
            }
        }

        if (!invokeEvents || !changed)
        {
            return;
        }

        if (isIsolated)
        {
            RaiseMissionSignal(isolatedSignalKey);
            onHazardIsolated?.Invoke();
        }
        else
        {
            RaiseMissionSignal(reactivatedSignalKey);
            onHazardReactivated?.Invoke();
        }
    }

    private void ResolveLinkedFires()
    {
        System.Collections.Generic.List<Fire> resolvedFires = new System.Collections.Generic.List<Fire>();
        AddUniqueNonNullFires(linkedFires, resolvedFires);

        if (autoCollectChildFires)
        {
            Fire[] childFires = GetComponentsInChildren<Fire>(true);
            AddUniqueNonNullFires(childFires, resolvedFires);
        }

        linkedFires = resolvedFires.ToArray();
    }

    private void ApplyLinkedFireConfiguration()
    {
        if (!applyHazardTypeToLinkedFires || hazardType == IsolationHazardType.None || linkedFires == null)
        {
            return;
        }

        FireHazardType fireHazardType = ResolveFireHazardType();
        for (int i = 0; i < linkedFires.Length; i++)
        {
            Fire fire = linkedFires[i];
            if (fire != null)
            {
                fire.SetFireHazardType(fireHazardType);
            }
        }
    }

    private FireHazardType ResolveFireHazardType()
    {
        switch (hazardType)
        {
            case IsolationHazardType.Electrical:
                return FireHazardType.Electrical;
            case IsolationHazardType.Gas:
                return FireHazardType.GasFed;
            default:
                return FireHazardType.OrdinaryCombustibles;
        }
    }

    private void RaiseMissionSignal(string signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey))
        {
            return;
        }

        ResolveMissionSystem();
        missionSystem?.NotifySignal(signalKey);
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    private static void AddUniqueNonNullFires(
        Fire[] source,
        System.Collections.Generic.List<Fire> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            Fire fire = source[i];
            if (fire == null || destination.Contains(fire))
            {
                continue;
            }

            destination.Add(fire);
        }
    }
}
