using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ToastConfirmService
{
    public static ToastConfirmView Show(
        GameObject toastPrefab,
        Transform parent,
        string title,
        string message,
        Action onYes,
        Action onNo = null,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
        if (toastPrefab == null)
        {
            Debug.LogError("ToastConfirmService.Show failed: toast prefab is null.");
            return null;
        }

        Transform resolvedParent = ResolveParent(parent);
        GameObject toastInstance = resolvedParent != null
            ? UnityEngine.Object.Instantiate(toastPrefab, resolvedParent)
            : UnityEngine.Object.Instantiate(toastPrefab);

        toastInstance.name = toastPrefab.name;
        toastInstance.transform.SetAsLastSibling();

        ToastConfirmView view = toastInstance.GetComponent<ToastConfirmView>();
        if (view == null)
        {
            view = toastInstance.AddComponent<ToastConfirmView>();
        }

        view.Present(title, message, yesLabel, noLabel, onYes, onNo);
        return view;
    }

    private static Transform ResolveParent(Transform explicitParent)
    {
        if (explicitParent != null)
        {
            return explicitParent;
        }

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            return canvas.transform;
        }

        return null;
    }
}

public class ToastConfirmView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TMP_Text yesButtonText;
    [SerializeField] private TMP_Text noButtonText;
    [SerializeField] private Button closeButton;

    private Action onYes;
    private Action onNo;

    private void Awake()
    {
        AutoBindReferencesIfNeeded();
    }

    public void Present(
        string title,
        string message,
        string yesLabel,
        string noLabel,
        Action yesAction,
        Action noAction)
    {
        AutoBindReferencesIfNeeded();

        onYes = yesAction;
        onNo = noAction;

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (yesButtonText != null)
        {
            yesButtonText.text = yesLabel;
        }

        if (noButtonText != null)
        {
            noButtonText.text = noLabel;
        }

        BindButtonEvents();
        SetVisible(true);
    }

    private void BindButtonEvents()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(HandleYes);
            yesButton.onClick.AddListener(HandleYes);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(HandleNo);
            noButton.onClick.AddListener(HandleNo);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleNo);
            closeButton.onClick.AddListener(HandleNo);
        }
    }

    private void AutoBindReferencesIfNeeded()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (titleText == null)
        {
            titleText = FindTMPText("Panel/Header/Text (TMP)");
        }

        if (messageText == null)
        {
            messageText = FindTMPText("Panel/Body/Body_Up/Text (TMP)");
        }

        if (yesButton == null)
        {
            yesButton = FindButton("Panel/Body/Body_Out/btn1");
        }

        if (noButton == null)
        {
            noButton = FindButton("Panel/Body/Body_Out/btn2");
        }

        if (closeButton == null)
        {
            closeButton = FindButton("Panel/Header/Button");
        }

        if (yesButtonText == null && yesButton != null)
        {
            yesButtonText = yesButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (noButtonText == null && noButton != null)
        {
            noButtonText = noButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private Button FindButton(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private TMP_Text FindTMPText(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private void HandleYes()
    {
        Action callback = onYes;
        Close();
        callback?.Invoke();
    }

    private void HandleNo()
    {
        Action callback = onNo;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        SetVisible(false);
        Destroy(gameObject);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    
}
