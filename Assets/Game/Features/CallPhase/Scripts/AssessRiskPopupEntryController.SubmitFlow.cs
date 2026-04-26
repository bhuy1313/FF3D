using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class AssessRiskPopupEntryController
{
    private void OpenSubmitPopup()
    {
        RefreshSubmitReportButtonState();

        if (isSubmitPopupOpen
            || isPopupOpen
            || submitReportButton == null
            || !submitReportButton.interactable
            || submitReportPopup == null
            || IsReportSubmitted())
        {
            return;
        }

        isSubmitPopupOpen = true;

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ClearSelection();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.EnterNormalMode();
        }

        if (submitPopupBlockerImage != null)
        {
            submitPopupBlockerImage.enabled = true;
        }

        PopulateSubmitReportSummary();
        PopulateConfirmedFacts();
        UpdateSubmitPopupButtonState();
        submitReportPopup.SetActive(true);
        ClearCurrentSelection();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void CloseSubmitPopup()
    {
        if (!isSubmitPopupOpen && (submitReportPopup == null || !submitReportPopup.activeSelf))
        {
            RefreshSubmitReportButtonState();
            RefreshAssessRiskButtonState();
            return;
        }

        isSubmitPopupOpen = false;

        if (submitPopupBlockerImage != null)
        {
            submitPopupBlockerImage.enabled = true;
        }

        HideSubmitPopupImmediate();

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ClearSelection();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.EnterNormalMode();
        }

        ClearCurrentSelection();
        RefreshSubmitReportButtonState();
        RefreshAssessRiskButtonState();
    }

    private void ConfirmSubmitReport()
    {
        if (incidentReportController == null || IsReportSubmitted())
        {
            UpdateSubmitPopupButtonState();
            return;
        }

        incidentReportController.MarkSubmitted();
        if (scenarioContext != null)
        {
            scenarioContext.FinalizeCallSession();
        }

        PersistIncidentWorldSetupPayload();

        CloseSubmitPopup();
        OpenResultPopup();
    }
}


