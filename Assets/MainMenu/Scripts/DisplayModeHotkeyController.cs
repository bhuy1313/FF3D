using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-1000)]
public sealed class DisplayModeHotkeyController : MonoBehaviour
{
    private const string RuntimeObjectName = "__DisplayModeHotkeyController";

    public static void EnsureCreated()
    {
        if (FindAnyObjectByType<DisplayModeHotkeyController>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<DisplayModeHotkeyController>();
    }

    private void Awake()
    {
        DisplayModeHotkeyController existing = FindAnyObjectByType<DisplayModeHotkeyController>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (WasToggleHotkeyPressedThisFrame())
        {
            DisplaySettingsService.ToggleFullScreenWindowed();
        }
    }

    private static bool WasToggleHotkeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F11))
        {
            return true;
        }
#endif
        return false;
    }
}
