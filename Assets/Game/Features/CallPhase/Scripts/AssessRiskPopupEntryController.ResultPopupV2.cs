using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AssessRiskPopupEntryController
{
    private static readonly Color ResultPopupV2GoodColor = new Color(0.22f, 0.78f, 0.38f, 1f);
    private static readonly Color ResultPopupV2AcceptColor = new Color(1f, 0.55f, 0f, 1f);
    private static readonly Color ResultPopupV2PoorColor = new Color(0.92f, 0.24f, 0.20f, 1f);

    private TMP_Text resultPopupV2CaseText;
    private TMP_Text resultPopupV2GradeText;
    private TMP_Text resultPopupV2PercentText;
    private TMP_Text resultPopupV2InfoCountText;
    private TMP_Text resultPopupV2InfoStatusText;
    private TMP_Text resultPopupV2ExpectedSeverityText;
    private TMP_Text resultPopupV2ChosenSeverityText;
    private TMP_Text resultPopupV2CallTimeText;
    private TMP_Text resultPopupV2TargetTimeText;
    private TMP_Text resultPopupV2TimeEfficiencyText;
    private TMP_Text resultPopupV2OptimalFollowUpText;
    private TMP_Text resultPopupV2AcceptableFollowUpText;
    private TMP_Text resultPopupV2PoorFollowUpText;
    private TMP_Text resultPopupV2AcceptableFollowUpLabelText;
    private TMP_Text resultPopupV2FeedbackBodyText;
    private readonly List<TMP_Text> resultPopupV2FeedbackItemTexts = new List<TMP_Text>();
    private RectTransform resultPopupV2InfoProgressBar;
    private RectTransform resultPopupV2TimeProgressBar;
    private RectTransform resultPopupV2FeedbackContent;
    private Image resultPopupV2InfoStatusIcon;
    private Image resultPopupV2SeverityStatusIcon;
    private Image resultPopupV2TimeStatusIcon;
    private Image resultPopupV2FollowUpStatusIcon;
    private GameObject resultPopupV2SeverityPerfectLogo;
    private GameObject resultPopupV2SeverityMissLogo;
    private Transform resultPopupV2OptimalFollowUpRoot;
    private Transform resultPopupV2AcceptFollowUpRoot;
    private Transform resultPopupV2PoorFollowUpRoot;

    private bool IsResultPopupV2()
    {
        return resultPopup != null
            && string.Equals(resultPopup.name, "ResultPopupV2", StringComparison.Ordinal);
    }

    private void ResolveResultPopupV2References()
    {
        if (!IsResultPopupV2())
        {
            return;
        }

        Transform root = resultPopup.transform;
        resultPopupV2CaseText = resultPopupV2CaseText ?? FindResultPopupV2Text(root, "Container/Header/Left/GroupText/Bottom/Text (TMP)");
        resultPopupV2GradeText = resultPopupV2GradeText ?? FindResultPopupV2Text(root, "Container/Header/Right/Left/GameObject/Text (TMP) (1)");
        resultPopupV2PercentText = resultPopupV2PercentText ?? FindResultPopupV2Text(root, "Container/Header/Right/Right/Text (TMP)");
        resultPopupV2InfoCountText = resultPopupV2InfoCountText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell2/Body/Top/Text (TMP)", "Container/Body/Left/Grid/Cell1 (1)/Body/Top/Text (TMP)");
        resultPopupV2InfoStatusText = resultPopupV2InfoStatusText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell2/Body/Top/Text (TMP) (1)", "Container/Body/Left/Grid/Cell1 (1)/Body/Top/Text (TMP) (1)");
        resultPopupV2ExpectedSeverityText = resultPopupV2ExpectedSeverityText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell1/Body/Left/GameObject (1)/Text (TMP)");
        resultPopupV2ChosenSeverityText = resultPopupV2ChosenSeverityText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell1/Body/Right/GameObject (1)/Text (TMP)");
        resultPopupV2CallTimeText = resultPopupV2CallTimeText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell4/Body/Container/R1/CallTimeText", "Container/Body/Left/Grid/Cell1 (3)/Body/Container/R1/CallTimeText");
        resultPopupV2TargetTimeText = resultPopupV2TargetTimeText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell4/Body/Container/R1/TimeTargetText", "Container/Body/Left/Grid/Cell1 (3)/Body/Container/R1/TimeTargetText");
        resultPopupV2TimeEfficiencyText = resultPopupV2TimeEfficiencyText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell4/Body/Container/R3/GameObject/Text (TMP)", "Container/Body/Left/Grid/Cell1 (3)/Body/Container/R3/GameObject/Text (TMP)");
        resultPopupV2OptimalFollowUpText = resultPopupV2OptimalFollowUpText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell3/Body/Col1/Left/Text (TMP)", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1/Left/Text (TMP)");
        resultPopupV2AcceptableFollowUpText = resultPopupV2AcceptableFollowUpText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell3/Body/Col1 (1)/Left/Text (TMP)", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1 (1)/Left/Text (TMP)");
        resultPopupV2PoorFollowUpText = resultPopupV2PoorFollowUpText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell3/Body/Col1 (2)/Left/Text (TMP)", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1 (2)/Left/Text (TMP)");
        resultPopupV2AcceptableFollowUpLabelText = resultPopupV2AcceptableFollowUpLabelText ?? FindResultPopupV2Text(root, "Container/Body/Left/Grid/Cell3/Body/Col1 (1)/Left/Text (TMP) (1)", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1 (1)/Left/Text (TMP) (1)");
        resultPopupV2FeedbackBodyText = resultPopupV2FeedbackBodyText ?? FindResultPopupV2Text(root, "Container/Body/Right/Body/Container/Bottom/Scroll View/Viewport/Content/Text (TMP)");

        if (resultPopupV2FeedbackItemTexts.Count == 0)
        {
            AddResultPopupV2FeedbackText(root, "Container/Body/Right/Body/Container/Top/Item1/Text/Text (TMP)");
            AddResultPopupV2FeedbackText(root, "Container/Body/Right/Body/Container/Top/Item1 (1)/Text/Text (TMP)");
            AddResultPopupV2FeedbackText(root, "Container/Body/Right/Body/Container/Top/Item1 (2)/Text/Text (TMP)");
        }

        if (resultPopupV2InfoProgressBar == null)
        {
            Transform bar = FindResultPopupV2Transform(root, "Container/Body/Left/Grid/Cell2/Body/Bottom/GameObject/BarBg/Bar", "Container/Body/Left/Grid/Cell1 (1)/Body/Bottom/GameObject/BarBg/Bar");
            resultPopupV2InfoProgressBar = bar != null ? bar.GetComponent<RectTransform>() : null;
        }

        if (resultPopupV2TimeProgressBar == null)
        {
            Transform bar = FindResultPopupV2Transform(root, "Container/Body/Left/Grid/Cell4/Body/Container/R2/GameObject/Image/Image (1)", "Container/Body/Left/Grid/Cell1 (3)/Body/Container/R2/GameObject/Image/Image (1)");
            resultPopupV2TimeProgressBar = bar != null ? bar.GetComponent<RectTransform>() : null;
        }

        if (resultPopupV2FeedbackContent == null)
        {
            Transform content = root.Find("Container/Body/Right/Body/Container/Bottom/Scroll View/Viewport/Content");
            resultPopupV2FeedbackContent = content != null ? content.GetComponent<RectTransform>() : null;
        }

        resultPopupV2InfoStatusIcon = resultPopupV2InfoStatusIcon ?? FindResultPopupV2Image(root, "Container/Body/Left/Grid/Cell2/GameObject/PerfectImage", "Container/Body/Left/Grid/Cell1 (1)/GameObject/PerfectImage");
        resultPopupV2SeverityStatusIcon = resultPopupV2SeverityStatusIcon ?? FindResultPopupV2Image(root, "Container/Body/Left/Grid/Cell1/GameObject/PerfectImage");
        resultPopupV2TimeStatusIcon = resultPopupV2TimeStatusIcon ?? FindResultPopupV2Image(root, "Container/Body/Left/Grid/Cell4/GameObject/PerfectImage", "Container/Body/Left/Grid/Cell1 (3)/GameObject/PerfectImage");
        resultPopupV2FollowUpStatusIcon = resultPopupV2FollowUpStatusIcon ?? FindResultPopupV2Image(root, "Container/Body/Left/Grid/Cell3/GameObject/PerfectImage", "Container/Body/Left/Grid/Cell1 (2)/GameObject/PerfectImage");
        resultPopupV2SeverityPerfectLogo = resultPopupV2SeverityPerfectLogo ?? FindResultPopupV2GameObject(root, "Container/Body/Left/Grid/Cell1/Body/Mid/Logo1");
        resultPopupV2SeverityMissLogo = resultPopupV2SeverityMissLogo ?? FindResultPopupV2GameObject(root, "Container/Body/Left/Grid/Cell1/Body/Mid/Logo2");

        resultPopupV2OptimalFollowUpRoot = resultPopupV2OptimalFollowUpRoot ?? FindResultPopupV2Transform(root, "Container/Body/Left/Grid/Cell3/Body/Col1/Left", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1/Left");
        resultPopupV2AcceptFollowUpRoot = resultPopupV2AcceptFollowUpRoot ?? FindResultPopupV2Transform(root, "Container/Body/Left/Grid/Cell3/Body/Col1 (1)/Left", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1 (1)/Left");
        resultPopupV2PoorFollowUpRoot = resultPopupV2PoorFollowUpRoot ?? FindResultPopupV2Transform(root, "Container/Body/Left/Grid/Cell3/Body/Col1 (2)/Left", "Container/Body/Left/Grid/Cell1 (2)/Body/Col1 (2)/Left");
    }

    private Button FindResultPopupV2NextPhaseButton()
    {
        if (!IsResultPopupV2())
        {
            return null;
        }

        Transform button = resultPopup.transform.Find("Container/Footer/Button");
        return button != null ? button.GetComponent<Button>() : null;
    }

    private void ClearResultPopupReferencesOutsideRoot()
    {
        if (!IsComponentUnderResultPopup(resultPopupBackButton))
        {
            resultPopupBackButton = null;
        }

        if (!IsComponentUnderResultPopup(resultPopupNextPhaseButton))
        {
            resultPopupNextPhaseButton = null;
        }

        if (!IsComponentUnderResultPopup(resultPopupSummaryText))
        {
            resultPopupSummaryText = null;
        }

        if (!IsComponentUnderResultPopup(resultPopupReviewText))
        {
            resultPopupReviewText = null;
        }
    }

    private bool IsComponentUnderResultPopup(Component component)
    {
        if (component == null || resultPopup == null)
        {
            return false;
        }

        Transform componentTransform = component.transform;
        Transform resultPopupTransform = resultPopup.transform;
        return componentTransform == resultPopupTransform
            || componentTransform.IsChildOf(resultPopupTransform);
    }

    private void PopulateResultPopupV2(CallPhaseResultSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        ResolveResultPopupV2References();

        int scorePercent = GetResultScorePercent(snapshot);
        SetResultPopupV2Text(resultPopupV2CaseText, BuildResultPopupV2CaseText(snapshot));
        SetResultPopupV2Text(resultPopupV2GradeText, GetResultGrade(scorePercent));
        SetResultPopupV2Text(resultPopupV2PercentText, $"{scorePercent}%");

        int gatheredCount = CountGatheredInfoFields();
        int gatheredTotal = SummaryFieldIds.Length;
        float gatheredRatio = gatheredTotal > 0 ? (float)gatheredCount / gatheredTotal : 0f;
        SetResultPopupV2Text(resultPopupV2InfoCountText, $"{gatheredCount}/{gatheredTotal}");
        SetResultPopupV2Text(resultPopupV2InfoStatusText, GetInfoGatheredStatus(gatheredCount, gatheredTotal));
        ApplyResultPopupV2Progress(resultPopupV2InfoProgressBar, gatheredRatio, GetProgressColor(gatheredRatio));
        SetResultPopupV2IconVisible(resultPopupV2InfoStatusIcon, gatheredTotal > 0 && gatheredCount >= gatheredTotal);

        SetResultPopupV2Text(resultPopupV2ExpectedSeverityText, GetSeverityLevelDisplay(snapshot.expectedSeverityDisplay));
        SetResultPopupV2Text(resultPopupV2ChosenSeverityText, GetSeverityLevelDisplay(snapshot.severityChosenDisplay));
        SetResultPopupV2Text(
            resultPopupV2CallTimeText,
            string.Format(
                CallPhaseUiChromeText.Tr("callphase.result.v2.call_time", "Call Time: {0}"),
                FormatDuration(snapshot.callDurationSeconds)));
        SetResultPopupV2Text(
            resultPopupV2TargetTimeText,
            string.Format(
                CallPhaseUiChromeText.Tr("callphase.result.v2.target_time", "Target: {0}"),
                FormatDuration(snapshot.targetCallTimeSeconds)));
        SetResultPopupV2Text(resultPopupV2TimeEfficiencyText, snapshot.callTimeEfficiencyLabel);
        float timeRatio = GetTimeEfficiencyRatio(snapshot);
        ApplyResultPopupV2Progress(resultPopupV2TimeProgressBar, timeRatio, GetProgressColor(timeRatio));

        SetResultPopupV2Text(resultPopupV2OptimalFollowUpText, snapshot.followUpOptimalCount.ToString());
        SetResultPopupV2Text(resultPopupV2AcceptableFollowUpText, snapshot.followUpAcceptableCount.ToString());
        SetResultPopupV2Text(resultPopupV2PoorFollowUpText, snapshot.followUpPoorCount.ToString());
        SetResultPopupV2Text(
            resultPopupV2AcceptableFollowUpLabelText,
            CallPhaseUiChromeText.Tr("callphase.result.v2.accept", "Accept"));
        ApplyResultPopupV2FollowUpColumnColors();

        bool isSeverityPerfect = IsSeverityPerfect(snapshot);
        SetResultPopupV2IconVisible(resultPopupV2SeverityStatusIcon, isSeverityPerfect);
        SetResultPopupV2ObjectVisible(resultPopupV2SeverityPerfectLogo, isSeverityPerfect);
        SetResultPopupV2ObjectVisible(resultPopupV2SeverityMissLogo, !isSeverityPerfect);
        SetResultPopupV2IconVisible(resultPopupV2TimeStatusIcon, IsTimePerfect(snapshot));
        SetResultPopupV2IconVisible(resultPopupV2FollowUpStatusIcon, IsFollowUpPerfect(snapshot));

        PopulateResultPopupV2Feedback(snapshot.feedbackLines);
        SetResultPopupV2Text(resultPopupV2FeedbackBodyText, BuildResultPopupV2FeedbackBody(snapshot));
        RebuildResultPopupV2FeedbackLayout();
    }

    private Transform FindResultPopupV2Transform(Transform root, params string[] relativePaths)
    {
        if (root == null || relativePaths == null)
        {
            return null;
        }

        for (int i = 0; i < relativePaths.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(relativePaths[i]))
            {
                continue;
            }

            Transform target = root.Find(relativePaths[i]);
            if (target != null)
            {
                return target;
            }
        }

        return null;
    }

    private TMP_Text FindResultPopupV2Text(Transform root, params string[] relativePaths)
    {
        Transform target = FindResultPopupV2Transform(root, relativePaths);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Image FindResultPopupV2Image(Transform root, params string[] relativePaths)
    {
        Transform target = FindResultPopupV2Transform(root, relativePaths);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private GameObject FindResultPopupV2GameObject(Transform root, params string[] relativePaths)
    {
        Transform target = FindResultPopupV2Transform(root, relativePaths);
        return target != null ? target.gameObject : null;
    }

    private void AddResultPopupV2FeedbackText(Transform root, string relativePath)
    {
        TMP_Text text = FindResultPopupV2Text(root, relativePath);
        if (text != null)
        {
            resultPopupV2FeedbackItemTexts.Add(text);
        }
    }

    private void SetResultPopupV2Text(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        CallPhaseUiChromeText.ApplyCurrentFont(text);
        text.text = value ?? string.Empty;
    }

    private void RebuildResultPopupV2FeedbackLayout()
    {
        if (resultPopupV2FeedbackBodyText != null)
        {
            resultPopupV2FeedbackBodyText.ForceMeshUpdate();
        }

        if (resultPopupV2FeedbackContent == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(resultPopupV2FeedbackContent);

        RectTransform parent = resultPopupV2FeedbackContent.parent as RectTransform;
        while (parent != null && parent != resultPopup.transform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            parent = parent.parent as RectTransform;
        }

        Canvas.ForceUpdateCanvases();
    }

    private int GetResultScorePercent(CallPhaseResultSnapshot snapshot)
    {
        if (snapshot == null || snapshot.maximumScore <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(
            Mathf.RoundToInt((float)snapshot.finalScore / snapshot.maximumScore * 100f),
            0,
            100);
    }

    private string GetResultGrade(int scorePercent)
    {
        if (scorePercent >= 97)
        {
            return "A+";
        }

        if (scorePercent >= 93)
        {
            return "A";
        }

        if (scorePercent >= 90)
        {
            return "A-";
        }

        if (scorePercent >= 87)
        {
            return "B+";
        }

        if (scorePercent >= 83)
        {
            return "B";
        }

        if (scorePercent >= 80)
        {
            return "B-";
        }

        if (scorePercent >= 70)
        {
            return "C";
        }

        if (scorePercent >= 60)
        {
            return "D";
        }

        return "F";
    }

    private string BuildResultPopupV2CaseText(CallPhaseResultSnapshot snapshot)
    {
        string caseId = !string.IsNullOrWhiteSpace(snapshot.caseId)
            ? snapshot.caseId.Trim()
            : CallPhaseUiChromeText.Tr("callphase.result.v2.unknown_case", "Unknown case");

        return string.Format(
            CallPhaseUiChromeText.Tr("callphase.result.v2.case_generated", "Case #{0} // Review generated"),
            caseId);
    }

    private int CountGatheredInfoFields()
    {
        int count = 0;
        for (int i = 0; i < SummaryFieldIds.Length; i++)
        {
            if (HasUsableFieldValue(SummaryFieldIds[i]))
            {
                count++;
            }
        }

        return count;
    }

    private string GetInfoGatheredStatus(int gatheredCount, int gatheredTotal)
    {
        if (gatheredTotal <= 0 || gatheredCount <= 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.info.none", "Missing");
        }

        if (gatheredCount >= gatheredTotal)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.info.complete", "Complete");
        }

        if (gatheredCount >= Mathf.CeilToInt(gatheredTotal * 0.66f))
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.info.almost_complete", "Almost Complete");
        }

        return CallPhaseUiChromeText.Tr("callphase.result.v2.info.incomplete", "Incomplete");
    }

    private void ApplyResultPopupV2Progress(RectTransform progressBar, float ratio, Color color)
    {
        if (progressBar == null)
        {
            return;
        }

        float clampedRatio = Mathf.Clamp01(ratio);

        Image progressImage = progressBar.GetComponent<Image>();
        if (progressImage != null)
        {
            progressImage.fillAmount = clampedRatio;
            progressImage.color = color;

            if (progressImage.type == Image.Type.Filled)
            {
                Vector3 filledScale = progressBar.localScale;
                filledScale.x = 1f;
                progressBar.localScale = filledScale;
                return;
            }
        }

        Vector3 scale = progressBar.localScale;
        scale.x = clampedRatio;
        progressBar.localScale = scale;
    }

    private Color GetProgressColor(float ratio)
    {
        float clampedRatio = Mathf.Clamp01(ratio);
        if (clampedRatio >= 0.99f)
        {
            return ResultPopupV2GoodColor;
        }

        if (clampedRatio >= 0.5f)
        {
            return ResultPopupV2AcceptColor;
        }

        return ResultPopupV2PoorColor;
    }

    private float GetTimeEfficiencyRatio(CallPhaseResultSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.callTimeQualified)
        {
            return 0f;
        }

        int maximumScore = GetMaximumCallTimeScore();
        if (maximumScore <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)snapshot.callTimeScore / maximumScore);
    }

    private void ApplyResultPopupV2FollowUpColumnColors()
    {
        ApplyResultPopupV2ColumnColor(resultPopupV2OptimalFollowUpRoot, ResultPopupV2GoodColor);
        ApplyResultPopupV2ColumnColor(resultPopupV2AcceptFollowUpRoot, ResultPopupV2AcceptColor);
        ApplyResultPopupV2ColumnColor(resultPopupV2PoorFollowUpRoot, ResultPopupV2PoorColor);
    }

    private void ApplyResultPopupV2ColumnColor(Transform root, Color color)
    {
        if (root == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].color = color;
            }
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.transform == root)
            {
                continue;
            }

            Color existingColor = image.color;
            image.color = new Color(color.r, color.g, color.b, existingColor.a);
        }
    }

    private void SetResultPopupV2IconVisible(Image icon, bool isVisible)
    {
        if (icon != null)
        {
            icon.gameObject.SetActive(isVisible);
        }
    }

    private void SetResultPopupV2ObjectVisible(GameObject target, bool isVisible)
    {
        if (target != null)
        {
            target.SetActive(isVisible);
        }
    }

    private bool IsSeverityPerfect(CallPhaseResultSnapshot snapshot)
    {
        return snapshot != null && snapshot.severityScore >= GetSeverityCorrectScore();
    }

    private bool IsTimePerfect(CallPhaseResultSnapshot snapshot)
    {
        return snapshot != null
            && snapshot.callTimeQualified
            && snapshot.callTimeScore >= GetMaximumCallTimeScore();
    }

    private bool IsFollowUpPerfect(CallPhaseResultSnapshot snapshot)
    {
        return snapshot != null
            && snapshot.followUpTotalCount > 0
            && snapshot.followUpPoorCount <= 0
            && snapshot.followUpScore >= GetMaximumFollowUpScore();
    }

    private string GetSeverityLevelDisplay(string severityDisplayValue)
    {
        if (string.IsNullOrWhiteSpace(severityDisplayValue)
            || string.Equals(
                severityDisplayValue,
                CallPhaseUiChromeText.Tr("callphase.value.not_provided", "Not provided"),
                StringComparison.OrdinalIgnoreCase))
        {
            return CallPhaseUiChromeText.Tr("callphase.value.not_provided", "Not provided");
        }

        if (severityDisplayValue.IndexOf(SeverityLow, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.severity.level1", "Level 1");
        }

        if (severityDisplayValue.IndexOf(SeverityMedium, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.severity.level2", "Level 2");
        }

        if (severityDisplayValue.IndexOf(SeverityHigh, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.v2.severity.level3", "Level 3");
        }

        return severityDisplayValue;
    }

    private void PopulateResultPopupV2Feedback(List<string> feedbackLines)
    {
        for (int i = 0; i < resultPopupV2FeedbackItemTexts.Count; i++)
        {
            TMP_Text feedbackText = resultPopupV2FeedbackItemTexts[i];
            if (feedbackText == null)
            {
                continue;
            }

            bool hasLine = feedbackLines != null
                && i < feedbackLines.Count
                && !string.IsNullOrWhiteSpace(feedbackLines[i]);
            SetResultPopupV2Text(feedbackText, hasLine ? feedbackLines[i] : string.Empty);

            Transform itemRoot = FindResultPopupV2FeedbackItemRoot(feedbackText.transform);
            if (itemRoot != null)
            {
                itemRoot.gameObject.SetActive(hasLine);
            }
        }
    }

    private Transform FindResultPopupV2FeedbackItemRoot(Transform textTransform)
    {
        Transform current = textTransform;
        while (current != null && current.parent != null)
        {
            if (current.name.StartsWith("Item1", StringComparison.Ordinal))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private string BuildResultPopupV2FeedbackBody(CallPhaseResultSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.final_score", "Final Score"));
        builder.Append(": ");
        builder.Append(snapshot.finalScore);
        builder.Append(" / ");
        builder.Append(snapshot.maximumScore);
        builder.Append('\n');
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.followup_quality", "Follow-up Quality"));
        builder.Append(": ");
        builder.Append(snapshot.followUpQualityLabel);
        builder.Append('\n');
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.efficiency", "Efficiency"));
        builder.Append(": ");
        builder.Append(snapshot.callTimeEfficiencyLabel);

        if (snapshot.feedbackLines != null && snapshot.feedbackLines.Count > 0)
        {
            builder.Append("\n\n");
            builder.Append(CallPhaseUiChromeText.Tr("callphase.result.feedback", "Feedback"));
            builder.Append(':');
            for (int i = 0; i < snapshot.feedbackLines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(snapshot.feedbackLines[i]))
                {
                    continue;
                }

                builder.Append("\n- ");
                builder.Append(snapshot.feedbackLines[i]);
            }
        }

        return builder.ToString();
    }
}
