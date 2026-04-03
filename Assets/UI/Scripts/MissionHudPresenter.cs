using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MissionHudPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text missionDescriptionText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text objectivesText;

    [Header("Display")]
    [SerializeField] private bool hideWhenMissionMissing = true;
    [SerializeField] private bool hideWhenMissionIdle = false;
    [SerializeField] private bool showMissionDescription = false;
    [SerializeField] private bool showCompletedObjectives = true;
    [SerializeField] private string pendingPrefix = "[ ]";
    [SerializeField] private string completedPrefix = "[DONE]";
    [SerializeField] private string failedPrefix = "[FAILED]";
    [SerializeField] private string timerRemainingFormat = "{0:F1}s left";
    [SerializeField] private string timerElapsedFormat = "{0:F1}s elapsed";

    private readonly StringBuilder objectiveBuilder = new StringBuilder();

    private void Awake()
    {
        ResolveReferences();
        RefreshView();
    }

    private void OnEnable()
    {
        RefreshView();
    }

    private void Update()
    {
        RefreshView();
    }

    private void ResolveReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void RefreshView()
    {
        ResolveReferences();
        bool visible = ShouldBeVisible();
        ApplyVisibility(visible);
        if (!visible || missionSystem == null)
        {
            return;
        }

        SetText(missionTitleText, missionSystem.MissionTitle);
        SetText(missionDescriptionText, showMissionDescription ? missionSystem.MissionDescription : string.Empty);
        SetText(stateText, $"State: {missionSystem.State}");
        SetText(stageText, BuildStageText());
        SetText(timerText, BuildTimerText());
        SetText(objectivesText, BuildObjectivesText());
    }

    private bool ShouldBeVisible()
    {
        if (missionSystem == null)
        {
            return !hideWhenMissionMissing;
        }

        if (hideWhenMissionIdle && missionSystem.State == IncidentMissionSystem.MissionState.Idle)
        {
            return false;
        }

        return true;
    }

    private void ApplyVisibility(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = visible;
        rootCanvasGroup.blocksRaycasts = visible;
    }

    private string BuildStageText()
    {
        if (missionSystem == null || !missionSystem.HasActiveStage)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(missionSystem.CurrentStageTitle))
        {
            return $"Stage {missionSystem.CurrentStageIndex + 1}/{missionSystem.TotalStageCount}";
        }

        return $"Stage {missionSystem.CurrentStageIndex + 1}/{missionSystem.TotalStageCount}: {missionSystem.CurrentStageTitle}";
    }

    private string BuildTimerText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        if (missionSystem.TimeLimitSeconds > 0f)
        {
            return string.Format(timerRemainingFormat, missionSystem.RemainingTimeSeconds);
        }

        return string.Format(timerElapsedFormat, missionSystem.ElapsedTime);
    }

    private string BuildObjectivesText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        objectiveBuilder.Clear();
        bool wroteAnyLine = false;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (!showCompletedObjectives && status.IsComplete && !status.HasFailed)
            {
                continue;
            }

            if (wroteAnyLine)
            {
                objectiveBuilder.Append('\n');
            }

            string prefix = status.HasFailed ? failedPrefix : status.IsComplete ? completedPrefix : pendingPrefix;
            objectiveBuilder.Append(prefix);
            objectiveBuilder.Append(' ');
            objectiveBuilder.Append(status.Summary);
            wroteAnyLine = true;
        }

        return objectiveBuilder.ToString();
    }

    private static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.text = value ?? string.Empty;
    }
}
