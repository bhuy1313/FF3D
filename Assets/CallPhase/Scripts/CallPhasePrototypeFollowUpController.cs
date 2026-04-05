using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to MainCallPhaseRoot or another parent above SelectableSpan objects.
/// This listens to OnExtractionConfirmed(SelectableSpan span) and advances follow-up progression
/// from scenario data, with a small serialized fallback for prototype safety.
/// </summary>
public class CallPhasePrototypeFollowUpController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TranscriptLogsController logsController;
    [SerializeField] private TranscriptAutoExtractEntry autoExtractEntry;
    [SerializeField] private TranscriptStateController stateController;
    [SerializeField] private IncidentReportController incidentReportController;

    [Header("Scenario")]
    [SerializeField] private CallPhaseScenarioData scenarioData;

    [Header("Follow-Up Mode")]
    [SerializeField] private CallPhaseFollowUpMode followUpMode = CallPhaseFollowUpMode.Auto;
    [SerializeField] private Button askFollowUpButton;

    [Header("Prototype Follow-Up Fallback")]
    [SerializeField] private float followUpLineDelaySeconds = 1f;
    [SerializeField] private string fireLocationFieldId = "fire_location";
    [SerializeField] private string fireLocationValue = "Kitchen";
    [SerializeField] private string addressFieldId = "Address";
    [SerializeField] private string addressValue = "27 Maple Street";
    [SerializeField] private string addressFollowUpQuestion = "What is the address?";
    [SerializeField] private string addressFollowUpReply = "It's 27 Maple Street!";
    [SerializeField] private string occupantRiskFieldId = "OccupantRisk";
    [SerializeField] private string occupantRiskValue = "Child trapped upstairs";
    [SerializeField] private string occupantRiskFollowUpQuestion = "Is anyone still inside?";
    [SerializeField] private string occupantRiskFollowUpReply = "My child is upstairs!";
    [SerializeField] private string hazardFieldId = "hazard";
    [SerializeField] private string hazardValue = "Gas cylinder near kitchen";
    [SerializeField] private string hazardFollowUpQuestion = "Is there any gas leak or gas cylinder nearby?";
    [SerializeField] private string hazardFollowUpReply = "Yes, there's a gas cylinder near the kitchen!";
    [SerializeField] private string spreadStatusFieldId = "SpreadStatus";
    [SerializeField] private string spreadStatusValue = "Spreading toward dining area";
    [SerializeField] private string spreadStatusFollowUpQuestion = "Is the fire spreading anywhere else?";
    [SerializeField] private string spreadStatusFollowUpReply = "It's spreading toward the dining area!";
    [SerializeField] private string callerSafetyFieldId = "CallerSafety";
    [SerializeField] private string callerSafetyValue = "Outside house";
    [SerializeField] private string callerSafetyFollowUpQuestion = "Are you somewhere safe right now?";
    [SerializeField] private string callerSafetyFollowUpReply = "Yes, I'm outside the house now!";

    [Header("Read-Back Confirmation Fallback")]
    [SerializeField] private string addressConfirmationQuestion = "Let me confirm the address: 27 Maple Street, correct?";
    [SerializeField] private string addressConfirmationReply = "Yes, that's correct.";
    [SerializeField] private string occupantRiskConfirmationQuestion = "Your child is still upstairs, correct?";
    [SerializeField] private string occupantRiskConfirmationReply = "Yes!";
    [SerializeField] private string callerSafetyConfirmationQuestion = "You are outside the house now, correct?";
    [SerializeField] private string callerSafetyConfirmationReply = "Yes, I'm outside.";
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<CallPhaseScenarioStepData> runtimeSteps = new List<CallPhaseScenarioStepData>();
    private readonly List<string> recentShownQuestionIds = new List<string>();
    private readonly HashSet<string> resolvedFollowUpStepIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private Coroutine activeStepRoutine;
    private Coroutine initialLoopCoroutine;
    private Coroutine deferredProgressionCoroutine;
    private CallPhaseScenarioStepData lastStartedStep;
    private CallPhaseScenarioStepData pendingManualStep;
    private CallPhaseScenarioStepData selectedManualFollowUpStep;
    private CallPhaseScenarioStepData returnPendingStepAfterOutOfOrder;
    private int nextStepIndex;
    private bool finalStepAwaitingCompletion;
    private bool pendingManualStepIsFinal;
    private bool returnPendingStepAfterOutOfOrderIsFinal;
    private bool progressionCompleted;
    private bool waitingForManualFollowUp;
    private bool isExecutingOutOfOrderRealStep;
    private int optimalFollowUpCount;
    private int acceptableFollowUpCount;
    private int poorFollowUpCount;
    private const int MaxRecentShownQuestions = 8;
    private bool lastCanAskFollowUp;

    public CallPhaseFollowUpMode FollowUpMode => followUpMode;
    public bool IsWaitingForManualFollowUp => waitingForManualFollowUp && pendingManualStep != null;
    public bool CanAskFollowUp => followUpMode == CallPhaseFollowUpMode.ManualPopup
                                  && IsWaitingForManualFollowUp
                                  && pendingManualStep != null
                                  && pendingManualStep.stepType == CallPhaseScenarioStepType.InformationCollection
                                  && !progressionCompleted
                                  && activeStepRoutine == null
                                  && (stateController == null || stateController.CurrentState != TranscriptPanelState.ExtractMode);
    public CallPhaseScenarioStepData PendingManualFollowUpStep => pendingManualStep;
    public CallPhaseScenarioStepData SelectedManualFollowUpStep => selectedManualFollowUpStep;
    public int OptimalFollowUpCount => optimalFollowUpCount;
    public int AcceptableFollowUpCount => acceptableFollowUpCount;
    public int PoorFollowUpCount => poorFollowUpCount;
    public bool HasResolvedFollowUpStep(string stepId) => !string.IsNullOrWhiteSpace(stepId) && resolvedFollowUpStepIds.Contains(stepId);
    public event System.Action ManualFollowUpAvailable;

    private void Awake()
    {
        ResolveReferences();
        ResolveScenarioData();
        RebuildRuntimeSteps();
        ResetRuntimeState();
        RefreshAskFollowUpAvailability();
    }

    private void OnDisable()
    {
        if (activeStepRoutine != null)
        {
            StopCoroutine(activeStepRoutine);
            activeStepRoutine = null;
        }

        if (initialLoopCoroutine != null)
        {
            StopCoroutine(initialLoopCoroutine);
            initialLoopCoroutine = null;
        }

        if (deferredProgressionCoroutine != null)
        {
            StopCoroutine(deferredProgressionCoroutine);
            deferredProgressionCoroutine = null;
        }
    }

    public void OnExtractionConfirmed(SelectableSpan span)
    {
        if (span == null || progressionCompleted)
        {
            return;
        }

        bool matchedCurrentStartedStep = DoesFinalStepCompletionMatch(span);
        if (matchedCurrentStartedStep && lastStartedStep != null && !string.IsNullOrWhiteSpace(lastStartedStep.stepId))
        {
            resolvedFollowUpStepIds.Add(lastStartedStep.stepId);
        }

        if (isExecutingOutOfOrderRealStep && matchedCurrentStartedStep)
        {
            FinishOutOfOrderRealStep();
            ScheduleDeferredProgressionSweep();
            return;
        }

        if (finalStepAwaitingCompletion && matchedCurrentStartedStep)
        {
            finalStepAwaitingCompletion = false;
            progressionCompleted = true;
            StartManagedRoutine(FinishPrototypeLoopRoutine(GetStepDebugName(lastStartedStep)));
            return;
        }

        if (activeStepRoutine != null)
        {
            return;
        }

        TryAdvanceLinearProgression(span);
        ScheduleDeferredProgressionSweep();
    }

    private IEnumerator PlayScenarioStepRoutine(CallPhaseScenarioStepData step)
    {
        yield return null;

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (incidentReportController != null)
        {
            if (step != null && step.stepType == CallPhaseScenarioStepType.ConfirmationReadBack)
            {
                incidentReportController.BeginConfirmationContext(
                    step.confirmationFieldId,
                    step.expectedConfirmedValue);
            }
            else
            {
                incidentReportController.ClearConfirmationContext();
            }
        }

        string operatorLine = step != null && step.operatorLine != null ? step.operatorLine.text : string.Empty;
        if (logsController != null && !string.IsNullOrWhiteSpace(operatorLine))
        {
            logsController.AddOperatorLog(operatorLine);
        }

        yield return new WaitForSecondsRealtime(CallPhaseResponseSpeedSettings.ApplyDelayPreference(followUpLineDelaySeconds));

        CallPhaseScenarioLineData callerLine = step != null ? step.callerLine : null;
        string callerText = callerLine != null ? callerLine.text : string.Empty;
        if (!string.IsNullOrWhiteSpace(callerText))
        {
            if (autoExtractEntry != null)
            {
                autoExtractEntry.AddCallerLogAndEnterExtractMode(callerText);
            }
            else if (logsController != null && stateController != null)
            {
                logsController.AddCallerLog(
                    callerText,
                    callerLine != null && callerLine.isExtractable,
                    callerLine != null && callerLine.startsAsActiveChunk);
                stateController.EnterExtractMode();
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhasePrototypeFollowUpController)}: Started scenario step '{GetStepDebugName(step)}'.", this);
        }

        RefreshAskFollowUpAvailability();
    }

    private IEnumerator BeginInitialPrototypeLoopRoutine()
    {
        if (progressionCompleted)
        {
            yield break;
        }

        while (!progressionCompleted)
        {
            if (logsController != null && logsController.GetCurrentActiveCallerEntry() != null)
            {
                break;
            }

            yield return null;
        }

        if (!progressionCompleted && stateController != null)
        {
            stateController.EnterExtractMode();
        }

        initialLoopCoroutine = null;
    }

    private IEnumerator FinishPrototypeLoopRoutine(string loopName)
    {
        yield return null;

        if (incidentReportController != null)
        {
            incidentReportController.ClearConfirmationContext();
        }

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhasePrototypeFollowUpController)}: Completed scenario progression at '{loopName}'.", this);
        }

        RefreshAskFollowUpAvailability();
    }

    private void ResolveReferences()
    {
        if (logsController == null)
        {
            logsController = GetComponentInChildren<TranscriptLogsController>(true);
        }

        if (autoExtractEntry == null)
        {
            autoExtractEntry = GetComponentInChildren<TranscriptAutoExtractEntry>(true);
        }

        if (stateController == null)
        {
            stateController = GetComponentInChildren<TranscriptStateController>(true);
        }

        if (incidentReportController == null)
        {
            incidentReportController = GetComponentInChildren<IncidentReportController>(true);
        }

        if (askFollowUpButton == null)
        {
            askFollowUpButton = FindButtonByName("btnAskFollowUp");
        }
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

    private void RebuildRuntimeSteps()
    {
        runtimeSteps.Clear();

        if (scenarioData != null && scenarioData.followUpSteps != null)
        {
            for (int i = 0; i < scenarioData.followUpSteps.Count; i++)
            {
                CallPhaseScenarioStepData step = scenarioData.followUpSteps[i];
                if (IsValidRuntimeStep(step))
                {
                    runtimeSteps.Add(step);
                }
            }
        }

        if (runtimeSteps.Count <= 0)
        {
            BuildFallbackRuntimeSteps();
        }
    }

    private void ResetRuntimeState()
    {
        nextStepIndex = 0;
        finalStepAwaitingCompletion = false;
        progressionCompleted = runtimeSteps.Count <= 0;
        lastStartedStep = null;
        pendingManualStep = null;
        selectedManualFollowUpStep = null;
        pendingManualStepIsFinal = false;
        waitingForManualFollowUp = false;
        recentShownQuestionIds.Clear();
        resolvedFollowUpStepIds.Clear();
        returnPendingStepAfterOutOfOrder = null;
        returnPendingStepAfterOutOfOrderIsFinal = false;
        isExecutingOutOfOrderRealStep = false;
        optimalFollowUpCount = 0;
        acceptableFollowUpCount = 0;
        poorFollowUpCount = 0;
        lastCanAskFollowUp = false;
    }

    public void ResetForScenarioRun()
    {
        if (activeStepRoutine != null)
        {
            StopCoroutine(activeStepRoutine);
            activeStepRoutine = null;
        }

        if (initialLoopCoroutine != null)
        {
            StopCoroutine(initialLoopCoroutine);
            initialLoopCoroutine = null;
        }

        if (deferredProgressionCoroutine != null)
        {
            StopCoroutine(deferredProgressionCoroutine);
            deferredProgressionCoroutine = null;
        }

        scenarioData = null;
        ResolveScenarioData();
        RebuildRuntimeSteps();
        ResetRuntimeState();

        if (incidentReportController != null)
        {
            incidentReportController.ClearConfirmationContext();
        }

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        RefreshAskFollowUpAvailability();
    }

    public void BeginScenarioRun()
    {
        if (initialLoopCoroutine != null)
        {
            StopCoroutine(initialLoopCoroutine);
        }

        initialLoopCoroutine = StartCoroutine(BeginInitialPrototypeLoopRoutine());
        RefreshAskFollowUpAvailability();
    }

    public bool TryStartPendingManualFollowUpStep()
    {
        if (!CanAskFollowUp || pendingManualStep == null)
        {
            return false;
        }

        CallPhaseScenarioStepData step = pendingManualStep;
        bool isFinalStep = pendingManualStepIsFinal;

        pendingManualStep = null;
        pendingManualStepIsFinal = false;
        waitingForManualFollowUp = false;

        StartScenarioStep(step, isFinalStep);
        return true;
    }

    public List<CallPhaseFollowUpQuestionOptionData> GetManualFollowUpQuestionCandidates()
    {
        List<CallPhaseFollowUpQuestionOptionData> candidates = new List<CallPhaseFollowUpQuestionOptionData>();
        if (!IsWaitingForManualFollowUp || pendingManualStep == null || scenarioData == null)
        {
            return candidates;
        }

        List<CallPhaseFollowUpQuestionOptionData> eligibleOptions = GetEligibleQuestionPoolOptions();
        HashSet<string> usedQuestionIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedQuestionTexts = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        CallPhaseFollowUpQuestionOptionData linkedCurrentOption = FindCurrentLinkedQuestionOption(eligibleOptions);
        AddQuestionCandidate(candidates, linkedCurrentOption, usedQuestionIds, usedQuestionTexts);

        AddUnresolvedRealLinkedQuestionCandidates(candidates, eligibleOptions, usedQuestionIds, usedQuestionTexts, 4);

        int remainingSlots = Mathf.Max(0, 4 - candidates.Count);
        if (remainingSlots > 0)
        {
            AddQuestionCandidatesByQuality(candidates, eligibleOptions, CallPhaseFollowUpQuestionQuality.Acceptable, remainingSlots, usedQuestionIds, usedQuestionTexts);
        }

        remainingSlots = Mathf.Max(0, 4 - candidates.Count);
        if (remainingSlots > 0)
        {
            AddQuestionCandidatesByQuality(candidates, eligibleOptions, CallPhaseFollowUpQuestionQuality.Poor, remainingSlots, usedQuestionIds, usedQuestionTexts);
        }

        if (candidates.Count < 4)
        {
            List<CallPhaseFollowUpQuestionOptionData> orderedEligibleOptions = GetOrderedCandidatesForDisplay(eligibleOptions);
            for (int i = 0; i < orderedEligibleOptions.Count && candidates.Count < 4; i++)
            {
                AddQuestionCandidate(candidates, orderedEligibleOptions[i], usedQuestionIds, usedQuestionTexts);
            }
        }

        if (candidates.Count <= 0 && pendingManualStep != null)
        {
            AddQuestionCandidate(candidates, CreateFallbackPendingQuestionOption(), usedQuestionIds, usedQuestionTexts);
        }

        RememberShownQuestions(candidates);
        return candidates;
    }

    private void AddUnresolvedRealLinkedQuestionCandidates(
        List<CallPhaseFollowUpQuestionOptionData> target,
        List<CallPhaseFollowUpQuestionOptionData> source,
        HashSet<string> usedQuestionIds,
        HashSet<string> usedQuestionTexts,
        int maxTotalCandidates)
    {
        if (target == null || source == null || target.Count >= maxTotalCandidates)
        {
            return;
        }

        List<CallPhaseFollowUpQuestionOptionData> orderedOptions = GetOrderedCandidatesForDisplay(source);
        for (int i = 0; i < orderedOptions.Count && target.Count < maxTotalCandidates; i++)
        {
            CallPhaseFollowUpQuestionOptionData option = orderedOptions[i];
            if (!IsRealUnresolvedLinkedQuestion(option))
            {
                continue;
            }

            AddQuestionCandidate(target, option, usedQuestionIds, usedQuestionTexts);
        }
    }

    public bool TryExecuteManualFollowUpQuestion(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null || !IsWaitingForManualFollowUp || pendingManualStep == null)
        {
            return false;
        }

        TrackSelectedFollowUpQuestion(questionOption);

        if (TryResolveLinkedRealStep(questionOption, out CallPhaseScenarioStepData linkedStep, out bool isCurrentPendingStep, out bool isFinalStep))
        {
            selectedManualFollowUpStep = linkedStep;
            if (isCurrentPendingStep)
            {
                pendingManualStep = null;
                pendingManualStepIsFinal = false;
                waitingForManualFollowUp = false;
                StartScenarioStep(linkedStep, isFinalStep);
                return true;
            }

            StartOutOfOrderRealQuestion(linkedStep);
            return true;
        }

        StartDistractorQuestion(questionOption);
        return true;
    }

    public int GetTotalSelectedFollowUpQuestions()
    {
        return optimalFollowUpCount + acceptableFollowUpCount + poorFollowUpCount;
    }

    public List<CallPhaseScenarioStepData> GetManualFollowUpOptionCandidates()
    {
        List<CallPhaseScenarioStepData> options = new List<CallPhaseScenarioStepData>();
        if (!IsWaitingForManualFollowUp || pendingManualStep == null)
        {
            return options;
        }

        if (pendingManualStep.stepType != CallPhaseScenarioStepType.InformationCollection)
        {
            return options;
        }

        options.Add(pendingManualStep);
        return options;
    }

    public void SetSelectedManualFollowUpStep(CallPhaseScenarioStepData step)
    {
        if (step == null)
        {
            selectedManualFollowUpStep = null;
            return;
        }

        selectedManualFollowUpStep = step;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Selected manual follow-up option '{GetStepDebugName(step)}'.",
                this);
        }
    }

    private void BuildFallbackRuntimeSteps()
    {
        runtimeSteps.Add(CreateFallbackInformationStep(
            "collect_address",
            fireLocationFieldId,
            fireLocationValue,
            addressFollowUpQuestion,
            addressFollowUpReply));

        runtimeSteps.Add(CreateFallbackInformationStep(
            "collect_occupant_risk",
            addressFieldId,
            addressValue,
            occupantRiskFollowUpQuestion,
            occupantRiskFollowUpReply));

        runtimeSteps.Add(CreateFallbackInformationStep(
            "collect_hazard",
            occupantRiskFieldId,
            occupantRiskValue,
            hazardFollowUpQuestion,
            hazardFollowUpReply));

        runtimeSteps.Add(CreateFallbackInformationStep(
            "collect_spread_status",
            hazardFieldId,
            hazardValue,
            spreadStatusFollowUpQuestion,
            spreadStatusFollowUpReply));

        runtimeSteps.Add(CreateFallbackInformationStep(
            "collect_caller_safety",
            spreadStatusFieldId,
            spreadStatusValue,
            callerSafetyFollowUpQuestion,
            callerSafetyFollowUpReply));

        runtimeSteps.Add(CreateFallbackConfirmationStep(
            "confirm_address",
            callerSafetyFieldId,
            callerSafetyValue,
            addressFieldId,
            addressValue,
            addressConfirmationQuestion,
            addressConfirmationReply));

        runtimeSteps.Add(CreateFallbackConfirmationStep(
            "confirm_occupant_risk",
            addressFieldId,
            addressValue,
            occupantRiskFieldId,
            occupantRiskValue,
            occupantRiskConfirmationQuestion,
            occupantRiskConfirmationReply));

        runtimeSteps.Add(CreateFallbackConfirmationStep(
            "confirm_caller_safety",
            occupantRiskFieldId,
            occupantRiskValue,
            callerSafetyFieldId,
            callerSafetyValue,
            callerSafetyConfirmationQuestion,
            callerSafetyConfirmationReply));
    }

    private CallPhaseScenarioStepData CreateFallbackInformationStep(
        string stepId,
        string triggerFieldId,
        string triggerValue,
        string operatorLine,
        string callerLine)
    {
        return new CallPhaseScenarioStepData
        {
            stepId = stepId,
            stepType = CallPhaseScenarioStepType.InformationCollection,
            triggerFieldId = triggerFieldId,
            triggerValue = triggerValue,
            operatorLine = CreateLineData(TranscriptSpeakerType.Operator, operatorLine, false, false),
            callerLine = CreateLineData(TranscriptSpeakerType.Caller, callerLine, true, true)
        };
    }

    private CallPhaseScenarioStepData CreateFallbackConfirmationStep(
        string stepId,
        string triggerFieldId,
        string triggerValue,
        string confirmationFieldId,
        string expectedConfirmedValue,
        string operatorLine,
        string callerLine)
    {
        return new CallPhaseScenarioStepData
        {
            stepId = stepId,
            stepType = CallPhaseScenarioStepType.ConfirmationReadBack,
            triggerFieldId = triggerFieldId,
            triggerValue = triggerValue,
            confirmationFieldId = confirmationFieldId,
            expectedConfirmedValue = expectedConfirmedValue,
            operatorLine = CreateLineData(TranscriptSpeakerType.Operator, operatorLine, false, false),
            callerLine = CreateLineData(TranscriptSpeakerType.Caller, callerLine, true, true)
        };
    }

    private CallPhaseScenarioLineData CreateLineData(
        TranscriptSpeakerType speaker,
        string text,
        bool isExtractable,
        bool startsAsActiveChunk)
    {
        return new CallPhaseScenarioLineData
        {
            speaker = speaker,
            text = text,
            isExtractable = isExtractable,
            startsAsActiveChunk = startsAsActiveChunk
        };
    }

    private bool IsValidRuntimeStep(CallPhaseScenarioStepData step)
    {
        return step != null
            && !string.IsNullOrWhiteSpace(step.triggerFieldId)
            && !string.IsNullOrWhiteSpace(step.triggerValue)
            && step.operatorLine != null
            && !string.IsNullOrWhiteSpace(step.operatorLine.text)
            && step.callerLine != null
            && !string.IsNullOrWhiteSpace(step.callerLine.text);
    }

    private bool DoesStepTriggerMatch(CallPhaseScenarioStepData step, SelectableSpan span)
    {
        return step != null
            && span != null
            && StringMatches(span.TargetFieldId, step.triggerFieldId)
            && StringMatches(span.NormalizedValue, step.triggerValue);
    }

    private bool DoesStepTriggerMatchResolvedState(CallPhaseScenarioStepData step)
    {
        if (step == null || incidentReportController == null || string.IsNullOrWhiteSpace(step.triggerFieldId))
        {
            return false;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(step.triggerFieldId);
        if (fieldView == null || fieldView.CurrentState == ReportFieldState.Empty || string.IsNullOrWhiteSpace(fieldView.CurrentValue))
        {
            return false;
        }

        return DoesStoredFieldValueMatchTrigger(step.triggerFieldId, fieldView.CurrentValue, step.triggerValue);
    }

    private bool DoesFinalStepCompletionMatch(SelectableSpan span)
    {
        if (lastStartedStep == null || span == null)
        {
            return false;
        }

        if (lastStartedStep.stepType == CallPhaseScenarioStepType.ConfirmationReadBack)
        {
            return StringMatches(span.TargetFieldId, lastStartedStep.confirmationFieldId)
                && StringMatches(span.NormalizedValue, lastStartedStep.expectedConfirmedValue);
        }

        if (lastStartedStep.callerLine == null || lastStartedStep.callerLine.extractableSpans == null)
        {
            return false;
        }

        for (int i = 0; i < lastStartedStep.callerLine.extractableSpans.Count; i++)
        {
            CallPhaseExtractableSpanData spanData = lastStartedStep.callerLine.extractableSpans[i];
            if (spanData == null)
            {
                continue;
            }

            if (StringMatches(span.TargetFieldId, spanData.targetFieldId)
                && StringMatches(span.NormalizedValue, spanData.normalizedValue))
            {
                return true;
            }
        }

        return false;
    }

    private string GetStepDebugName(CallPhaseScenarioStepData step)
    {
        return step != null && !string.IsNullOrWhiteSpace(step.stepId)
            ? step.stepId
            : "scenario_step";
    }

    private void TryAdvanceLinearProgression(SelectableSpan latestSpan)
    {
        while (nextStepIndex >= 0 && nextStepIndex < runtimeSteps.Count)
        {
            CallPhaseScenarioStepData step = runtimeSteps[nextStepIndex];
            if (step == null)
            {
                nextStepIndex++;
                continue;
            }

            if (IsStepResolved(step))
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"{nameof(CallPhasePrototypeFollowUpController)}: Skipping already resolved step '{GetStepDebugName(step)}'.",
                        this);
                }

                nextStepIndex++;
                continue;
            }

            bool isReady = IsStepReadyForProgression(step, latestSpan);
            if (!isReady)
            {
                return;
            }

            bool isFinalStep = IsFinalRemainingRuntimeStep(nextStepIndex);
            nextStepIndex++;

            if (followUpMode == CallPhaseFollowUpMode.ManualPopup
                && step.stepType == CallPhaseScenarioStepType.InformationCollection)
            {
                QueueManualFollowUpStep(step, isFinalStep);
                return;
            }

            StartScenarioStep(step, isFinalStep);
            return;
        }
    }

    private bool IsFinalRemainingRuntimeStep(int stepIndex)
    {
        for (int i = stepIndex + 1; i < runtimeSteps.Count; i++)
        {
            CallPhaseScenarioStepData candidate = runtimeSteps[i];
            if (candidate == null)
            {
                continue;
            }

            if (IsStepResolved(candidate))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void StartScenarioStep(CallPhaseScenarioStepData step, bool isFinalStep)
    {
        lastStartedStep = step;
        finalStepAwaitingCompletion = isFinalStep;
        pendingManualStep = null;
        pendingManualStepIsFinal = false;
        waitingForManualFollowUp = false;
        RefreshAskFollowUpAvailability();
        StartManagedRoutine(PlayScenarioStepRoutine(step));
    }

    private void QueueManualFollowUpStep(CallPhaseScenarioStepData step, bool isFinalStep)
    {
        pendingManualStep = step;
        selectedManualFollowUpStep = null;
        pendingManualStepIsFinal = isFinalStep;
        waitingForManualFollowUp = true;
        finalStepAwaitingCompletion = false;

        if (incidentReportController != null)
        {
            incidentReportController.ClearConfirmationContext();
        }

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Waiting for manual follow-up selection at '{GetStepDebugName(step)}'.",
                this);
        }

        RefreshAskFollowUpAvailability();
    }

    private void StartDistractorQuestion(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (pendingManualStep == null)
        {
            return;
        }

        CallPhaseScenarioStepData preservedPendingStep = pendingManualStep;
        bool preservedPendingIsFinal = pendingManualStepIsFinal;

        waitingForManualFollowUp = false;
        RefreshAskFollowUpAvailability();
        StartManagedRoutine(PlayDistractorQuestionRoutine(questionOption, preservedPendingStep, preservedPendingIsFinal));
    }

    private void StartOutOfOrderRealQuestion(CallPhaseScenarioStepData linkedStep)
    {
        if (linkedStep == null || pendingManualStep == null)
        {
            return;
        }

        returnPendingStepAfterOutOfOrder = pendingManualStep;
        returnPendingStepAfterOutOfOrderIsFinal = pendingManualStepIsFinal;
        isExecutingOutOfOrderRealStep = true;
        finalStepAwaitingCompletion = false;
        pendingManualStep = null;
        pendingManualStepIsFinal = false;
        waitingForManualFollowUp = false;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Executing out-of-order real follow-up step '{GetStepDebugName(linkedStep)}'.",
                this);
        }

        StartScenarioStep(linkedStep, false);
    }

    private void FinishOutOfOrderRealStep()
    {
        pendingManualStep = returnPendingStepAfterOutOfOrder;
        pendingManualStepIsFinal = returnPendingStepAfterOutOfOrderIsFinal;
        waitingForManualFollowUp = pendingManualStep != null;
        returnPendingStepAfterOutOfOrder = null;
        returnPendingStepAfterOutOfOrderIsFinal = false;
        isExecutingOutOfOrderRealStep = false;
        finalStepAwaitingCompletion = false;

        if (incidentReportController != null)
        {
            incidentReportController.ClearConfirmationContext();
        }

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (pendingManualStep != null && IsStepResolved(pendingManualStep))
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"{nameof(CallPhasePrototypeFollowUpController)}: Suspended pending step '{GetStepDebugName(pendingManualStep)}' was already resolved, advancing instead of restoring it.",
                    this);
            }

            pendingManualStep = null;
            pendingManualStepIsFinal = false;
            waitingForManualFollowUp = false;
            TryAdvanceLinearProgression(null);
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Out-of-order real follow-up completed and waiting state restored.",
                this);
        }

        RefreshAskFollowUpAvailability();
    }

    private void TrackSelectedFollowUpQuestion(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null)
        {
            return;
        }

        switch (questionOption.quality)
        {
            case CallPhaseFollowUpQuestionQuality.Optimal:
                optimalFollowUpCount++;
                break;
            case CallPhaseFollowUpQuestionQuality.Acceptable:
                acceptableFollowUpCount++;
                break;
            case CallPhaseFollowUpQuestionQuality.Poor:
                poorFollowUpCount++;
                break;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Follow-up quality tracked. Optimal={optimalFollowUpCount}, Acceptable={acceptableFollowUpCount}, Poor={poorFollowUpCount}.",
                this);
        }
    }

    private IEnumerator PlayDistractorQuestionRoutine(
        CallPhaseFollowUpQuestionOptionData questionOption,
        CallPhaseScenarioStepData preservedPendingStep,
        bool preservedPendingIsFinal)
    {
        yield return null;

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (incidentReportController != null)
        {
            incidentReportController.ClearConfirmationContext();
        }

        string operatorQuestion = questionOption != null ? questionOption.questionText : string.Empty;
        if (logsController != null && !string.IsNullOrWhiteSpace(operatorQuestion))
        {
            logsController.AddOperatorLog(operatorQuestion);
        }

        yield return new WaitForSecondsRealtime(CallPhaseResponseSpeedSettings.ApplyDelayPreference(followUpLineDelaySeconds));

        string callerReply = BuildDistractorCallerReply(questionOption);
        if (!string.IsNullOrWhiteSpace(callerReply) && logsController != null)
        {
            logsController.AddCallerLog(callerReply, false, false);
        }

        pendingManualStep = preservedPendingStep;
        pendingManualStepIsFinal = preservedPendingIsFinal;
        waitingForManualFollowUp = preservedPendingStep != null;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(CallPhasePrototypeFollowUpController)}: Played distractor follow-up '{GetQuestionDebugName(questionOption)}' and returned to waiting state.",
                this);
        }

        RefreshAskFollowUpAvailability();
    }

    private List<CallPhaseFollowUpQuestionOptionData> GetEligibleQuestionPoolOptions()
    {
        List<CallPhaseFollowUpQuestionOptionData> options = new List<CallPhaseFollowUpQuestionOptionData>();
        if (scenarioData == null || scenarioData.followUpQuestionPool == null)
        {
            return options;
        }

        for (int i = 0; i < scenarioData.followUpQuestionPool.Count; i++)
        {
            CallPhaseFollowUpQuestionOptionData option = scenarioData.followUpQuestionPool[i];
            if (IsQuestionOptionEligible(option))
            {
                options.Add(option);
            }
        }

        return options;
    }

    private bool IsQuestionOptionEligible(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null || string.IsNullOrWhiteSpace(questionOption.questionText))
        {
            return false;
        }

        bool hasRelatedField = !string.IsNullOrWhiteSpace(questionOption.relatedFieldId);
        bool fieldKnown = hasRelatedField
            && incidentReportController != null
            && incidentReportController.HasCollectedValue(questionOption.relatedFieldId);

        if (!string.IsNullOrWhiteSpace(questionOption.linkedStepId))
        {
            if (HasResolvedFollowUpStep(questionOption.linkedStepId))
            {
                return false;
            }

            if (IsRealUnresolvedLinkedQuestion(questionOption))
            {
                return true;
            }

            if (hasRelatedField && questionOption.hideIfFieldAlreadyKnown && fieldKnown)
            {
                return false;
            }

            return hasRelatedField && !fieldKnown;
        }

        if (hasRelatedField && questionOption.hideIfFieldAlreadyKnown && fieldKnown)
        {
            return false;
        }

        if (questionOption.suggestedWhenFieldMissing && hasRelatedField)
        {
            return !fieldKnown;
        }

        return true;
    }

    private CallPhaseFollowUpQuestionOptionData FindCurrentLinkedQuestionOption(List<CallPhaseFollowUpQuestionOptionData> eligibleOptions)
    {
        if (eligibleOptions == null || pendingManualStep == null)
        {
            return null;
        }

        for (int i = 0; i < eligibleOptions.Count; i++)
        {
            CallPhaseFollowUpQuestionOptionData option = eligibleOptions[i];
            if (option != null && StringMatches(option.linkedStepId, pendingManualStep.stepId))
            {
                return option;
            }
        }

        return null;
    }

    private void AddQuestionCandidatesByQuality(
        List<CallPhaseFollowUpQuestionOptionData> target,
        List<CallPhaseFollowUpQuestionOptionData> source,
        CallPhaseFollowUpQuestionQuality quality,
        int maxToAdd,
        HashSet<string> usedQuestionIds,
        HashSet<string> usedQuestionTexts)
    {
        if (source == null || maxToAdd <= 0)
        {
            return;
        }

        int added = 0;
        List<CallPhaseFollowUpQuestionOptionData> orderedOptions = GetOrderedCandidatesForQuality(source, quality);
        for (int i = 0; i < orderedOptions.Count && added < maxToAdd; i++)
        {
            if (AddQuestionCandidate(target, orderedOptions[i], usedQuestionIds, usedQuestionTexts))
            {
                added++;
            }
        }
    }

    private bool AddQuestionCandidate(
        List<CallPhaseFollowUpQuestionOptionData> target,
        CallPhaseFollowUpQuestionOptionData option,
        HashSet<string> usedQuestionIds,
        HashSet<string> usedQuestionTexts)
    {
        if (target == null || option == null)
        {
            return false;
        }

        string questionId = option.questionId ?? string.Empty;
        string questionText = option.questionText != null ? option.questionText.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(questionText))
        {
            return false;
        }

        if ((!string.IsNullOrWhiteSpace(questionId) && usedQuestionIds.Contains(questionId))
            || usedQuestionTexts.Contains(questionText))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(questionId))
        {
            usedQuestionIds.Add(questionId);
        }

        usedQuestionTexts.Add(questionText);
        target.Add(option);
        return true;
    }

    private List<CallPhaseFollowUpQuestionOptionData> GetOrderedCandidatesForQuality(
        List<CallPhaseFollowUpQuestionOptionData> source,
        CallPhaseFollowUpQuestionQuality quality)
    {
        List<CallPhaseFollowUpQuestionOptionData> orderedOptions = new List<CallPhaseFollowUpQuestionOptionData>();
        if (source == null)
        {
            return orderedOptions;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CallPhaseFollowUpQuestionOptionData option = source[i];
            if (option != null && option.quality == quality)
            {
                orderedOptions.Add(option);
            }
        }

        orderedOptions.Sort(CompareQuestionOptionsForDisplay);
        return orderedOptions;
    }

    private List<CallPhaseFollowUpQuestionOptionData> GetOrderedCandidatesForDisplay(
        List<CallPhaseFollowUpQuestionOptionData> source)
    {
        List<CallPhaseFollowUpQuestionOptionData> orderedOptions = new List<CallPhaseFollowUpQuestionOptionData>();
        if (source == null)
        {
            return orderedOptions;
        }

        orderedOptions.AddRange(source);
        orderedOptions.Sort(CompareQuestionOptionsForDisplay);
        return orderedOptions;
    }

    private int CompareQuestionOptionsForDisplay(
        CallPhaseFollowUpQuestionOptionData left,
        CallPhaseFollowUpQuestionOptionData right)
    {
        int rightScore = GetQuestionDisplayScore(right);
        int leftScore = GetQuestionDisplayScore(left);
        int scoreCompare = rightScore.CompareTo(leftScore);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        string leftId = left != null ? left.questionId ?? string.Empty : string.Empty;
        string rightId = right != null ? right.questionId ?? string.Empty : string.Empty;
        return string.Compare(leftId, rightId, System.StringComparison.OrdinalIgnoreCase);
    }

    private int GetQuestionDisplayScore(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null)
        {
            return int.MinValue;
        }

        int score = 0;
        bool hasRelatedField = !string.IsNullOrWhiteSpace(questionOption.relatedFieldId);
        bool fieldKnown = hasRelatedField
            && incidentReportController != null
            && incidentReportController.HasCollectedValue(questionOption.relatedFieldId);
        bool isCurrentLinked = !string.IsNullOrWhiteSpace(questionOption.linkedStepId)
            && pendingManualStep != null
            && StringMatches(questionOption.linkedStepId, pendingManualStep.stepId);
        bool isRealUnresolvedLinked = IsRealUnresolvedLinkedQuestion(questionOption);

        if (isCurrentLinked)
        {
            score += 100;
        }
        else if (isRealUnresolvedLinked)
        {
            score += 50;
        }

        if (hasRelatedField && !fieldKnown)
        {
            score += 20;
        }

        if (questionOption.suggestedWhenFieldMissing && hasRelatedField && !fieldKnown)
        {
            score += 20;
        }

        if (!questionOption.isDistractorQuestion)
        {
            score += 8;
        }

        score -= GetRecentQuestionPenalty(questionOption.questionId);
        return score;
    }

    private bool IsRealUnresolvedLinkedQuestion(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null || string.IsNullOrWhiteSpace(questionOption.linkedStepId))
        {
            return false;
        }

        if (HasResolvedFollowUpStep(questionOption.linkedStepId))
        {
            return false;
        }

        int linkedStepIndex = FindRuntimeStepIndex(questionOption.linkedStepId);
        if (linkedStepIndex < 0)
        {
            return false;
        }

        CallPhaseScenarioStepData linkedStep = runtimeSteps[linkedStepIndex];
        return linkedStep != null && linkedStep.stepType == CallPhaseScenarioStepType.InformationCollection;
    }

    private int GetRecentQuestionPenalty(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            return 0;
        }

        for (int i = recentShownQuestionIds.Count - 1, distance = 0; i >= 0; i--, distance++)
        {
            if (!StringMatches(recentShownQuestionIds[i], questionId))
            {
                continue;
            }

            return Mathf.Max(10, 40 - (distance * 5));
        }

        return 0;
    }

    private void RememberShownQuestions(List<CallPhaseFollowUpQuestionOptionData> shownQuestions)
    {
        if (shownQuestions == null)
        {
            return;
        }

        for (int i = 0; i < shownQuestions.Count; i++)
        {
            CallPhaseFollowUpQuestionOptionData question = shownQuestions[i];
            if (question == null || string.IsNullOrWhiteSpace(question.questionId))
            {
                continue;
            }

            recentShownQuestionIds.Add(question.questionId);
        }

        while (recentShownQuestionIds.Count > MaxRecentShownQuestions)
        {
            recentShownQuestionIds.RemoveAt(0);
        }
    }

    private bool TryResolveLinkedRealStep(
        CallPhaseFollowUpQuestionOptionData questionOption,
        out CallPhaseScenarioStepData linkedStep,
        out bool isCurrentPendingStep,
        out bool isFinalStep)
    {
        linkedStep = null;
        isCurrentPendingStep = false;
        isFinalStep = false;

        if (questionOption == null
            || string.IsNullOrWhiteSpace(questionOption.linkedStepId))
        {
            return false;
        }

        int linkedStepIndex = FindRuntimeStepIndex(questionOption.linkedStepId);
        if (linkedStepIndex < 0)
        {
            return false;
        }

        linkedStep = runtimeSteps[linkedStepIndex];
        if (linkedStep == null || linkedStep.stepType != CallPhaseScenarioStepType.InformationCollection)
        {
            linkedStep = null;
            return false;
        }

        isCurrentPendingStep = pendingManualStep != null && StringMatches(linkedStep.stepId, pendingManualStep.stepId);
        isFinalStep = isCurrentPendingStep && pendingManualStepIsFinal;
        return true;
    }

    private string BuildDistractorCallerReply(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption != null && !string.IsNullOrWhiteSpace(questionOption.distractorCallerReplyText))
        {
            return questionOption.distractorCallerReplyText.Trim();
        }

        if (questionOption != null && questionOption.quality == CallPhaseFollowUpQuestionQuality.Acceptable)
        {
            return "I'm not sure about that right now, I just need help fast!";
        }

        if (questionOption != null && !string.IsNullOrWhiteSpace(questionOption.relatedFieldId)
            && incidentReportController != null
            && incidentReportController.HasCollectedValue(questionOption.relatedFieldId))
        {
            return "I already told you that, please send help!";
        }

        return "I don't know, I just need the fire department here now!";
    }

    private bool IsStepResolved(CallPhaseScenarioStepData step)
    {
        return step != null && !string.IsNullOrWhiteSpace(step.stepId) && HasResolvedFollowUpStep(step.stepId);
    }

    private bool IsStepReadyForProgression(CallPhaseScenarioStepData step, SelectableSpan latestSpan)
    {
        return step != null
            && (DoesStepTriggerMatch(step, latestSpan) || DoesStepTriggerMatchResolvedState(step));
    }

    private bool DoesStoredFieldValueMatchTrigger(string fieldId, string storedValue, string expectedValue)
    {
        if (StringMatches(fieldId, "hazard"))
        {
            return HazardValueUtility.ContainsValue(storedValue, expectedValue);
        }

        return StringMatches(storedValue, expectedValue);
    }

    private string GetQuestionDebugName(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null)
        {
            return "manual_question";
        }

        if (!string.IsNullOrWhiteSpace(questionOption.questionId))
        {
            return questionOption.questionId;
        }

        return !string.IsNullOrWhiteSpace(questionOption.questionText)
            ? questionOption.questionText
            : "manual_question";
    }

    private CallPhaseFollowUpQuestionOptionData CreateFallbackPendingQuestionOption()
    {
        return new CallPhaseFollowUpQuestionOptionData
        {
            questionId = pendingManualStep != null ? pendingManualStep.stepId : "pending_follow_up",
            questionText = pendingManualStep != null && pendingManualStep.operatorLine != null
                ? pendingManualStep.operatorLine.text
                : "Follow-up Question",
            quality = CallPhaseFollowUpQuestionQuality.Optimal,
            linkedStepId = pendingManualStep != null ? pendingManualStep.stepId : string.Empty
        };
    }

    private int FindRuntimeStepIndex(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            return -1;
        }

        for (int i = 0; i < runtimeSteps.Count; i++)
        {
            CallPhaseScenarioStepData step = runtimeSteps[i];
            if (step != null && StringMatches(step.stepId, stepId))
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshAskFollowUpAvailability()
    {
        bool canAskFollowUp = CanAskFollowUp;

        if (askFollowUpButton != null)
        {
            askFollowUpButton.interactable = canAskFollowUp;
            CallPhaseFunctionButtonVisuals.Apply(askFollowUpButton, canAskFollowUp);
        }

        if (canAskFollowUp && !lastCanAskFollowUp)
        {
            ManualFollowUpAvailable?.Invoke();
        }

        lastCanAskFollowUp = canAskFollowUp;
    }

    private void ScheduleDeferredProgressionSweep()
    {
        if (deferredProgressionCoroutine != null)
        {
            StopCoroutine(deferredProgressionCoroutine);
        }

        deferredProgressionCoroutine = StartCoroutine(DeferredProgressionSweepRoutine());
    }

    private IEnumerator DeferredProgressionSweepRoutine()
    {
        yield return null;
        deferredProgressionCoroutine = null;

        if (progressionCompleted || activeStepRoutine != null || isExecutingOutOfOrderRealStep)
        {
            yield break;
        }

        if (waitingForManualFollowUp && pendingManualStep != null)
        {
            if (!IsStepResolved(pendingManualStep))
            {
                yield break;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"{nameof(CallPhasePrototypeFollowUpController)}: Deferred sweep cleared stale waiting step '{GetStepDebugName(pendingManualStep)}' after it was resolved in report state.",
                    this);
            }

            pendingManualStep = null;
            pendingManualStepIsFinal = false;
            waitingForManualFollowUp = false;
            RefreshAskFollowUpAvailability();
        }

        TryAdvanceLinearProgression(null);
    }

    private Button FindButtonByName(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && string.Equals(button.name, buttonName, System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private void StartManagedRoutine(IEnumerator routine)
    {
        if (routine == null)
        {
            return;
        }

        if (activeStepRoutine != null)
        {
            StopCoroutine(activeStepRoutine);
        }

        activeStepRoutine = StartCoroutine(RunManagedRoutine(routine));
    }

    private IEnumerator RunManagedRoutine(IEnumerator routine)
    {
        yield return StartCoroutine(routine);
        activeStepRoutine = null;
        RefreshAskFollowUpAvailability();
    }

    private bool StringMatches(string left, string right)
    {
        return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
    }
}

public enum CallPhaseFollowUpMode
{
    Auto,
    ManualPopup
}
