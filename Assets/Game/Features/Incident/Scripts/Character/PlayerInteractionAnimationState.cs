using UnityEngine;

public enum PlayerInteractionAnimationAction
{
    None = 0,
    OpeningDoor = 1,
    OpeningWindow = 2,
    BreakingObject = 3,
    UsingDevice = 4,
    ConnectingHose = 5,
    Rescuing = 6,
    Stabilizing = 7,
    Climb = 8,
    ClimbOver = 9
}

[DisallowMultipleComponent]
public sealed class PlayerInteractionAnimationState : MonoBehaviour
{
    [SerializeField] private PlayerInteractionAnimationAction currentAction;
    [SerializeField] private Object currentSource;
    [SerializeField] private float activeUntilTime;
    [SerializeField] private int pendingTriggerMask;

    public PlayerInteractionAnimationAction CurrentAction => IsAnyActionActive ? currentAction : PlayerInteractionAnimationAction.None;
    public Object CurrentSource => currentSource;
    public bool IsAnyActionActive => currentAction != PlayerInteractionAnimationAction.None && (currentSource != null || Time.time < activeUntilTime);

    public static PlayerInteractionAnimationState GetOrCreate(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        if (owner.TryGetComponent(out PlayerInteractionAnimationState existing))
        {
            return existing;
        }

        return owner.AddComponent<PlayerInteractionAnimationState>();
    }

    public void BeginAction(PlayerInteractionAnimationAction action, Object source, float minDuration = 0.1f)
    {
        if (action == PlayerInteractionAnimationAction.None)
        {
            return;
        }

        currentAction = action;
        currentSource = source;
        activeUntilTime = Time.time + Mathf.Max(0f, minDuration);
        TriggerAction(action);
    }

    public void PulseAction(PlayerInteractionAnimationAction action, float duration = 0.18f, Object source = null)
    {
        BeginAction(action, source, duration);
        if (source == null)
        {
            currentSource = null;
        }
    }

    public void EndAction(PlayerInteractionAnimationAction action, Object source, bool force = false)
    {
        if (currentAction != action)
        {
            return;
        }

        if (source != null && currentSource != null && currentSource != source)
        {
            return;
        }

        if (!force && Time.time < activeUntilTime)
        {
            return;
        }

        ClearAction();
    }

    public bool IsActionActive(PlayerInteractionAnimationAction action)
    {
        return IsAnyActionActive && currentAction == action;
    }

    public void TriggerAction(PlayerInteractionAnimationAction action)
    {
        if (action == PlayerInteractionAnimationAction.None)
        {
            return;
        }

        int bit = 1 << ((int)action - 1);
        pendingTriggerMask |= bit;
    }

    public bool ConsumeTrigger(PlayerInteractionAnimationAction action)
    {
        if (action == PlayerInteractionAnimationAction.None)
        {
            return false;
        }

        int bit = 1 << ((int)action - 1);
        bool hasTrigger = (pendingTriggerMask & bit) != 0;
        if (hasTrigger)
        {
            pendingTriggerMask &= ~bit;
        }

        return hasTrigger;
    }

    public void ClearAction()
    {
        currentAction = PlayerInteractionAnimationAction.None;
        currentSource = null;
        activeUntilTime = 0f;
    }

    private void Update()
    {
        if (currentSource == null && currentAction != PlayerInteractionAnimationAction.None && Time.time >= activeUntilTime)
        {
            ClearAction();
        }
    }
}
