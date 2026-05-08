using UnityEngine;
using UnityEngine.InputSystem;

public sealed class KeyHintContext
{
    public KeyHintContext(
        PlayerInput playerInput,
        InputActionAsset inputActionsAsset,
        InputActionMap actionMap,
        string controlScheme,
        AppLanguage language,
        IncidentMissionSystem missionSystem,
        IncidentMissionSystem.MissionState missionState,
        string missionId,
        StarterAssets.FPSInteractionSystem interactionSystem,
        FPSInventorySystem inventorySystem,
        StarterAssets.FPSCommandSystem commandSystem,
        GameObject currentTarget,
        GameObject heldObject,
        bool isGrabActive,
        bool isCarryingRescuable,
        int inventoryItemCount,
        int inventoryMaxSlots,
        bool isAwaitingCommandDestination,
        GameObject hoveredCommandTarget,
        GameObject selectedCommandTarget)
    {
        PlayerInput = playerInput;
        InputActionsAsset = inputActionsAsset;
        ActionMap = actionMap;
        ControlScheme = controlScheme ?? string.Empty;
        Language = language;
        MissionSystem = missionSystem;
        MissionState = missionState;
        MissionId = missionId ?? string.Empty;
        InteractionSystem = interactionSystem;
        InventorySystem = inventorySystem;
        CommandSystem = commandSystem;
        CurrentTarget = currentTarget;
        HeldObject = heldObject;
        IsGrabActive = isGrabActive;
        IsCarryingRescuable = isCarryingRescuable;
        InventoryItemCount = Mathf.Max(0, inventoryItemCount);
        InventoryMaxSlots = Mathf.Max(0, inventoryMaxSlots);
        IsAwaitingCommandDestination = isAwaitingCommandDestination;
        HoveredCommandTarget = hoveredCommandTarget;
        SelectedCommandTarget = selectedCommandTarget;
    }

    public PlayerInput PlayerInput { get; }
    public InputActionAsset InputActionsAsset { get; }
    public InputActionMap ActionMap { get; }
    public string ControlScheme { get; }
    public AppLanguage Language { get; }
    public IncidentMissionSystem MissionSystem { get; }
    public IncidentMissionSystem.MissionState MissionState { get; }
    public string MissionId { get; }
    public StarterAssets.FPSInteractionSystem InteractionSystem { get; }
    public FPSInventorySystem InventorySystem { get; }
    public StarterAssets.FPSCommandSystem CommandSystem { get; }
    public GameObject CurrentTarget { get; }
    public GameObject HeldObject { get; }
    public bool IsGrabActive { get; }
    public bool IsCarryingRescuable { get; }
    public int InventoryItemCount { get; }
    public int InventoryMaxSlots { get; }
    public bool IsAwaitingCommandDestination { get; }
    public GameObject HoveredCommandTarget { get; }
    public GameObject SelectedCommandTarget { get; }

    public bool IsMissionRunning => MissionState == IncidentMissionSystem.MissionState.Running;

    public InputAction FindAction(string actionName)
    {
        if (ActionMap == null || string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        return ActionMap.FindAction(actionName, throwIfNotFound: false);
    }
}
