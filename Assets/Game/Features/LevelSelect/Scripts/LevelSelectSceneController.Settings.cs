using UnityEngine;
using UnityEngine.UI;

public partial class LevelSelectSceneController
{
    private void OpenSettingsPanel()
    {
        if (!EnsureSettingsPanel())
        {
            return;
        }

        CloseLevelInfo();
        CloseSubMenu();

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
        OpenSubMenu();
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
                Debug.LogWarning("LevelSelectSceneController: Setting panel prefab is not assigned.", this);
                return false;
            }

            RectTransform host = settingsHostRoot != null ? settingsHostRoot : GetCanvasRect();
            if (host == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Could not resolve a host canvas for Setting panel.", this);
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
                Debug.LogWarning("LevelSelectSceneController: Setting panel instance is missing Setting_UIScript.", settingsInstance);
            }
        }

        if (settingsBackButton == null)
        {
            settingsBackButton = FindNestedButton(settingsInstance.transform, "btnBack");
            if (settingsBackButton == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Could not find btnBack on Setting panel instance.", settingsInstance);
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

        runtimeToastContainer = FindAnyObjectByType<ToastContainerController>();
        if (runtimeToastContainer != null)
        {
            return;
        }

        if (toastPrefab == null)
        {
            Debug.LogWarning("LevelSelectSceneController: Toast prefab is not assigned. Settings back confirmation will be unavailable if changes are unsaved.", this);
            return;
        }

        RectTransform host = toastHostRoot != null ? toastHostRoot : GetCanvasRect();
        if (host == null)
        {
            Debug.LogWarning("LevelSelectSceneController: Could not resolve a host canvas for Toast container.", this);
            return;
        }

        GameObject go = new GameObject("Toast Container", typeof(RectTransform), typeof(ToastContainerController));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(host, false);
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
        }

        settingsCanvasGroup.alpha = visible ? 1f : 0f;
        settingsCanvasGroup.interactable = visible;
        settingsCanvasGroup.blocksRaycasts = visible;
    }
}
