using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyHintPanelController_All : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;

    [Header("UI")]
    [SerializeField] private Transform container;        // Panel with VerticalLayoutGroup
    [SerializeField] private KeyHintItemView itemPrefab; // Prefab with 2 TMP fields + KeyHintItemView

    [Header("Config")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private bool includeValueActions = true; // Include Move/Look value actions
    [SerializeField] private bool hideLookAction = true;

    private readonly List<KeyHintItemView> spawned = new();

    private void Awake()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        if (container == null) container = transform;
    }

    private void OnEnable()
    {
        if (playerInput != null)
            playerInput.onControlsChanged += OnControlsChanged;

        InputSystem.onActionChange += OnActionChange;

        Rebuild();
    }

    private void OnDisable()
    {
        if (playerInput != null)
            playerInput.onControlsChanged -= OnControlsChanged;

        InputSystem.onActionChange -= OnActionChange;
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

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        ClearAll();

        if (playerInput == null || playerInput.actions == null || itemPrefab == null) return;

        var asset = playerInput.actions;
        var map = asset.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null) return;

        string scheme = playerInput.currentControlScheme ?? "";

        foreach (var action in map.actions)
        {
            if (hideLookAction && action.name == "Look")
                continue;

            if (!includeValueActions && action.type != InputActionType.Button)
                continue;

            string key = KeyHintBindingUtil.GetKeyDisplay(action, scheme);
            if (string.IsNullOrEmpty(key))
                continue;

            var item = Instantiate(itemPrefab, container);
            item.Set(key, GetActionDisplayName(action.name));
            spawned.Add(item);
        }
    }

    private void ClearAll()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }

        spawned.Clear();
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
