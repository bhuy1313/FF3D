using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class OnsitePhasePayloadDebugController : MonoBehaviour
{
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private string debugTextObjectName = "Text (TMP)";
    [SerializeField] private bool clearPendingPayloadAfterRender;

    private void Awake()
    {
        ResolveReferences();
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void ResolveReferences()
    {
        if (debugText != null)
        {
            return;
        }

        TMP_Text[] sceneTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneTexts.Length; i++)
        {
            TMP_Text candidate = sceneTexts[i];
            if (candidate == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(debugTextObjectName) && candidate.name == debugTextObjectName)
            {
                debugText = candidate;
                return;
            }
        }

        if (sceneTexts.Length > 0)
        {
            debugText = sceneTexts[0];
        }
    }

    private void Refresh()
    {
        ResolveReferences();
        if (debugText == null)
        {
            return;
        }

        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
            debugText.text = "Onsite Payload Debug\nNo pending incident payload.";
            return;
        }

        StringBuilder builder = new StringBuilder(768);
        builder.AppendLine("Onsite Payload Debug");
        builder.Append("caseId: ").AppendLine(ValueOrDash(payload.caseId));
        builder.Append("scenarioId: ").AppendLine(ValueOrDash(payload.scenarioId));
        builder.Append("fireOrigin: ").AppendLine(ValueOrDash(payload.fireOrigin));
        builder.Append("logicalFireLocation: ").AppendLine(ValueOrDash(payload.logicalFireLocation));
        builder.Append("hazardType: ").AppendLine(ValueOrDash(payload.hazardType));
        builder.Append("isolationType: ").AppendLine(ValueOrDash(payload.isolationType));
        builder.Append("requiresIsolation: ").AppendLine(payload.requiresIsolation ? "true" : "false");
        builder.Append("initialFireIntensity: ").AppendLine(payload.initialFireIntensity.ToString("0.00"));
        builder.Append("initialFireCount: ").AppendLine(payload.initialFireCount.ToString());
        builder.Append("fireSpreadPreset: ").AppendLine(ValueOrDash(payload.fireSpreadPreset));
        builder.Append("startSmokeDensity: ").AppendLine(payload.startSmokeDensity.ToString("0.00"));
        builder.Append("smokeAccumulationMultiplier: ").AppendLine(payload.smokeAccumulationMultiplier.ToString("0.00"));
        builder.Append("ventilationPreset: ").AppendLine(ValueOrDash(payload.ventilationPreset));
        builder.Append("occupantRiskPreset: ").AppendLine(ValueOrDash(payload.occupantRiskPreset));
        builder.Append("severityBand: ").AppendLine(ValueOrDash(payload.severityBand));
        builder.Append("confidenceScore: ").AppendLine(payload.confidenceScore.ToString("0.00"));

        if (payload.reportSnapshot != null)
        {
            builder.AppendLine();
            builder.AppendLine("Report Snapshot");
            builder.Append("Address: ").AppendLine(ValueOrDash(payload.reportSnapshot.address));
            builder.Append("Fire Location: ").AppendLine(ValueOrDash(payload.reportSnapshot.fireLocation));
            builder.Append("Occupant Risk: ").AppendLine(ValueOrDash(payload.reportSnapshot.occupantRisk));
            builder.Append("Hazard: ").AppendLine(ValueOrDash(payload.reportSnapshot.hazard));
            builder.Append("Spread Status: ").AppendLine(ValueOrDash(payload.reportSnapshot.spreadStatus));
            builder.Append("Caller Safety: ").AppendLine(ValueOrDash(payload.reportSnapshot.callerSafety));
            builder.Append("Severity: ").AppendLine(ValueOrDash(payload.reportSnapshot.severity));
        }

        if (payload.appliedSignals != null && payload.appliedSignals.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Applied Signals");
            for (int i = 0; i < payload.appliedSignals.Count; i++)
            {
                builder.Append("- ").AppendLine(payload.appliedSignals[i]);
            }
        }

        debugText.text = builder.ToString();

        if (clearPendingPayloadAfterRender)
        {
            LoadingFlowState.ClearPendingIncidentPayload();
        }
    }

    private static string ValueOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
