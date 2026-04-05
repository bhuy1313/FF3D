using UnityEngine;

[DisallowMultipleComponent]
public class BreakActionLock : PlayerActionLock
{
    public void Acquire()
    {
        AcquireFullLock();
    }

    public void Release()
    {
        ReleaseFullLock();
    }
}
