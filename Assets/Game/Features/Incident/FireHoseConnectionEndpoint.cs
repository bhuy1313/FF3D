using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseConnectionEndpoint : MonoBehaviour, IInteractable
{
    public enum EndpointKind
    {
        TruckPickup = 0,
        HydrantSupply = 1
    }

    [Header("Endpoint")]
    [SerializeField] private EndpointKind endpointKind;
    [SerializeField] private FireHoseConnectionSystem connectionSystem;
    [SerializeField] private FireTruckHosePickupPoint truckPickupPoint;
    [SerializeField] private FireHydrantHoseConnectPoint hydrantConnectPoint;

    public EndpointKind Kind => endpointKind;
    public FireHoseConnectionSystem ConnectionSystem => connectionSystem;

    private void Awake()
    {
        ResolveReferences();
        connectionSystem?.RegisterEndpoint(this);
    }

    private void OnEnable()
    {
        ResolveReferences();
        connectionSystem?.RegisterEndpoint(this);
    }

    private void OnDisable()
    {
        connectionSystem?.UnregisterEndpoint(this);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void Interact(GameObject interactor)
    {
        switch (endpointKind)
        {
            case EndpointKind.TruckPickup:
                truckPickupPoint?.Interact(interactor);
                break;

            case EndpointKind.HydrantSupply:
                hydrantConnectPoint?.Interact(interactor);
                break;
        }
    }

    private void ResolveReferences()
    {
        connectionSystem ??= GetComponentInParent<FireHoseConnectionSystem>();
        connectionSystem ??= FindAnyObjectByType<FireHoseConnectionSystem>();
        truckPickupPoint ??= GetComponent<FireTruckHosePickupPoint>();
        hydrantConnectPoint ??= GetComponent<FireHydrantHoseConnectPoint>();
    }
}
