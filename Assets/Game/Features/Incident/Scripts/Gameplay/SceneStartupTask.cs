using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class SceneStartupTask : MonoBehaviour
{
    public enum StartupTaskPhase
    {
        Normal = 0,
        Final = 1,
    }

    [Header("Flow")]
    [SerializeField] private bool blocksStartupSequence = true;
    [SerializeField] private StartupTaskPhase startupTaskPhase = StartupTaskPhase.Normal;

    public string TaskName => name;
    public virtual bool BlocksStartupSequence => blocksStartupSequence;
    public virtual StartupTaskPhase TaskPhase => startupTaskPhase;

    public IEnumerator Run(SceneStartupFlow startupFlow)
    {
        yield return Execute(startupFlow);
    }

    protected abstract IEnumerator Execute(SceneStartupFlow startupFlow);
}
