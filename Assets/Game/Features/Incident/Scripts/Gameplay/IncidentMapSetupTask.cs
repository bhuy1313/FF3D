using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class IncidentMapSetupTask : MonoBehaviour
{
    public string TaskName => name;

    public IEnumerator Apply(IncidentMapSetupContext context)
    {
        yield return Execute(context);
    }

    protected abstract IEnumerator Execute(IncidentMapSetupContext context);
}
