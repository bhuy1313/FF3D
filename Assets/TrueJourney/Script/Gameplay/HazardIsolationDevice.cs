using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HazardIsolationDevice : MonoBehaviour, IInteractable
{
    [Header("Isolation")]
    [SerializeField] private bool startsIsolated;
    [SerializeField] private bool allowToggleAfterIsolation;
    [SerializeField] private bool autoCollectChildFires = true;
    [SerializeField] private Fire[] linkedFires = new Fire[0];

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

        if (linkedFires != null)
        {
            for (int i = 0; i < linkedFires.Length; i++)
            {
                Fire fire = linkedFires[i];
                if (fire != null)
                {
                    fire.SetHazardSourceIsolated(isolated);
                }
            }
        }

        if (!invokeEvents || !changed)
        {
            return;
        }

        if (isIsolated)
        {
            onHazardIsolated?.Invoke();
        }
        else
        {
            onHazardReactivated?.Invoke();
        }
    }

    private void ResolveLinkedFires()
    {
        if (!autoCollectChildFires || (linkedFires != null && linkedFires.Length > 0))
        {
            return;
        }

        linkedFires = GetComponentsInChildren<Fire>(true);
    }
}
