using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SubMenuEscapeHost : MonoBehaviour
{
    private static int suppressEscapeFrame = -1;

    [SerializeField] private SubMenuPanelController subMenuPanelController;
    [SerializeField] private WheelSelector wheelSelector;
    [SerializeField] private MissionEndOverlayController missionEndOverlayController;
    [SerializeField] private bool closePanelOnStart = true;
    [SerializeField] private GameObject settingPanelPrefab;
    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private RectTransform settingsHostRoot;
    [SerializeField] private RectTransform toastHostRoot;

    private GameObject settingsInstance;
    private CanvasGroup settingsCanvasGroup;
    private Setting_UIScript settingsUI;
    private Button settingsBackButton;
    private ToastContainerController runtimeToastContainer;

    public bool IsSettingsVisible => settingsCanvasGroup != null && settingsCanvasGroup.alpha > 0.001f;

    private void Start()
    {
        ResolveController();
        BindSubMenuActions();

        if (closePanelOnStart && subMenuPanelController != null)
        {
            subMenuPanelController.Close();
        }
    }

    private void Update()
    {
        if (IsMissionResultOpen())
        {
            ForceCloseAll();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (IsEscapeSuppressedForCurrentFrame())
        {
            return;
        }

        ResolveWheelSelector();
        if (wheelSelector != null && wheelSelector.IsSelectionWheelActive)
        {
            return;
        }

        if (IsSettingsOpen())
        {
            RequestCloseSettings();
            return;
        }

        ResolveController();
        if (subMenuPanelController == null)
        {
            return;
        }

        if (subMenuPanelController.IsOpen)
        {
            subMenuPanelController.Close();
        }
        else
        {
            subMenuPanelController.Open();
        }
    }

    private void ResolveController()
    {
        if (subMenuPanelController != null)
        {
            ResolveMissionEndOverlayController();
            return;
        }

        Transform panel = transform.root != null ? transform.root.Find("SubMenuPanel") : null;
        if (panel == null)
        {
            GameObject sceneObject = GameObject.Find("SubMenuPanel");
            panel = sceneObject != null ? sceneObject.transform : null;
        }

        if (panel != null)
        {
            subMenuPanelController = panel.GetComponent<SubMenuPanelController>();
        }

        ResolveMissionEndOverlayController();
    }

    private void ResolveWheelSelector()
    {
        if (wheelSelector != null)
        {
            return;
        }

        wheelSelector = FindAnyObjectByType<WheelSelector>(FindObjectsInactive.Include);
    }

    private void BindSubMenuActions()
    {
        if (subMenuPanelController == null)
        {
            return;
        }

        subMenuPanelController.SetSettingsAction(OpenSettingsPanel);
    }

    private void OpenSettingsPanel()
    {
        if (!EnsureSettingsPanel())
        {
            return;
        }

        if (subMenuPanelController != null)
        {
            subMenuPanelController.Close();
        }

        if (settingsUI != null)
        {
            settingsUI.BeginEditSession();
        }

        SetSettingsVisible(true);
    }

    private void RequestCloseSettings()
    {
        if (settingsUI != null && settingsUI.HandleBackRequest(CloseSettingsImmediate))
        {
            return;
        }

        CloseSettingsImmediate();
    }

    private void CloseSettingsImmediate()
    {
        SetSettingsVisible(false);
        if (subMenuPanelController != null)
        {
            subMenuPanelController.Open();
        }
    }

    private bool IsSettingsOpen()
    {
        return settingsCanvasGroup != null && settingsCanvasGroup.alpha > 0.001f;
    }

    private bool EnsureSettingsPanel()
    {
        if (settingsInstance == null)
        {
            if (settingPanelPrefab == null)
            {
                Debug.LogWarning("SubMenuEscapeHost: Setting panel prefab is not assigned.", this);
                return false;
            }

            RectTransform host = settingsHostRoot != null ? settingsHostRoot : GetCanvasRect();
            if (host == null)
            {
                Debug.LogWarning("SubMenuEscapeHost: Could not find canvas host for Setting panel.", this);
                return false;
            }

            settingsInstance = Instantiate(settingPanelPrefab, host);
            settingsInstance.name = settingPanelPrefab.name;
            settingsInstance.transform.SetAsLastSibling();
        }

        if (settingsCanvasGroup == null)
        {
            settingsCanvasGroup = settingsInstance.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
            {
                settingsCanvasGroup = settingsInstance.AddComponent<CanvasGroup>();
            }
        }

        if (settingsUI == null)
        {
            settingsUI = settingsInstance.GetComponentInChildren<Setting_UIScript>(true);
            if (settingsUI == null)
            {
                Debug.LogWarning("SubMenuEscapeHost: Setting panel instance is missing Setting_UIScript.", settingsInstance);
            }
        }

        if (settingsBackButton == null)
        {
            settingsBackButton = FindNestedButton(settingsInstance.transform, "btnBack");
            if (settingsBackButton == null)
            {
                Debug.LogWarning("SubMenuEscapeHost: Could not find btnBack on Setting panel instance.", settingsInstance);
            }
            else
            {
                settingsBackButton.onClick.RemoveAllListeners();
                settingsBackButton.onClick.AddListener(RequestCloseSettings);
            }
        }

        EnsureRuntimeToastContainer();
        SetSettingsVisible(false);
        return true;
    }

    private void EnsureRuntimeToastContainer()
    {
        if (runtimeToastContainer != null)
        {
            return;
        }

        if (toastPrefab == null)
        {
            Debug.LogWarning("SubMenuEscapeHost: Toast prefab is not assigned. Settings back confirmation will be unavailable if changes are unsaved.", this);
            return;
        }

        RectTransform host = toastHostRoot != null ? toastHostRoot : GetCanvasRect();
        if (host == null)
        {
            Debug.LogWarning("SubMenuEscapeHost: Could not find canvas host for Toast container.", this);
            return;
        }

        GameObject go = new GameObject("Toast Container", typeof(RectTransform), typeof(CanvasGroup), typeof(ToastContainerController));
        go.transform.SetParent(host, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        runtimeToastContainer = go.GetComponent<ToastContainerController>();
        runtimeToastContainer.Configure(toastPrefab, rect);
    }

    private void SetSettingsVisible(bool visible)
    {
        if (settingsInstance == null || settingsCanvasGroup == null)
        {
            return;
        }

        if (visible)
        {
            settingsInstance.transform.SetAsLastSibling();
            settingsCanvasGroup.alpha = 1f;
            settingsCanvasGroup.interactable = true;
            settingsCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.interactable = false;
            settingsCanvasGroup.blocksRaycasts = false;
        }
    }

    public void ForceCloseAll()
    {
        if (IsSettingsOpen())
        {
            SetSettingsVisible(false);
        }

        if (subMenuPanelController != null && subMenuPanelController.IsOpen)
        {
            subMenuPanelController.Close();
        }
    }

    private RectTransform GetCanvasRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.transform as RectTransform : null;
    }

    private static Button FindNestedButton(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindDeepChild(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static void SuppressEscapeForCurrentFrame()
    {
        suppressEscapeFrame = Time.frameCount;
    }

    private static bool IsEscapeSuppressedForCurrentFrame()
    {
        return suppressEscapeFrame == Time.frameCount;
    }

    private void ResolveMissionEndOverlayController()
    {
        if (
            missionEndOverlayController != null
            && missionEndOverlayController.gameObject.scene == gameObject.scene
        )
        {
            return;
        }

        missionEndOverlayController = null;
        MissionEndOverlayController[] controllers = FindObjectsByType<MissionEndOverlayController>(FindObjectsInactive.Include);
        Scene currentScene = gameObject.scene;
        for (int i = 0; i < controllers.Length; i++)
        {
            MissionEndOverlayController candidate = controllers[i];
            if (candidate == null || candidate.gameObject.scene != currentScene)
            {
                continue;
            }

            missionEndOverlayController = candidate;
            break;
        }
    }

    private bool IsMissionResultOpen()
    {
        ResolveMissionEndOverlayController();
        return missionEndOverlayController != null
            && missionEndOverlayController.isActiveAndEnabled
            && missionEndOverlayController.gameObject.activeInHierarchy
            && missionEndOverlayController.IsResultOverlayOpen;
    }
}
