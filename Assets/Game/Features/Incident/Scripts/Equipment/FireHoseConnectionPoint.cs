using StarterAssets;
using TrueJourney.BotBehavior;
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class FireHoseConnectionPoint : MonoBehaviour, IInteractable
{
    [Header("Supply")]
    [SerializeField] private bool providesPressurizedWater = true;
    [SerializeField] private float supplyPressureMultiplier = 1f;
    [SerializeField] private float refillInternalTankPerSecond = 0f;
    [SerializeField] private bool logConnections = false;

    [Header("Interaction")]
    [SerializeField] private float interactionDuration = 0.65f;
    [SerializeField] private bool lockPlayerWhileInteracting = true;

    [Header("Runtime")]
    [SerializeField] private FireHose connectedHose;
    [SerializeField] private bool interactionInProgress;

    private Coroutine interactionRoutine;
    private GameObject activeInteractor;
    private PlayerActionLock activePlayerLock;
    private PlayerInteractionAnimationState activeAnimationState;

    public bool ProvidesPressurizedWater => providesPressurizedWater && SupplyPressureMultiplier > 0f;
    public float SupplyPressureMultiplier => providesPressurizedWater ? Mathf.Max(0f, supplyPressureMultiplier) : 0f;
    public float RefillInternalTankPerSecond => Mathf.Max(0f, refillInternalTankPerSecond);
    public bool IsOccupied => connectedHose != null;
    public FireHose ConnectedHose => connectedHose;

    public void Interact(GameObject interactor)
    {
        if (interactionInProgress)
        {
            return;
        }

        FireHose hose = ResolveHeldFireHose(interactor);
        if (hose == null)
        {
            return;
        }

        if (interactionDuration <= 0.01f)
        {
            ApplyInteraction(hose);
            return;
        }

        interactionRoutine = StartCoroutine(PerformInteractionAfterDelay(interactor, hose));
    }

    private IEnumerator PerformInteractionAfterDelay(GameObject interactor, FireHose hose)
    {
        interactionInProgress = true;
        activeInteractor = interactor;
        AcquirePlayerLock(interactor);
        float duration = Mathf.Max(0.01f, interactionDuration);
        bool isPlayerInteractor = interactor != null && interactor.GetComponent<BotCommandAgent>() == null;
        if (isPlayerInteractor)
        {
            activeAnimationState = PlayerInteractionAnimationState.GetOrCreate(interactor);
            activeAnimationState?.BeginAction(PlayerInteractionAnimationAction.ConnectingHose, this, duration);
            PlayerContinuousActionBus.StartAction();
        }

        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            if (!isActiveAndEnabled || hose == null)
            {
                if (isPlayerInteractor)
                {
                    PlayerContinuousActionBus.EndAction(false);
                }

                interactionRoutine = null;
                interactionInProgress = false;
                activeInteractor = null;
                ReleasePlayerLock();
                activeAnimationState?.EndAction(PlayerInteractionAnimationAction.ConnectingHose, this, force: true);
                activeAnimationState = null;
                yield break;
            }

            if (isPlayerInteractor)
            {
                float progress = 1f - ((endTime - Time.time) / duration);
                PlayerContinuousActionBus.UpdateProgress(progress);
            }

            yield return null;
        }

        interactionRoutine = null;
        interactionInProgress = false;
        activeInteractor = null;
        ReleasePlayerLock();

        if (!isActiveAndEnabled || hose == null)
        {
            if (isPlayerInteractor)
            {
                PlayerContinuousActionBus.EndAction(false);
            }
            yield break;
        }

        ApplyInteraction(hose);
        activeAnimationState?.EndAction(PlayerInteractionAnimationAction.ConnectingHose, this, force: true);
        activeAnimationState = null;
        if (isPlayerInteractor)
        {
            PlayerContinuousActionBus.EndAction(true);
        }
    }

    private void ApplyInteraction(FireHose hose)
    {
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
            Debug.Log(
                $"[FireHoseConnectionPoint] Connected hose '{hose.name}' to '{name}' with {SupplyPressureMultiplier:0.00}x supply pressure.",
                this);
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
        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        interactionInProgress = false;
        activeInteractor = null;
        ReleasePlayerLock();

        if (connectedHose == null)
        {
            return;
        }

        FireHose hose = connectedHose;
        connectedHose = null;
        hose.DisconnectFromSupply(this);
    }

    private void OnValidate()
    {
        supplyPressureMultiplier = Mathf.Max(0f, supplyPressureMultiplier);
        refillInternalTankPerSecond = Mathf.Max(0f, refillInternalTankPerSecond);
        interactionDuration = Mathf.Max(0f, interactionDuration);
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

                FireHoseHeadPickup headPickup =
                    heldObject.GetComponent<FireHoseHeadPickup>() ??
                    heldObject.GetComponentInParent<FireHoseHeadPickup>();
                if (headPickup != null)
                {
                    FireHoseAssembly assembly = headPickup.GetComponentInParent<FireHoseAssembly>();
                    if (assembly != null && assembly.Shooter != null)
                    {
                        return assembly.Shooter;
                    }
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

    private void AcquirePlayerLock(GameObject interactor)
    {
        if (!lockPlayerWhileInteracting || interactor == null || interactor.GetComponent<BotCommandAgent>() != null)
        {
            return;
        }

        activePlayerLock = PlayerActionLock.GetOrCreate(interactor);
        activePlayerLock?.AcquireFullLock();
    }

    private void ReleasePlayerLock()
    {
        if (activePlayerLock == null)
        {
            return;
        }

        activePlayerLock.ReleaseFullLock();
        activePlayerLock = null;
    }
}
