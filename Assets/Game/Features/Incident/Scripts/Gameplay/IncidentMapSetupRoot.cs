using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentMapSetupRoot : MonoBehaviour
{
    [Header("Steps")]
    [SerializeField] private bool collectStepsFromChildren = true;
    [SerializeField] private bool includeInactiveSteps = true;
    [SerializeField] private List<IncidentMapSetupStep> explicitSteps = new List<IncidentMapSetupStep>();

    [Header("Diagnostics")]
    [SerializeField] private bool logSetupSteps = true;

    private readonly List<string> lastWarnings = new List<string>();

    public IncidentPayloadAnchor LastResolvedAnchor { get; private set; }
    public IReadOnlyList<string> LastWarnings => lastWarnings;

    public IEnumerator ApplyPayload(SceneStartupFlow startupFlow, IncidentWorldSetupPayload payload, Fire defaultFirePrefab)
    {
        LastResolvedAnchor = null;
        lastWarnings.Clear();

        if (payload == null)
        {
            yield break;
        }

        List<IncidentMapSetupStep> steps = BuildStepList();
        IncidentMapSetupContext context = new IncidentMapSetupContext(
            payload,
            startupFlow,
            this,
            defaultFirePrefab,
            lastWarnings);

        for (int i = 0; i < steps.Count; i++)
        {
            IncidentMapSetupStep step = steps[i];
            if (step == null || !step.RunStep)
            {
                continue;
            }

            if (logSetupSteps)
            {
                Debug.Log(
                    $"{nameof(IncidentMapSetupRoot)} running step '{step.StepName}' for payload " +
                    $"caseId='{payload.caseId}', scenarioId='{payload.scenarioId}'.",
                    step);
            }

            yield return step.Apply(context);
        }

        LastResolvedAnchor = context.ResolvedAnchor;
    }

    private List<IncidentMapSetupStep> BuildStepList()
    {
        List<IncidentMapSetupStep> results = new List<IncidentMapSetupStep>();
        HashSet<IncidentMapSetupStep> seen = new HashSet<IncidentMapSetupStep>();

        for (int i = 0; i < explicitSteps.Count; i++)
        {
            IncidentMapSetupStep step = explicitSteps[i];
            if (step == null || !seen.Add(step))
            {
                continue;
            }

            results.Add(step);
        }

        if (collectStepsFromChildren)
        {
            IncidentMapSetupStep[] childSteps = GetComponentsInChildren<IncidentMapSetupStep>(includeInactiveSteps);
            for (int i = 0; i < childSteps.Length; i++)
            {
                IncidentMapSetupStep step = childSteps[i];
                if (step == null || !seen.Add(step))
                {
                    continue;
                }

                results.Add(step);
            }
        }

        results.Sort(CompareSteps);
        return results;
    }

    private static int CompareSteps(IncidentMapSetupStep left, IncidentMapSetupStep right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int orderComparison = left.Order.CompareTo(right.Order);
        if (orderComparison != 0)
        {
            return orderComparison;
        }

        return string.CompareOrdinal(left.StepName, right.StepName);
    }
}
