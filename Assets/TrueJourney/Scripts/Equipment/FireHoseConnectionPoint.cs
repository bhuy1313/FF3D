using StarterAssets;
using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseConnectionPoint : MonoBehaviour, IInteractable
{
    [Header("Supply")]
    [SerializeField] private bool providesPressurizedWater = true;
    [SerializeField] private float refillInternalTankPerSecond = 0f;
    [SerializeField] private bool logConnections = false;

    [Header("Runtime")]
    [SerializeField] private FireHose connectedHose;

    public bool ProvidesPressurizedWater => providesPressurizedWater;
    public float RefillInternalTankPerSecond => Mathf.Max(0f, refillInternalTankPerSecond);
    public bool IsOccupied => connectedHose != null;
    public FireHose ConnectedHose => connectedHose;

    public void Interact(GameObject interactor)
    {
        FireHose hose = ResolveHeldFireHose(interactor);
        if (hose == null)
        {
            return;
        }

        if (ReferenceEquals(connectedHose, hose))
        {
            if (hose.DisconnectFromSupply(this) && logConnections)
            {
                Debug.Log($"[FireHoseConnectionPoint] Disconnected hose '{hose.name}' from '{name}'.", this);
            }

            return;
        }

        if (!hose.TryConnectToSupply(this))
        {
            return;
        }

        if (logConnections)
        {
            Debug.Log($"[FireHoseConnectionPoint] Connected hose '{hose.name}' to '{name}'.", this);
        }
    }

    internal bool TryRegisterConnection(FireHose hose)
    {
        if (hose == null)
        {
            return false;
        }

        if (connectedHose != null && !ReferenceEquals(connectedHose, hose))
        {
            return false;
        }

        connectedHose = hose;
        return true;
    }

    internal void ClearConnection(FireHose hose)
    {
        if (ReferenceEquals(connectedHose, hose))
        {
            connectedHose = null;
        }
    }

    private void OnDisable()
    {
        if (connectedHose == null)
        {
            return;
        }

        FireHose hose = connectedHose;
        connectedHose = null;
        hose.DisconnectFromSupply(this);
    }

    private static FireHose ResolveHeldFireHose(GameObject interactor)
    {
        if (interactor == null)
        {
            return null;
        }

        if (interactor.TryGetComponent(out FPSInventorySystem fpsInventory))
        {
            GameObject heldObject = fpsInventory.HeldObject;
            if (heldObject != null)
            {
                FireHose heldHose = heldObject.GetComponent<FireHose>() ?? heldObject.GetComponentInParent<FireHose>();
                if (heldHose != null)
                {
                    return heldHose;
                }
            }
        }

        if (interactor.TryGetComponent(out BotInventorySystem botInventory) &&
            botInventory.TryEquipItem<FireHose>(out FireHose equippedHose))
        {
            return equippedHose;
        }

        return null;
    }
}
