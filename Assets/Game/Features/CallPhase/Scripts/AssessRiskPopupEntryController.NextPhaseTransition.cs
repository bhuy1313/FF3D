using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class AssessRiskPopupEntryController
{
    private void PersistIncidentWorldSetupPayload()
    {
        CallPhaseScenarioData activeScenarioData = scenarioContext != null ? scenarioContext.ScenarioData : scenarioData;
        string caseId = scenarioContext != null ? scenarioContext.CurrentCaseId : string.Empty;
        IncidentWorldSetupPayload payload = IncidentWorldSetupPayloadBuilder.Build(activeScenarioData, incidentReportController, caseId);
        LoadingFlowState.SetPendingIncidentPayload(payload);
    }

    private void PersistCallPhaseResultSnapshot()
    {
        CallPhaseResultSnapshot snapshot = BuildCallPhaseResultSnapshot();
        if (snapshot == null)
        {
            return;
        }

        LoadingFlowState.SetPendingCallPhaseResult(snapshot);
    }

    private void ProceedToNextPhase()
    {
        string resolvedNextPhaseSceneName = nextPhaseSceneName;
        if (LoadingFlowState.TryGetPendingOnsiteScene(out string pendingOnsiteSceneName))
        {
            resolvedNextPhaseSceneName = pendingOnsiteSceneName;
        }

        if (string.IsNullOrWhiteSpace(loadingSceneName) || string.IsNullOrWhiteSpace(resolvedNextPhaseSceneName))
        {
            Debug.LogWarning($"{nameof(AssessRiskPopupEntryController)}: Missing next-phase scene configuration.", this);
            return;
        }

        LoadingFlowState.SetPendingTargetScene(resolvedNextPhaseSceneName.Trim());
        SceneManager.LoadScene(loadingSceneName.Trim());
    }
}


