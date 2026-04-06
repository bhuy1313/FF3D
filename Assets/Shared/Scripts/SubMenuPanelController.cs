using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SubMenuPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;

    [Header("Events")]
    [SerializeField] private UnityEvent onResumePressed;
    [SerializeField] private UnityEvent onQuitPressed;
    [SerializeField] private UnityEvent onMainMenuPressed;
    [SerializeField] private UnityEvent onSettingsPressed;

    [Header("Default Actions")]
    [SerializeField] private bool closeOnResumePressed = true;
    [SerializeField] private bool quitApplicationOnQuitPressed = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private UnityAction runtimeResumeAction;
    private UnityAction runtimeQuitAction;
    private UnityAction runtimeMainMenuAction;
    private UnityAction runtimeSettingsAction;
    private bool buttonEventsBound;

    public bool IsOpen => canvasGroup != null && canvasGroup.alpha > 0.001f;
    public Button ResumeButton => resumeButton;
    public Button QuitButton => quitButton;
    public Button MainMenuButton => mainMenuButton;
    public Button SettingsButton => settingsButton;

    public void EnsureInitialized()
    {
        if (contentRoot == null)
        {
            contentRoot = FindNestedRect(transform, "Container") ??
                          FindNestedRect(transform, "SubMenuContent");
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);
        }

        gameObject.SetActive(true);

        if (resumeButton == null)
        {
            resumeButton = FindNestedButton(transform, "BtnResume") ??
                           FindButtonByLabel(transform, "Resume");
        }

        if (quitButton == null)
        {
            quitButton = FindNestedButton(transform, "BtnQuit") ??
                         FindButtonByLabel(transform, "Quit");
        }

        if (mainMenuButton == null)
        {
            mainMenuButton = FindNestedButton(transform, "BtnMainMenu") ??
                             FindButtonByLabel(transform, "Main Menu") ??
                             FindButtonByLabel(transform, "MainMenu");
        }

        if (settingsButton == null)
        {
            settingsButton = FindNestedButton(transform, "BtnSetting") ??
                             FindButtonByLabel(transform, "Settings") ??
                             FindButtonByLabel(transform, "Setting");
        }

        BindButtonEvents();
    }

    public void Open()
    {
        EnsureInitialized();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void Close()
    {
        EnsureInitialized();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void SetResumeAction(UnityAction action)
    {
        EnsureInitialized();
        runtimeResumeAction = action;
    }

    public void SetQuitAction(UnityAction action)
    {
        EnsureInitialized();
        runtimeQuitAction = action;
    }

    public void SetMainMenuAction(UnityAction action)
    {
        EnsureInitialized();
        runtimeMainMenuAction = action;
    }

    public void SetSettingsAction(UnityAction action)
    {
        EnsureInitialized();
        runtimeSettingsAction = action;
    }

    private void BindButtonEvents()
    {
        if (buttonEventsBound)
        {
            return;
        }

        buttonEventsBound = true;

        RebindButton(resumeButton, HandleResumePressed);
        RebindButton(quitButton, HandleQuitPressed);
        RebindButton(mainMenuButton, HandleMainMenuPressed);
        RebindButton(settingsButton, HandleSettingsPressed);
    }

    private void HandleResumePressed()
    {
        if (closeOnResumePressed)
        {
            Close();
        }

        onResumePressed?.Invoke();
        runtimeResumeAction?.Invoke();
    }

    private void HandleQuitPressed()
    {
        if (quitApplicationOnQuitPressed)
        {
            Application.Quit();
        }

        onQuitPressed?.Invoke();
        runtimeQuitAction?.Invoke();
    }

    private void HandleMainMenuPressed()
    {
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SubMenuGameplayLock.SuppressNextCursorRestore();
            SceneManager.LoadScene(mainMenuSceneName.Trim());
        }

        onMainMenuPressed?.Invoke();
        runtimeMainMenuAction?.Invoke();
    }

    private void HandleSettingsPressed()
    {
        onSettingsPressed?.Invoke();
        runtimeSettingsAction?.Invoke();
    }

    private static void RebindButton(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static RectTransform FindNestedRect(Transform root, string objectName)
    {
        Transform target = FindDeepChild(root, objectName);
        return target as RectTransform;
    }

    private static Button FindNestedButton(Transform root, string objectName)
    {
        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Button FindButtonByLabel(Transform root, string labelText)
    {
        if (root == null || string.IsNullOrWhiteSpace(labelText))
        {
            return null;
        }

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || !string.Equals(label.text?.Trim(), labelText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Button button = label.GetComponentInParent<Button>(true);
            if (button != null)
            {
                return button;
            }
        }

        return null;
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
            if (string.Equals(child.name, objectName, StringComparison.OrdinalIgnoreCase))
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

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
