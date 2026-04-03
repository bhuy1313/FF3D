using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup shell for manual follow-up selection.
/// This owns modal open/close, prefab-spawned options, and selected option handoff.
/// </summary>
public class FollowUpPopupController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CallPhasePrototypeFollowUpController followUpController;
    [SerializeField] private Button askFollowUpButton;
    [SerializeField] private GameObject followUpPopupRootObject;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private RectTransform questionListRoot;
    [SerializeField] private GameObject questionOptionPrefab;

    [Header("Behavior")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<FollowUpQuestionOptionView> spawnedOptionViews = new List<FollowUpQuestionOptionView>();
    private readonly HashSet<string> loggedMissingWarnings = new HashSet<string>();
    private CallPhaseFollowUpQuestionOptionData selectedQuestionOption;
    private bool isPopupOpen;

    private void Awake()
    {
        ResolveReferences();
        HidePopupImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToUi();
        RefreshConfirmButtonState();
    }

    private void Start()
    {
        HidePopupImmediate();
    }

    private void OnDisable()
    {
        UnsubscribeFromUi();
        HidePopupImmediate();
    }

    private void OnDestroy()
    {
        UnsubscribeFromUi();
    }

    public void ResetForScenarioRun()
    {
        ResolveReferences();
        HidePopupImmediate();
    }

    public void OpenPopup()
    {
        if (isPopupOpen)
        {
            return;
        }

        ResolveReferences();
        if (followUpController == null)
        {
            LogMissingReference(nameof(followUpController), this);
            return;
        }

        if (followUpController.FollowUpMode != CallPhaseFollowUpMode.ManualPopup || !followUpController.CanAskFollowUp)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(FollowUpPopupController)}: Open ignored because manual follow-up is not available.", this);
            }
            return;
        }

        if (followUpPopupRootObject == null || questionListRoot == null || questionOptionPrefab == null)
        {
            LogMissingReference(nameof(followUpPopupRootObject), followUpPopupRootObject);
            LogMissingReference(nameof(questionListRoot), this);
            LogMissingReference(nameof(questionOptionPrefab), this);
            return;
        }

        BuildQuestionOptions();
        selectedQuestionOption = null;
        RefreshConfirmButtonState();

        followUpPopupRootObject.SetActive(true);
        followUpPopupRootObject.transform.SetAsLastSibling();
        isPopupOpen = true;

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(FollowUpPopupController)}: Follow-up popup opened.", this);
        }
    }

    public void ClosePopup()
    {
        HidePopupImmediate();
    }

    private void ConfirmSelection()
    {
        if (selectedQuestionOption == null || followUpController == null)
        {
            return;
        }

        bool started = followUpController.TryExecuteManualFollowUpQuestion(selectedQuestionOption);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(FollowUpPopupController)}: Confirmed follow-up option '{GetDisplayText(selectedQuestionOption)}' (started={started}).",
                this);
        }

        HidePopupImmediate();
    }

    private void HandleOptionClicked(FollowUpQuestionOptionView optionView)
    {
        if (optionView == null)
        {
            return;
        }

        selectedQuestionOption = optionView.QuestionOptionData;

        for (int i = 0; i < spawnedOptionViews.Count; i++)
        {
            FollowUpQuestionOptionView view = spawnedOptionViews[i];
            if (view != null)
            {
                view.SetSelected(view == optionView);
            }
        }

        RefreshConfirmButtonState();
    }

    private void BuildQuestionOptions()
    {
        ClearSpawnedOptions();
        EnsureQuestionListLayout();

        List<CallPhaseFollowUpQuestionOptionData> optionCandidates = followUpController != null
            ? followUpController.GetManualFollowUpQuestionCandidates()
            : new List<CallPhaseFollowUpQuestionOptionData>();
        List<CallPhaseFollowUpQuestionOptionData> displayCandidates = new List<CallPhaseFollowUpQuestionOptionData>(optionCandidates);
        ShuffleQuestionDisplayOrder(displayCandidates);

        for (int i = 0; i < displayCandidates.Count; i++)
        {
            CallPhaseFollowUpQuestionOptionData questionOption = displayCandidates[i];
            GameObject optionInstance = Instantiate(questionOptionPrefab, questionListRoot);
            optionInstance.name = $"FollowUpOption_{i + 1}";

            LayoutElement layoutElement = optionInstance.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = optionInstance.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = 44f;
            layoutElement.flexibleWidth = 1f;

            FollowUpQuestionOptionView optionView = optionInstance.GetComponent<FollowUpQuestionOptionView>();
            if (optionView == null)
            {
                optionView = optionInstance.AddComponent<FollowUpQuestionOptionView>();
            }

            if (optionView == null)
            {
                Debug.LogWarning($"{nameof(FollowUpPopupController)}: Question option prefab is missing {nameof(FollowUpQuestionOptionView)}.", optionInstance);
                Destroy(optionInstance);
                continue;
            }

            optionView.Configure(questionOption, GetDisplayText(questionOption));
            optionView.Clicked += HandleOptionClicked;
            spawnedOptionViews.Add(optionView);
        }

        if (spawnedOptionViews.Count <= 0)
        {
            Debug.LogWarning($"{nameof(FollowUpPopupController)}: No follow-up options were available to show.", this);
        }
    }

    private void ShuffleQuestionDisplayOrder(List<CallPhaseFollowUpQuestionOptionData> displayCandidates)
    {
        if (displayCandidates == null || displayCandidates.Count <= 1)
        {
            return;
        }

        for (int i = displayCandidates.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            if (swapIndex == i)
            {
                continue;
            }

            CallPhaseFollowUpQuestionOptionData temp = displayCandidates[i];
            displayCandidates[i] = displayCandidates[swapIndex];
            displayCandidates[swapIndex] = temp;
        }
    }

    private void ClearSpawnedOptions()
    {
        for (int i = 0; i < spawnedOptionViews.Count; i++)
        {
            FollowUpQuestionOptionView optionView = spawnedOptionViews[i];
            if (optionView != null)
            {
                optionView.Clicked -= HandleOptionClicked;
            }
        }

        spawnedOptionViews.Clear();
        selectedQuestionOption = null;

        if (questionListRoot == null)
        {
            return;
        }

        while (questionListRoot.childCount > 0)
        {
            Transform child = questionListRoot.GetChild(0);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void EnsureQuestionListLayout()
    {
        if (questionListRoot == null)
        {
            return;
        }

        VerticalLayoutGroup layoutGroup = questionListRoot.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = questionListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 12f;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = questionListRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = questionListRoot.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RefreshConfirmButtonState()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = selectedQuestionOption != null;
        }
    }

    private void HidePopupImmediate()
    {
        ClearSpawnedOptions();
        selectedQuestionOption = null;
        isPopupOpen = false;

        if (followUpPopupRootObject != null)
        {
            followUpPopupRootObject.SetActive(false);
        }

        RefreshConfirmButtonState();
    }

    private void ResolveReferences()
    {
        if (followUpController == null)
        {
            followUpController = GetComponent<CallPhasePrototypeFollowUpController>();
        }

        if (askFollowUpButton == null)
        {
            askFollowUpButton = FindButtonByName("btnAskFollowUp");
        }

        if (followUpPopupRootObject == null)
        {
            followUpPopupRootObject = FindGameObjectByName("FollowUpPopup");
        }

        if (cancelButton == null && followUpPopupRootObject != null)
        {
            cancelButton = FindButtonByNameWithin(followUpPopupRootObject.transform, "btnBack");
        }

        if (confirmButton == null && followUpPopupRootObject != null)
        {
            confirmButton = FindButtonByNameWithin(followUpPopupRootObject.transform, "btnConfirm");
        }

        if (questionListRoot == null && followUpPopupRootObject != null)
        {
            questionListRoot = FindQuestionListRootFallback(followUpPopupRootObject.transform);
        }
    }

    private void SubscribeToUi()
    {
        if (askFollowUpButton != null)
        {
            askFollowUpButton.onClick.RemoveListener(OpenPopup);
            askFollowUpButton.onClick.AddListener(OpenPopup);
        }
        else
        {
            LogMissingReference(nameof(askFollowUpButton), this);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ClosePopup);
            cancelButton.onClick.AddListener(ClosePopup);
        }
        else
        {
            LogMissingReference(nameof(cancelButton), followUpPopupRootObject != null ? followUpPopupRootObject : this);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
            confirmButton.onClick.AddListener(ConfirmSelection);
        }
        else
        {
            LogMissingReference(nameof(confirmButton), followUpPopupRootObject != null ? followUpPopupRootObject : this);
        }
    }

    private void UnsubscribeFromUi()
    {
        if (askFollowUpButton != null)
        {
            askFollowUpButton.onClick.RemoveListener(OpenPopup);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ClosePopup);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
        }

        for (int i = 0; i < spawnedOptionViews.Count; i++)
        {
            FollowUpQuestionOptionView optionView = spawnedOptionViews[i];
            if (optionView != null)
            {
                optionView.Clicked -= HandleOptionClicked;
            }
        }
    }

    private string GetDisplayText(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption != null && !string.IsNullOrWhiteSpace(questionOption.questionText))
        {
            return questionOption.questionText.Trim();
        }

        return questionOption != null && !string.IsNullOrWhiteSpace(questionOption.questionId)
            ? questionOption.questionId
            : "Follow-up Question";
    }

    private void LogMissingReference(string key, Object context)
    {
        if (!loggedMissingWarnings.Add(key))
        {
            return;
        }

        Debug.LogWarning($"{nameof(FollowUpPopupController)}: Missing required reference '{key}'.", context);
    }

    private Button FindButtonByName(string objectName)
    {
        GameObject foundObject = FindGameObjectByName(objectName);
        return foundObject != null ? foundObject.GetComponent<Button>() : null;
    }

    private GameObject FindGameObjectByName(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private Button FindButtonByNameWithin(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.GetComponent<Button>();
            }
        }

        return null;
    }

    private RectTransform FindQuestionListRootFallback(Transform popupRoot)
    {
        if (popupRoot == null)
        {
            return null;
        }

        RectTransform[] rectTransforms = popupRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform candidate = rectTransforms[i];
            if (candidate == null || candidate == popupRoot)
            {
                continue;
            }

            if (candidate.childCount >= 3 && candidate.GetComponentInParent<Button>() == null)
            {
                return candidate;
            }
        }

        return null;
    }
}
