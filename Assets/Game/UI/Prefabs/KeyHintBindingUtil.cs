using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class KeyHintBindingUtil : MonoBehaviour
{
    public static string GetKeyDisplay(InputAction action, string controlScheme)
    {
        if (action == null) return "";

        int idx = FindBindingIndexForScheme(action, controlScheme);
        if (idx < 0) idx = FindFirstUsableBinding(action);
        if (idx < 0) return "";

        if (TryGetPreferredCompositeDisplay(action, idx, controlScheme, out string compositeDisplay))
        {
            return compositeDisplay;
        }

        return action.GetBindingDisplayString(idx);
    }

    private static int FindBindingIndexForScheme(InputAction action, string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return -1;

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.isComposite)
            {
                if (CompositeMatchesScheme(bindings, i, scheme))
                    return i;

                continue;
            }

            if (binding.isPartOfComposite) continue;
            if (GroupsContainScheme(binding.groups, scheme))
                return i;
        }

        return -1;
    }

    private static int FindFirstUsableBinding(InputAction action)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.isComposite)
            {
                if (CompositeHasUsablePart(bindings, i))
                    return i;

                continue;
            }

            if (binding.isPartOfComposite) continue;
            if (string.IsNullOrEmpty(binding.effectivePath)) continue;
            return i;
        }

        return -1;
    }

    private static bool CompositeMatchesScheme(ReadOnlyArray<InputBinding> bindings, int compositeIndex, string scheme)
    {
        for (int i = compositeIndex + 1; i < bindings.Count; i++)
        {
            var part = bindings[i];
            if (!part.isPartOfComposite)
                break;

            if (string.IsNullOrEmpty(part.effectivePath))
                continue;

            if (GroupsContainScheme(part.groups, scheme))
                return true;
        }

        return false;
    }

    private static bool CompositeHasUsablePart(ReadOnlyArray<InputBinding> bindings, int compositeIndex)
    {
        for (int i = compositeIndex + 1; i < bindings.Count; i++)
        {
            var part = bindings[i];
            if (!part.isPartOfComposite)
                break;

            if (!string.IsNullOrEmpty(part.effectivePath))
                return true;
        }

        return false;
    }

    private static bool GroupsContainScheme(string groups, string scheme)
    {
        if (string.IsNullOrEmpty(groups) || string.IsNullOrEmpty(scheme))
            return false;

        string[] split = groups.Split(';');
        for (int i = 0; i < split.Length; i++)
        {
            if (string.Equals(split[i], scheme, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryGetPreferredCompositeDisplay(InputAction action, int bindingIndex, string controlScheme, out string display)
    {
        display = null;
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return false;
        }

        InputBinding composite = action.bindings[bindingIndex];
        if (!composite.isComposite)
        {
            return false;
        }

        if (!string.Equals(controlScheme, "KeyboardMouse", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(composite.path, "2DVector(mode=1)", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string up = null;
        string down = null;
        string left = null;
        string right = null;

        for (int i = bindingIndex + 1; i < action.bindings.Count; i++)
        {
            InputBinding part = action.bindings[i];
            if (!part.isPartOfComposite)
            {
                break;
            }

            if (!GroupsContainScheme(part.groups, controlScheme))
            {
                continue;
            }

            string keyName = GetPreferredKeyboardPartName(part.effectivePath);
            if (string.IsNullOrEmpty(keyName))
            {
                continue;
            }

            switch (part.name)
            {
                case "up":
                    up ??= keyName;
                    break;
                case "down":
                    down ??= keyName;
                    break;
                case "left":
                    left ??= keyName;
                    break;
                case "right":
                    right ??= keyName;
                    break;
            }
        }

        if (up == "W" && left == "A" && down == "S" && right == "D")
        {
            display = "W A S D";
            return true;
        }

        if (!string.IsNullOrEmpty(up) &&
            !string.IsNullOrEmpty(left) &&
            !string.IsNullOrEmpty(down) &&
            !string.IsNullOrEmpty(right))
        {
            display = $"{up} {left} {down} {right}";
            return true;
        }

        return false;
    }

    private static string GetPreferredKeyboardPartName(string effectivePath)
    {
        if (string.IsNullOrEmpty(effectivePath))
        {
            return null;
        }

        string explicitName = effectivePath switch
        {
            "<Keyboard>/w" => "W",
            "<Keyboard>/a" => "A",
            "<Keyboard>/s" => "S",
            "<Keyboard>/d" => "D",
            _ => null
        };

        if (!string.IsNullOrEmpty(explicitName))
        {
            return explicitName;
        }

        string humanReadable = InputControlPath.ToHumanReadableString(
            effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        return string.IsNullOrWhiteSpace(humanReadable) ? null : humanReadable;
    }
}
