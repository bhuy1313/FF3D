using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireHoseConnectionPoint))]
public class FireHydrantHoseConnectPoint : MonoBehaviour, IInteractable
{
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
        connectionPoint?.Interact(interactor);
    }

    private void ResolveReferences()
    {
        connectionPoint ??= GetComponent<FireHoseConnectionPoint>();
    }
}
