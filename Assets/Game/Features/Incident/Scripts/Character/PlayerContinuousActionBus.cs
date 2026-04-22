using System;

public static class PlayerContinuousActionBus
{
    public static event Action<string> OnActionStarted;
    public static event Action<float> OnActionProgressed;
    public static event Action<bool> OnActionEnded;

    public static void StartAction(string actionText = "")
    {
        OnActionStarted?.Invoke(actionText);
    }

    public static void UpdateProgress(float progress)
    {
        OnActionProgressed?.Invoke(progress);
    }

    public static void EndAction(bool success)
    {
        OnActionEnded?.Invoke(success);
    }
}