using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class SceneStartupTask : MonoBehaviour
{
    [SerializeField] private bool runTask = true;
    [SerializeField] private int order;
    [SerializeField] private string taskNameOverride;

    public bool RunTask => runTask;
    public int Order => order;
    public string TaskName => string.IsNullOrWhiteSpace(taskNameOverride) ? name : taskNameOverride;

    public IEnumerator Run(SceneStartupFlow startupFlow)
    {
        if (!runTask)
        {
            yield break;
        }

        yield return Execute(startupFlow);
    }

    protected abstract IEnumerator Execute(SceneStartupFlow startupFlow);
}
