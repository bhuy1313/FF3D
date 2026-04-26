using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class AssessRiskPopupEntryController
{
    private void HandleTranscriptStateChanged(TranscriptPanelState state)
    {
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void RefreshAssessRiskButtonState()
    {
        bool canOpenPopup = !isPopupOpen
            && !isSubmitPopupOpen
            && !isResultPopupOpen
            && !IsReportSubmitted()
            && MeetsAssessRiskFieldRequirements()
            && !HasPendingConfirmation()
            && IsTranscriptStableForAssessment();

        SetAssessRiskButtonInteractable(canOpenPopup);
    }

    private void RefreshSubmitReportButtonState()
    {
        bool canOpenPopup = !isSubmitPopupOpen
            && !isPopupOpen
            && !isResultPopupOpen
            && !IsReportSubmitted()
            && MeetsSubmitFieldRequirements()
            && !HasPendingConfirmation()
            && IsTranscriptStableForAssessment();

        SetSubmitReportButtonInteractable(canOpenPopup);
        UpdateSubmitReportButtonLabel();
    }

    private bool MeetsAssessRiskFieldRequirements()
    {
        if (incidentReportController == null)
        {
            return false;
        }

        List<string> requiredFields = GetRequiredAssessRiskFields();
        if (!HasAllCollectedValues(requiredFields))
        {
            return false;
        }

        int minimumCount = GetMinimumRecommendedCount();
        if (minimumCount <= 0)
        {
            return true;
        }

        return CountCollectedValues(GetRecommendedAssessRiskFields()) >= minimumCount;
    }

    private bool MeetsSubmitFieldRequirements()
    {
        return incidentReportController != null
            && incidentReportController.HasCollectedValue("Address")
            && incidentReportController.HasCollectedValue(FireLocationFieldId)
            && incidentReportController.HasCollectedValue(SeverityFieldId);
    }

    private bool IsReportSubmitted()
    {
        return incidentReportController != null && incidentReportController.IsSubmitted;
    }

    private bool HasAllCollectedValues(List<string> fieldIds)
    {
        if (fieldIds == null)
        {
            return true;
        }

        for (int i = 0; i < fieldIds.Count; i++)
        {
            string fieldId = fieldIds[i];
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                continue;
            }

            if (!incidentReportController.HasCollectedValue(fieldId))
            {
                return false;
            }
        }

        return true;
    }

    private int CountCollectedValues(List<string> fieldIds)
    {
        if (fieldIds == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < fieldIds.Count; i++)
        {
            string fieldId = fieldIds[i];
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                continue;
            }

            if (incidentReportController.HasCollectedValue(fieldId))
            {
                count++;
            }
        }

        return count;
    }

    private bool HasPendingConfirmation()
    {
        return incidentReportController != null && incidentReportController.HasActiveConfirmationContext;
    }

    private bool IsTranscriptStableForAssessment()
    {
        return transcriptStateController == null
            || transcriptStateController.CurrentState == TranscriptPanelState.Normal;
    }

    private void SetAssessRiskButtonInteractable(bool interactable)
    {
        if (assessRiskButton != null)
        {
            assessRiskButton.interactable = interactable;
            CallPhaseFunctionButtonVisuals.Apply(assessRiskButton, interactable);
        }

        UpdateAssessRiskBorderVisuals(interactable);
    }

    private void SetSubmitReportButtonInteractable(bool interactable)
    {
        if (submitReportButton != null)
        {
            submitReportButton.interactable = interactable;
            CallPhaseFunctionButtonVisuals.Apply(submitReportButton, interactable);
        }

        UpdateSubmitReportBorderVisuals(interactable);

        if (submitReportButtonLabel != null)
        {
            submitReportButtonLabel.color = interactable ? EnabledBorderColor : DisabledBorderColor;
        }
    }

    private void UpdateSubmitReportButtonLabel()
    {
        // Preserve the scene-authored label text for this button.
    }
}


