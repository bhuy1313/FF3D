using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;

            // groups chứa tên scheme (vd "Keyboard&Mouse", "Gamepad")
            if (!string.IsNullOrEmpty(b.groups) &&
                b.groups.IndexOf(scheme, StringComparison.OrdinalIgnoreCase) >= 0)
                return i;
        }

        return -1;
    }

    private static int FindFirstUsableBinding(InputAction action)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (string.IsNullOrEmpty(b.effectivePath)) continue;
            return i;
        }
        return -1;
    }
}
