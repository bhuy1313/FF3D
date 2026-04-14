using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentVentilationPresetBinding : MonoBehaviour
{
    [Header("Preset Match")]
    [SerializeField] private string[] presetKeys = Array.Empty<string>();
    [SerializeField] private bool isFallbackBinding;

    [Header("Doors")]
    [SerializeField] private bool forceUnlockDoorsWhenOpening = true;
    [SerializeField] private Door[] doorsToOpen = Array.Empty<Door>();
    [SerializeField] private Door[] doorsToClose = Array.Empty<Door>();

    [Header("Windows")]
    [SerializeField] private Window[] windowsToOpen = Array.Empty<Window>();
    [SerializeField] private Window[] windowsToClose = Array.Empty<Window>();

    [Header("Vents")]
    [SerializeField] private Vent[] ventsToOpen = Array.Empty<Vent>();
    [SerializeField] private Vent[] ventsToClose = Array.Empty<Vent>();

    [Header("Scene Objects")]
    [SerializeField] private GameObject[] activateObjects = Array.Empty<GameObject>();
    [SerializeField] private GameObject[] deactivateObjects = Array.Empty<GameObject>();

    public bool IsFallbackBinding => isFallbackBinding;

    public bool MatchesPreset(string ventilationPreset)
    {
        if (string.IsNullOrWhiteSpace(ventilationPreset) || presetKeys == null || presetKeys.Length <= 0)
        {
            return false;
        }

        string normalizedPreset = NormalizePreset(ventilationPreset);
        for (int i = 0; i < presetKeys.Length; i++)
        {
            if (normalizedPreset == NormalizePreset(presetKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyBinding()
    {
        ApplyDoors(doorsToOpen, true);
        ApplyDoors(doorsToClose, false);
        ApplyWindows(windowsToOpen, true);
        ApplyWindows(windowsToClose, false);
        ApplyVents(ventsToOpen, true);
        ApplyVents(ventsToClose, false);
        ApplyGameObjectState(activateObjects, true);
        ApplyGameObjectState(deactivateObjects, false);
    }

    private void ApplyDoors(Door[] targets, bool isOpen)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Door door = targets[i];
            if (door != null)
            {
                door.SetOpenState(isOpen, forceUnlockDoorsWhenOpening);
            }
        }
    }

    private static void ApplyWindows(Window[] targets, bool isOpen)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Window window = targets[i];
            if (window != null)
            {
                window.SetOpenState(isOpen);
            }
        }
    }

    private static void ApplyVents(Vent[] targets, bool isOpen)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Vent vent = targets[i];
            if (vent != null)
            {
                vent.SetOpenState(isOpen);
            }
        }
    }

    private static void ApplyGameObjectState(GameObject[] targets, bool isActive)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }
    }

    private static string NormalizePreset(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
