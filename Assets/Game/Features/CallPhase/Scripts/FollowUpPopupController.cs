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
    [SerializeField] private CallPhaseScenarioRuntimeController followUpController;
    [SerializeField] private Button askFollowUpButton;
    [SerializeField] private GameObject followUpPopupRootObject;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private RectTransform questionListRoot;
    [SerializeField] private GameObject questionOptionPrefab;

    [Header("Behavior")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<FollowUpQuestionOptionView> spawnedOptionViews = new List<FollowUpQuestionOptionView>();
    private readonly List<FollowUpQuestionOptionView> fixedOptionViews = new List<FollowUpQuestionOptionView>();
    private readonly HashSet<string> loggedMissingWarnings = new HashSet<string>();
    private CallPhaseFollowUpQuestionOptionData selectedQuestionOption;
    private Coroutine deferredLayoutRefreshCoroutine;
    private bool isPopupOpen;

    public static bool AnyPopupOpen { get; private set; }

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
        TryAutoOpenPopup();
    }

    private void Start()
    {
        HidePopupImmediate();
    }

    private void Update()
    {
        if (!isPopupOpen)
        {
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Escape, KeyCode.Backspace))
        {
            ClosePopup();
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Alpha1, KeyCode.Keypad1))
        {
            SelectOptionByIndex(0);
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Alpha2, KeyCode.Keypad2))
        {
            SelectOptionByIndex(1);
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Alpha3, KeyCode.Keypad3))
        {
            SelectOptionByIndex(2);
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Alpha4, KeyCode.Keypad4))
        {
            SelectOptionByIndex(3);
            return;
        }

        if (WasPopupShortcutPressed(KeyCode.Return, KeyCode.KeypadEnter))
        {
            TriggerButton(confirmButton);
        }
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

        bool isV2 = IsFollowUpPopupV2();
        if (followUpPopupRootObject == null
            || (!isV2 && (questionListRoot == null || questionOptionPrefab == null)))
        {
            LogMissingReference(nameof(followUpPopupRootObject), followUpPopupRootObject);
            if (!isV2)
            {
                LogMissingReference(nameof(questionListRoot), this);
                LogMissingReference(nameof(questionOptionPrefab), this);
            }
            return;
        }

        followUpPopupRootObject.SetActive(true);
        followUpPopupRootObject.transform.SetAsLastSibling();

        BuildQuestionOptions();
        selectedQuestionOption = null;
        RefreshConfirmButtonState();
        RefreshQuestionListLayoutImmediate();
        StartDeferredQuestionListLayoutRefresh();

        isPopupOpen = true;
        AnyPopupOpen = true;

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

        if (IsFollowUpPopupV2())
        {
            ConfirmSelection();
        }
    }

    private void SelectOptionByIndex(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= spawnedOptionViews.Count)
        {
            return;
        }

        HandleOptionClicked(spawnedOptionViews[optionIndex]);
    }

    private void BuildQuestionOptions()
    {
        ClearSpawnedOptions();

        List<CallPhaseFollowUpQuestionOptionData> optionCandidates = followUpController != null
            ? followUpController.GetManualFollowUpQuestionCandidates()
            : new List<CallPhaseFollowUpQuestionOptionData>();
        List<CallPhaseFollowUpQuestionOptionData> displayCandidates = new List<CallPhaseFollowUpQuestionOptionData>(optionCandidates);
        ShuffleQuestionDisplayOrder(displayCandidates);

        if (IsFollowUpPopupV2())
        {
            BuildFixedQuestionOptions(displayCandidates);
            return;
        }

        EnsureQuestionListLayout();

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

    private void BuildFixedQuestionOptions(List<CallPhaseFollowUpQuestionOptionData> displayCandidates)
    {
        ResolveFixedOptionViews();

        int optionCount = displayCandidates != null ? displayCandidates.Count : 0;
        for (int i = 0; i < fixedOptionViews.Count; i++)
        {
            FollowUpQuestionOptionView optionView = fixedOptionViews[i];
            if (optionView == null)
            {
                continue;
            }

            bool hasOption = i < optionCount && displayCandidates[i] != null;
            optionView.gameObject.SetActive(hasOption);
            optionView.Clicked -= HandleOptionClicked;

            if (!hasOption)
            {
                optionView.SetSelected(false);
                continue;
            }

            CallPhaseFollowUpQuestionOptionData questionOption = displayCandidates[i];
            optionView.Configure(questionOption, GetDisplayText(questionOption));
            optionView.Clicked += HandleOptionClicked;
            spawnedOptionViews.Add(optionView);
        }

        if (spawnedOptionViews.Count <= 0)
        {
            Debug.LogWarning($"{nameof(FollowUpPopupController)}: No follow-up options were available to show.", this);
        }
    }

    private void ResolveFixedOptionViews()
    {
        if (!IsFollowUpPopupV2() || followUpPopupRootObject == null)
        {
            return;
        }

        fixedOptionViews.Clear();
        Transform container = followUpPopupRootObject.transform.Find("GameObject/Container");
        if (container == null)
        {
            container = followUpPopupRootObject.transform.Find("Container");
        }

        for (int i = 1; i <= 4; i++)
        {
            Transform cell = container != null ? container.Find($"Cell{i}") : null;
            if (cell == null)
            {
                continue;
            }

            FollowUpQuestionOptionView optionView = cell.GetComponent<FollowUpQuestionOptionView>();
            if (optionView == null)
            {
                optionView = cell.gameObject.AddComponent<FollowUpQuestionOptionView>();
            }

            fixedOptionViews.Add(optionView);
        }
    }

    private bool IsFollowUpPopupV2()
    {
        return followUpPopupRootObject != null
            && string.Equals(followUpPopupRootObject.name, "FollowUpPopupV2", System.StringComparison.Ordinal);
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

        for (int i = 0; i < fixedOptionViews.Count; i++)
        {
            FollowUpQuestionOptionView optionView = fixedOptionViews[i];
            if (optionView != null)
            {
                optionView.Clicked -= HandleOptionClicked;
                optionView.SetSelected(false);
                optionView.gameObject.SetActive(false);
            }
        }

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

    private void RefreshQuestionListLayoutImmediate()
    {
        if (questionListRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        RectTransform current = questionListRoot;
        while (current != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(current);
            current = current.parent as RectTransform;
        }

        ScrollRect scrollRect = questionListRoot.GetComponentInParent<ScrollRect>(true);
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.transform as RectTransform);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void StartDeferredQuestionListLayoutRefresh()
    {
        if (deferredLayoutRefreshCoroutine != null)
        {
            StopCoroutine(deferredLayoutRefreshCoroutine);
        }

        deferredLayoutRefreshCoroutine = StartCoroutine(DeferredQuestionListLayoutRefresh());
    }

    private System.Collections.IEnumerator DeferredQuestionListLayoutRefresh()
    {
        yield return null;
        RefreshQuestionListLayoutImmediate();
        deferredLayoutRefreshCoroutine = null;
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
        if (deferredLayoutRefreshCoroutine != null)
        {
            StopCoroutine(deferredLayoutRefreshCoroutine);
            deferredLayoutRefreshCoroutine = null;
        }

        ClearSpawnedOptions();
        selectedQuestionOption = null;
        isPopupOpen = false;
        AnyPopupOpen = false;

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
            followUpController = GetComponent<CallPhaseScenarioRuntimeController>();
        }

        if (askFollowUpButton == null)
        {
            askFollowUpButton = FindButtonByName("btnAskFollowUp");
        }

        GameObject v2Root = FindGameObjectByName("FollowUpPopupV2");
        if (v2Root != null)
        {
            followUpPopupRootObject = v2Root;
            cancelButton = null;
            confirmButton = null;
            questionListRoot = null;
        }

        if (followUpPopupRootObject == null)
        {
            followUpPopupRootObject = FindGameObjectByName("FollowUpPopup");
        }

        if (cancelButton == null && followUpPopupRootObject != null)
        {
            cancelButton = IsFollowUpPopupV2()
                ? FindButtonByNameWithin(followUpPopupRootObject.transform, "Close")
                : FindButtonByNameWithin(followUpPopupRootObject.transform, "btnBack");
        }

        if (confirmButton == null && followUpPopupRootObject != null && !IsFollowUpPopupV2())
        {
            confirmButton = FindButtonByNameWithin(followUpPopupRootObject.transform, "btnConfirm");
        }

        if (questionListRoot == null && followUpPopupRootObject != null && !IsFollowUpPopupV2())
        {
            questionListRoot = FindQuestionListRootFallback(followUpPopupRootObject.transform);
        }

        if (IsFollowUpPopupV2())
        {
            ResolveFixedOptionViews();
        }
    }

    private void SubscribeToUi()
    {
        if (followUpController != null)
        {
            followUpController.ManualFollowUpAvailable -= HandleManualFollowUpAvailable;
            followUpController.ManualFollowUpAvailable += HandleManualFollowUpAvailable;
        }
        else
        {
            LogMissingReference(nameof(followUpController), this);
        }

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
        else if (!IsFollowUpPopupV2())
        {
            LogMissingReference(nameof(confirmButton), followUpPopupRootObject != null ? followUpPopupRootObject : this);
        }
    }

    private void UnsubscribeFromUi()
    {
        if (followUpController != null)
        {
            followUpController.ManualFollowUpAvailable -= HandleManualFollowUpAvailable;
        }

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

    private void HandleManualFollowUpAvailable()
    {
        TryAutoOpenPopup();
    }

    private void TryAutoOpenPopup()
    {
        if (isPopupOpen || !CallPhaseAutoQuestionSettings.GetSavedOrDefaultEnabled())
        {
            return;
        }

        OpenPopup();
    }

    private string GetDisplayText(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        string localizedQuestion = followUpController != null
            ? followUpController.GetQuestionDisplayText(questionOption)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(localizedQuestion))
        {
            return localizedQuestion;
        }

        return questionOption != null && !string.IsNullOrWhiteSpace(questionOption.questionId)
            ? questionOption.questionId
            : CallPhaseUiChromeText.Tr("callphase.followup.question_fallback", "Follow-up Question");
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

    private static bool WasPopupShortcutPressed(KeyCode primary, KeyCode secondary)
    {
        return Input.GetKeyDown(primary) || Input.GetKeyDown(secondary);
    }

    private static void TriggerButton(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            return;
        }

        button.onClick.Invoke();
    }
}

