using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ToastContainerController : MonoBehaviour
{
    [Header("Toast")]
    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private RectTransform toastParent;
    [SerializeField] private bool fallbackToSelfParent = true;

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

        Transform parent = toastParent != null ? toastParent : transform;
        ToastConfirmService.Show(toastPrefab, parent, title, message, onYes, onNo, yesLabel, noLabel);
    }

    private void ResolveParentReference()
    {
        if (toastParent == null && fallbackToSelfParent)
        {
            toastParent = transform as RectTransform;
        }
    }
}
