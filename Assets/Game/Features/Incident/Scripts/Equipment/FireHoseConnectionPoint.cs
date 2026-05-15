using StarterAssets;
using TrueJourney.BotBehavior;
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class FireHoseConnectionPoint : MonoBehaviour, IInteractable
{
    private enum PendingInteractionKind
    {
        None = 0,
        DirectHose = 1,
        LiteRig = 2
    }

    public enum InteractionMode
    {
        Auto = 0,
        DirectHoseOnly = 1,
        LiteRigOnly = 2
    }

    [Header("Supply")]
    [SerializeField] private bool providesPressurizedWater = true;
    [SerializeField] private float supplyPressureMultiplier = 1f;
    [SerializeField] private float refillInternalTankPerSecond = 0f;
    [SerializeField] private bool logConnections = false;

    [Header("Interaction")]
    [SerializeField] private InteractionMode interactionMode = InteractionMode.Auto;
    [SerializeField] private FireHoseConnectionSystemLite liteConnectionSystem;
    [SerializeField] private Transform connectAnchor;
    [SerializeField] private float interactionDuration = 0.65f;
    [SerializeField] private bool lockPlayerWhileInteracting = true;

    [Header("Runtime")]
    [SerializeField] private FireHose connectedHose;
    [SerializeField] private FireHoseRig connectedRig;
    [SerializeField] private bool interactionInProgress;

    private Coroutine interactionRoutine;
    private GameObject activeInteractor;
    private PlayerActionLock activePlayerLock;
    private PlayerInteractionAnimationState activeAnimationState;
    private PendingInteractionKind activeInteractionKind;
    private FireHose activeDirectHose;
    private FireHoseRig activeLiteRig;

    public bool ProvidesPressurizedWater => providesPressurizedWater && SupplyPressureMultiplier > 0f;
    public float SupplyPressureMultiplier => providesPressurizedWater ? Mathf.Max(0f, supplyPressureMultiplier) : 0f;
    public float RefillInternalTankPerSecond => Mathf.Max(0f, refillInternalTankPerSecond);
    public bool IsOccupied => connectedHose != null || connectedRig != null;
    public FireHose ConnectedHose => connectedHose;
    public FireHoseRig ConnectedRig => connectedRig;
    public Transform ConnectAnchor => connectAnchor != null ? connectAnchor : transform;

    public void Interact(GameObject interactor)
    {
        if (interactionInProgress)
        {
            return;
        }

        if (!TryResolveInteractionTarget(interactor, out PendingInteractionKind interactionKind, out FireHose hose, out FireHoseRig rig))
        {
            return;
        }

        if (interactionDuration <= 0.01f)
        {
            ApplyInteraction(interactionKind, hose, rig, interactor);
            return;
        }

        interactionRoutine = StartCoroutine(PerformInteractionAfterDelay(interactor, interactionKind, hose, rig));
    }

    private IEnumerator PerformInteractionAfterDelay(
        GameObject interactor,
        PendingInteractionKind interactionKind,
        FireHose hose,
        FireHoseRig rig)
    {
        interactionInProgress = true;
        activeInteractor = interactor;
        activeInteractionKind = interactionKind;
        activeDirectHose = hose;
        activeLiteRig = rig;
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
            bool targetInvalid =
                interactionKind == PendingInteractionKind.DirectHose ? hose == null :
                interactionKind == PendingInteractionKind.LiteRig ? rig == null :
                true;

            if (!isActiveAndEnabled || targetInvalid)
            {
                if (isPlayerInteractor)
                {
                    PlayerContinuousActionBus.EndAction(false);
                }

                ClearActiveInteractionState();
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

        ClearActiveInteractionState();
        ReleasePlayerLock();

        bool finalTargetInvalid =
            interactionKind == PendingInteractionKind.DirectHose ? hose == null :
            interactionKind == PendingInteractionKind.LiteRig ? rig == null :
            true;

        if (!isActiveAndEnabled || finalTargetInvalid)
        {
            if (isPlayerInteractor)
            {
                PlayerContinuousActionBus.EndAction(false);
            }
            yield break;
        }

        ApplyInteraction(interactionKind, hose, rig, interactor);
        activeAnimationState?.EndAction(PlayerInteractionAnimationAction.ConnectingHose, this, force: true);
        activeAnimationState = null;
        if (isPlayerInteractor)
        {
            PlayerContinuousActionBus.EndAction(true);
        }
    }

    private void ApplyInteraction(
        PendingInteractionKind interactionKind,
        FireHose hose,
        FireHoseRig rig,
        GameObject interactor)
    {
        switch (interactionKind)
        {
            case PendingInteractionKind.DirectHose:
                ApplyDirectHoseInteraction(hose);
                break;

            case PendingInteractionKind.LiteRig:
                ApplyLiteRigInteraction(rig, interactor);
                break;
        }
    }

    private void ApplyDirectHoseInteraction(FireHose hose)
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

    private void ApplyLiteRigInteraction(FireHoseRig rig, GameObject interactor)
    {
        if (rig == null)
        {
            return;
        }

        if (ReferenceEquals(rig.ConnectedHydrant, this))
        {
            if (rig.DisconnectHydrant(this) && logConnections)
            {
                Debug.Log($"[FireHoseConnectionPoint] Disconnected lite rig '{rig.name}' from '{name}'.", this);
            }
            return;
        }

        if (rig.IsConnectedToHydrant)
        {
            rig.DisconnectHydrant();
        }

        ResolveLiteConnectionSystem();
        if (liteConnectionSystem == null)
        {
            return;
        }

        if (!liteConnectionSystem.TryConnectRigToHydrant(this, rig, interactor))
        {
            return;
        }

        if (logConnections)
        {
            Debug.Log(
                $"[FireHoseConnectionPoint] Connected lite rig '{rig.name}' to '{name}' with {SupplyPressureMultiplier:0.00}x supply pressure.",
                this);
        }
    }

    internal bool TryRegisterConnection(FireHose hose)
    {
        if (hose == null)
        {
            return false;
        }

        if (connectedRig != null)
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

    internal bool TryRegisterConnection(FireHoseRig rig)
    {
        if (rig == null)
        {
            return false;
        }

        if (connectedHose != null)
        {
            return false;
        }

        if (connectedRig != null && !ReferenceEquals(connectedRig, rig))
        {
            return false;
        }

        connectedRig = rig;
        return true;
    }

    internal void ClearConnection(FireHose hose)
    {
        if (ReferenceEquals(connectedHose, hose))
        {
            connectedHose = null;
        }
    }

    internal void ClearConnection(FireHoseRig rig)
    {
        if (ReferenceEquals(connectedRig, rig))
        {
            connectedRig = null;
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
        activeInteractionKind = PendingInteractionKind.None;
        activeDirectHose = null;
        activeLiteRig = null;
        ReleasePlayerLock();

        if (connectedHose != null)
        {
            FireHose hose = connectedHose;
            connectedHose = null;
            hose.DisconnectFromSupply(this);
        }

        if (connectedRig == null)
        {
            return;
        }

        FireHoseRig rig = connectedRig;
        connectedRig = null;
        rig.DisconnectHydrant(this);
    }

    private void OnValidate()
    {
        supplyPressureMultiplier = Mathf.Max(0f, supplyPressureMultiplier);
        refillInternalTankPerSecond = Mathf.Max(0f, refillInternalTankPerSecond);
        interactionDuration = Mathf.Max(0f, interactionDuration);
        ResolveLiteConnectionSystem();
        connectAnchor ??= transform;
    }

    private void Awake()
    {
        ResolveLiteConnectionSystem();
        connectAnchor ??= transform;
    }

    private static FireHose ResolveHeldDirectFireHose(GameObject interactor)
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

    private bool ShouldUseLiteInteraction()
    {
        return interactionMode != InteractionMode.DirectHoseOnly;
    }

    private bool TryResolveInteractionTarget(
        GameObject interactor,
        out PendingInteractionKind interactionKind,
        out FireHose hose,
        out FireHoseRig rig)
    {
        interactionKind = PendingInteractionKind.None;
        hose = null;
        rig = null;

        if (ShouldUseLiteInteraction())
        {
            rig = ResolveHeldLiteRig(interactor);
            if (rig != null)
            {
                interactionKind = PendingInteractionKind.LiteRig;
                return true;
            }
        }

        if (interactionMode == InteractionMode.LiteRigOnly)
        {
            return false;
        }

        hose = ResolveHeldDirectFireHose(interactor);
        if (hose == null)
        {
            return false;
        }

        interactionKind = PendingInteractionKind.DirectHose;
        return true;
    }

    private static FireHoseRig ResolveHeldLiteRig(GameObject interactor)
    {
        if (interactor == null || !interactor.TryGetComponent(out FPSInventorySystem inventory))
        {
            return null;
        }

        GameObject heldObject = inventory.HeldObject;
        if (heldObject == null)
        {
            return null;
        }

        FireHoseHeadPickup headPickup =
            heldObject.GetComponent<FireHoseHeadPickup>() ??
            heldObject.GetComponentInParent<FireHoseHeadPickup>();
        if (headPickup == null)
        {
            return null;
        }

        if (headPickup.Assembly != null && headPickup.Assembly.Rig != null)
        {
            return headPickup.Assembly.Rig;
        }

        return headPickup.GetComponentInParent<FireHoseRig>();
    }

    private void ResolveLiteConnectionSystem()
    {
        if (liteConnectionSystem == null)
        {
            liteConnectionSystem = GetComponentInParent<FireHoseConnectionSystemLite>();
            liteConnectionSystem ??= FindAnyObjectByType<FireHoseConnectionSystemLite>();
        }
    }

    private void ClearActiveInteractionState()
    {
        interactionRoutine = null;
        interactionInProgress = false;
        activeInteractor = null;
        activeInteractionKind = PendingInteractionKind.None;
        activeDirectHose = null;
        activeLiteRig = null;
    }
}
