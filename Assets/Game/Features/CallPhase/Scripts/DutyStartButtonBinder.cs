using UnityEngine;

/*
Usage:
- Hook any duty-start trigger to CallInScheduler.StartDutySession() or DutyStartButtonBinder.OnStartDutyPressed().
*/
[DisallowMultipleComponent]
public class DutyStartButtonBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CallInScheduler scheduler;

    public void OnStartDutyPressed()
    {
        if (scheduler == null)
        {
            Debug.LogWarning($"{nameof(DutyStartButtonBinder)} on {name}: scheduler is not assigned.", this);
            return;
        }

        scheduler.StartDutySession();
    }
}
