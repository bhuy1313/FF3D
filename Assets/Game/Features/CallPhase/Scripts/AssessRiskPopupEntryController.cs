using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Minimal controller for the Assess Risk button gate and popup lifecycle.
/// Attach to MainCallPhaseRoot.
/// </summary>
public partial class AssessRiskPopupEntryController : MonoBehaviour
{
    private static readonly Color DisabledBorderColor = CallPhaseFunctionButtonVisuals.InactiveColor;
    private static readonly Color EnabledBorderColor = CallPhaseFunctionButtonVisuals.ActiveColor;
    private static readonly Color PopupButtonNormalColor = Color.white;
    private static readonly Color PopupButtonSelectedColor = new Color(0.95f, 0.72f, 0.38f, 1f);
    private static readonly Color PopupButtonEnabledTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
    private static readonly Color PopupButtonDisabledTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    private const string MissingValueToken = "__CALLPHASE_MISSING__";
    private const string SeverityFieldId = "Severity";
    private const string FireLocationFieldId = "fire_location";
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

    private static readonly string[] ConfirmedFactsFieldIds =
    {
        "Address",
        FireLocationFieldId,
        "OccupantRisk",
        "hazard",
        "SpreadStatus",
        "CallerSafety"
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

    [Header("Next Phase Flow")]
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string nextPhaseSceneName = "";

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
    private Button askFollowUpButton;
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
    private CallPhaseScenarioRuntimeController followUpController;
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

    private void Update()
    {
        if (isPopupOpen || isSubmitPopupOpen || isResultPopupOpen || FollowUpPopupController.AnyPopupOpen)
        {
            return;
        }

        if (WasFunctionShortcutPressed(KeyCode.Alpha1, KeyCode.Keypad1))
        {
            TriggerFunctionButton(askFollowUpButton);
            return;
        }

        if (WasFunctionShortcutPressed(KeyCode.Alpha2, KeyCode.Keypad2))
        {
            TriggerFunctionButton(assessRiskButton);
            return;
        }

        if (WasFunctionShortcutPressed(KeyCode.Alpha3, KeyCode.Keypad3))
        {
            TriggerFunctionButton(submitReportButton);
        }
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

        isPopupOpen = false;
        ResetPopupSelectionState();

        if (popupBlockerImage != null)
        {
            popupBlockerImage.enabled = true;
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
            resultPopupNextPhaseButton.onClick.RemoveListener(ProceedToNextPhase);
            resultPopupNextPhaseButton.onClick.AddListener(ProceedToNextPhase);
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
            resultPopupNextPhaseButton.onClick.RemoveListener(ProceedToNextPhase);
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
            followUpController = GetComponent<CallPhaseScenarioRuntimeController>();
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

        if (askFollowUpButton == null)
        {
            askFollowUpButton = FindButtonInChildren(transform, "btnAskFollowUp", null);
        }

        if (assessRiskButton == null)
        {
            assessRiskButton = FindButtonInChildren(transform, "btnAssessRisk", null);
        }

        CacheAssessRiskBorderImages();

        if (submitReportButton == null)
        {
            submitReportButton = GetButtonFromObject(submitReportButtonObject);
        }

        if (submitReportButton == null)
        {
            submitReportButton = FindButtonInChildren(transform, "btnSubmitReport", null);
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
                popupBackButton = FindButtonInChildren(assessRiskPopup.transform, "btnBack", CallPhaseUiChromeText.Tr("common.btn.back", "Back"));
            }

            if (popupConfirmAssessmentButton == null)
            {
                popupConfirmAssessmentButton = GetButtonFromObject(assessRiskConfirmAssessmentButtonObject);
            }

            if (popupConfirmAssessmentButton == null)
            {
                popupConfirmAssessmentButton = FindButtonInChildren(assessRiskPopup.transform, "btnConfirm", CallPhaseUiChromeText.Tr("callphase.btn.confirm_assessment", "Confirm Assessment"));
            }

            if (lowSeverityButton == null)
            {
                lowSeverityButton = GetButtonFromObject(assessRiskLowSeverityButtonObject);
            }

            if (lowSeverityButton == null)
            {
                lowSeverityButton = FindButtonInChildren(assessRiskPopup.transform, "btnLow", CallPhaseUiChromeText.Tr("callphase.severity.low", "LOW"));
            }

            if (mediumSeverityButton == null)
            {
                mediumSeverityButton = GetButtonFromObject(assessRiskMediumSeverityButtonObject);
            }

            if (mediumSeverityButton == null)
            {
                mediumSeverityButton = FindButtonInChildren(assessRiskPopup.transform, "btnMedium", CallPhaseUiChromeText.Tr("callphase.severity.medium", "MEDIUM"));
            }

            if (highSeverityButton == null)
            {
                highSeverityButton = GetButtonFromObject(assessRiskHighSeverityButtonObject);
            }

            if (highSeverityButton == null)
            {
                highSeverityButton = FindButtonInChildren(assessRiskPopup.transform, "btnHigh", CallPhaseUiChromeText.Tr("callphase.severity.high", "HIGH"));
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
                submitPopupBackButton = FindButtonInChildren(submitReportPopup.transform, "btnBack", CallPhaseUiChromeText.Tr("common.btn.back", "Back"));
            }

            if (submitPopupConfirmButton == null)
            {
                submitPopupConfirmButton = GetButtonFromObject(submitPopupConfirmButtonObject);
            }

            if (submitPopupConfirmButton == null)
            {
                submitPopupConfirmButton = FindButtonInChildren(submitReportPopup.transform, "btnConfirmSubmit", CallPhaseUiChromeText.Tr("callphase.btn.confirm_submit", "Confirm Submit"));
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
                resultPopupBackButton = FindButtonInChildren(resultPopup.transform, "btnBack", CallPhaseUiChromeText.Tr("common.btn.back", "Back"));
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
        WarnIfMissingReference("Ask Follow-Up button", askFollowUpButton, this);
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

        isResultPopupOpen = false;

        if (resultPopupBlockerImage != null)
        {
            resultPopupBlockerImage.enabled = true;
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

        CallPhaseUiChromeText.ApplyCurrentFont(submitPopupSummaryText);
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

        CallPhaseUiChromeText.ApplyCurrentFont(resultPopupSummaryText);
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

        CallPhaseUiChromeText.ApplyCurrentFont(resultPopupReviewText);
        resultPopupReviewText.text = BuildResultPopupReviewText();
    }
// Efficiency is only scored once the report is complete enough to be meaningfully comparable.
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
            : CallPhaseUiChromeText.Tr("callphase.value.no_confirmed_facts", "No confirmed facts yet.");

        if (popupConfirmedFactsText == null)
        {
            popupConfirmedFactsText = FindConfirmedFactsValueText();
        }

        if (popupConfirmedFactsText != null)
        {
            CallPhaseUiChromeText.ApplyCurrentFont(popupConfirmedFactsText);
            popupConfirmedFactsText.text = confirmedFactsText;
        }

        if (submitPopupConfirmedText == null && submitReportPopup != null)
        {
            submitPopupConfirmedText = FindTextInChildrenByName(submitReportPopup.transform, "confirmedText");
        }

        if (submitPopupConfirmedText != null)
        {
            CallPhaseUiChromeText.ApplyCurrentFont(submitPopupConfirmedText);
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

            confirmedFacts.Add(GetFieldDisplayName(ConfirmedFactsFieldIds[i]));
        }

        return confirmedFacts;
    }

    private TMP_Text FindConfirmedFactsValueText()
    {
        if (assessRiskPopup == null)
        {
            return null;
        }

        TMP_Text headerText = FindTextByLocalizationKeyOrValue(
            assessRiskPopup.transform,
            "callphase.header.confirmed_facts",
            "CONFIRMED FACTS");
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

    private TMP_Text FindTextByLocalizationKeyOrValue(Transform root, string localizationKey, string fallbackValue)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            LocalizedText localizedText = text.GetComponent<LocalizedText>();
            if (localizedText != null && string.Equals(localizedText.LocalizationKey, localizationKey, StringComparison.Ordinal))
            {
                return text;
            }
        }

        string localizedValue = CallPhaseUiChromeText.Tr(localizationKey, fallbackValue);
        TMP_Text match = FindTextByExactValue(root, localizedValue);
        if (match != null)
        {
            return match;
        }

        return FindTextByExactValue(root, fallbackValue);
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

            valueText.text = GetDisplayValue(GetPopupSummaryValue(fieldId), fieldId);
            CallPhaseUiChromeText.ApplyCurrentFont(valueText);
        }
    }

    private string GetPopupSummaryValue(string fieldId)
    {
        if (incidentReportController == null)
        {
            return MissingValueToken;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        if (fieldView == null || fieldView.CurrentState == ReportFieldState.Empty || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
        {
            return MissingValueToken;
        }

        return LocalizeFieldValueIfNeeded(fieldId, fieldView.CurrentValue);
    }

    private string GetFieldValueOrFallback(string fieldId)
    {
        if (incidentReportController == null)
        {
            return MissingValueToken;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        if (fieldView == null || fieldView.CurrentState == ReportFieldState.Empty || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
        {
            return MissingValueToken;
        }

        return LocalizeFieldValueIfNeeded(fieldId, fieldView.CurrentValue.Trim());
    }

    private bool HasUsableFieldValue(string fieldId)
    {
        return !IsMissingValue(GetFieldValueOrFallback(fieldId));
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
        if (string.IsNullOrWhiteSpace(value) || IsMissingValue(value))
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
            return CallPhaseUiChromeText.Tr(
                "callphase.result.issue.hazard_incomplete",
                "Hazard information was incomplete.");
        }

        string resolvedActualValue = IsMissingValue(actualHazardValue)
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

    private static bool WasFunctionShortcutPressed(KeyCode primary, KeyCode secondary)
    {
        return Input.GetKeyDown(primary) || Input.GetKeyDown(secondary);
    }

    private static void TriggerFunctionButton(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            return;
        }

        button.onClick.Invoke();
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
        if (text == null)
        {
            return null;
        }

        LocalizedText localizedText = text.GetComponent<LocalizedText>();
        if (localizedText != null
            && CallPhaseUiChromeText.TryGetFieldIdForLocalizationKey(localizedText.LocalizationKey, out string localizedFieldId))
        {
            return localizedFieldId;
        }

        string normalizedLabel = NormalizeSummaryLabel(text.text);
        if (string.IsNullOrWhiteSpace(normalizedLabel))
        {
            return null;
        }

        for (int i = 0; i < SummaryFieldIds.Length; i++)
        {
            string fieldId = SummaryFieldIds[i];
            foreach (string candidateLabel in CallPhaseUiChromeText.GetFieldDisplayNameCandidates(fieldId))
            {
                if (normalizedLabel == NormalizeSummaryLabel(candidateLabel))
                {
                    return fieldId;
                }
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


