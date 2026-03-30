using UnityEngine;

/*
Usage:
- Hook the Skip button's OnClick to CallInSkipButtonBinder.OnSkipPressed().
*/
[DisallowMultipleComponent]
public class CallInSkipButtonBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CallInScheduler scheduler;

    public void OnSkipPressed()
    {
        if (scheduler == null)
        {
            Debug.LogWarning($"{nameof(CallInSkipButtonBinder)} on {name}: scheduler is not assigned.", this);
            return;
        }

        scheduler.ShowCallInNow();
    }
}
