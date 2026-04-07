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
}
