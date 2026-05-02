using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class SceneStartupTask : MonoBehaviour
{
    public string TaskName => name;

    public IEnumerator Run(SceneStartupFlow startupFlow)
    {
        yield return Execute(startupFlow);
    }

    protected abstract IEnumerator Execute(SceneStartupFlow startupFlow);
}
