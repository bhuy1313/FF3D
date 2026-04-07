using System;
using UnityEngine;

public sealed class BotPathClearingController
{
    private readonly Func<Vector3, bool> tryNavigateTo;
    private readonly Func<bool> shouldRefreshPathCheck;

    public BotPathClearingController(Func<Vector3, bool> tryNavigateTo, Func<bool> shouldRefreshPathCheck)
    {
        this.tryNavigateTo = tryNavigateTo;
        this.shouldRefreshPathCheck = shouldRefreshPathCheck;
    }

    public bool TryNavigateTo(Vector3 destination)
    {
        return tryNavigateTo != null && tryNavigateTo(destination);
    }

    public bool ShouldRefreshPathClearingCheck()
    {
        return shouldRefreshPathCheck != null && shouldRefreshPathCheck();
    }
}
