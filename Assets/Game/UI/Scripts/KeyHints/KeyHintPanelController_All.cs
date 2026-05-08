using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyHintPanelController_All : MonoBehaviour
{
    private enum ObjectKind
    {
        None,
        Door,
        Rescuable,
        SafeZone,
        HoseConnection,
        Breakable,
        FireHose,
        Tool,
        Explosive,
        Grabbable,
        Usable,
        Other,
    }

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

    private readonly struct ObjectContextAnalysis
    {
        public ObjectContextAnalysis(
            GameObject targetObject,
            ObjectKind kind,
            bool targetIsPickupable,
            bool targetIsGrabbable,
            FireHose fireHose,
            Tool tool,
            IUsable usable,
            SafeZone safeZone,
            Door door,
            Rescuable rescuable,
            FireHoseConnectionPoint hoseConnection,
            Breakable breakable,
            Explosive explosive,
            IInteractable interactable,
            ICommandable commandable)
        {
            TargetObject = targetObject;
            Kind = kind;
            TargetIsPickupable = targetIsPickupable;
            TargetIsGrabbable = targetIsGrabbable;
            FireHose = fireHose;
            Tool = tool;
            Usable = usable;
            SafeZone = safeZone;
            Door = door;
            Rescuable = rescuable;
            HoseConnection = hoseConnection;
            Breakable = breakable;
            Explosive = explosive;
            Interactable = interactable;
            Commandable = commandable;
        }

        public GameObject TargetObject { get; }
        public ObjectKind Kind { get; }
        public bool TargetIsPickupable { get; }
        public bool TargetIsGrabbable { get; }
        public FireHose FireHose { get; }
        public Tool Tool { get; }
        public IUsable Usable { get; }
        public SafeZone SafeZone { get; }
        public Door Door { get; }
        public Rescuable Rescuable { get; }
        public FireHoseConnectionPoint HoseConnection { get; }
        public Breakable Breakable { get; }
        public Explosive Explosive { get; }
        public IInteractable Interactable { get; }
        public ICommandable Commandable { get; }
    }

    private readonly struct ContextState
    {
        public ContextState(
            IncidentMissionSystem.MissionState missionState,
            string missionId,
            string controlScheme,
            int inventoryItemCount,
            bool isGrabActive,
            bool isCarryingRescuable,
            bool isAwaitingCommandDestination,
            GameObject hoveredCommandTarget,
            GameObject selectedCommandTarget,
            GameObject currentTarget,
            ObjectKind targetKind,
            bool targetDoorIsOpen,
            bool targetRequiresStabilization,
            Object connectedHose,
            GameObject heldObject,
            ObjectKind heldKind)
        {
            MissionState = missionState;
            MissionId = missionId ?? string.Empty;
            ControlScheme = controlScheme ?? string.Empty;
            InventoryItemCount = inventoryItemCount;
            IsGrabActive = isGrabActive;
            IsCarryingRescuable = isCarryingRescuable;
            IsAwaitingCommandDestination = isAwaitingCommandDestination;
            HoveredCommandTarget = hoveredCommandTarget;
            SelectedCommandTarget = selectedCommandTarget;
            CurrentTarget = currentTarget;
            TargetKind = targetKind;
            TargetDoorIsOpen = targetDoorIsOpen;
            TargetRequiresStabilization = targetRequiresStabilization;
            ConnectedHose = connectedHose;
            HeldObject = heldObject;
            HeldKind = heldKind;
        }

        public IncidentMissionSystem.MissionState MissionState { get; }
        public string MissionId { get; }
        public string ControlScheme { get; }
        public int InventoryItemCount { get; }
        public bool IsGrabActive { get; }
        public bool IsCarryingRescuable { get; }
        public bool IsAwaitingCommandDestination { get; }
        public GameObject HoveredCommandTarget { get; }
        public GameObject SelectedCommandTarget { get; }
        public GameObject CurrentTarget { get; }
        public ObjectKind TargetKind { get; }
        public bool TargetDoorIsOpen { get; }
        public bool TargetRequiresStabilization { get; }
        public Object ConnectedHose { get; }
        public GameObject HeldObject { get; }
        public ObjectKind HeldKind { get; }
    }

    [System.Serializable]
    private sealed class ContextualHintProfile
    {
        public string missionId;
        public List<string> actionNames = new List<string>();

        public bool Matches(string currentMissionId)
        {
            bool missionMatches = string.IsNullOrWhiteSpace(missionId) ||
                                  string.Equals(missionId.Trim(), currentMissionId, System.StringComparison.OrdinalIgnoreCase);
            return missionMatches;
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
    [SerializeField] private float contextualCheckInterval = 0.1f;
    [SerializeField] private List<string> defaultContextActionNames = new List<string>();
    [SerializeField] private List<ContextualHintProfile> contextualHintProfiles = new List<ContextualHintProfile>();

    private readonly List<KeyHintItemView> spawned = new();
    private readonly List<ActionHint> hintEntriesBuffer = new();
    private readonly List<RenderedHint> renderedHintsBuffer = new();
    private ContextState lastContextState;
    private bool rebuildRequested = true;
    private float nextContextCheckTime;

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

        RequestRebuild();
    }

    private void Update()
    {
        if (!useContextualMissionHints)
        {
            if (rebuildRequested)
            {
                Rebuild();
            }

            return;
        }

        if (rebuildRequested)
        {
            Rebuild();
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextContextCheckTime)
        {
            return;
        }

        nextContextCheckTime = now + Mathf.Max(0.01f, contextualCheckInterval);
        ContextState currentState = CaptureContextState();
        if (!AreContextStatesEqual(currentState, lastContextState))
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
        RequestRebuild();
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
            RequestRebuild();
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        RequestRebuild();
    }

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        ResolveReferences();
        rebuildRequested = false;
        nextContextCheckTime = Time.unscaledTime + Mathf.Max(0.01f, contextualCheckInterval);

        if (playerInput == null || playerInput.actions == null || itemPrefab == null) return;

        var asset = playerInput.actions;
        var map = asset.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null) return;

        string scheme = playerInput.currentControlScheme ?? "";
        renderedHintsBuffer.Clear();
        if (TryRebuildFromService(map, scheme, renderedHintsBuffer))
        {
            ApplyRenderedHints(renderedHintsBuffer);
            UpdateCachedContextState();
            return;
        }

        hintEntriesBuffer.Clear();
        AppendLiveContextHints(hintEntriesBuffer);

        if (TryResolveCurrentContextActionNames(out List<string> contextualActionNames))
        {
            for (int i = 0; i < contextualActionNames.Count; i++)
            {
                AddUniqueHint(hintEntriesBuffer, contextualActionNames[i]);
            }

            AddHintEntries(map, hintEntriesBuffer, scheme, renderedHintsBuffer);
            ApplyRenderedHints(renderedHintsBuffer);
            UpdateCachedContextState();
            return;
        }

        foreach (var action in map.actions)
        {
            if (hideLookAction && action.name == "Look")
                continue;

            if (!includeValueActions && action.type != InputActionType.Button)
                continue;

            AddUniqueHint(hintEntriesBuffer, action.name);
        }

        AddHintEntries(map, hintEntriesBuffer, scheme, renderedHintsBuffer);
        ApplyRenderedHints(renderedHintsBuffer);
        UpdateCachedContextState();
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

    private void RequestRebuild()
    {
        rebuildRequested = true;
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

        if (TryGetConfiguredContextActionNames(missionId, out actionNames))
        {
            return true;
        }

        if (useBuiltInTutorialProfiles && TryGetBuiltInTutorialActionNames(missionId, out actionNames))
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

    private bool TryGetConfiguredContextActionNames(string missionId, out List<string> actionNames)
    {
        actionNames = null;
        if (contextualHintProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < contextualHintProfiles.Count; i++)
        {
            ContextualHintProfile profile = contextualHintProfiles[i];
            if (profile == null || !profile.Matches(missionId))
            {
                continue;
            }

            actionNames = profile.actionNames ?? new List<string>();
            return true;
        }

        return false;
    }

    private static bool TryGetBuiltInTutorialActionNames(string missionId, out List<string> actionNames)
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
        bool layoutChanged = false;

        if (renderedHints == null)
        {
            layoutChanged = DeactivateUnusedItems(0);
            if (layoutChanged)
            {
                ForceContainerLayoutRebuild();
            }

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
                layoutChanged = true;
            }

            layoutChanged |= item.Set(renderedHints[i].Key, renderedHints[i].Label);
        }

        layoutChanged |= DeactivateUnusedItems(renderedHints.Count);
        if (layoutChanged)
        {
            ForceContainerLayoutRebuild();
        }
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

    private bool DeactivateUnusedItems(int usedCount)
    {
        bool changed = false;
        for (int i = usedCount; i < spawned.Count; i++)
        {
            if (spawned[i] != null && spawned[i].gameObject.activeSelf)
            {
                spawned[i].gameObject.SetActive(false);
                changed = true;
            }
        }

        return changed;
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
        ObjectContextAnalysis heldAnalysis = AnalyzeObjectContext(heldObject);
        ObjectContextAnalysis targetAnalysis = AnalyzeObjectContext(currentTarget);

        FireHose heldFireHose = heldAnalysis.FireHose;
        Tool heldTool = heldAnalysis.Tool;
        IUsable heldUsable = heldAnalysis.Usable;
        bool targetIsPickupable = targetAnalysis.TargetIsPickupable;
        bool targetIsGrabbable = targetAnalysis.TargetIsGrabbable;
        SafeZone targetSafeZone = targetAnalysis.SafeZone;
        Door targetDoor = targetAnalysis.Door;
        Rescuable targetRescuable = targetAnalysis.Rescuable;
        FireHoseConnectionPoint targetHoseConnection = targetAnalysis.HoseConnection;
        Breakable targetBreakable = targetAnalysis.Breakable;
        Explosive targetExplosive = targetAnalysis.Explosive;
        Window targetWindow = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Window>(currentTarget);
        IInteractable targetInteractable = targetAnalysis.Interactable;
        ICommandable targetCommandable = targetAnalysis.Commandable;

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

        if (targetWindow != null && targetWindow.CanClimbOver(interactionSystem.gameObject))
        {
            AddUniqueHint(hints, "ClimbOver", "Climb Over");
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

        ObjectContextAnalysis analysis = AnalyzeObjectContext(target);
        if (analysis.FireHose != null)
        {
            return "Pick Up Hose";
        }

        if (analysis.Tool != null)
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

    private ContextState CaptureContextState()
    {
        string scheme = playerInput != null ? playerInput.currentControlScheme ?? string.Empty : string.Empty;
        if (!useContextualMissionHints || missionSystem == null)
        {
            return new ContextState(
                IncidentMissionSystem.MissionState.Idle,
                string.Empty,
                scheme,
                0,
                false,
                false,
                false,
                null,
                null,
                null,
                ObjectKind.None,
                false,
                false,
                null,
                null,
                ObjectKind.None);
        }

        GameObject currentTarget = interactionSystem != null ? interactionSystem.CurrentTarget : null;
        GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;
        GameObject occupyingObject = interactionSystem != null ? interactionSystem.CurrentHandOccupyingObject : heldObject;
        bool isGrabActive = interactionSystem != null && interactionSystem.IsGrabActive;
        bool isCarryingRescuable = interactionSystem != null && interactionSystem.CurrentCarryWeightKg > 0.01f;
        int inventoryItemCount = inventorySystem != null ? inventorySystem.ItemCount : 0;
        bool isAwaitingCommandDestination = commandSystem != null && commandSystem.IsAwaitingDestination;
        GameObject hoveredCommandTarget = commandSystem != null && commandSystem.HoveredCommandTarget != null
            ? commandSystem.HoveredCommandTarget
            : null;
        GameObject selectedCommandTarget = commandSystem != null && commandSystem.SelectedCommandTarget != null
            ? commandSystem.SelectedCommandTarget
            : null;
        ObjectContextAnalysis targetAnalysis = AnalyzeObjectContext(currentTarget);
        ObjectContextAnalysis heldAnalysis = AnalyzeObjectContext(occupyingObject);

        return new ContextState(
            missionSystem.State,
            missionSystem.MissionId,
            scheme,
            inventoryItemCount,
            isGrabActive,
            isCarryingRescuable,
            isAwaitingCommandDestination,
            hoveredCommandTarget,
            selectedCommandTarget,
            currentTarget,
            targetAnalysis.Kind,
            targetAnalysis.Door != null && targetAnalysis.Door.IsOpen,
            targetAnalysis.Rescuable != null && targetAnalysis.Rescuable.RequiresStabilization,
            targetAnalysis.HoseConnection != null ? targetAnalysis.HoseConnection.ConnectedHose : null,
            occupyingObject,
            heldAnalysis.Kind);
    }

    private void UpdateCachedContextState()
    {
        lastContextState = CaptureContextState();
    }

    private static bool AreContextStatesEqual(ContextState left, ContextState right)
    {
        return left.MissionState == right.MissionState &&
               left.InventoryItemCount == right.InventoryItemCount &&
               left.IsGrabActive == right.IsGrabActive &&
               left.IsCarryingRescuable == right.IsCarryingRescuable &&
               left.IsAwaitingCommandDestination == right.IsAwaitingCommandDestination &&
               left.CurrentTarget == right.CurrentTarget &&
               left.TargetKind == right.TargetKind &&
               left.TargetDoorIsOpen == right.TargetDoorIsOpen &&
               left.TargetRequiresStabilization == right.TargetRequiresStabilization &&
               left.ConnectedHose == right.ConnectedHose &&
               left.HeldObject == right.HeldObject &&
               left.HeldKind == right.HeldKind &&
               left.HoveredCommandTarget == right.HoveredCommandTarget &&
               left.SelectedCommandTarget == right.SelectedCommandTarget &&
               string.Equals(left.MissionId, right.MissionId, System.StringComparison.Ordinal) &&
               string.Equals(left.ControlScheme, right.ControlScheme, System.StringComparison.Ordinal);
    }

    private static ObjectContextAnalysis AnalyzeObjectContext(GameObject target)
    {
        if (target == null)
        {
            return new ObjectContextAnalysis(null, ObjectKind.None, false, false, null, null, null, null, null, null, null, null, null, null, null);
        }

        FireHose fireHose = FindComponentInTargetHierarchy<FireHose>(target);
        Tool tool = FindComponentInTargetHierarchy<Tool>(target);
        IUsable usable = FindComponentInTargetHierarchy<IUsable>(target);
        SafeZone safeZone = FindComponentInTargetHierarchy<SafeZone>(target);
        Door door = FindComponentInTargetHierarchy<Door>(target);
        Rescuable rescuable = FindComponentInTargetHierarchy<Rescuable>(target);
        FireHoseConnectionPoint hoseConnection = FindComponentInTargetHierarchy<FireHoseConnectionPoint>(target);
        Breakable breakable = FindComponentInTargetHierarchy<Breakable>(target);
        Explosive explosive = FindComponentInTargetHierarchy<Explosive>(target);
        IInteractable interactable = FindComponentInTargetHierarchy<IInteractable>(target);
        ICommandable commandable = FindComponentInTargetHierarchy<ICommandable>(target);
        bool targetIsPickupable = FindComponentInTargetHierarchy<IPickupable>(target) != null;
        bool targetIsGrabbable = FindComponentInTargetHierarchy<IGrabbable>(target) != null;

        ObjectKind kind = ObjectKind.Other;
        if (door != null)
        {
            kind = ObjectKind.Door;
        }
        else if (rescuable != null)
        {
            kind = ObjectKind.Rescuable;
        }
        else if (safeZone != null)
        {
            kind = ObjectKind.SafeZone;
        }
        else if (hoseConnection != null)
        {
            kind = ObjectKind.HoseConnection;
        }
        else if (breakable != null)
        {
            kind = ObjectKind.Breakable;
        }
        else if (fireHose != null)
        {
            kind = ObjectKind.FireHose;
        }
        else if (tool != null)
        {
            kind = ObjectKind.Tool;
        }
        else if (explosive != null)
        {
            kind = ObjectKind.Explosive;
        }
        else if (targetIsGrabbable)
        {
            kind = ObjectKind.Grabbable;
        }
        else if (usable != null)
        {
            kind = ObjectKind.Usable;
        }

        return new ObjectContextAnalysis(
            target,
            kind,
            targetIsPickupable,
            targetIsGrabbable,
            fireHose,
            tool,
            usable,
            safeZone,
            door,
            rescuable,
            hoseConnection,
            breakable,
            explosive,
            interactable,
            commandable);
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
            "ClimbOver" => "Climb Over",
            "Slot1" => "Slot 1",
            "Slot2" => "Slot 2",
            "Slot3" => "Slot 3",
            "Slot4" => "Slot 4",
            "Slot5" => "Slot 5",
            "Slot6" => "Slot 6",
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
