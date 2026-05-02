using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class CallPhaseIncidentSeedDebugController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CallPhaseScenarioContext scenarioContext;
    [SerializeField] private IncidentReportController incidentReportController;
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TMP_Text debugText;

    [Header("Lookup")]
    [SerializeField] private string debugPanelName = "IncidentSeedDebugPanel";
    [SerializeField] private string debugTextName = "IncidentSeedDebugText";
    [SerializeField] private bool hidePanelWhenUnavailable = false;

    private void Awake()
    {
        ResolveReferences();
        RefreshDebugText();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (incidentReportController != null)
        {
            incidentReportController.ReportStateChanged -= HandleReportStateChanged;
            incidentReportController.ReportStateChanged += HandleReportStateChanged;
        }

        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;

        RefreshDebugText();
    }

    private void Start()
    {
        RefreshDebugText();
    }

    private void OnDisable()
    {
        if (incidentReportController != null)
        {
            incidentReportController.ReportStateChanged -= HandleReportStateChanged;
        }

        LanguageManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleReportStateChanged()
    {
        RefreshDebugText();
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        RefreshDebugText();
    }

    private void ResolveReferences()
    {
        if (scenarioContext == null)
        {
            scenarioContext = GetComponent<CallPhaseScenarioContext>();
        }

        if (incidentReportController == null)
        {
            incidentReportController = GetComponent<IncidentReportController>();
        }

        if (debugPanel == null && !string.IsNullOrWhiteSpace(debugPanelName))
        {
            debugPanel = FindGameObjectByName(debugPanelName);
        }

        if (debugText == null && !string.IsNullOrWhiteSpace(debugTextName))
        {
            GameObject textObject = FindGameObjectByName(debugTextName);
            if (textObject != null)
            {
                debugText = textObject.GetComponent<TMP_Text>();
            }
        }
    }

    private void RefreshDebugText()
    {
        ResolveReferences();

        bool hasTarget = debugPanel != null && debugText != null;
        if (debugPanel != null)
        {
            debugPanel.SetActive(hasTarget || !hidePanelWhenUnavailable);
        }

        if (!hasTarget)
        {
            return;
        }

        CallPhaseScenarioData scenarioData = scenarioContext != null ? scenarioContext.ScenarioData : null;
        if (scenarioData == null)
        {
            debugText.text = "Incident Seed Debug\nNo scenario loaded.";
            return;
        }

        string caseId = scenarioContext != null ? scenarioContext.CurrentCaseId : string.Empty;
        IncidentWorldSetupPayload payload = IncidentWorldSetupPayloadBuilder.Build(scenarioData, incidentReportController, caseId);

        StringBuilder builder = new StringBuilder(768);
        builder.AppendLine("Incident Seed Debug");
        builder.Append("scenarioId: ").AppendLine(NullToPlaceholder(payload.scenarioId));
        builder.Append("caseId: ").AppendLine(NullToPlaceholder(payload.caseId));
        builder.Append("fireOrigin: ").AppendLine(NullToPlaceholder(payload.fireOrigin));
        builder.Append("logicalFireLocation: ").AppendLine(NullToPlaceholder(payload.logicalFireLocation));
        builder.Append("hazardType: ").AppendLine(NullToPlaceholder(payload.hazardType));
        builder.Append("isolationType: ").AppendLine(NullToPlaceholder(payload.isolationType));
        builder.Append("requiresIsolation: ").AppendLine(payload.requiresIsolation ? "true" : "false");
        builder.Append("initialFireIntensity: ").AppendLine(payload.initialFireIntensity.ToString("0.00"));
        builder.Append("initialFireCount: ").AppendLine(payload.initialFireCount.ToString());
        builder.Append("fireSpreadPreset: ").AppendLine(NullToPlaceholder(payload.fireSpreadPreset));
        builder.Append("startSmokeDensity: ").AppendLine(payload.startSmokeDensity.ToString("0.00"));
        builder.Append("smokeAccumulationMultiplier: ").AppendLine(payload.smokeAccumulationMultiplier.ToString("0.00"));
        builder.Append("ventilationPreset: ").AppendLine(NullToPlaceholder(payload.ventilationPreset));
        builder.Append("occupantRiskPreset: ").AppendLine(NullToPlaceholder(payload.occupantRiskPreset));
        builder.Append("severityBand: ").AppendLine(NullToPlaceholder(payload.severityBand));
        builder.Append("estimatedTrappedCountKnown: ").AppendLine(payload.estimatedTrappedCountKnown ? "true" : "false");
        builder.Append("estimatedTrappedCountMin: ").AppendLine(payload.estimatedTrappedCountMin.ToString());
        builder.Append("estimatedTrappedCountMax: ").AppendLine(payload.estimatedTrappedCountMax.ToString());
        builder.Append("confidenceScore: ").AppendLine(payload.confidenceScore.ToString("0.00"));
        builder.AppendLine();
        builder.AppendLine("Report Snapshot");
        builder.Append("Address: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.address));
        builder.Append("Fire Location: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.fireLocation));
        builder.Append("Occupant Risk: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.occupantRisk));
        builder.Append("Hazard: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.hazard));
        builder.Append("Spread Status: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.spreadStatus));
        builder.Append("Caller Safety: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.callerSafety));
        builder.Append("Severity: ").AppendLine(NullToPlaceholder(payload.reportSnapshot.severity));

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
    }

    private static string NullToPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static GameObject FindGameObjectByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, targetName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }
}
