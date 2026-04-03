using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyHintPanelController_All : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;

    [Header("UI")]
    [SerializeField] private Transform container;          // Panel có VerticalLayoutGroup
    [SerializeField] private KeyHintItemView itemPrefab;   // Prefab có 2 TMP + script KeyHintItemView

    [Header("Config")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private bool includeValueActions = true; // Move/Look (Vector2) nếu muốn show

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
        // đổi control scheme -> đổi key hiển thị
        Rebuild();
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        // rebind -> đổi key hiển thị
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
            // 1) Bỏ qua Look hoàn toàn
            if (action.name == "Look")
                continue;

            // 2) Move: hiển thị WASD cố định
            if (action.name == "Move")
            {
                var item = Instantiate(itemPrefab, container);
                item.Set("WASD", action.name);
                spawned.Add(item);
                continue;
            }

            // Nếu không muốn show Value actions (Move/Look) - Move đã xử lý ở trên rồi
            if (!includeValueActions && action.type != InputActionType.Button)
                continue;

            string key = KeyHintBindingUtil.GetKeyDisplay(action, scheme);
            if (string.IsNullOrEmpty(key))
                continue;

            var itemNormal = Instantiate(itemPrefab, container);
            itemNormal.Set(key, action.name);
            spawned.Add(itemNormal);
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
}
