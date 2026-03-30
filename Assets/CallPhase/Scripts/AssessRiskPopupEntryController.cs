using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minimal controller for the Assess Risk button gate and popup lifecycle.
/// Attach to MainCallPhaseRoot.
/// </summary>
public class AssessRiskPopupEntryController : MonoBehaviour
{
    private static readonly Color DisabledBorderColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color EnabledBorderColor = Color.white;
    private static readonly Color PopupButtonNormalColor = Color.white;
    private static readonly Color PopupButtonSelectedColor = new Color(0.95f, 0.72f, 0.38f, 1f);
    private static readonly Color PopupButtonEnabledTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
    private static readonly Color PopupButtonDisabledTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    private const string MissingSummaryValue = "Not provided";
    private const string NoConfirmedFactsText = "No confirmed facts yet.";
    private const string SeverityFieldId = "Severity";
    private const string FireLocationFieldId = "fire_location";
    private const string SubmitButtonReadyLabel = "Submit Incident Report";
    private const string SubmitButtonSubmittedLabel = "Incident Report Submitted";
    private const string ResultFallbackValue = "Not provided";
    private const string ExpectedSeverityValue = "High";
    private const string SeverityLow = "Low";
    private const string SeverityMedium = "Medium";
    private const string SeverityHigh = "High";

    private static readonly string[] SummaryFieldIds =
    {
        "Address",
        FireLocationFieldId,
        "OccupantRisk",
        "hazard",
        "SpreadStatus",
        "CallerSafety"
    };

    private static readonly string[] SummaryLabelKeys =
    {
        "address",
        "fire location",
        "occupant risk",
        "hazard",
        "spread status",
        "caller safety"
    };

    private static readonly string[] ConfirmedFactsFieldIds =
    {
        "Address",
        FireLocationFieldId,
        "OccupantRisk",
        "hazard",
        "SpreadStatus",
        "CallerSafety"
    };

    private static readonly string[] ConfirmedFactsDisplayNames =
    {
        "Address",
        "Fire Location",
        "Occupant Risk",
        "Hazard",
        "Spread Status",
        "Caller Safety"
    };

    private static readonly string[] SubmitSummaryFieldIds =
    {
        "Address",
        FireLocationFieldId,
        "OccupantRisk",
        "hazard",
        "SpreadStatus",
        "CallerSafety",
        SeverityFieldId
    };

    private static readonly string[] SubmitSummaryDisplayNames =
    {
        "Address",
        "Fire Location",
        "Occupant Risk",
        "Hazard",
        "Spread Status",
        "Caller Safety",
        "Severity"
    };

    [Header("Assess Risk Requirements")]
    [SerializeField] private List<string> requiredForAssessRisk = new List<string>
    {
        "Address",
        FireLocationFieldId
    };
    [SerializeField] private List<string> recommendedForAssessRisk = new List<string>
    {
        "OccupantRisk",
        "hazard",
        "SpreadStatus",
        "CallerSafety"
    };
    [SerializeField] private int minimumRecommendedCount = 1;

    [Header("Scenario")]
    [SerializeField] private CallPhaseScenarioData scenarioData;

    [Header("UI References")]
    [SerializeField] private GameObject assessRiskButtonObject;
    [SerializeField] private GameObject submitReportButtonObject;
    [SerializeField] private GameObject assessRiskPopupRootObject;
    [SerializeField] private GameObject assessRiskBackButtonObject;
    [SerializeField] private GameObject assessRiskConfirmAssessmentButtonObject;
    [SerializeField] private GameObject assessRiskLowSeverityButtonObject;
    [SerializeField] private GameObject assessRiskMediumSeverityButtonObject;
    [SerializeField] private GameObject assessRiskHighSeverityButtonObject;
    [SerializeField] private GameObject submitReportPopupRootObject;
    [SerializeField] private GameObject submitPopupBackButtonObject;
    [SerializeField] private GameObject submitPopupConfirmButtonObject;
    [SerializeField] private GameObject submitPopupSummaryTextObject;
    [SerializeField] private GameObject submitPopupConfirmedTextObject;
    [SerializeField] private GameObject resultPopupRootObject;
    [SerializeField] private GameObject resultPopupBackButtonObject;
    [SerializeField] private GameObject resultPopupNextPhaseButtonObject;
    [SerializeField] private GameObject resultPopupSummaryTextObject;
    [SerializeField] private GameObject resultPopupReviewTextObject;

    private Button assessRiskButton;
    private Button submitReportButton;
    private GameObject assessRiskPopup;
    private GameObject submitReportPopup;
    private GameObject resultPopup;
    private Image popupBlockerImage;
    private Image submitPopupBlockerImage;
    private Image resultPopupBlockerImage;
    private Button popupBackButton;
    private Button popupConfirmAssessmentButton;
    private Button submitPopupBackButton;
    private Button submitPopupConfirmButton;
    private Button resultPopupBackButton;
    private Button resultPopupNextPhaseButton;
    private TMP_Text popupConfirmAssessmentLabel;
    private TMP_Text popupConfirmedFactsText;
    private TMP_Text submitReportButtonLabel;
    private TMP_Text submitPopupSummaryText;
    private TMP_Text submitPopupConfirmedText;
    private TMP_Text submitPopupConfirmLabel;
    private TMP_Text resultPopupSummaryText;
    private TMP_Text resultPopupReviewText;
    private Button lowSeverityButton;
    private Button mediumSeverityButton;
    private Button highSeverityButton;
    private CallPhaseScenarioContext scenarioContext;
    private IncidentReportController incidentReportController;
    private CallPhasePrototypeFollowUpController followUpController;
    private TranscriptStateController transcriptStateController;
    private TranscriptExtractionController transcriptExtractionController;
    private readonly List<Image> assessRiskBorderImages = new List<Image>();
    private readonly List<Image> submitReportBorderImages = new List<Image>();
    private readonly Dictionary<string, TMP_Text> popupSummaryValueTexts = new Dictionary<string, TMP_Text>();
    private readonly HashSet<string> loggedMissingUiReferenceWarnings = new HashSet<string>();
    private bool isPopupOpen;
    private bool isSubmitPopupOpen;
    private bool isResultPopupOpen;
    private string selectedSeverityValue = string.Empty;

    private void Awake()
    {
        ResolveReferences();
        SetAssessRiskButtonInteractable(false);
        SetSubmitReportButtonInteractable(false);
        UpdateSubmitReportButtonLabel();
        HidePopupImmediate();
        HideSubmitPopupImmediate();
        HideResultPopupImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToUi();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void Start()
    {
        HidePopupImmediate();
        HideSubmitPopupImmediate();
        HideResultPopupImmediate();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void OnDisable()
    {
        UnsubscribeFromUi();
        HidePopupImmediate();
        HideSubmitPopupImmediate();
        HideResultPopupImmediate();
    }

    private void OnDestroy()
    {
        UnsubscribeFromUi();
    }

    public void ResetForScenarioRun()
    {
        scenarioData = null;
        ResolveReferences();
        popupSummaryValueTexts.Clear();
        HidePopupImmediate();
        HideSubmitPopupImmediate();
        HideResultPopupImmediate();
        ResetPopupSelectionState();
        ClearCurrentSelection();

        if (submitPopupSummaryText != null)
        {
            submitPopupSummaryText.text = string.Empty;
        }

        if (submitPopupConfirmedText != null)
        {
            submitPopupConfirmedText.text = string.Empty;
        }

        if (resultPopupSummaryText != null)
        {
            resultPopupSummaryText.text = string.Empty;
        }

        if (resultPopupReviewText != null)
        {
            resultPopupReviewText.text = string.Empty;
        }

        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
        UpdateSubmitReportButtonLabel();
    }

    private void OpenPopup()
    {
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();

        if (isPopupOpen || isSubmitPopupOpen || assessRiskButton == null || !assessRiskButton.interactable || assessRiskPopup == null)
        {
            return;
        }

        isPopupOpen = true;

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ClearSelection();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.EnterNormalMode();
        }

        if (popupBlockerImage != null)
        {
            popupBlockerImage.enabled = true;
        }

        ResetPopupSelectionState();
        PopulatePopupSummary();
        PopulateConfirmedFacts();
        assessRiskPopup.SetActive(true);
        ClearCurrentSelection();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void ClosePopup()
    {
        if (!isPopupOpen && (assessRiskPopup == null || !assessRiskPopup.activeSelf))
        {
            RefreshAssessRiskButtonState();
            RefreshSubmitReportButtonState();
            return;
        }

        HidePopupImmediate();

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ClearSelection();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.EnterNormalMode();
        }

        ClearCurrentSelection();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void HandleReportStateChanged()
    {
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
        UpdateSubmitReportButtonLabel();

        if (isPopupOpen)
        {
            PopulatePopupSummary();
            PopulateConfirmedFacts();
        }

        if (isSubmitPopupOpen)
        {
            PopulateSubmitReportSummary();
            PopulateConfirmedFacts();
            UpdateSubmitPopupButtonState();
        }
    }

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
        }

        UpdateAssessRiskBorderVisuals(interactable);
    }

    private void SetSubmitReportButtonInteractable(bool interactable)
    {
        if (submitReportButton != null)
        {
            submitReportButton.interactable = interactable;
        }

        UpdateSubmitReportBorderVisuals(interactable);

        if (submitReportButtonLabel != null)
        {
            submitReportButtonLabel.color = interactable ? EnabledBorderColor : DisabledBorderColor;
        }
    }

    private void UpdateSubmitReportButtonLabel()
    {
        if (submitReportButtonLabel == null)
        {
            return;
        }

        submitReportButtonLabel.text = IsReportSubmitted()
            ? SubmitButtonSubmittedLabel
            : SubmitButtonReadyLabel;
    }

    private void HidePopupImmediate()
    {
        isPopupOpen = false;
        ResetPopupSelectionState();

        if (popupBlockerImage != null)
        {
            popupBlockerImage.enabled = true;
        }

        if (assessRiskPopup != null)
        {
            assessRiskPopup.SetActive(false);
        }
    }

    private void HideSubmitPopupImmediate()
    {
        isSubmitPopupOpen = false;

        if (submitPopupBlockerImage != null)
        {
            submitPopupBlockerImage.enabled = true;
        }

        if (submitReportPopup != null)
        {
            submitReportPopup.SetActive(false);
        }
    }

    private void HideResultPopupImmediate()
    {
        isResultPopupOpen = false;

        if (resultPopupBlockerImage != null)
        {
            resultPopupBlockerImage.enabled = true;
        }

        if (resultPopup != null)
        {
            resultPopup.SetActive(false);
        }
    }

    private void SubscribeToUi()
    {
        if (assessRiskButton != null)
        {
            assessRiskButton.onClick.RemoveListener(OpenPopup);
            assessRiskButton.onClick.AddListener(OpenPopup);
        }

        if (submitReportButton != null)
        {
            submitReportButton.onClick.RemoveListener(OpenSubmitPopup);
            submitReportButton.onClick.AddListener(OpenSubmitPopup);
        }

        if (popupBackButton != null)
        {
            popupBackButton.onClick.RemoveListener(ClosePopup);
            popupBackButton.onClick.AddListener(ClosePopup);
        }

        if (popupConfirmAssessmentButton != null)
        {
            popupConfirmAssessmentButton.onClick.RemoveListener(ConfirmAssessment);
            popupConfirmAssessmentButton.onClick.AddListener(ConfirmAssessment);
        }

        if (submitPopupBackButton != null)
        {
            submitPopupBackButton.onClick.RemoveListener(CloseSubmitPopup);
            submitPopupBackButton.onClick.AddListener(CloseSubmitPopup);
        }

        if (submitPopupConfirmButton != null)
        {
            submitPopupConfirmButton.onClick.RemoveListener(ConfirmSubmitReport);
            submitPopupConfirmButton.onClick.AddListener(ConfirmSubmitReport);
        }

        if (resultPopupBackButton != null)
        {
            resultPopupBackButton.onClick.RemoveListener(CloseResultPopup);
            resultPopupBackButton.onClick.AddListener(CloseResultPopup);
        }

        if (resultPopupNextPhaseButton != null)
        {
            resultPopupNextPhaseButton.onClick.RemoveListener(CloseResultPopup);
            resultPopupNextPhaseButton.onClick.AddListener(CloseResultPopup);
        }

        if (lowSeverityButton != null)
        {
            lowSeverityButton.onClick.RemoveListener(SelectLowSeverity);
            lowSeverityButton.onClick.AddListener(SelectLowSeverity);
        }

        if (mediumSeverityButton != null)
        {
            mediumSeverityButton.onClick.RemoveListener(SelectMediumSeverity);
            mediumSeverityButton.onClick.AddListener(SelectMediumSeverity);
        }

        if (highSeverityButton != null)
        {
            highSeverityButton.onClick.RemoveListener(SelectHighSeverity);
            highSeverityButton.onClick.AddListener(SelectHighSeverity);
        }

        if (incidentReportController != null)
        {
            incidentReportController.ReportStateChanged -= HandleReportStateChanged;
            incidentReportController.ReportStateChanged += HandleReportStateChanged;
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.StateChanged -= HandleTranscriptStateChanged;
            transcriptStateController.StateChanged += HandleTranscriptStateChanged;
        }
    }

    private void UnsubscribeFromUi()
    {
        if (assessRiskButton != null)
        {
            assessRiskButton.onClick.RemoveListener(OpenPopup);
        }

        if (submitReportButton != null)
        {
            submitReportButton.onClick.RemoveListener(OpenSubmitPopup);
        }

        if (popupBackButton != null)
        {
            popupBackButton.onClick.RemoveListener(ClosePopup);
        }

        if (popupConfirmAssessmentButton != null)
        {
            popupConfirmAssessmentButton.onClick.RemoveListener(ConfirmAssessment);
        }

        if (submitPopupBackButton != null)
        {
            submitPopupBackButton.onClick.RemoveListener(CloseSubmitPopup);
        }

        if (submitPopupConfirmButton != null)
        {
            submitPopupConfirmButton.onClick.RemoveListener(ConfirmSubmitReport);
        }

        if (resultPopupBackButton != null)
        {
            resultPopupBackButton.onClick.RemoveListener(CloseResultPopup);
        }

        if (resultPopupNextPhaseButton != null)
        {
            resultPopupNextPhaseButton.onClick.RemoveListener(CloseResultPopup);
        }

        if (lowSeverityButton != null)
        {
            lowSeverityButton.onClick.RemoveListener(SelectLowSeverity);
        }

        if (mediumSeverityButton != null)
        {
            mediumSeverityButton.onClick.RemoveListener(SelectMediumSeverity);
        }

        if (highSeverityButton != null)
        {
            highSeverityButton.onClick.RemoveListener(SelectHighSeverity);
        }

        if (incidentReportController != null)
        {
            incidentReportController.ReportStateChanged -= HandleReportStateChanged;
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.StateChanged -= HandleTranscriptStateChanged;
        }
    }

    private void ResolveReferences()
    {
        ResolveScenarioData();

        if (incidentReportController == null)
        {
            incidentReportController = GetComponent<IncidentReportController>();
        }

        if (followUpController == null)
        {
            followUpController = GetComponent<CallPhasePrototypeFollowUpController>();
        }

        if (transcriptStateController == null)
        {
            transcriptStateController = GetComponentInChildren<TranscriptStateController>(true);
        }

        if (transcriptExtractionController == null)
        {
            transcriptExtractionController = GetComponent<TranscriptExtractionController>();
        }

        if (scenarioContext == null)
        {
            scenarioContext = GetComponent<CallPhaseScenarioContext>();
        }

        if (assessRiskButton == null)
        {
            assessRiskButton = GetButtonFromObject(assessRiskButtonObject);
        }

        if (assessRiskButton == null)
        {
            assessRiskButton = FindButtonInChildren(transform, "btnAssessRisk", "Assess Risk");
        }

        CacheAssessRiskBorderImages();

        if (submitReportButton == null)
        {
            submitReportButton = GetButtonFromObject(submitReportButtonObject);
        }

        if (submitReportButton == null)
        {
            submitReportButton = FindButtonInChildren(transform, "btnSubmitReport", "Submit Report");
            if (submitReportButton == null)
            {
                submitReportButton = FindButtonInChildren(transform, "btnSubmitReport", SubmitButtonReadyLabel);
            }
        }

        if (submitReportButtonLabel == null)
        {
            submitReportButtonLabel = GetButtonLabel(submitReportButton);
        }

        CacheSubmitReportBorderImages();

        if (assessRiskPopup == null)
        {
            assessRiskPopup = assessRiskPopupRootObject;
        }

        if (assessRiskPopup == null)
        {
            assessRiskPopup = FindSiblingObject("AssessRiskPopup");
        }

        if (assessRiskPopup != null)
        {
            if (popupBlockerImage == null)
            {
                popupBlockerImage = assessRiskPopup.GetComponent<Image>();
            }

            if (popupBackButton == null)
            {
                popupBackButton = GetButtonFromObject(assessRiskBackButtonObject);
            }

            if (popupBackButton == null)
            {
                popupBackButton = FindButtonInChildren(assessRiskPopup.transform, null, "Back");
            }

            if (popupConfirmAssessmentButton == null)
            {
                popupConfirmAssessmentButton = GetButtonFromObject(assessRiskConfirmAssessmentButtonObject);
            }

            if (popupConfirmAssessmentButton == null)
            {
                popupConfirmAssessmentButton = FindButtonInChildren(assessRiskPopup.transform, null, "Confirm Assessment");
            }

            if (lowSeverityButton == null)
            {
                lowSeverityButton = GetButtonFromObject(assessRiskLowSeverityButtonObject);
            }

            if (lowSeverityButton == null)
            {
                lowSeverityButton = FindButtonInChildren(assessRiskPopup.transform, null, "LOW");
            }

            if (mediumSeverityButton == null)
            {
                mediumSeverityButton = GetButtonFromObject(assessRiskMediumSeverityButtonObject);
            }

            if (mediumSeverityButton == null)
            {
                mediumSeverityButton = FindButtonInChildren(assessRiskPopup.transform, null, "MEDIUM");
            }

            if (highSeverityButton == null)
            {
                highSeverityButton = GetButtonFromObject(assessRiskHighSeverityButtonObject);
            }

            if (highSeverityButton == null)
            {
                highSeverityButton = FindButtonInChildren(assessRiskPopup.transform, null, "HIGH");
            }

            if (popupConfirmAssessmentLabel == null)
            {
                popupConfirmAssessmentLabel = GetButtonLabel(popupConfirmAssessmentButton);
            }

            if (popupConfirmedFactsText == null)
            {
                popupConfirmedFactsText = FindConfirmedFactsValueText();
            }

            CachePopupSummaryValueTexts();
            PopulateConfirmedFacts();
            UpdateSeverityOptionVisuals();
            UpdateConfirmAssessmentButtonState();
        }

        if (submitReportPopup == null)
        {
            submitReportPopup = submitReportPopupRootObject;
        }

        if (submitReportPopup == null)
        {
            submitReportPopup = FindSiblingObject("SubmitReportPopup");
        }

        if (submitReportPopup != null)
        {
            if (submitPopupBlockerImage == null)
            {
                submitPopupBlockerImage = submitReportPopup.GetComponent<Image>();
            }

            if (submitPopupBackButton == null)
            {
                submitPopupBackButton = GetButtonFromObject(submitPopupBackButtonObject);
            }

            if (submitPopupBackButton == null)
            {
                submitPopupBackButton = FindButtonInChildren(submitReportPopup.transform, "btnBack", "Back");
            }

            if (submitPopupConfirmButton == null)
            {
                submitPopupConfirmButton = GetButtonFromObject(submitPopupConfirmButtonObject);
            }

            if (submitPopupConfirmButton == null)
            {
                submitPopupConfirmButton = FindButtonInChildren(submitReportPopup.transform, "btnConfirmSubmit", "Confirm Submit");
            }

            if (submitPopupConfirmLabel == null)
            {
                submitPopupConfirmLabel = GetButtonLabel(submitPopupConfirmButton);
            }

            if (submitPopupSummaryText == null)
            {
                submitPopupSummaryText = GetTextFromObject(submitPopupSummaryTextObject);
            }

            if (submitPopupSummaryText == null)
            {
                submitPopupSummaryText = FindTextInChildrenByName(submitReportPopup.transform, "summaryText");
            }

            if (submitPopupConfirmedText == null)
            {
                submitPopupConfirmedText = GetTextFromObject(submitPopupConfirmedTextObject);
            }

            if (submitPopupConfirmedText == null)
            {
                submitPopupConfirmedText = FindTextInChildrenByName(submitReportPopup.transform, "confirmedText");
            }

            UpdateSubmitPopupButtonState();
            PopulateSubmitReportSummary();
            PopulateConfirmedFacts();
        }

        if (resultPopup == null)
        {
            resultPopup = resultPopupRootObject;
        }

        if (resultPopup == null)
        {
            resultPopup = FindSiblingObject("ResultPopup");
        }

        if (resultPopup != null)
        {
            if (resultPopupBlockerImage == null)
            {
                resultPopupBlockerImage = resultPopup.GetComponent<Image>();
            }

            if (resultPopupBackButton == null)
            {
                resultPopupBackButton = GetButtonFromObject(resultPopupBackButtonObject);
            }

            if (resultPopupBackButton == null)
            {
                resultPopupBackButton = FindButtonInChildren(resultPopup.transform, "btnBack", "Back");
            }

            if (resultPopupNextPhaseButton == null)
            {
                resultPopupNextPhaseButton = GetButtonFromObject(resultPopupNextPhaseButtonObject);
            }

            if (resultPopupNextPhaseButton == null)
            {
                resultPopupNextPhaseButton = FindButtonInChildren(resultPopup.transform, "btnNextPhase", null);
            }

            if (resultPopupSummaryText == null)
            {
                resultPopupSummaryText = GetTextFromObject(resultPopupSummaryTextObject);
            }

            if (resultPopupSummaryText == null)
            {
                resultPopupSummaryText = FindTextInChildrenByName(resultPopup.transform, "summaryText");
            }

            if (resultPopupReviewText == null)
            {
                resultPopupReviewText = GetTextFromObject(resultPopupReviewTextObject);
            }

            if (resultPopupReviewText == null)
            {
                resultPopupReviewText = FindTextInChildrenByName(resultPopup.transform, "resultText");
            }
        }

        WarnIfMissingReference("Assess Risk button", assessRiskButton, this);
        WarnIfMissingReference("Submit Report button", submitReportButton, this);
        WarnIfMissingReference("Assess Risk popup", assessRiskPopup, this);
        WarnIfMissingReference("Assess Risk back button", popupBackButton, assessRiskPopup);
        WarnIfMissingReference("Assess Risk confirm button", popupConfirmAssessmentButton, assessRiskPopup);
        WarnIfMissingReference("LOW severity button", lowSeverityButton, assessRiskPopup);
        WarnIfMissingReference("MEDIUM severity button", mediumSeverityButton, assessRiskPopup);
        WarnIfMissingReference("HIGH severity button", highSeverityButton, assessRiskPopup);
        WarnIfMissingReference("Submit popup", submitReportPopup, this);
        WarnIfMissingReference("Submit popup back button", submitPopupBackButton, submitReportPopup);
        WarnIfMissingReference("Submit popup confirm button", submitPopupConfirmButton, submitReportPopup);
        WarnIfMissingReference("Submit popup summary text", submitPopupSummaryText, submitReportPopup);
        WarnIfMissingReference("Submit popup confirmed text", submitPopupConfirmedText, submitReportPopup);
        WarnIfMissingReference("Result popup", resultPopup, this);
        WarnIfMissingReference("Result popup back button", resultPopupBackButton, resultPopup);
        WarnIfMissingReference("Result popup next phase button", resultPopupNextPhaseButton, resultPopup);
        WarnIfMissingReference("Result popup summary text", resultPopupSummaryText, resultPopup);
        WarnIfMissingReference("Result popup review text", resultPopupReviewText, resultPopup);
    }

    private void SelectLowSeverity()
    {
        SelectSeverity(SeverityLow);
    }

    private void SelectMediumSeverity()
    {
        SelectSeverity(SeverityMedium);
    }

    private void SelectHighSeverity()
    {
        SelectSeverity(SeverityHigh);
    }

    private void SelectSeverity(string severity)
    {
        selectedSeverityValue = severity ?? string.Empty;
        UpdateSeverityOptionVisuals();
        UpdateConfirmAssessmentButtonState();
        ClearCurrentSelection();
    }

    private void ConfirmAssessment()
    {
        if (string.IsNullOrWhiteSpace(selectedSeverityValue) || incidentReportController == null)
        {
            UpdateConfirmAssessmentButtonState();
            return;
        }

        incidentReportController.ApplySeverityAssessment(selectedSeverityValue);
        ClosePopup();
    }

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

        string playerName = LoadingFlowState.GetPlayerName();
        string currentLevelId = LoadingFlowState.GetCurrentLevelId();
        if (!string.IsNullOrWhiteSpace(playerName) && !string.IsNullOrWhiteSpace(currentLevelId))
        {
            PlayerProgressProfileStore.MarkLevelCompleted(playerName, currentLevelId);
        }

        CloseSubmitPopup();
        OpenResultPopup();
    }

    private void OpenResultPopup()
    {
        if (isResultPopupOpen || resultPopup == null)
        {
            return;
        }

        isResultPopupOpen = true;

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ClearSelection();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.EnterNormalMode();
        }

        if (resultPopupBlockerImage != null)
        {
            resultPopupBlockerImage.enabled = true;
        }

        PopulateResultPopupSummary();
        PopulateResultPopupReview();
        resultPopup.SetActive(true);
        ClearCurrentSelection();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void CloseResultPopup()
    {
        if (!isResultPopupOpen && (resultPopup == null || !resultPopup.activeSelf))
        {
            RefreshAssessRiskButtonState();
            RefreshSubmitReportButtonState();
            return;
        }

        HideResultPopupImmediate();
        ClearCurrentSelection();
        RefreshAssessRiskButtonState();
        RefreshSubmitReportButtonState();
    }

    private void ResetPopupSelectionState()
    {
        selectedSeverityValue = string.Empty;
        UpdateSeverityOptionVisuals();
        UpdateConfirmAssessmentButtonState();
    }

    private void UpdateSeverityOptionVisuals()
    {
        ApplySeverityOptionVisual(lowSeverityButton, SeverityLow);
        ApplySeverityOptionVisual(mediumSeverityButton, SeverityMedium);
        ApplySeverityOptionVisual(highSeverityButton, SeverityHigh);
    }

    private void ApplySeverityOptionVisual(Button button, string severity)
    {
        if (button == null)
        {
            return;
        }

        bool isSelected = selectedSeverityValue == severity;
        Image background = button.targetGraphic as Image;
        if (background != null)
        {
            background.color = isSelected ? PopupButtonSelectedColor : PopupButtonNormalColor;
        }

        TMP_Text label = GetButtonLabel(button);
        if (label != null)
        {
            label.color = PopupButtonEnabledTextColor;
            label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void UpdateConfirmAssessmentButtonState()
    {
        if (popupConfirmAssessmentButton != null)
        {
            popupConfirmAssessmentButton.interactable = !string.IsNullOrWhiteSpace(selectedSeverityValue);
        }

        Image background = popupConfirmAssessmentButton != null ? popupConfirmAssessmentButton.targetGraphic as Image : null;
        if (background != null)
        {
            background.color = popupConfirmAssessmentButton != null && popupConfirmAssessmentButton.interactable
                ? PopupButtonSelectedColor
                : PopupButtonNormalColor;
        }

        if (popupConfirmAssessmentLabel != null)
        {
            popupConfirmAssessmentLabel.color = popupConfirmAssessmentButton != null && popupConfirmAssessmentButton.interactable
                ? PopupButtonEnabledTextColor
                : PopupButtonDisabledTextColor;
        }
    }

    private void UpdateSubmitPopupButtonState()
    {
        if (submitPopupConfirmButton != null)
        {
            submitPopupConfirmButton.interactable = !IsReportSubmitted();
        }

        Image background = submitPopupConfirmButton != null ? submitPopupConfirmButton.targetGraphic as Image : null;
        if (background != null)
        {
            background.color = submitPopupConfirmButton != null && submitPopupConfirmButton.interactable
                ? PopupButtonSelectedColor
                : PopupButtonNormalColor;
        }

        if (submitPopupConfirmLabel != null)
        {
            submitPopupConfirmLabel.color = submitPopupConfirmButton != null && submitPopupConfirmButton.interactable
                ? PopupButtonEnabledTextColor
                : PopupButtonDisabledTextColor;
        }
    }

    private TMP_Text GetButtonLabel(Button button)
    {
        return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void CacheAssessRiskBorderImages()
    {
        if (assessRiskButton == null)
        {
            assessRiskBorderImages.Clear();
            return;
        }

        assessRiskBorderImages.Clear();

        Transform buttonTransform = assessRiskButton.transform;
        for (int i = 0; i < buttonTransform.childCount; i++)
        {
            Transform child = buttonTransform.GetChild(i);
            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                assessRiskBorderImages.Add(image);
            }
        }
    }

    private void UpdateAssessRiskBorderVisuals(bool interactable)
    {
        Color targetColor = interactable ? EnabledBorderColor : DisabledBorderColor;

        for (int i = 0; i < assessRiskBorderImages.Count; i++)
        {
            Image image = assessRiskBorderImages[i];
            if (image != null)
            {
                image.color = targetColor;
            }
        }
    }

    private void CacheSubmitReportBorderImages()
    {
        if (submitReportButton == null)
        {
            submitReportBorderImages.Clear();
            return;
        }

        submitReportBorderImages.Clear();

        Transform buttonTransform = submitReportButton.transform;
        for (int i = 0; i < buttonTransform.childCount; i++)
        {
            Transform child = buttonTransform.GetChild(i);
            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                submitReportBorderImages.Add(image);
            }
        }
    }

    private void UpdateSubmitReportBorderVisuals(bool interactable)
    {
        Color targetColor = interactable ? EnabledBorderColor : DisabledBorderColor;

        for (int i = 0; i < submitReportBorderImages.Count; i++)
        {
            Image image = submitReportBorderImages[i];
            if (image != null)
            {
                image.color = targetColor;
            }
        }
    }

    private void PopulateSubmitReportSummary()
    {
        if (submitPopupSummaryText == null)
        {
            return;
        }

        submitPopupSummaryText.text = BuildSubmittedReportSummaryText();
    }

    private void PopulateResultPopupSummary()
    {
        if (resultPopupSummaryText == null && resultPopup != null)
        {
            resultPopupSummaryText = FindTextInChildrenByName(resultPopup.transform, "summaryText");
        }

        if (resultPopupSummaryText == null)
        {
            return;
        }

        resultPopupSummaryText.text = BuildSubmittedReportSummaryText();
    }

    private void PopulateResultPopupReview()
    {
        if (resultPopupReviewText == null && resultPopup != null)
        {
            resultPopupReviewText = FindTextInChildrenByName(resultPopup.transform, "resultText");
        }

        if (resultPopupReviewText == null)
        {
            return;
        }

        resultPopupReviewText.text = BuildResultPopupReviewText();
    }

    private string BuildSubmittedReportSummaryText()
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < SubmitSummaryFieldIds.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(SubmitSummaryDisplayNames[i]);
            builder.Append(": ");
            builder.Append(GetPopupSummaryValue(SubmitSummaryFieldIds[i]));
        }

        return builder.ToString();
    }

    private string BuildResultPopupReviewText()
    {
        int keyValuesScore = CalculateKeyValuesScore(out List<string> keyValueIssues);
        int confirmedFactsCount = CountConfirmedFacts();
        int confirmedFactsScore = CalculateConfirmedFactsScore();
        string severityChosen = GetFieldValueOrFallback(SeverityFieldId);
        int severityScore = CalculateSeverityScore(severityChosen);
        int readinessScore = CalculateSubmissionReadinessScore();
        int followUpTotalCount = GetTotalSelectedFollowUpQuestions();
        int followUpScore = CalculateFollowUpQualityScore(followUpTotalCount);
        string followUpQualityLabel = GetFollowUpQualityLabel(followUpScore, followUpTotalCount);
        int callTimeSeconds = GetCallDurationSeconds();
        bool callTimeQualified = HasQualifiedForCallTimeScore(readinessScore);
        int callTimeScore = CalculateCallTimeScore(callTimeQualified, callTimeSeconds);
        string callTimeEfficiencyLabel = GetCallTimeEfficiencyLabel(callTimeQualified, callTimeScore);
        int maximumScore = GetMaximumResultScore(callTimeQualified);
        int finalScore = keyValuesScore + confirmedFactsScore + severityScore + readinessScore + followUpScore + callTimeScore;
        finalScore = Mathf.Clamp(finalScore, 0, maximumScore);

        List<string> feedbackLines = BuildReviewFeedbackLines(
            keyValuesScore,
            keyValueIssues,
            confirmedFactsCount,
            confirmedFactsScore,
            severityChosen,
            severityScore,
            readinessScore,
            followUpScore,
            followUpTotalCount,
            callTimeQualified,
            callTimeScore);

        StringBuilder builder = new StringBuilder();
        builder.Append("Final Score: ");
        builder.Append(finalScore);
        builder.Append(" / ");
        builder.Append(maximumScore);
        builder.Append("\n\nSeverity Chosen: ");
        builder.Append(severityChosen);
        builder.Append("\nExpected Severity: ");
        builder.Append(GetExpectedSeverityValue());
        builder.Append("\n\nConfirmed Facts: ");
        builder.Append(confirmedFactsCount);
        builder.Append("\n\nFollow-up Quality: ");
        builder.Append(followUpQualityLabel);
        builder.Append("\nOptimal Questions Chosen: ");
        builder.Append(GetOptimalFollowUpCount());
        builder.Append("\nAcceptable Questions Chosen: ");
        builder.Append(GetAcceptableFollowUpCount());
        builder.Append("\nPoor Questions Chosen: ");
        builder.Append(GetPoorFollowUpCount());
        builder.Append("\n\nCall Time: ");
        builder.Append(FormatDuration(callTimeSeconds));
        builder.Append("\nTarget Time: ");
        builder.Append(FormatDuration(GetTargetCallTimeSeconds()));
        builder.Append("\nEfficiency: ");
        builder.Append(callTimeEfficiencyLabel);
        builder.Append("\n\nFeedback:");

        for (int i = 0; i < feedbackLines.Count; i++)
        {
            builder.Append("\n- ");
            builder.Append(feedbackLines[i]);
        }

        return builder.ToString();
    }

    private int CalculateKeyValuesScore(out List<string> issues)
    {
        List<string> criticalIssues = new List<string>();
        List<string> nonCriticalIssues = new List<string>();
        int score = 0;

        List<CallPhaseScenarioScoredFieldData> correctnessFields = GetReviewCorrectnessFields();
        for (int i = 0; i < correctnessFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = correctnessFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (FieldMatchesExpectation(fieldConfig.fieldId, GetFieldValueOrFallback(fieldConfig.fieldId)))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
                continue;
            }

            string issueText = !string.IsNullOrWhiteSpace(fieldConfig.issueText)
                ? fieldConfig.issueText.Trim()
                : BuildDefaultFieldIssueText(fieldConfig.fieldId, fieldConfig.displayName);

            if (StringMatches(fieldConfig.fieldId, "hazard"))
            {
                issueText = BuildHazardIssueText(GetFieldValueOrFallback(fieldConfig.fieldId));
            }

            if (IsCriticalReviewField(fieldConfig.fieldId))
            {
                criticalIssues.Add(issueText);
            }
            else
            {
                nonCriticalIssues.Add(issueText);
            }
        }

        issues = new List<string>(criticalIssues.Count + nonCriticalIssues.Count);
        issues.AddRange(criticalIssues);
        issues.AddRange(nonCriticalIssues);

        return score;
    }

    private int CalculateConfirmedFactsScore()
    {
        int score = 0;
        List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = GetReviewExpectedConfirmedFields();
        for (int i = 0; i < expectedConfirmedFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = expectedConfirmedFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (IsFieldConfirmed(fieldConfig.fieldId))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
            }
        }

        return score;
    }

    private int CalculateSeverityScore(string severityChosen)
    {
        string expectedSeverity = GetExpectedSeverityValue();
        if (string.Equals(severityChosen, expectedSeverity, StringComparison.OrdinalIgnoreCase))
        {
            return GetSeverityCorrectScore();
        }

        if (string.Equals(severityChosen, GetSeverityPartialValue(), StringComparison.OrdinalIgnoreCase))
        {
            return GetSeverityPartialScore();
        }

        return 0;
    }

    private int CalculateSubmissionReadinessScore()
    {
        int score = 0;
        List<CallPhaseScenarioScoredFieldData> readinessFields = GetReviewReadinessFields();
        for (int i = 0; i < readinessFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = readinessFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (HasUsableFieldValue(fieldConfig.fieldId))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
            }
        }

        return score;
    }

    private List<string> BuildReviewFeedbackLines(
        int keyValuesScore,
        List<string> keyValueIssues,
        int confirmedFactsCount,
        int confirmedFactsScore,
        string severityChosen,
        int severityScore,
        int readinessScore,
        int followUpScore,
        int followUpTotalCount,
        bool callTimeQualified,
        int callTimeScore)
    {
        List<string> feedbackLines = new List<string>();
        CallPhaseScenarioResultFeedbackData feedbackConfig = GetResultReviewFeedbackConfig();

        if (severityScore >= GetSeverityCorrectScore())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(feedbackConfig.positiveSeverity, "Good risk assessment."));
        }
        else if (string.Equals(severityChosen, ResultFallbackValue, StringComparison.OrdinalIgnoreCase))
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(feedbackConfig.missingSeverity, "Severity assessment was missing."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(feedbackConfig.incorrectSeverity, "Severity was lower than expected."));
        }

        int maximumConfirmedFactsScore = GetMaximumScore(GetReviewExpectedConfirmedFields());
        if (maximumConfirmedFactsScore > 0 && confirmedFactsScore >= maximumConfirmedFactsScore)
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.allConfirmed,
                BuildAllConfirmedFeedbackFallback()));
        }
        else if (confirmedFactsCount <= 0)
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(feedbackConfig.noneConfirmed, "No critical facts were confirmed."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.partialConfirmed,
                "More read-back confirmation was needed for critical facts."));
        }

        if (keyValuesScore >= GetStrongCorrectnessThreshold())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.strongCorrectness,
                "Key incident details matched the scenario well."));
        }
        else if (keyValueIssues.Count > 0)
        {
            feedbackLines.Add(keyValueIssues[0]);
        }

        if (readinessScore >= GetReadySubmissionThreshold())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.readyToSubmit,
                "Report was ready to submit."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.incompleteSubmission,
                "Some report details were missing at submission."));
        }

        if (followUpTotalCount > 0)
        {
            if (followUpScore >= GetFollowUpGoodThreshold())
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.strongFollowUp,
                    "You generally prioritized relevant follow-up questions."));
            }
            else if (followUpScore >= GetFollowUpMixedThreshold())
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.mixedFollowUp,
                    "Some follow-up questions were useful, but prioritization was uneven."));
            }
            else
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.poorFollowUp,
                    "Some follow-up questions were lower priority than the situation required."));
            }
        }

        if (callTimeQualified)
        {
            if (callTimeScore >= GetMaximumCallTimeScore())
            {
                feedbackLines.Add("Call handling was efficient for this scenario.");
            }
            else if (callTimeScore > 0)
            {
                feedbackLines.Add("Call handling pace was acceptable, but could be faster.");
            }
            else
            {
                feedbackLines.Add("Call handling took longer than the scenario target.");
            }
        }
        else
        {
            feedbackLines.Add("Call Time efficiency was not scored because the submitted report was not complete enough.");
        }

        return feedbackLines;
    }

    private int GetMaximumResultScore(bool callTimeQualified)
    {
        return GetMaximumScore(GetReviewCorrectnessFields())
            + GetMaximumScore(GetReviewExpectedConfirmedFields())
            + Mathf.Max(0, GetSeverityCorrectScore())
            + GetMaximumScore(GetReviewReadinessFields())
            + (GetTotalSelectedFollowUpQuestions() > 0 ? GetMaximumFollowUpScore() : 0)
            + (callTimeQualified ? GetMaximumCallTimeScore() : 0);
    }

    private int GetMaximumScore(List<CallPhaseScenarioScoredFieldData> fieldConfigs)
    {
        int score = 0;
        if (fieldConfigs == null)
        {
            return score;
        }

        for (int i = 0; i < fieldConfigs.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = fieldConfigs[i];
            if (fieldConfig == null)
            {
                continue;
            }

            score += Mathf.Max(0, fieldConfig.scoreWeight);
        }

        return score;
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewCorrectnessFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.correctnessFields != null
            && scenarioData.resultReview.correctnessFields.Count > 0)
        {
            return scenarioData.resultReview.correctnessFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 8, issueText = "Address was incorrect or missing." },
            new CallPhaseScenarioScoredFieldData { fieldId = FireLocationFieldId, displayName = "Fire Location", scoreWeight = 6, issueText = "Fire location was incorrect or missing." },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 8, issueText = "Occupant risk was incomplete." },
            new CallPhaseScenarioScoredFieldData { fieldId = "hazard", displayName = "Hazard", scoreWeight = 6, issueText = "Hazard information was incomplete." },
            new CallPhaseScenarioScoredFieldData { fieldId = "SpreadStatus", displayName = "Spread Status", scoreWeight = 6, issueText = "Spread status was incomplete." },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 6, issueText = "Caller safety information was incomplete." }
        };
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewExpectedConfirmedFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.expectedConfirmedFields != null
            && scenarioData.resultReview.expectedConfirmedFields.Count > 0)
        {
            return scenarioData.resultReview.expectedConfirmedFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 8 },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 6 },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 6 }
        };
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewReadinessFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.readinessFields != null
            && scenarioData.resultReview.readinessFields.Count > 0)
        {
            return scenarioData.resultReview.readinessFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 4 },
            new CallPhaseScenarioScoredFieldData { fieldId = FireLocationFieldId, displayName = "Fire Location", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "hazard", displayName = "Hazard", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "SpreadStatus", displayName = "Spread Status", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 2 },
            new CallPhaseScenarioScoredFieldData { fieldId = SeverityFieldId, displayName = "Severity", scoreWeight = 2 }
        };
    }

    private int CalculateFollowUpQualityScore(int totalSelectedQuestions)
    {
        if (totalSelectedQuestions <= 0)
        {
            return 0;
        }

        int score = (GetOptimalFollowUpCount() * Mathf.Max(0, GetFollowUpOptimalScore()))
            + (GetAcceptableFollowUpCount() * Mathf.Max(0, GetFollowUpAcceptableScore()))
            - (GetPoorFollowUpCount() * Mathf.Max(0, GetFollowUpPoorPenalty()));

        return Mathf.Clamp(score, 0, GetMaximumFollowUpScore());
    }

    private string GetFollowUpQualityLabel(int followUpScore, int totalSelectedQuestions)
    {
        if (totalSelectedQuestions <= 0)
        {
            return "Not used";
        }

        if (followUpScore >= GetFollowUpGoodThreshold())
        {
            return "Good";
        }

        if (followUpScore >= GetFollowUpMixedThreshold())
        {
            return "Mixed";
        }

        return "Poor";
    }

    private int GetOptimalFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.OptimalFollowUpCount) : 0;
    }

    private int GetAcceptableFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.AcceptableFollowUpCount) : 0;
    }

    private int GetPoorFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.PoorFollowUpCount) : 0;
    }

    private int GetTotalSelectedFollowUpQuestions()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.GetTotalSelectedFollowUpQuestions()) : 0;
    }

    private int GetFollowUpOptimalScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpOptimalScore);
        }

        return 2;
    }

    private int GetFollowUpAcceptableScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpAcceptableScore);
        }

        return 1;
    }

    private int GetFollowUpPoorPenalty()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpPoorPenalty);
        }

        return 1;
    }

    private int GetMaximumFollowUpScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpMaxScore);
        }

        return 8;
    }

    private int GetFollowUpGoodThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpGoodThreshold);
        }

        return 4;
    }

    private int GetFollowUpMixedThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpMixedThreshold);
        }

        return 1;
    }

    private int GetCallDurationSeconds()
    {
        return scenarioContext != null ? Mathf.Max(0, scenarioContext.CurrentCallDurationSeconds) : 0;
    }

    // Efficiency is only scored once the report is complete enough to be meaningfully comparable.
    private bool HasQualifiedForCallTimeScore(int readinessScore)
    {
        return readinessScore >= GetReadySubmissionThreshold();
    }

    private int CalculateCallTimeScore(bool isQualified, int callTimeSeconds)
    {
        if (!isQualified)
        {
            return 0;
        }

        int targetSeconds = GetTargetCallTimeSeconds();
        int acceptableSeconds = GetAcceptableCallTimeSeconds();
        int maximumScore = GetMaximumCallTimeScore();
        if (maximumScore <= 0 || targetSeconds <= 0)
        {
            return 0;
        }

        if (callTimeSeconds <= targetSeconds)
        {
            return maximumScore;
        }

        if (acceptableSeconds <= targetSeconds || callTimeSeconds > acceptableSeconds)
        {
            return 0;
        }

        float remainingWindow = acceptableSeconds - targetSeconds;
        float remainingScoreRatio = (acceptableSeconds - callTimeSeconds) / remainingWindow;
        return Mathf.Clamp(Mathf.RoundToInt(maximumScore * remainingScoreRatio), 0, maximumScore);
    }

    private string GetCallTimeEfficiencyLabel(bool isQualified, int callTimeScore)
    {
        if (!isQualified)
        {
            return "Not counted";
        }

        if (callTimeScore >= GetMaximumCallTimeScore())
        {
            return "Good";
        }

        if (callTimeScore > 0)
        {
            return "Acceptable";
        }

        return "Slow";
    }

    private int GetTargetCallTimeSeconds()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.targetCallTimeSeconds);
        }

        return 90;
    }

    private int GetAcceptableCallTimeSeconds()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(GetTargetCallTimeSeconds(), scenarioData.resultReview.acceptableCallTimeSeconds);
        }

        return 150;
    }

    private int GetMaximumCallTimeScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.callTimeMaxScore);
        }

        return 4;
    }

    private string FormatDuration(int totalSeconds)
    {
        int safeSeconds = Mathf.Max(0, totalSeconds);
        int minutes = safeSeconds / 60;
        int seconds = safeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private CallPhaseScenarioResultFeedbackData GetResultReviewFeedbackConfig()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.feedback != null)
        {
            return scenarioData.resultReview.feedback;
        }

        return new CallPhaseScenarioResultFeedbackData();
    }

    private bool IsCriticalReviewField(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            return false;
        }

        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.criticalFieldIds != null
            && scenarioData.resultReview.criticalFieldIds.Count > 0)
        {
            for (int i = 0; i < scenarioData.resultReview.criticalFieldIds.Count; i++)
            {
                if (StringMatches(fieldId, scenarioData.resultReview.criticalFieldIds[i]))
                {
                    return true;
                }
            }
        }

        return StringMatches(fieldId, "Address")
            || StringMatches(fieldId, FireLocationFieldId)
            || StringMatches(fieldId, "OccupantRisk");
    }

    private int GetSeverityCorrectScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.severityCorrectScore);
        }

        return 20;
    }

    private int GetSeverityPartialScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.severityPartialScore);
        }

        return 10;
    }

    private string GetSeverityPartialValue()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && !string.IsNullOrWhiteSpace(scenarioData.resultReview.severityPartialValue))
        {
            return scenarioData.resultReview.severityPartialValue.Trim();
        }

        return SeverityMedium;
    }

    private int GetStrongCorrectnessThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null && scenarioData.resultReview.strongCorrectnessThreshold > 0)
        {
            return scenarioData.resultReview.strongCorrectnessThreshold;
        }

        return 32;
    }

    private int GetReadySubmissionThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null && scenarioData.resultReview.readySubmissionThreshold > 0)
        {
            return scenarioData.resultReview.readySubmissionThreshold;
        }

        return 18;
    }

    private string GetConfiguredFeedbackLine(string configuredValue, string fallbackValue)
    {
        return !string.IsNullOrWhiteSpace(configuredValue)
            ? configuredValue.Trim()
            : fallbackValue;
    }

    private string BuildAllConfirmedFeedbackFallback()
    {
        List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = GetReviewExpectedConfirmedFields();
        List<string> names = new List<string>();
        for (int i = 0; i < expectedConfirmedFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = expectedConfirmedFields[i];
            if (fieldConfig == null)
            {
                continue;
            }

            string displayName = !string.IsNullOrWhiteSpace(fieldConfig.displayName)
                ? fieldConfig.displayName.Trim()
                : GetFieldDisplayName(fieldConfig.fieldId);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                names.Add(displayName.ToLowerInvariant());
            }
        }

        if (names.Count <= 0)
        {
            return "Critical facts were confirmed.";
        }

        if (names.Count == 1)
        {
            return names[0] + " was confirmed.";
        }

        if (names.Count == 2)
        {
            return names[0] + " and " + names[1] + " were confirmed.";
        }

        string trailingName = names[names.Count - 1];
        names.RemoveAt(names.Count - 1);
        return string.Join(", ", names) + ", and " + trailingName + " were confirmed.";
    }

    private string BuildDefaultFieldIssueText(string fieldId, string displayName)
    {
        string resolvedDisplayName = !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : GetFieldDisplayName(fieldId);

        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
        {
            resolvedDisplayName = "Field";
        }

        return resolvedDisplayName + " was incorrect or missing.";
    }

    private string GetFieldDisplayName(string fieldId)
    {
        switch (fieldId)
        {
            case "Address":
                return "Address";
            case FireLocationFieldId:
                return "Fire Location";
            case "OccupantRisk":
                return "Occupant Risk";
            case "hazard":
                return "Hazard";
            case "SpreadStatus":
                return "Spread Status";
            case "CallerSafety":
                return "Caller Safety";
            case SeverityFieldId:
                return "Severity";
            default:
                return fieldId ?? string.Empty;
        }
    }

    private void CachePopupSummaryValueTexts()
    {
        popupSummaryValueTexts.Clear();

        if (assessRiskPopup == null)
        {
            return;
        }

        Transform popupRoot = assessRiskPopup.transform;
        Transform[] transforms = popupRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "ValueRoot")
            {
                continue;
            }

            TMP_Text valueText = GetFirstTextInHierarchy(candidate);
            if (valueText == null)
            {
                continue;
            }

            string fieldId = FindFieldIdForValueRoot(candidate);
            if (string.IsNullOrWhiteSpace(fieldId) || popupSummaryValueTexts.ContainsKey(fieldId))
            {
                continue;
            }

            popupSummaryValueTexts[fieldId] = valueText;
        }
    }

    private void PopulateConfirmedFacts()
    {
        List<string> confirmedFacts = GetConfirmedFactDisplayNames();
        string confirmedFactsText = confirmedFacts.Count > 0
            ? "- " + string.Join("\n- ", confirmedFacts)
            : NoConfirmedFactsText;

        if (popupConfirmedFactsText == null)
        {
            popupConfirmedFactsText = FindConfirmedFactsValueText();
        }

        if (popupConfirmedFactsText != null)
        {
            popupConfirmedFactsText.text = confirmedFactsText;
        }

        if (submitPopupConfirmedText == null && submitReportPopup != null)
        {
            submitPopupConfirmedText = FindTextInChildrenByName(submitReportPopup.transform, "confirmedText");
        }

        if (submitPopupConfirmedText != null)
        {
            submitPopupConfirmedText.text = confirmedFactsText;
        }
    }

    private List<string> GetConfirmedFactDisplayNames()
    {
        List<string> confirmedFacts = new List<string>();
        if (incidentReportController == null)
        {
            return confirmedFacts;
        }

        for (int i = 0; i < ConfirmedFactsFieldIds.Length; i++)
        {
            IncidentReportFieldView fieldView = incidentReportController.GetFieldView(ConfirmedFactsFieldIds[i]);
            if (fieldView == null || fieldView.CurrentState != ReportFieldState.Confirmed || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
            {
                continue;
            }

            confirmedFacts.Add(ConfirmedFactsDisplayNames[i]);
        }

        return confirmedFacts;
    }

    private TMP_Text FindConfirmedFactsValueText()
    {
        if (assessRiskPopup == null)
        {
            return null;
        }

        TMP_Text headerText = FindTextByExactValue(assessRiskPopup.transform, "CONFIRMED FACTS");
        if (headerText == null)
        {
            return null;
        }

        Transform current = headerText.transform;
        Transform popupRoot = assessRiskPopup.transform;

        while (current != null && current != popupRoot)
        {
            TMP_Text valueText = FindTextInSiblingNamedObject(current, "ValueText");
            if (valueText != null)
            {
                return valueText;
            }

            current = current.parent;
        }

        return null;
    }

    private void PopulatePopupSummary()
    {
        if (popupSummaryValueTexts.Count == 0)
        {
            CachePopupSummaryValueTexts();
        }

        for (int i = 0; i < SummaryFieldIds.Length; i++)
        {
            string fieldId = SummaryFieldIds[i];
            if (!popupSummaryValueTexts.TryGetValue(fieldId, out TMP_Text valueText) || valueText == null)
            {
                continue;
            }

            valueText.text = GetPopupSummaryValue(fieldId);
        }
    }

    private string GetPopupSummaryValue(string fieldId)
    {
        if (incidentReportController == null)
        {
            return MissingSummaryValue;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        if (fieldView == null || fieldView.CurrentState == ReportFieldState.Empty || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
        {
            return MissingSummaryValue;
        }

        return fieldView.CurrentValue;
    }

    private string GetFieldValueOrFallback(string fieldId)
    {
        if (incidentReportController == null)
        {
            return ResultFallbackValue;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        if (fieldView == null || fieldView.CurrentState == ReportFieldState.Empty || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
        {
            return ResultFallbackValue;
        }

        return fieldView.CurrentValue.Trim();
    }

    private bool HasUsableFieldValue(string fieldId)
    {
        return !string.Equals(GetFieldValueOrFallback(fieldId), ResultFallbackValue, StringComparison.OrdinalIgnoreCase);
    }

    private int CountConfirmedFacts()
    {
        int confirmedCount = 0;
        List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = GetReviewExpectedConfirmedFields();
        for (int i = 0; i < expectedConfirmedFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = expectedConfirmedFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (IsFieldConfirmed(fieldConfig.fieldId))
            {
                confirmedCount++;
            }
        }

        return confirmedCount;
    }

    private bool IsFieldConfirmed(string fieldId)
    {
        if (incidentReportController == null)
        {
            return false;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        return fieldView != null
            && fieldView.CurrentState == ReportFieldState.Confirmed
            && !string.IsNullOrWhiteSpace(fieldView.CurrentValue);
    }

    private bool FieldMatchesExpectation(string fieldId, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, ResultFallbackValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string normalizedValue = NormalizeForScenarioComparison(value);
        string normalizedExpectedValue = NormalizeForScenarioComparison(GetExpectedFieldValue(fieldId));
        if (!string.IsNullOrWhiteSpace(normalizedExpectedValue))
        {
            return DoesFieldMatchExpectedValue(fieldId, normalizedValue, normalizedExpectedValue);
        }

        switch (fieldId)
        {
            case "Address":
                return normalizedValue.Contains("27 maple street");
            case FireLocationFieldId:
                return normalizedValue.Contains("kitchen");
            case "OccupantRisk":
                return normalizedValue.Contains("child") && (normalizedValue.Contains("trapped") || normalizedValue.Contains("upstairs"));
            case "hazard":
                return HazardValueUtility.MatchesExpectedSet(value, GetExpectedFieldValue(fieldId));
            case "SpreadStatus":
                return normalizedValue.Contains("dining");
            case "CallerSafety":
                return normalizedValue.Contains("outside");
            default:
                return false;
        }
    }

    private string NormalizeForScenarioComparison(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace("\r", " ").Replace("\n", " ");
    }

    private void ResolveScenarioData()
    {
        if (scenarioData == null)
        {
            scenarioData = CallPhaseScenarioContext.ResolveFrom(this);
        }

        if (scenarioData == null)
        {
            scenarioData = Resources.Load<CallPhaseScenarioData>(CallPhaseScenarioData.DefaultScenarioResourcePath);
        }
    }

    private List<string> GetRequiredAssessRiskFields()
    {
        if (scenarioData != null
            && scenarioData.assessRisk != null
            && scenarioData.assessRisk.requiredForAssessRisk != null
            && scenarioData.assessRisk.requiredForAssessRisk.Count > 0)
        {
            return scenarioData.assessRisk.requiredForAssessRisk;
        }

        return requiredForAssessRisk;
    }

    private List<string> GetRecommendedAssessRiskFields()
    {
        if (scenarioData != null
            && scenarioData.assessRisk != null
            && scenarioData.assessRisk.recommendedForAssessRisk != null
            && scenarioData.assessRisk.recommendedForAssessRisk.Count > 0)
        {
            return scenarioData.assessRisk.recommendedForAssessRisk;
        }

        return recommendedForAssessRisk;
    }

    private int GetMinimumRecommendedCount()
    {
        if (scenarioData != null && scenarioData.assessRisk != null)
        {
            return Mathf.Max(0, scenarioData.assessRisk.minimumRecommendedCount);
        }

        return Mathf.Max(0, minimumRecommendedCount);
    }

    private string GetExpectedSeverityValue()
    {
        if (scenarioData != null && scenarioData.assessRisk != null && !string.IsNullOrWhiteSpace(scenarioData.assessRisk.expectedSeverity))
        {
            return scenarioData.assessRisk.expectedSeverity.Trim();
        }

        string expectedSeverity = GetExpectedFieldValue(SeverityFieldId);
        return !string.IsNullOrWhiteSpace(expectedSeverity) ? expectedSeverity : ExpectedSeverityValue;
    }

    private string GetExpectedFieldValue(string fieldId)
    {
        if (scenarioData != null)
        {
            string assetValue = scenarioData.GetExpectedFieldValue(fieldId);
            if (!string.IsNullOrWhiteSpace(assetValue))
            {
                return assetValue;
            }
        }

        switch (fieldId)
        {
            case "Address":
                return "27 Maple Street";
            case FireLocationFieldId:
                return "Kitchen";
            case "OccupantRisk":
                return "Child trapped upstairs";
            case "hazard":
                return "Gas cylinder near kitchen";
            case "SpreadStatus":
                return "Spreading toward dining area";
            case "CallerSafety":
                return "Outside house";
            case SeverityFieldId:
                return ExpectedSeverityValue;
            default:
                return string.Empty;
        }
    }

    private bool DoesFieldMatchExpectedValue(string fieldId, string normalizedActualValue, string normalizedExpectedValue)
    {
        if (string.IsNullOrWhiteSpace(normalizedActualValue) || string.IsNullOrWhiteSpace(normalizedExpectedValue))
        {
            return false;
        }

        if (StringMatches(fieldId, "hazard"))
        {
            return HazardValueUtility.MatchesExpectedSet(normalizedActualValue, normalizedExpectedValue);
        }

        return normalizedActualValue.Contains(normalizedExpectedValue)
            || normalizedExpectedValue.Contains(normalizedActualValue)
            || TokenOverlapMatches(normalizedActualValue, normalizedExpectedValue);
    }

    private string BuildHazardIssueText(string actualHazardValue)
    {
        string expectedHazardValue = GetExpectedFieldValue("hazard");
        if (string.IsNullOrWhiteSpace(expectedHazardValue))
        {
            return "Hazard information was incomplete.";
        }

        string resolvedActualValue = string.Equals(actualHazardValue, ResultFallbackValue, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : actualHazardValue;

        return HazardValueUtility.BuildMismatchText(resolvedActualValue, expectedHazardValue);
    }

    private bool TokenOverlapMatches(string normalizedActualValue, string normalizedExpectedValue)
    {
        string[] actualTokens = SplitScenarioComparisonTokens(normalizedActualValue);
        string[] expectedTokens = SplitScenarioComparisonTokens(normalizedExpectedValue);
        if (actualTokens.Length <= 0 || expectedTokens.Length <= 0)
        {
            return false;
        }

        int matchedTokenCount = 0;
        for (int i = 0; i < expectedTokens.Length; i++)
        {
            string expectedToken = expectedTokens[i];
            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                continue;
            }

            for (int tokenIndex = 0; tokenIndex < actualTokens.Length; tokenIndex++)
            {
                if (actualTokens[tokenIndex] == expectedToken)
                {
                    matchedTokenCount++;
                    break;
                }
            }
        }

        return matchedTokenCount >= Mathf.Max(1, expectedTokens.Length - 1);
    }

    private string[] SplitScenarioComparisonTokens(string normalizedValue)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return Array.Empty<string>();
        }

        char[] separators = { ' ', ',', '.', '!', '?', ':', ';', '-', '_' };
        return normalizedValue.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    }

    private string FindFieldIdForValueRoot(Transform valueRoot)
    {
        if (valueRoot == null || assessRiskPopup == null)
        {
            return null;
        }

        Transform current = valueRoot;
        Transform popupRoot = assessRiskPopup.transform;

        while (current != null && current != popupRoot)
        {
            string fieldId = FindFieldIdInSiblingLabels(current);
            if (!string.IsNullOrWhiteSpace(fieldId))
            {
                return fieldId;
            }

            current = current.parent;
        }

        return null;
    }

    private string FindFieldIdInSiblingLabels(Transform current)
    {
        Transform parent = current != null ? current.parent : null;
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == current)
            {
                continue;
            }

            if (sibling.name == "ValueRoot")
            {
                continue;
            }

            TMP_Text[] texts = sibling.GetComponentsInChildren<TMP_Text>(true);
            for (int textIndex = 0; textIndex < texts.Length; textIndex++)
            {
                string fieldId = GetFieldIdForSummaryLabel(texts[textIndex]);
                if (!string.IsNullOrWhiteSpace(fieldId))
                {
                    return fieldId;
                }
            }
        }

        return null;
    }

    private TMP_Text GetFirstTextInHierarchy(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                return texts[i];
            }
        }

        return null;
    }

    private TMP_Text FindTextByExactValue(Transform root, string textValue)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.text == textValue)
            {
                return text;
            }
        }

        return null;
    }

    private Button GetButtonFromObject(GameObject targetObject)
    {
        return targetObject != null ? targetObject.GetComponent<Button>() : null;
    }

    private TMP_Text GetTextFromObject(GameObject targetObject)
    {
        return targetObject != null ? targetObject.GetComponent<TMP_Text>() : null;
    }

    private void WarnIfMissingReference(string referenceName, UnityEngine.Object resolvedReference, UnityEngine.Object context)
    {
        if (resolvedReference != null || string.IsNullOrWhiteSpace(referenceName))
        {
            return;
        }

        if (!loggedMissingUiReferenceWarnings.Add(referenceName))
        {
            return;
        }

        Debug.LogWarning($"{nameof(AssessRiskPopupEntryController)}: Missing UI reference for '{referenceName}'.", context != null ? context : this);
    }

    private TMP_Text FindTextInChildrenByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            TMP_Text text = candidate.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private TMP_Text FindTextInSiblingNamedObject(Transform current, string objectName)
    {
        Transform parent = current != null ? current.parent : null;
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == current || sibling.name != objectName)
            {
                continue;
            }

            TMP_Text valueText = GetFirstTextInHierarchy(sibling);
            if (valueText != null)
            {
                return valueText;
            }
        }

        return null;
    }

    private string GetFieldIdForSummaryLabel(TMP_Text text)
    {
        string normalizedLabel = NormalizeSummaryLabel(text != null ? text.text : string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedLabel))
        {
            return null;
        }

        for (int i = 0; i < SummaryLabelKeys.Length; i++)
        {
            if (normalizedLabel == SummaryLabelKeys[i])
            {
                return SummaryFieldIds[i];
            }
        }

        return null;
    }

    private string NormalizeSummaryLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();

        if (value.EndsWith(":"))
        {
            value = value.Substring(0, value.Length - 1);
        }

        bool previousWasWhitespace = false;
        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsWhiteSpace(character))
            {
                if (previousWasWhitespace)
                {
                    continue;
                }

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private bool StringMatches(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private Button FindButtonInChildren(Transform root, string objectName, string labelText)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(objectName) && button.gameObject.name == objectName)
            {
                return button;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text == labelText)
            {
                return button;
            }
        }

        return null;
    }

    private GameObject FindSiblingObject(string objectName)
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return null;
        }

        Transform target = parent.Find(objectName);
        return target != null ? target.gameObject : null;
    }

    private void ClearCurrentSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
