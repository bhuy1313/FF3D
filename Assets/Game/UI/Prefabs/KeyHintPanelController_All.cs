using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyHintPanelController_All : MonoBehaviour
{
    private readonly struct ActionHint
    {
        public ActionHint(string actionName, string displayNameOverride = null)
        {
            ActionName = actionName;
            DisplayNameOverride = displayNameOverride;
        }

        public string ActionName { get; }
        public string DisplayNameOverride { get; }
    }

    private readonly struct RenderedHint
    {
        public RenderedHint(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; }
    }

    [System.Serializable]
    private sealed class ContextualHintProfile
    {
        public string missionId;
        public string stageId;
        public List<string> actionNames = new List<string>();

        public bool Matches(string currentMissionId, string currentStageId)
        {
            bool missionMatches = string.IsNullOrWhiteSpace(missionId) ||
                                  string.Equals(missionId.Trim(), currentMissionId, System.StringComparison.OrdinalIgnoreCase);
            bool stageMatches = string.IsNullOrWhiteSpace(stageId) ||
                                string.Equals(stageId.Trim(), currentStageId, System.StringComparison.OrdinalIgnoreCase);
            return missionMatches && stageMatches;
        }
    }

    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private StarterAssets.FPSInteractionSystem interactionSystem;
    [SerializeField] private FPSInventorySystem inventorySystem;
    [SerializeField] private StarterAssets.FPSCommandSystem commandSystem;
    [SerializeField] private KeyHintService keyHintService;

    [Header("UI")]
    [SerializeField] private Transform container;        // Panel with VerticalLayoutGroup
    [SerializeField] private KeyHintItemView itemPrefab; // Prefab with 2 TMP fields + KeyHintItemView

    [Header("Config")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private bool preferServiceDrivenHints = true;
    [SerializeField] private bool fallbackToLegacyWhenServiceReturnsEmpty = true;
    [SerializeField] private bool includeValueActions = true; // Include Move/Look value actions
    [SerializeField] private bool hideLookAction = true;
    [SerializeField] private bool useContextualMissionHints = true;
    [SerializeField] private bool hideHintsOutsideRunningMission = true;
    [SerializeField] private bool fallbackToAllActionsWhenNoContextMatch = true;
    [SerializeField] private bool useBuiltInTutorialProfiles = true;
    [SerializeField] private List<string> defaultContextActionNames = new List<string>();
    [SerializeField] private List<ContextualHintProfile> contextualHintProfiles = new List<ContextualHintProfile>();

    private readonly List<KeyHintItemView> spawned = new();
    private string lastContextSignature = string.Empty;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (playerInput != null)
            playerInput.onControlsChanged += OnControlsChanged;

        InputSystem.onActionChange += OnActionChange;
        LanguageManager.LanguageChanged += OnLanguageChanged;

        Rebuild();
    }

    private void Update()
    {
        if (!useContextualMissionHints)
        {
            return;
        }

        string currentSignature = BuildContextSignature();
        if (!string.Equals(currentSignature, lastContextSignature, System.StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
            playerInput.onControlsChanged -= OnControlsChanged;

        InputSystem.onActionChange -= OnActionChange;
        LanguageManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnControlsChanged(PlayerInput _)
    {
        Rebuild();
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
            Rebuild();
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        Rebuild();
    }

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        ResolveReferences();

        if (playerInput == null || playerInput.actions == null || itemPrefab == null) return;

        var asset = playerInput.actions;
        var map = asset.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null) return;

        string scheme = playerInput.currentControlScheme ?? "";
        var renderedHints = new List<RenderedHint>();
        if (TryRebuildFromService(map, scheme, renderedHints))
        {
            ApplyRenderedHints(renderedHints);
            lastContextSignature = BuildContextSignature();
            return;
        }

        var hintEntries = new List<ActionHint>();
        AppendLiveContextHints(hintEntries);

        if (TryResolveCurrentContextActionNames(out List<string> contextualActionNames))
        {
            for (int i = 0; i < contextualActionNames.Count; i++)
            {
                AddUniqueHint(hintEntries, contextualActionNames[i]);
            }

            AddHintEntries(map, hintEntries, scheme, renderedHints);
            ApplyRenderedHints(renderedHints);
            lastContextSignature = BuildContextSignature();
            return;
        }

        foreach (var action in map.actions)
        {
            if (hideLookAction && action.name == "Look")
                continue;

            if (!includeValueActions && action.type != InputActionType.Button)
                continue;

            AddUniqueHint(hintEntries, action.name);
        }

        AddHintEntries(map, hintEntries, scheme, renderedHints);
        ApplyRenderedHints(renderedHints);
        lastContextSignature = BuildContextSignature();
    }

    private void OnDestroy()
    {
        DestroySpawnedItems();
    }

    private void ResolveReferences()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        if (missionSystem == null) missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        if (interactionSystem == null)
        {
            interactionSystem = playerInput != null
                ? playerInput.GetComponent<StarterAssets.FPSInteractionSystem>()
                : FindAnyObjectByType<StarterAssets.FPSInteractionSystem>();
        }

        if (inventorySystem == null)
        {
            inventorySystem = playerInput != null
                ? playerInput.GetComponent<FPSInventorySystem>()
                : FindAnyObjectByType<FPSInventorySystem>();
        }

        if (commandSystem == null)
        {
            commandSystem = playerInput != null
                ? playerInput.GetComponent<StarterAssets.FPSCommandSystem>()
                : FindAnyObjectByType<StarterAssets.FPSCommandSystem>();
        }

        if (keyHintService == null)
        {
            keyHintService = GetComponent<KeyHintService>();
        }

        if (keyHintService == null)
        {
            keyHintService = FindAnyObjectByType<KeyHintService>();
        }

        if (container == null) container = transform;
    }

    private bool TryRebuildFromService(InputActionMap map, string scheme, List<RenderedHint> renderedHints)
    {
        if (!preferServiceDrivenHints || keyHintService == null || map == null || itemPrefab == null || renderedHints == null)
        {
            return false;
        }

        IReadOnlyList<KeyHintRequest> requests = keyHintService.RebuildHints();
        if (requests == null)
        {
            return false;
        }

        bool addedAny = false;
        for (int index = 0; index < requests.Count; index++)
        {
            KeyHintRequest request = requests[index];
            if (TryAddServiceHint(map, request, scheme, renderedHints))
            {
                addedAny = true;
            }
        }

        return addedAny || !fallbackToLegacyWhenServiceReturnsEmpty;
    }

    private bool TryAddServiceHint(InputActionMap map, KeyHintRequest request, string scheme, List<RenderedHint> renderedHints)
    {
        if (map == null || !request.IsValid || renderedHints == null)
        {
            return false;
        }

        InputAction action = map.FindAction(request.ActionName, throwIfNotFound: false);
        if (action == null)
        {
            return false;
        }

        string key = KeyHintBindingUtil.GetKeyDisplay(action, scheme);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        renderedHints.Add(new RenderedHint(key, KeyHintGameplayUtility.ResolveLocalizedLabel(request)));
        return true;
    }

    private bool TryResolveCurrentContextActionNames(out List<string> actionNames)
    {
        actionNames = null;
        if (!useContextualMissionHints)
        {
            return false;
        }

        if (missionSystem == null)
        {
            return false;
        }

        if (hideHintsOutsideRunningMission && missionSystem.State != IncidentMissionSystem.MissionState.Running)
        {
            actionNames = new List<string>();
            return true;
        }

        string missionId = missionSystem.MissionId;
        string stageId = missionSystem.CurrentStageId;

        if (TryGetConfiguredContextActionNames(missionId, stageId, out actionNames))
        {
            return true;
        }

        if (useBuiltInTutorialProfiles && TryGetBuiltInTutorialActionNames(missionId, stageId, out actionNames))
        {
            return true;
        }

        if (defaultContextActionNames != null && defaultContextActionNames.Count > 0)
        {
            actionNames = defaultContextActionNames;
            return true;
        }

        return !fallbackToAllActionsWhenNoContextMatch;
    }

    private bool TryGetConfiguredContextActionNames(string missionId, string stageId, out List<string> actionNames)
    {
        actionNames = null;
        if (contextualHintProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < contextualHintProfiles.Count; i++)
        {
            ContextualHintProfile profile = contextualHintProfiles[i];
            if (profile == null || !profile.Matches(missionId, stageId))
            {
                continue;
            }

            actionNames = profile.actionNames ?? new List<string>();
            return true;
        }

        return false;
    }

    private static bool TryGetBuiltInTutorialActionNames(string missionId, string stageId, out List<string> actionNames)
    {
        actionNames = null;
        if (!string.Equals(missionId, "tutorial-incident", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        actionNames = new List<string> { "Move", "Sprint", "Jump", "Crouch" };
        return true;
    }

    private void AddHintEntries(InputActionMap map, List<ActionHint> hints, string scheme, List<RenderedHint> renderedHints)
    {
        if (map == null || hints == null || renderedHints == null)
        {
            return;
        }

        for (int i = 0; i < hints.Count; i++)
        {
            TryAddActionHint(map, hints[i], scheme, renderedHints);
        }
    }

    private bool TryAddActionHint(InputActionMap map, ActionHint hint, string scheme, List<RenderedHint> renderedHints)
    {
        if (map == null || string.IsNullOrWhiteSpace(hint.ActionName) || renderedHints == null)
        {
            return false;
        }

        InputAction action = map.FindAction(hint.ActionName, throwIfNotFound: false);
        if (action == null)
        {
            return false;
        }

        AddActionHint(action, scheme, renderedHints, hint.DisplayNameOverride);
        return true;
    }

    private void AddActionHint(InputAction action, string scheme, List<RenderedHint> renderedHints, string displayNameOverride = null)
    {
        if (action == null || renderedHints == null)
        {
            return;
        }

        string key = KeyHintBindingUtil.GetKeyDisplay(action, scheme);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        renderedHints.Add(new RenderedHint(
            key,
            string.IsNullOrWhiteSpace(displayNameOverride) ? GetActionDisplayName(action.name) : displayNameOverride));
    }

    private void ApplyRenderedHints(List<RenderedHint> renderedHints)
    {
        if (renderedHints == null)
        {
            DeactivateUnusedItems(0);
            ForceContainerLayoutRebuild();
            return;
        }

        for (int i = 0; i < renderedHints.Count; i++)
        {
            KeyHintItemView item = GetOrCreateItem(i);
            if (item == null)
            {
                continue;
            }

            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
            }

            item.Set(renderedHints[i].Key, renderedHints[i].Label);
        }

        DeactivateUnusedItems(renderedHints.Count);
        ForceContainerLayoutRebuild();
    }

    private KeyHintItemView GetOrCreateItem(int index)
    {
        while (spawned.Count <= index)
        {
            KeyHintItemView item = Instantiate(itemPrefab, container);
            spawned.Add(item);
        }

        return spawned[index];
    }

    private void DeactivateUnusedItems(int usedCount)
    {
        for (int i = usedCount; i < spawned.Count; i++)
        {
            if (spawned[i] != null && spawned[i].gameObject.activeSelf)
            {
                spawned[i].gameObject.SetActive(false);
            }
        }
    }

    private void DestroySpawnedItems()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Destroy(spawned[i].gameObject);
            }
        }

        spawned.Clear();
    }

    private void ForceContainerLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (container is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            if (containerRect.parent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }

    private void AppendLiveContextHints(List<ActionHint> hints)
    {
        if (hints == null)
        {
            return;
        }

        AppendCommandContextHints(hints);

        if (interactionSystem == null)
        {
            return;
        }

        GameObject currentTarget = interactionSystem.CurrentTarget;
        bool isGrabActive = interactionSystem.IsGrabActive;
        bool isCarryingRescuable = interactionSystem.CurrentCarryWeightKg > 0.01f;
        GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;
        GameObject occupyingObject = interactionSystem.CurrentHandOccupyingObject;
        bool canPickupMoreItems = inventorySystem != null && inventorySystem.ItemCount < inventorySystem.MaxSlots;
        bool heldItemBlocksStow = HandOccupancyUtility.BlocksInventoryStow(occupyingObject, inventorySystem != null ? inventorySystem.gameObject : null);

        FireHose heldFireHose = FindComponentInTargetHierarchy<FireHose>(heldObject);
        Tool heldTool = FindComponentInTargetHierarchy<Tool>(heldObject);
        IUsable heldUsable = FindComponentInTargetHierarchy<IUsable>(heldObject);

        bool targetIsPickupable = FindComponentInTargetHierarchy<IPickupable>(currentTarget) != null;
        bool targetIsGrabbable = FindComponentInTargetHierarchy<IGrabbable>(currentTarget) != null;
        SafeZone targetSafeZone = FindComponentInTargetHierarchy<SafeZone>(currentTarget);
        Door targetDoor = FindComponentInTargetHierarchy<Door>(currentTarget);
        Rescuable targetRescuable = FindComponentInTargetHierarchy<Rescuable>(currentTarget);
        FireHoseConnectionPoint targetHoseConnection = FindComponentInTargetHierarchy<FireHoseConnectionPoint>(currentTarget);
        Breakable targetBreakable = FindComponentInTargetHierarchy<Breakable>(currentTarget);
        Explosive targetExplosive = FindComponentInTargetHierarchy<Explosive>(currentTarget);
        IInteractable targetInteractable = FindComponentInTargetHierarchy<IInteractable>(currentTarget);
        ICommandable targetCommandable = FindComponentInTargetHierarchy<ICommandable>(currentTarget);

        if (isCarryingRescuable)
        {
            if (targetSafeZone != null)
            {
                AddUniqueHint(hints, "Interact", "Deliver Victim");
            }
            else if (targetDoor != null)
            {
                AddUniqueHint(hints, "Interact", targetDoor.IsOpen ? "Close Door" : "Open Door");
            }

            return;
        }

        if (isGrabActive)
        {
            AddUniqueHint(hints, "Grab", "Release Object");
            AddUniqueHint(hints, "Use", "Place Object");
            if (targetDoor != null)
            {
                AddUniqueHint(hints, "Interact", targetDoor.IsOpen ? "Close Door" : "Open Door");
            }

            return;
        }

        if (targetRescuable != null)
        {
            AddUniqueHint(hints, "Interact", targetRescuable.RequiresStabilization ? "Stabilize Victim" : "Carry Victim");
        }
        else if (targetHoseConnection != null && heldFireHose != null)
        {
            bool isConnectedToFocusedPoint = ReferenceEquals(targetHoseConnection.ConnectedHose, heldFireHose);
            AddUniqueHint(hints, "Interact", isConnectedToFocusedPoint ? "Disconnect Hose" : "Connect Hose");
        }
        else if (targetDoor != null)
        {
            AddUniqueHint(hints, "Interact", targetDoor.IsOpen ? "Close Door" : "Open Door");
        }
        else if (targetExplosive != null)
        {
            AddUniqueHint(hints, "Interact", "Trigger Explosive");
        }
        else if (targetInteractable != null &&
                 targetCommandable == null &&
                 heldObject == null &&
                 !targetIsPickupable &&
                 !targetIsGrabbable &&
                 targetSafeZone == null &&
                 targetHoseConnection == null &&
                 targetBreakable == null)
        {
            AddUniqueHint(hints, "Interact");
        }

        if (heldFireHose != null)
        {
            AddUniqueHint(hints, "Use", "Spray Water");
            AddUniqueHint(hints, "ToggleSprayPattern");
            AddUniqueHint(hints, "IncreasePressure");
            AddUniqueHint(hints, "DecreasePressure");
            AddUniqueHint(hints, "Drop", "Drop Hose");
        }
        else if (heldTool != null)
        {
            AddUniqueHint(hints, "Use", targetBreakable != null ? "Break Target" : "Use Tool");
            AddUniqueHint(hints, "Drop", "Drop Tool");
        }
        else if (heldUsable != null)
        {
            AddUniqueHint(hints, "Use", "Use Item");
            AddUniqueHint(hints, "Drop", "Drop Item");
        }
        else if (inventorySystem != null && inventorySystem.ItemCount > 0 && !heldItemBlocksStow)
        {
            AppendInventorySelectionHints(hints, inventorySystem.ItemCount, inventorySystem.MaxSlots);
        }

        if (targetIsPickupable && canPickupMoreItems && !heldItemBlocksStow)
        {
            AddUniqueHint(hints, "Pickup", ResolvePickupDisplayName(currentTarget));
        }

        if (targetIsGrabbable && occupyingObject == null && !isCarryingRescuable)
        {
            AddUniqueHint(hints, "Grab", "Grab Object");
        }
    }

    private void AppendCommandContextHints(List<ActionHint> hints)
    {
        if (hints == null || commandSystem == null)
        {
            return;
        }

        if (commandSystem.IsAwaitingDestination)
        {
            AddUniqueHint(hints, "CommandConfirm", "Confirm Command");
            AddUniqueHint(hints, "CommandCancel", "Cancel Command");
            return;
        }

        if (commandSystem.HoveredCommandTarget != null)
        {
            AddUniqueHint(hints, "CommandMove", "Command Bot");
        }
    }

    private static void AddUniqueHint(List<ActionHint> hints, string actionName, string displayNameOverride = null)
    {
        if (hints == null || string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        for (int i = 0; i < hints.Count; i++)
        {
            if (string.Equals(hints[i].ActionName, actionName, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        hints.Add(new ActionHint(actionName, displayNameOverride));
    }

    private static string ResolvePickupDisplayName(GameObject target)
    {
        if (target == null)
        {
            return "Pick Up";
        }

        if (FindComponentInTargetHierarchy<FireHose>(target) != null)
        {
            return "Pick Up Hose";
        }

        if (FindComponentInTargetHierarchy<Tool>(target) != null)
        {
            return "Pick Up Tool";
        }

        return "Pick Up";
    }

    private static void AppendInventorySelectionHints(List<ActionHint> hints, int itemCount, int maxSlots)
    {
        if (hints == null || itemCount <= 0 || maxSlots <= 0)
        {
            return;
        }

        int slotCount = Mathf.Min(itemCount, maxSlots, 6);
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            AddUniqueHint(hints, $"Slot{slotIndex + 1}", $"Equip Slot {slotIndex + 1}");
        }
    }

    private static T FindComponentInTargetHierarchy<T>(GameObject target) where T : class
    {
        if (target == null)
        {
            return null;
        }

        Component direct = target.GetComponent(typeof(T));
        if (direct is T typedDirect)
        {
            return typedDirect;
        }

        Rigidbody attachedBody = target.GetComponentInParent<Rigidbody>();
        if (attachedBody != null)
        {
            Component rigidbodyOwner = attachedBody.GetComponent(typeof(T));
            if (rigidbodyOwner is T typedRigidbodyOwner)
            {
                return typedRigidbodyOwner;
            }
        }

        Transform parent = target.transform.parent;
        while (parent != null)
        {
            Component parentComponent = parent.GetComponent(typeof(T));
            if (parentComponent is T typedParent)
            {
                return typedParent;
            }

            parent = parent.parent;
        }

        return null;
    }

    private string BuildContextSignature()
    {
        string scheme = playerInput != null ? playerInput.currentControlScheme ?? string.Empty : string.Empty;
        if (!useContextualMissionHints || missionSystem == null)
        {
            return $"all|{scheme}";
        }

        GameObject currentTarget = interactionSystem != null ? interactionSystem.CurrentTarget : null;
        GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;
        GameObject occupyingObject = interactionSystem != null ? interactionSystem.CurrentHandOccupyingObject : heldObject;
        bool isGrabActive = interactionSystem != null && interactionSystem.IsGrabActive;
        bool isCarryingRescuable = interactionSystem != null && interactionSystem.CurrentCarryWeightKg > 0.01f;
        int inventoryItemCount = inventorySystem != null ? inventorySystem.ItemCount : 0;
        bool isAwaitingCommandDestination = commandSystem != null && commandSystem.IsAwaitingDestination;
        string hoveredCommandTargetName = commandSystem != null && commandSystem.HoveredCommandTarget != null
            ? commandSystem.HoveredCommandTarget.name
            : string.Empty;
        string selectedCommandTargetName = commandSystem != null && commandSystem.SelectedCommandTarget != null
            ? commandSystem.SelectedCommandTarget.name
            : string.Empty;

        return $"{missionSystem.State}|{missionSystem.MissionId}|{missionSystem.CurrentStageId}|{scheme}|items:{inventoryItemCount}|grab:{isGrabActive}|carry:{isCarryingRescuable}|cmdAwait:{isAwaitingCommandDestination}|cmdHover:{hoveredCommandTargetName}|cmdSelected:{selectedCommandTargetName}|{BuildTargetContextDescriptor(currentTarget)}|held:{BuildHeldObjectDescriptor(occupyingObject)}";
    }

    private static string BuildTargetContextDescriptor(GameObject currentTarget)
    {
        if (currentTarget == null)
        {
            return "target:none";
        }

        Door door = FindComponentInTargetHierarchy<Door>(currentTarget);
        if (door != null)
        {
            return $"target:door:{door.name}:open:{door.IsOpen}";
        }

        Rescuable rescuable = FindComponentInTargetHierarchy<Rescuable>(currentTarget);
        if (rescuable != null)
        {
            return $"target:rescuable:{rescuable.name}:stabilize:{rescuable.RequiresStabilization}";
        }

        SafeZone safeZone = FindComponentInTargetHierarchy<SafeZone>(currentTarget);
        if (safeZone != null)
        {
            return $"target:safezone:{safeZone.name}";
        }

        FireHoseConnectionPoint hoseConnection = FindComponentInTargetHierarchy<FireHoseConnectionPoint>(currentTarget);
        if (hoseConnection != null)
        {
            string connectedHoseName = hoseConnection.ConnectedHose != null ? hoseConnection.ConnectedHose.name : string.Empty;
            return $"target:hose-connection:{hoseConnection.name}:connected:{connectedHoseName}";
        }

        Breakable breakable = FindComponentInTargetHierarchy<Breakable>(currentTarget);
        if (breakable != null)
        {
            return $"target:breakable:{breakable.name}";
        }

        FireHose hose = FindComponentInTargetHierarchy<FireHose>(currentTarget);
        if (hose != null)
        {
            return $"target:hose:{hose.name}";
        }

        Tool tool = FindComponentInTargetHierarchy<Tool>(currentTarget);
        if (tool != null)
        {
            return $"target:tool:{tool.name}";
        }

        Explosive explosive = FindComponentInTargetHierarchy<Explosive>(currentTarget);
        if (explosive != null)
        {
            return $"target:explosive:{explosive.name}";
        }

        if (FindComponentInTargetHierarchy<IGrabbable>(currentTarget) != null)
        {
            return $"target:grabbable:{currentTarget.name}";
        }

        return $"target:{currentTarget.name}";
    }

    private static string BuildHeldObjectDescriptor(GameObject heldObject)
    {
        if (heldObject == null)
        {
            return "none";
        }

        if (FindComponentInTargetHierarchy<FireHose>(heldObject) != null)
        {
            return $"hose:{heldObject.name}";
        }

        if (FindComponentInTargetHierarchy<Tool>(heldObject) != null)
        {
            return $"tool:{heldObject.name}";
        }

        if (FindComponentInTargetHierarchy<IUsable>(heldObject) != null)
        {
            return $"usable:{heldObject.name}";
        }

        return heldObject.name;
    }

    private static string GetActionDisplayName(string actionName)
    {
        return actionName switch
        {
            "Move" => "Move",
            "Look" => "Look",
            "Jump" => "Jump",
            "Sprint" => "Sprint",
            "Crouch" => "Crouch",
            "Interact" => "Interact",
            "Pickup" => "Pick Up",
            "Use" => "Use",
            "Drop" => "Drop",
            "Grab" => "Grab",
            "Slot1" => "Slot 1",
            "Slot2" => "Slot 2",
            "Slot3" => "Slot 3",
            "Slot4" => "Slot 4",
            "Slot5" => "Slot 5",
            "Slot6" => "Slot 6",
            "ToolWheel" => "Tool Wheel",
            "CommandMove" => "Command Bot",
            "CommandCancel" => "Cancel Command",
            "CommandCancelAllFollow" => "Cancel All Follow",
            "ToggleBotOutline" => "Toggle Bot Outline",
            "CommandConfirm" => "Confirm Command",
            "ToggleSprayPattern" => "Toggle Spray",
            "IncreasePressure" => "Increase Pressure",
            "DecreasePressure" => "Decrease Pressure",
            _ => actionName
        };
    }
}
