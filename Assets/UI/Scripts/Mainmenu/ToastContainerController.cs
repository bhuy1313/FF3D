using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ToastContainerController : MonoBehaviour
{
    [Header("Toast")]
    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private RectTransform toastParent;
    [SerializeField] private bool fallbackToSelfParent = true;

    private ToastConfirmView activeToastView;

    private void Awake()
    {
        ResolveParentReference();
    }

    public void ShowConfirmation(
        string title,
        string message,
        Action onYes,
        Action onNo = null,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
        if (toastPrefab == null)
        {
            Debug.LogError("ToastContainerController: toastPrefab is not assigned.", this);
            return;
        }

        ResolveParentReference();
        BringToFront();
        CloseActiveToastImmediate();

        Transform parent = toastParent != null ? toastParent : transform;
        activeToastView = ToastConfirmService.Show(toastPrefab, parent, title, message, onYes, onNo, yesLabel, noLabel);
    }

    public void Configure(GameObject runtimeToastPrefab, RectTransform runtimeToastParent)
    {
        toastPrefab = runtimeToastPrefab;
        toastParent = runtimeToastParent;
        ResolveParentReference();
    }

    private void ResolveParentReference()
    {
        if (toastParent == null && fallbackToSelfParent)
        {
            toastParent = transform as RectTransform;
        }
    }

    private void BringToFront()
    {
        if (toastParent != null)
        {
            toastParent.SetAsLastSibling();
        }
        else if (transform is RectTransform rectTransform)
        {
            rectTransform.SetAsLastSibling();
        }
        else
        {
            transform.SetAsLastSibling();
        }
    }

    private void CloseActiveToastImmediate()
    {
        if (activeToastView != null)
        {
            Destroy(activeToastView.gameObject);
            activeToastView = null;
        }

        Transform parent = toastParent != null ? toastParent : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<ToastConfirmView>() != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
