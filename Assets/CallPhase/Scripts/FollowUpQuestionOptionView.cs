using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual row for one follow-up question option inside FollowUpPopup.
/// </summary>
public class FollowUpQuestionOptionView : MonoBehaviour
{
    private static readonly Color NormalBackgroundColor = new Color(0.11764706f, 0.11764706f, 0.11764706f, 1f);
    private static readonly Color SelectedBackgroundColor = new Color(0.95f, 0.72f, 0.38f, 1f);
    private static readonly Color NormalTextColor = Color.white;
    private static readonly Color SelectedTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);

    [SerializeField] private Button optionButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text questionText;

    private readonly List<TMP_Text> questionTextTargets = new List<TMP_Text>();
    private CallPhaseFollowUpQuestionOptionData questionOptionData;
    private CallPhaseScenarioStepData stepData;

    public event Action<FollowUpQuestionOptionView> Clicked;

    public CallPhaseScenarioStepData StepData => stepData;
    public CallPhaseFollowUpQuestionOptionData QuestionOptionData => questionOptionData;

    private void Awake()
    {
        ResolveReferences();

        if (optionButton != null)
        {
            optionButton.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (optionButton != null)
        {
            optionButton.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Configure(CallPhaseScenarioStepData step, string displayText)
    {
        stepData = step;
        questionOptionData = null;
        ApplyDisplayText(displayText);
        SetSelected(false);
    }

    public void Configure(CallPhaseFollowUpQuestionOptionData optionData, string displayText)
    {
        questionOptionData = optionData;
        stepData = null;
        ApplyDisplayText(displayText);
        SetSelected(false);
    }

    private void ApplyDisplayText(string displayText)
    {
        ResolveReferences();

        string resolvedText = string.IsNullOrWhiteSpace(displayText) ? "Follow-up Question" : displayText;

        if (questionText != null)
        {
            questionText.text = resolvedText;
        }

        for (int i = 0; i < questionTextTargets.Count; i++)
        {
            TMP_Text textTarget = questionTextTargets[i];
            if (textTarget != null)
            {
                textTarget.text = resolvedText;
            }
        }

    }

    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
        {
            backgroundImage.enabled = true;
            backgroundImage.color = selected ? SelectedBackgroundColor : NormalBackgroundColor;
        }

        if (questionText != null)
        {
            questionText.color = selected ? SelectedTextColor : NormalTextColor;
        }
    }

    private void ResolveReferences()
    {
        if (optionButton == null)
        {
            optionButton = GetComponent<Button>();
        }

        if (optionButton == null)
        {
            optionButton = GetComponentInChildren<Button>(true);
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage == null && optionButton != null)
        {
            backgroundImage = optionButton.targetGraphic as Image;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponentInChildren<Image>(true);
        }

        if (questionText == null)
        {
            questionText = GetComponentInChildren<TMP_Text>(true);
        }

        questionTextTargets.Clear();
        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text textTarget = allTexts[i];
            if (textTarget != null && !questionTextTargets.Contains(textTarget))
            {
                questionTextTargets.Add(textTarget);
            }
        }
    }

    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }
}
