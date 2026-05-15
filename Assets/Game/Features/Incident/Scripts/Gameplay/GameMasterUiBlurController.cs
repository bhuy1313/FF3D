using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameMasterUiBlurController : MonoBehaviour
{
    [Header("Blur")]
    [SerializeField] private GameObject blurObject;
    [SerializeField] private string blurObjectName = "Blur";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showRuntimeOverlay = true;
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F9;

    private static GameMasterUiBlurController instance;
    private readonly HashSet<Object> activeRequesters = new HashSet<Object>();
    private Vector2 debugScrollPosition;

    public static GameMasterUiBlurController Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("GameMasterUiBlurController: Duplicate instance found. Destroying duplicate.", this);
            Destroy(this);
            return;
        }

        instance = this;
        ResolveBlurObject();
        RefreshBlurVisibility();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleOverlayKey))
        {
            showRuntimeOverlay = !showRuntimeOverlay;
            LogDebug($"Runtime overlay toggled => {showRuntimeOverlay}");
        }
    }

    public static void SetBlurRequested(Object requester, bool requested)
    {
        if (requester == null)
        {
            return;
        }

        GameMasterUiBlurController controller = GetExistingInstance();
        if (controller == null)
        {
            if (requested)
            {
                Debug.LogWarning(
                    $"GameMasterUiBlurController: No active instance found while processing blur request from '{requester.name}'."
                );
            }

            return;
        }

        controller.LogDebug(
            $"Request from '{requester.name}' ({requester.GetType().Name}) => requested={requested}"
        );

        if (requested)
        {
            controller.activeRequesters.Add(requester);
        }
        else
        {
            controller.activeRequesters.Remove(requester);
        }

        controller.RefreshBlurVisibility();
    }

    private static GameMasterUiBlurController GetExistingInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<GameMasterUiBlurController>(FindObjectsInactive.Include);
        if (instance != null)
        {
            instance.LogDebug("Found existing GameMasterUiBlurController in scene.");
        }

        return instance;
    }

    private void ResolveBlurObject()
    {
        if (blurObject != null)
        {
            return;
        }

        blurObject = FindSceneObjectByName(blurObjectName);
        if (blurObject != null)
        {
            LogDebug($"Resolved blur object: '{blurObject.name}'.");
        }
        else
        {
            LogDebug($"Could not find blur object named '{blurObjectName}'.");
        }
    }

    private void RefreshBlurVisibility()
    {
        ResolveBlurObject();
        activeRequesters.RemoveWhere(requester => requester == null);

        bool shouldShowBlur = activeRequesters.Count > 0;
        LogDebug(
            $"RefreshBlurVisibility => requesters={activeRequesters.Count}, blurFound={(blurObject != null)}, shouldShow={shouldShowBlur}"
        );

        if (blurObject == null)
        {
            return;
        }

        if (blurObject.activeSelf != shouldShowBlur)
        {
            blurObject.SetActive(shouldShowBlur);
            LogDebug($"Set blur active state => {shouldShowBlur}");
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log($"[GameMasterUiBlurController] {message}", this);
    }

    private void OnGUI()
    {
        if (!showRuntimeOverlay)
        {
            return;
        }

        GUI.depth = -1000;

        const float width = 460f;
        const float height = 260f;
        Rect area = new Rect(12f, 12f, width, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.Box(area, GUIContent.none);
        GUI.color = previousColor;

        GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 10f, area.width - 20f, area.height - 20f));
        GUILayout.Label("<b>UI Blur Debug</b>");
        GUILayout.Label($"Toggle: {toggleOverlayKey}");
        GUILayout.Label($"Controller object: {name}");
        GUILayout.Label($"Blur target name: {blurObjectName}");
        GUILayout.Label($"Blur found: {(blurObject != null ? "Yes" : "No")}");
        GUILayout.Label($"Blur activeSelf: {(blurObject != null ? blurObject.activeSelf.ToString() : "N/A")}");
        GUILayout.Label($"Active requests: {activeRequesters.Count}");

        string requesterText = BuildRequesterDebugText();
        debugScrollPosition = GUILayout.BeginScrollView(debugScrollPosition, GUILayout.Height(140f));
        GUILayout.Label(requesterText);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private string BuildRequesterDebugText()
    {
        activeRequesters.RemoveWhere(requester => requester == null);

        if (activeRequesters.Count <= 0)
        {
            return "No active blur requesters.";
        }

        StringBuilder builder = new StringBuilder();
        foreach (Object requester in activeRequesters)
        {
            if (requester == null)
            {
                continue;
            }

            builder.Append("- ");
            builder.Append(requester.name);
            builder.Append(" (");
            builder.Append(requester.GetType().Name);
            builder.AppendLine(")");
        }

        return builder.ToString();
    }
}
