using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class InputBindingOverridesStore
{
    private const string BindingOverridesPlayerPrefsKey = "settings.input.binding_overrides";

#if ENABLE_INPUT_SYSTEM
    public static string GetCurrentOverridesJson(InputActionAsset actions)
    {
        if (actions == null)
        {
            return string.Empty;
        }

        string json = actions.SaveBindingOverridesAsJson();
        return string.IsNullOrWhiteSpace(json) ? string.Empty : json;
    }

    public static string GetSavedOverridesJson()
    {
        if (!PlayerPrefs.HasKey(BindingOverridesPlayerPrefsKey))
        {
            return string.Empty;
        }

        string json = PlayerPrefs.GetString(BindingOverridesPlayerPrefsKey, string.Empty);
        return string.IsNullOrWhiteSpace(json) ? string.Empty : json;
    }

    public static void SaveCurrentOverrides(InputActionAsset actions)
    {
        if (actions == null)
        {
            return;
        }

        PlayerPrefs.SetString(BindingOverridesPlayerPrefsKey, GetCurrentOverridesJson(actions));
    }

    public static void ApplySavedOverrides(InputActionAsset actions)
    {
        ApplyOverridesJson(actions, GetSavedOverridesJson());
    }

    public static void ApplyOverridesJson(InputActionAsset actions, string json)
    {
        if (actions == null)
        {
            return;
        }

        actions.RemoveAllBindingOverrides();
        if (!string.IsNullOrWhiteSpace(json))
        {
            actions.LoadBindingOverridesFromJson(json);
        }
    }

    public static void ResetToDefault(InputActionAsset actions, bool clearSavedOverrides)
    {
        ApplyOverridesJson(actions, string.Empty);
        if (clearSavedOverrides)
        {
            PlayerPrefs.DeleteKey(BindingOverridesPlayerPrefsKey);
        }
    }

    public static void ApplySavedOverridesToActivePlayerInputs()
    {
        ApplyOverridesJsonToActivePlayerInputs(GetSavedOverridesJson());
    }

    public static void ApplyCurrentOverridesToActivePlayerInputs(InputActionAsset sourceActions)
    {
        ApplyOverridesJsonToActivePlayerInputs(GetCurrentOverridesJson(sourceActions));
    }

    private static void ApplyOverridesJsonToActivePlayerInputs(string json)
    {
        PlayerInput[] playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsInactive.Include);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            PlayerInput playerInput = playerInputs[i];
            if (playerInput == null || playerInput.actions == null)
            {
                continue;
            }

            ApplyOverridesJson(playerInput.actions, json);
        }
    }
#else
    public static string GetCurrentOverridesJson(object actions)
    {
        return string.Empty;
    }

    public static string GetSavedOverridesJson()
    {
        return string.Empty;
    }

    public static void SaveCurrentOverrides(object actions)
    {
    }

    public static void ApplySavedOverrides(object actions)
    {
    }

    public static void ApplyOverridesJson(object actions, string json)
    {
    }

    public static void ResetToDefault(object actions, bool clearSavedOverrides)
    {
        if (clearSavedOverrides)
        {
            PlayerPrefs.DeleteKey(BindingOverridesPlayerPrefsKey);
        }
    }

    public static void ApplySavedOverridesToActivePlayerInputs()
    {
    }

    public static void ApplyCurrentOverridesToActivePlayerInputs(object sourceActions)
    {
    }
#endif
}
