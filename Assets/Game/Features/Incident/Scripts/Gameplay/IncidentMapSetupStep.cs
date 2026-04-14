using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class IncidentMapSetupStep : MonoBehaviour
{
    [SerializeField] private bool runStep = true;
    [SerializeField] private int order;
    [SerializeField] private string stepNameOverride;

    public bool RunStep => runStep;
    public int Order => order;
    public string StepName => string.IsNullOrWhiteSpace(stepNameOverride) ? name : stepNameOverride;

    public IEnumerator Apply(IncidentMapSetupContext context)
    {
        if (!runStep)
        {
            yield break;
        }

        yield return Execute(context);
    }

    protected abstract IEnumerator Execute(IncidentMapSetupContext context);
}
