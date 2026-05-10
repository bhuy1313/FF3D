using UnityEngine;

[DisallowMultipleComponent]
public sealed class UiBlurCoordinator : MonoBehaviour
{
    public static void SetBlurRequested(Object requester, bool requested)
    {
        GameMasterUiBlurController.SetBlurRequested(requester, requested);
    }
}
