using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CallPhaseUiChromeRuntimeLocalizer
{
    private static readonly Dictionary<string, string> StaticTextKeyMap = new Dictionary<string, string>(System.StringComparer.Ordinal)
    {
        { "CALL IN", "callphase.header.call_in" },
        { "INCOMING EMERGENCY CALL", "callphase.header.incoming_emergency_call" },
        { "Waiting for operator response...", "callphase.callin.waiting_operator" },
        { "ACCEPT CALL", "callphase.btn.accept_call" },
        { "SKIP", "common.btn.skip" },
        { "Start Duty", "callphase.btn.start_duty" },
        { "FIRE REPORT INTAKE SYSTEM", "callphase.header.fire_report_intake_system" },
        { "INCIDENT REPORT", "callphase.header.incident_report" },
        { "[1] Ask Follow-up", "callphase.btn.ask_followup" },
        { "[2] Assess Risk", "callphase.btn.assess_risk" },
        { "[3] Submit Report", "callphase.btn.submit_report" },
        { "ASK FOLLOW-UP QUESTION", "callphase.header.ask_followup_question" },
        { "Choose the next question to ask the caller.", "callphase.followup.hint.choose_question" },
        { "ASSESS INCIDENT RISK", "callphase.header.assess_incident_risk" },
        { "EVERITY ASSESSMENT", "callphase.header.severity_assessment" },
        { "Review gathered information and select severity", "callphase.assess.hint.review_severity" },
        { "SUBMIT INCIDENT REPORT", "callphase.header.submit_incident_report" },
        { "Review the final report before submission", "callphase.submit.hint.review_final_report" },
        { "SUBMISSION SUMMARY", "callphase.header.submission_summary" },
        { "FINAL REPORT SUMMARY", "callphase.header.final_report_summary" },
        { "PERFORMANCE REVIEW", "callphase.header.performance_review" },
        { "CALL PHASE RESULT", "callphase.header.call_phase_result" },
        { "INCIDENT SUMMARY", "callphase.header.incident_summary" },
        { "CONFIRMED FACTS", "callphase.header.confirmed_facts" },
        { "SUBMISSION READINESS", "callphase.header.submission_readiness" },
        { "SEVERITY INDICATORS", "callphase.header.severity_indicators" },
        { "SELECTION", "callphase.header.selection" },
        { "transcript logs", "callphase.header.transcript_logs" },
        { "TRANSCRIPT LOGS", "callphase.header.transcript_logs" },
        { "INFO TYPE", "callphase.header.info_type" },
        { "MATCH TYPE", "callphase.header.match_type" },
        { "PENALTY", "callphase.header.penalty" },
        { "TARGET FIELD", "callphase.header.target_field" },
        { "Address:", "callphase.field.address" },
        { "Fire Location:", "callphase.field.fire_location" },
        { "Occupant Risk:", "callphase.field.occupant_risk" },
        { "Hazard:", "callphase.field.hazard" },
        { "Spread Status:", "callphase.field.spread_status" },
        { "Caller Safety:", "callphase.field.caller_safety" },
        { "Severity:", "callphase.field.severity" },
        { "Category\t:", "callphase.field.category" },
        { "Source\t:", "callphase.field.source" },
        { "Priority\t:", "callphase.field.priority" },
        { "Call Id\t:", "callphase.field.call_id" },
        { "Back", "common.btn.back" },
        { "BACK", "common.btn.back" },
        { "Confirm", "common.btn.confirm" },
        { "Clear", "common.btn.clear" },
        { "Exit", "callphase.btn.exit_extraction" },
        { "Confirm Submit", "callphase.btn.confirm_submit" },
        { "Confirm Assessment", "callphase.btn.confirm_assessment" },
        { "Next", "common.btn.next" },
        { "Retry", "common.btn.retry" },
        { "LOW", "callphase.severity.low" },
        { "MEDIUM", "callphase.severity.medium" },
        { "HIGH", "callphase.severity.high" },
        { "Unassessed", "callphase.value.unassessed" },
        { "Status: Ready to submit", "callphase.status.ready_to_submit" },
        { "Status: Active", "callphase.status.active" },
        { "Enter text...", "common.placeholder.enter_text" },
        { "Optional note / reminder:", "callphase.label.optional_note" },
    };

    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
        {
            LocalizeAllLoadedCallPhaseUi();
            return;
        }

        initialized = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        LanguageManager.LanguageChanged += OnLanguageChanged;
        LocalizeAllLoadedCallPhaseUi();
    }

    private static void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        LocalizeAllLoadedCallPhaseUi();
    }

    private static void OnLanguageChanged(AppLanguage _)
    {
        LocalizeAllLoadedCallPhaseUi();
    }

    private static void LocalizeAllLoadedCallPhaseUi()
    {
        TMP_Text[] tmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text tmpText = tmpTexts[i];
            if (!IsCallPhaseUiText(tmpText))
            {
                continue;
            }

            AttachLocalizedTextIfNeeded(tmpText.gameObject, tmpText.text);
            AttachShortValueOverrideIfNeeded(tmpText);
        }

        Text[] legacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            Text legacyText = legacyTexts[i];
            if (!IsCallPhaseUiText(legacyText))
            {
                continue;
            }

            AttachLocalizedTextIfNeeded(legacyText.gameObject, legacyText.text);
        }
    }

    private static bool IsCallPhaseUiText(Component textComponent)
    {
        if (textComponent == null || textComponent.gameObject.scene.rootCount <= 0)
        {
            return false;
        }

        if (textComponent.GetComponentInParent<CallPhaseScenarioContext>(true) != null)
        {
            return true;
        }

        if (textComponent.GetComponentInParent<CallInScheduler>(true) != null)
        {
            return true;
        }

        return false;
    }

    private static void AttachLocalizedTextIfNeeded(GameObject targetObject, string authoredText)
    {
        if (targetObject == null)
        {
            return;
        }

        if (IsRuntimeValueText(targetObject))
        {
            return;
        }

        LocalizedText localizedText = targetObject.GetComponent<LocalizedText>();
        if (localizedText != null)
        {
            return;
        }

        string normalizedText = authoredText != null ? authoredText.Trim() : string.Empty;
        if (!StaticTextKeyMap.TryGetValue(normalizedText, out string localizationKey))
        {
            return;
        }

        localizedText = targetObject.AddComponent<LocalizedText>();
        localizedText.SetKey(localizationKey);
    }

    private static bool IsRuntimeValueText(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return false;
        }

        string objectName = targetObject.name ?? string.Empty;
        if (
            objectName.IndexOf("ValueText", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("SeverityText", System.StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            return true;
        }

        Transform current = targetObject.transform;
        while (current != null)
        {
            if (string.Equals(current.name, "ValueRoot", System.StringComparison.Ordinal))
            {
                return true;
            }

            if (
                string.Equals(current.name, "SubmitReportPopupV2", System.StringComparison.Ordinal)
                && string.Equals(targetObject.name, "ValueTextV2", System.StringComparison.Ordinal)
            )
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void AttachShortValueOverrideIfNeeded(TMP_Text text)
    {
        if (text == null || !string.Equals(text.name, "StatusText", System.StringComparison.Ordinal))
        {
            return;
        }

        LocalizedText localizedText = text.GetComponent<LocalizedText>();
        if (localizedText == null)
        {
            return;
        }

        CallPhaseUiChromeShortValueOverride overrideComponent = text.GetComponent<CallPhaseUiChromeShortValueOverride>();
        if (overrideComponent == null)
        {
            overrideComponent = text.gameObject.AddComponent<CallPhaseUiChromeShortValueOverride>();
        }

        switch (localizedText.LocalizationKey)
        {
            case "callphase.status.active":
                overrideComponent.Configure("callphase.status.active_short", "Dang hoat dong");
                break;
            case "callphase.status.ready_to_submit":
                overrideComponent.Configure("callphase.status.ready_to_submit_short", "San sang nop");
                break;
        }
    }
}
