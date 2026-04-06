using UnityEngine;
using TMPro;

/// <summary>
/// Scene-level provider for the currently active Call Phase scenario.
/// This keeps multi-scenario testing simple without requiring a scenario selection UI yet.
/// </summary>
[DisallowMultipleComponent]
public class CallPhaseScenarioContext : MonoBehaviour
{
    [Tooltip("Assign the scenario asset that this reusable Call Phase scene should run.")]
    [SerializeField] private CallPhaseScenarioData scenarioData;
    [Header("Session Header")]
    [SerializeField] private TMP_Text callTimeText;
    [SerializeField] private TMP_Text caseIdText;

    private TranscriptLogsController transcriptLogsController;
    private TranscriptExtractionController transcriptExtractionController;
    private TranscriptStateController transcriptStateController;
    private IncidentReportController incidentReportController;
    private AssessRiskPopupEntryController assessRiskPopupEntryController;
    private CallPhasePrototypeFollowUpController callPhasePrototypeFollowUpController;
    private FollowUpPopupController followUpPopupController;
    private Coroutine scenarioInitializationCoroutine;
    private string currentCaseId = string.Empty;
    private float callSessionStartRealtime;
    private bool callSessionActive;
    private int finalizedCallDurationSeconds = -1;

    public CallPhaseScenarioData ScenarioData => scenarioData;
    public int CurrentCallDurationSeconds => finalizedCallDurationSeconds >= 0
        ? finalizedCallDurationSeconds
        : GetCurrentElapsedCallSeconds();
    public bool HasStartedCallSession => callSessionActive || finalizedCallDurationSeconds >= 0;

    private void Awake()
    {
        ApplyPendingScenarioOverride();
        ResolveHeaderReferences();
        RefreshSessionHeaderTexts();
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;
    }

    private void Start()
    {
        PrepareScenarioRun();

        if (Object.FindFirstObjectByType<CallInScheduler>(FindObjectsInactive.Include) == null)
        {
            BeginCallSession();
        }
    }

    private void Update()
    {
        if (!callSessionActive)
        {
            return;
        }

        RefreshCallTimeText();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        callSessionActive = false;
    }

    public void PrepareScenarioRun()
    {
        ResolveRuntimeReferences();
        ResolveHeaderReferences();
        if (!callSessionActive)
        {
            finalizedCallDurationSeconds = -1;
            RefreshSessionHeaderTexts();
        }

        if (scenarioInitializationCoroutine != null)
        {
            StopCoroutine(scenarioInitializationCoroutine);
        }

        scenarioInitializationCoroutine = StartCoroutine(PrepareScenarioRunRoutine());
    }

    public static CallPhaseScenarioData ResolveFrom(Component source)
    {
        if (source == null)
        {
            return null;
        }

        CallPhaseScenarioContext context = source.GetComponentInParent<CallPhaseScenarioContext>();
        if (context == null)
        {
            context = Object.FindFirstObjectByType<CallPhaseScenarioContext>(FindObjectsInactive.Include);
        }

        return context != null ? context.ScenarioData : null;
    }

    public void BeginCallSession()
    {
        ResolveHeaderReferences();
        LoadPendingCaseIdIfNeeded();
        finalizedCallDurationSeconds = -1;
        callSessionStartRealtime = Time.unscaledTime;
        callSessionActive = true;
        RefreshSessionHeaderTexts();
    }

    public void FinalizeCallSession()
    {
        if (!HasStartedCallSession || finalizedCallDurationSeconds >= 0)
        {
            return;
        }

        finalizedCallDurationSeconds = GetCurrentElapsedCallSeconds();
        callSessionActive = false;
        RefreshCallTimeText();
    }

    private System.Collections.IEnumerator PrepareScenarioRunRoutine()
    {
        yield return null;

        if (transcriptExtractionController != null)
        {
            transcriptExtractionController.ResetForScenarioRun();
        }

        if (transcriptStateController != null)
        {
            transcriptStateController.ResetForScenarioRun();
        }

        if (transcriptLogsController != null)
        {
            transcriptLogsController.ResetForScenarioRun();
        }

        if (incidentReportController != null)
        {
            incidentReportController.ResetReport();
        }

        if (assessRiskPopupEntryController != null)
        {
            assessRiskPopupEntryController.ResetForScenarioRun();
        }

        if (callPhasePrototypeFollowUpController != null)
        {
            callPhasePrototypeFollowUpController.ResetForScenarioRun();
        }

        if (followUpPopupController != null)
        {
            followUpPopupController.ResetForScenarioRun();
        }

        yield return null;

        if (transcriptLogsController != null)
        {
            transcriptLogsController.BeginScenarioRun();
        }

        if (callPhasePrototypeFollowUpController != null)
        {
            callPhasePrototypeFollowUpController.BeginScenarioRun();
        }

        scenarioInitializationCoroutine = null;
    }

    private void ResolveRuntimeReferences()
    {
        if (transcriptLogsController == null)
        {
            transcriptLogsController = GetComponentInChildren<TranscriptLogsController>(true);
        }

        if (transcriptExtractionController == null)
        {
            transcriptExtractionController = GetComponent<TranscriptExtractionController>();
        }

        if (transcriptStateController == null)
        {
            transcriptStateController = GetComponentInChildren<TranscriptStateController>(true);
        }

        if (incidentReportController == null)
        {
            incidentReportController = GetComponent<IncidentReportController>();
        }

        if (assessRiskPopupEntryController == null)
        {
            assessRiskPopupEntryController = GetComponent<AssessRiskPopupEntryController>();
        }

        if (callPhasePrototypeFollowUpController == null)
        {
            callPhasePrototypeFollowUpController = GetComponent<CallPhasePrototypeFollowUpController>();
        }

        if (followUpPopupController == null)
        {
            followUpPopupController = GetComponent<FollowUpPopupController>();
        }
    }

    private void ResolveHeaderReferences()
    {
        if (callTimeText == null)
        {
            callTimeText = FindHeaderTextByName("CallTimeText");
        }

        if (caseIdText == null)
        {
            caseIdText = FindHeaderTextByName("CaseIdText");
        }
    }

    private void ApplyPendingScenarioOverride()
    {
        if (!LoadingFlowState.TryGetPendingScenarioResourcePath(out string resourcePath))
        {
            return;
        }

        CallPhaseScenarioData resolvedScenario = Resources.Load<CallPhaseScenarioData>(resourcePath);
        if (resolvedScenario == null)
        {
            Debug.LogError($"CallPhaseScenarioContext: Could not load scenario resource '{resourcePath}'. Using scene-assigned fallback.", this);
            LoadingFlowState.ClearPendingScenarioResourcePath();
            return;
        }

        scenarioData = resolvedScenario;
        LoadingFlowState.ClearPendingScenarioResourcePath();
    }

    private void LoadPendingCaseIdIfNeeded()
    {
        if (!LoadingFlowState.TryGetPendingCaseId(out string pendingCaseId))
        {
            return;
        }

        currentCaseId = pendingCaseId.Trim();
        LoadingFlowState.ClearPendingCaseId();
    }

    private void RefreshSessionHeaderTexts()
    {
        RefreshCaseIdText();
        RefreshCallTimeText();
    }

    private void RefreshCaseIdText()
    {
        if (caseIdText == null)
        {
            return;
        }

        CallPhaseUiChromeText.ApplyCurrentFont(caseIdText);
        if (CallPhaseUiChromeText.IsCurrentLanguageVietnamese())
        {
            caseIdText.text = !string.IsNullOrWhiteSpace(currentCaseId)
                ? currentCaseId
                : "N/A";
            return;
        }

        caseIdText.text = !string.IsNullOrWhiteSpace(currentCaseId)
            ? CallPhaseUiChromeText.Format("callphase.header.case_id", "Case ID: {0}", currentCaseId)
            : CallPhaseUiChromeText.Tr("callphase.header.case_id_na", "Case ID: N/A");
    }

    private void RefreshCallTimeText()
    {
        if (callTimeText == null)
        {
            return;
        }

        CallPhaseUiChromeText.ApplyCurrentFont(callTimeText);
        int totalSeconds = CurrentCallDurationSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        if (CallPhaseUiChromeText.IsCurrentLanguageVietnamese())
        {
            callTimeText.text = $"{minutes:00}:{seconds:00}";
            return;
        }

        callTimeText.text = CallPhaseUiChromeText.Format("callphase.header.call_time", "Call Time: {0:00}:{1:00}", minutes, seconds);
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        RefreshSessionHeaderTexts();
    }

    private int GetCurrentElapsedCallSeconds()
    {
        if (!callSessionActive)
        {
            return 0;
        }

        float elapsedSeconds = Mathf.Max(0f, Time.unscaledTime - callSessionStartRealtime);
        return Mathf.FloorToInt(elapsedSeconds);
    }

    private TMP_Text FindHeaderTextByName(string objectName)
    {
        TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text textComponent = textComponents[i];
            if (textComponent != null && string.Equals(textComponent.name, objectName, System.StringComparison.Ordinal))
            {
                return textComponent;
            }
        }

        TMP_Text[] sceneTextComponents = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTextComponents.Length; i++)
        {
            TMP_Text textComponent = sceneTextComponents[i];
            if (textComponent != null && string.Equals(textComponent.name, objectName, System.StringComparison.Ordinal))
            {
                return textComponent;
            }
        }

        return null;
    }
}
