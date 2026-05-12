using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireHoseConnectionPoint))]
public class FireHydrantHoseConnectPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private FireHoseConnectionSystem connectionSystem;
    [SerializeField] private FireHoseConnectionPoint connectionPoint;

    void OnValidate()
    {
        ResolveReferences();
    }

    void Reset()
    {
        ResolveReferences();
    }

    void Awake()
    {
        ResolveReferences();
    }

    public void Interact(GameObject interactor)
    {
        if (connectionSystem != null && connectionSystem.TryConnectHeldRigToHydrant(connectionPoint, interactor))
        {
            return;
        }

        connectionPoint?.Interact(interactor);
    }

    private void ResolveReferences()
    {
        connectionPoint ??= GetComponent<FireHoseConnectionPoint>();
        connectionSystem ??= GetComponentInParent<FireHoseConnectionSystem>();
        connectionSystem ??= FindAnyObjectByType<FireHoseConnectionSystem>();
    }
}
