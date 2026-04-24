using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class HubV2MissionPresenter : MonoBehaviour
{
    private const string HubFallbackOperationKey = "mission.hub.fallback_operation";
    private const string HubObjectivesCompletedFormatKey = "mission.hub.progress.objectives_completed";

    private enum ObjectiveVisualState
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    [Serializable]
    private sealed class ObjectiveItemView
    {
        public GameObject Root;
        public IncidentMissionSystem.ObjectivePresentationKind Kind;
        public Image Icon;
        public TMP_Text Label;
        public RectTransform ProgressFill;
        public TMP_Text ProgressValueText;
        public TMP_Text RescueCounterText;
        public Image RescueCounterBackground;
        public List<Image> RescueVictimIcons = new List<Image>();

        public bool IsValid => Root != null && Label != null;
        public bool IsFireItem => Kind == IncidentMissionSystem.ObjectivePresentationKind.Fire;
        public bool IsRescueItem => Kind == IncidentMissionSystem.ObjectivePresentationKind.Rescue;

        public bool SetActive(bool active)
        {
            if (Root != null && Root.activeSelf != active)
            {
                Root.SetActive(active);
                return true;
            }

            return false;
        }
    }

    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text objectiveCounterText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text progressValueText;
    [SerializeField] private RectTransform objectivesListRoot;
    [SerializeField] private RectTransform hubDetailRoot;
    [SerializeField] private RectTransform hubTitleRoot;
    [SerializeField] private Image accentBarImage;
    [SerializeField] private GameObject fireObjectiveItemPrefab;
    [SerializeField] private GameObject rescueObjectiveItemPrefab;
    [SerializeField] private GameObject genericObjectiveItemPrefab;
    [SerializeField] private bool rebuildLayoutOnChanges = true;
    [SerializeField, Min(0f)] private float layoutRebuildThrottleSeconds = 0.05f;

    [Header("Display")]
    [SerializeField] private bool hideWhenMissionMissing = true;
    [SerializeField] private bool hideWhenMissionIdle = false;
    [SerializeField] private bool showObjectiveScores = true;
    [SerializeField] private string fallbackMissionTitle = "OPERATION";
    [SerializeField] private string idleTimerText = "--:--";
    [SerializeField] private string objectiveCounterFormat = "{0}";
    [SerializeField] private string progressTextFormat = "{0}/{1} OBJECTIVES COMPLETED";
    [SerializeField] private string progressValueFormat = "{0:0}%";

    [Header("Timer Animation")]
    [Tooltip("Bật hiệu ứng đếm số cho Timer khi bắt đầu nhiệm vụ")]
    [SerializeField] private bool animateTimerOnStart = true;
    [Tooltip("Thời gian chạy hiệu ứng đếm số (giây)")]
    [SerializeField] private float timerAnimDuration = 1.5f;

    [Header("Colors")]
    [SerializeField] private Color idleAccentColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color runningAccentColor = new Color(0.18f, 0.84f, 0.43f, 1f);
    [SerializeField] private Color completedAccentColor = new Color(0.21f, 0.86f, 0.62f, 1f);
    [SerializeField] private Color failedAccentColor = new Color(0.92f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color pendingObjectiveColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private Color activeObjectiveColor = Color.white;
    [SerializeField] private Color completedObjectiveColor = new Color(0.59f, 1f, 0.77f, 0.9f);
    [SerializeField] private Color failedObjectiveColor = new Color(1f, 0.62f, 0.62f, 0.95f);
    [SerializeField] private Color rescueCounterPendingColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color rescueCounterCompletedColor = new Color(0.18f, 0.84f, 0.43f, 1f);
    [SerializeField] private Color rescueVictimPendingColor = Color.white;
    [SerializeField] private Color rescueVictimRescuedColor = new Color(0.18f, 0.84f, 0.43f, 1f);
    [SerializeField] private Color rescueVictimInactiveColor = new Color(1f, 1f, 1f, 0.2f);

    private readonly List<ObjectiveItemView> objectiveViews = new List<ObjectiveItemView>();
    private bool objectiveViewsInitialized;
    private int lastObjectiveLayoutSignature = int.MinValue;
    private bool layoutRebuildPending;
    private float nextLayoutRebuildTime;

    private float animatedTimerValue = 0f;
    private bool isTimerAnimating = false;
    private bool hasTimerAnimated = false;

    private void Awake()
    {
        ResolveReferences();
        RefreshView();
        RequestLayoutRebuild();
        ProcessPendingLayoutRebuild(forceImmediate: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshView();
        RequestLayoutRebuild();
        ProcessPendingLayoutRebuild(forceImmediate: true);
    }

    private void Update()
    {
        RefreshView();
        ProcessPendingLayoutRebuild(forceImmediate: false);
    }

    private void ResolveReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>(FindObjectsInactive.Include);
        }

        if (missionSystem == null)
        {
            IncidentMissionSystem[] missionSystems = Resources.FindObjectsOfTypeAll<IncidentMissionSystem>();
            for (int i = 0; i < missionSystems.Length; i++)
            {
                if (missionSystems[i] != null)
                {
                    missionSystem = missionSystems[i];
                    break;
                }
            }
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        missionTitleText ??= FindText("MissionTitleText");
        timerText ??= FindText("TimerText");
        progressText ??= FindText("ProgressText");
        objectivesListRoot ??= FindRectTransform("ObjectivesList");
        hubDetailRoot ??= FindRectTransform("HubV2_Detail");
        hubTitleRoot ??= FindRectTransform("HubV2_Tittle");
        if (hubTitleRoot == null)
        {
            hubTitleRoot = FindRectTransform("HubV2_Title");
        }
        progressValueText ??= FindTextOutsideObjectives("ProgressValueText");
        accentBarImage ??= FindImage("Bar");
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

        bool titleChanged = SetText(missionTitleText, BuildMissionTitleText());
        SetText(timerText, BuildTimerText());
        if (objectiveCounterText != null)
        {
            SetText(objectiveCounterText, BuildObjectiveCounterText());
        }
        SetText(progressText, BuildProgressText());
        SetText(progressValueText, BuildProgressValueText());

        if (accentBarImage != null)
        {
            accentBarImage.color = ResolveAccentColor(missionSystem.State);
        }

        if (titleChanged)
        {
            RequestLayoutRebuild();
        }

        RefreshObjectiveList();
    }

    private bool ShouldBeVisible()
    {
        if (missionSystem == null)
        {
            return !hideWhenMissionMissing;
        }

        return !(hideWhenMissionIdle && missionSystem.State == IncidentMissionSystem.MissionState.Idle);
    }

    private void ApplyVisibility(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
    }

    private string BuildMissionTitleText()
    {
        string fallbackTitle = LanguageManager.Tr(HubFallbackOperationKey, fallbackMissionTitle);
        if (missionSystem == null)
        {
            return fallbackTitle;
        }

        return string.IsNullOrWhiteSpace(missionSystem.MissionOperationTitle)
            ? fallbackTitle
            : missionSystem.MissionOperationTitle.Trim();
    }

    private string BuildTimerText()
    {
        if (missionSystem == null)
        {
            return idleTimerText;
        }

        // Đặt lại cờ animation nếu nhiệm vụ đang Idle (để chuẩn bị cho nhiệm vụ mới)
        if (missionSystem.State == IncidentMissionSystem.MissionState.Idle)
        {
            hasTimerAnimated = false;
            isTimerAnimating = false;
            return idleTimerText;
        }

        float targetSeconds = missionSystem.TimeLimitSeconds > 0f
            ? missionSystem.RemainingTimeSeconds
            : missionSystem.ElapsedTime;

        // Nếu bật hiệu ứng chạy chữ từ 0 lúc bắt đầu nhiệm vụ (và chưa chạy)
        if (animateTimerOnStart && !hasTimerAnimated && missionSystem.State == IncidentMissionSystem.MissionState.Running)
        {
            if (!isTimerAnimating)
            {
                isTimerAnimating = true;
                animatedTimerValue = 0f;
                
                // Animate từ 0 đến giá trị thời gian bắt đầu
                DOTween.To(() => animatedTimerValue, x => animatedTimerValue = x, targetSeconds, timerAnimDuration)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() =>
                    {
                        isTimerAnimating = false;
                        hasTimerAnimated = true;
                    });
            }
            
            return FormatClock(animatedTimerValue);
        }
        
        // Nếu animation vẫn đang chạy, trả về giá trị đang được tween
        if (isTimerAnimating)
        {
            return FormatClock(animatedTimerValue);
        }

        // Khi kết thúc animation, hiển thị thời gian thực
        return FormatClock(targetSeconds);
    }

    private string BuildObjectiveCounterText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        int activeObjectiveNumber = ResolveActiveObjectiveNumber();
        return string.Format(objectiveCounterFormat, activeObjectiveNumber);
    }

    private int ResolveActiveObjectiveNumber()
    {
        if (missionSystem == null || missionSystem.ObjectiveStatusCount <= 0)
        {
            return 0;
        }

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (!status.IsComplete && !status.HasFailed)
            {
                return i + 1;
            }
        }

        return missionSystem.ObjectiveStatusCount;
    }

    private string BuildProgressText()
    {
        int totalObjectives = missionSystem != null ? Mathf.Max(0, missionSystem.ObjectiveStatusCount) : 0;
        int completedObjectives = ResolveCompletedObjectiveCount();
        return string.Format(
            LanguageManager.Tr(HubObjectivesCompletedFormatKey, progressTextFormat),
            completedObjectives,
            totalObjectives);
    }

    private string BuildProgressValueText()
    {
        float progressPercent = BuildProgressNormalized() * 100f;
        return MissionLocalization.Format("mission.hud.progress.percent", progressValueFormat, progressPercent);
    }

    private float BuildProgressNormalized()
    {
        if (missionSystem == null || missionSystem.ObjectiveStatusCount <= 0)
        {
            return 0f;
        }

        float totalNormalizedProgress = 0f;
        int relevantObjectives = 0;

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            relevantObjectives++;
            if (status.MaxScore > 0)
            {
                totalNormalizedProgress += Mathf.Clamp01(status.Score / (float)status.MaxScore);
            }
            else if (status.IsComplete && !status.HasFailed)
            {
                totalNormalizedProgress += 1f;
            }
        }

        if (relevantObjectives > 0)
        {
            return totalNormalizedProgress / relevantObjectives;
        }

        return 0f;
    }

    private int ResolveCompletedObjectiveCount()
    {
        if (missionSystem == null || missionSystem.ObjectiveStatusCount <= 0)
        {
            return 0;
        }

        int completedCount = 0;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (status.IsComplete && !status.HasFailed)
            {
                completedCount++;
            }
        }

        return completedCount;
    }

    private void RefreshObjectiveList()
    {
        if (missionSystem == null || objectivesListRoot == null)
        {
            return;
        }

        EnsureObjectiveViewsInitialized();

        int visibleCount = 0;
        bool assignedActiveObjective = false;
        bool layoutChanged = false;
        int signature = 17;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            IncidentMissionSystem.ObjectivePresentationKind presentationKind = ResolveObjectivePresentationKind(i);
            layoutChanged |= EnsureObjectiveViewCapacity(visibleCount + 1, presentationKind);
            if (visibleCount >= objectiveViews.Count || objectiveViews[visibleCount] == null)
            {
                break;
            }

            ObjectiveVisualState visualState = ResolveObjectiveVisualState(status, ref assignedActiveObjective);
            layoutChanged |= BindObjectiveView(objectiveViews[visibleCount], status, visualState);
            signature = (signature * 31) + (int)presentationKind;
            signature = (signature * 31) + (status.Summary?.GetHashCode() ?? 0);
            signature = (signature * 31) + status.Score;
            signature = (signature * 31) + status.MaxScore;
            signature = (signature * 31) + (status.IsComplete ? 1 : 0);
            signature = (signature * 31) + (status.HasFailed ? 1 : 0);
            visibleCount++;
        }

        for (int i = visibleCount; i < objectiveViews.Count; i++)
        {
            if (objectiveViews[i].SetActive(false))
            {
                layoutChanged = true;
            }
        }

        signature = (signature * 31) + visibleCount;
        if (signature != lastObjectiveLayoutSignature)
        {
            lastObjectiveLayoutSignature = signature;
            layoutChanged = true;
        }

        if (layoutChanged)
        {
            RequestLayoutRebuild();
        }
    }

    private void EnsureObjectiveViewsInitialized()
    {
        if (objectiveViewsInitialized)
        {
            return;
        }

        objectiveViews.Clear();
        if (HasObjectivePrefabConfiguration())
        {
            for (int i = 0; i < objectivesListRoot.childCount; i++)
            {
                Transform child = objectivesListRoot.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            for (int i = 0; i < objectivesListRoot.childCount; i++)
            {
                RegisterObjectiveView(
                    objectivesListRoot.GetChild(i).gameObject,
                    IncidentMissionSystem.ObjectivePresentationKind.Generic);
            }
        }

        objectiveViewsInitialized = true;
    }

    private bool EnsureObjectiveViewCapacity(int count, IncidentMissionSystem.ObjectivePresentationKind kind)
    {
        bool changed = false;
        EnsureObjectiveViewsInitialized();
        if (objectivesListRoot == null)
        {
            return false;
        }

        while (objectiveViews.Count < count)
        {
            GameObject prefab = ResolveObjectiveItemPrefab(kind);
            if (prefab == null)
            {
                if (objectivesListRoot.childCount > 0)
                {
                    GameObject template = objectivesListRoot.GetChild(0).gameObject;
                    GameObject clone = Instantiate(template, objectivesListRoot, false);
                    clone.name = $"{template.name}_Clone{objectiveViews.Count}";
                    RegisterObjectiveView(clone, IncidentMissionSystem.ObjectivePresentationKind.Generic);
                    changed = true;
                }
                else
                {
                    break;
                }
            }
            else
            {
                GameObject instance = Instantiate(prefab, objectivesListRoot, false);
                RegisterObjectiveView(instance, kind);
                changed = true;
            }
        }

        int targetIndex = count - 1;
        if (targetIndex < 0 || targetIndex >= objectiveViews.Count)
        {
            return changed;
        }

        ObjectiveItemView view = objectiveViews[targetIndex];
        if (view == null || view.Root == null)
        {
            return changed;
        }

        if (view.Kind != kind)
        {
            GameObject prefab = ResolveObjectiveItemPrefab(kind);
            if (prefab != null)
            {
                Destroy(view.Root);
                GameObject replacement = Instantiate(prefab, objectivesListRoot, false);
                objectiveViews[targetIndex] = CreateObjectiveView(replacement, kind);
                view = objectiveViews[targetIndex];
                changed = true;
            }
        }

        if (view?.Root != null)
        {
            view.Root.transform.SetSiblingIndex(targetIndex);
        }

        return changed;
    }

    private void RegisterObjectiveView(GameObject itemRoot, IncidentMissionSystem.ObjectivePresentationKind kind)
    {
        ObjectiveItemView view = CreateObjectiveView(itemRoot, kind);
        if (view != null)
        {
            objectiveViews.Add(view);
        }
    }

    private ObjectiveItemView CreateObjectiveView(GameObject itemRoot, IncidentMissionSystem.ObjectivePresentationKind kind)
    {
        if (itemRoot == null)
        {
            return null;
        }

        ObjectiveItemView view = new ObjectiveItemView
        {
            Root = itemRoot,
            Kind = kind,
            Icon = FindImage(itemRoot.transform, "Icon"),
            Label = FindText(itemRoot.transform, "ObjectiveLabel"),
            ProgressFill = FindRectTransform(itemRoot.transform, "ProgressBarFill"),
            ProgressValueText = FindText(itemRoot.transform, "ProgressValueText"),
            RescueCounterText = FindText(itemRoot.transform, "CounterText"),
            RescueCounterBackground = FindImage(itemRoot.transform, "CounterBG")
        };

        if (view.Icon == null)
        {
            view.Icon = FindFirstImageInChildren(itemRoot.transform);
        }

        if (view.Label == null)
        {
            view.Label = itemRoot.GetComponentInChildren<TMP_Text>(true);
        }

        Transform iconsContainer = FindDescendantByName(itemRoot.transform, "IconsContainer");
        if (iconsContainer != null)
        {
            Image[] icons = iconsContainer.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null &&
                    icons[i].transform != iconsContainer &&
                    icons[i].name.StartsWith("VictimIcon", StringComparison.Ordinal))
                {
                    view.RescueVictimIcons.Add(icons[i]);
                }
            }

            view.RescueVictimIcons.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        }

        if (view.IsValid)
        {
            return view;
        }

        return null;
    }

    private bool BindObjectiveView(ObjectiveItemView view, MissionObjectiveStatusSnapshot status, ObjectiveVisualState visualState)
    {
        if (view == null || !view.IsValid)
        {
            return false;
        }

        bool changed = view.SetActive(true);
        string objectiveText = BuildObjectiveText(status);
        changed |= SetText(view.Label, objectiveText);

        Color color = ResolveObjectiveColor(visualState);
        if (view.Label.color != color)
        {
            view.Label.color = color;
            changed = true;
        }

        if (view.Icon != null)
        {
            if (view.Icon.color != color)
            {
                view.Icon.color = color;
                changed = true;
            }
        }

        float normalized = ResolveObjectiveNormalizedProgress(view, status);
        if (view.ProgressFill != null)
        {
            SetHorizontalFill(view.ProgressFill, normalized);
        }

        if (view.ProgressValueText != null)
        {
            string progressValue = MissionLocalization.Format("mission.hud.progress.percent", progressValueFormat, normalized * 100f);
            changed |= SetText(view.ProgressValueText, progressValue);
        }

        if (view.IsRescueItem)
        {
            BindRescueObjectiveView(view);
        }

        return changed;
    }

    private float ResolveObjectiveNormalizedProgress(ObjectiveItemView view, MissionObjectiveStatusSnapshot status)
    {
        if (missionSystem != null && view != null && view.IsFireItem)
        {
            int totalFires = Mathf.Max(0, missionSystem.DisplayedTotalTrackedFires);
            int extinguishedFires = Mathf.Clamp(missionSystem.DisplayedExtinguishedFireCount, 0, totalFires);
            if (totalFires > 0)
            {
                return Mathf.Clamp01(extinguishedFires / (float)totalFires);
            }
        }

        if (status.MaxScore > 0)
        {
            return Mathf.Clamp01(status.Score / (float)status.MaxScore);
        }

        return status.IsComplete && !status.HasFailed ? 1f : 0f;
    }

    private void BindRescueObjectiveView(ObjectiveItemView view)
    {
        if (missionSystem == null || view == null)
        {
            return;
        }

        int totalRescuables = Mathf.Max(0, missionSystem.DisplayedTotalTrackedRescuables);
        int rescuedVictims = Mathf.Clamp(missionSystem.DisplayedRescuedCount, 0, totalRescuables);
        int remainingVictims = Mathf.Max(0, totalRescuables - rescuedVictims);

        if (view.RescueCounterText != null)
        {
            view.RescueCounterText.text = remainingVictims.ToString();
        }

        if (view.RescueCounterBackground != null)
        {
            view.RescueCounterBackground.color = remainingVictims > 0
                ? rescueCounterPendingColor
                : rescueCounterCompletedColor;
        }

        for (int i = 0; i < view.RescueVictimIcons.Count; i++)
        {
            Image icon = view.RescueVictimIcons[i];
            if (icon == null)
            {
                continue;
            }

            bool visible = i < totalRescuables;
            icon.gameObject.SetActive(visible);
            if (!visible)
            {
                icon.color = rescueVictimInactiveColor;
                continue;
            }

            icon.color = i < rescuedVictims ? rescueVictimRescuedColor : rescueVictimPendingColor;
        }
    }

    private string BuildObjectiveText(MissionObjectiveStatusSnapshot status)
    {
        return !string.IsNullOrWhiteSpace(status.Summary) ? status.Summary : status.Title;
    }

    private static ObjectiveVisualState ResolveObjectiveVisualState(MissionObjectiveStatusSnapshot status, ref bool assignedActiveObjective)
    {
        if (status.HasFailed)
        {
            return ObjectiveVisualState.Failed;
        }

        if (status.IsComplete)
        {
            return ObjectiveVisualState.Completed;
        }

        if (!assignedActiveObjective)
        {
            assignedActiveObjective = true;
            return ObjectiveVisualState.Active;
        }

        return ObjectiveVisualState.Pending;
    }

    private Color ResolveAccentColor(IncidentMissionSystem.MissionState state)
    {
        return state switch
        {
            IncidentMissionSystem.MissionState.Running => runningAccentColor,
            IncidentMissionSystem.MissionState.Completed => completedAccentColor,
            IncidentMissionSystem.MissionState.Failed => failedAccentColor,
            _ => idleAccentColor
        };
    }

    private Color ResolveObjectiveColor(ObjectiveVisualState visualState)
    {
        return visualState switch
        {
            ObjectiveVisualState.Active => activeObjectiveColor,
            ObjectiveVisualState.Completed => completedObjectiveColor,
            ObjectiveVisualState.Failed => failedObjectiveColor,
            _ => pendingObjectiveColor
        };
    }

    private TMP_Text FindText(string objectName)
    {
        return FindRectTransform(objectName)?.GetComponent<TMP_Text>();
    }

    private TMP_Text FindTextOutsideObjectives(string objectName)
    {
        RectTransform target = FindRectTransformOutside(objectName, objectivesListRoot);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string objectName)
    {
        return FindRectTransform(objectName)?.GetComponent<Image>();
    }

    private RectTransform FindRectTransform(string objectName)
    {
        return FindRectTransform(transform, objectName);
    }

    private static TMP_Text FindText(Transform root, string objectName)
    {
        return FindRectTransform(root, objectName)?.GetComponent<TMP_Text>();
    }

    private static Image FindImage(Transform root, string objectName)
    {
        return FindRectTransform(root, objectName)?.GetComponent<Image>();
    }

    private static RectTransform FindRectTransform(Transform root, string objectName)
    {
        return FindDescendantByName(root, objectName) as RectTransform;
    }

    private RectTransform FindRectTransformOutside(string objectName, Transform excludedRoot)
    {
        return FindDescendantByNameOutside(transform, objectName, excludedRoot) as RectTransform;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDescendantByNameOutside(Transform root, string objectName, Transform excludedRoot)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root == excludedRoot)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByNameOutside(root.GetChild(i), objectName, excludedRoot);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Image FindFirstImageInChildren(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].transform != root)
            {
                return images[i];
            }
        }

        return null;
    }

    private static void SetHorizontalFill(RectTransform fill, float normalized)
    {
        if (fill == null)
        {
            return;
        }

        normalized = Mathf.Clamp01(normalized);
        fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
        fill.anchorMax = new Vector2(normalized, fill.anchorMax.y);
        fill.sizeDelta = new Vector2(0f, fill.sizeDelta.y);
    }

    private static bool SetText(TMP_Text textComponent, string value)
    {
        if (textComponent == null)
        {
            return false;
        }

        string resolved = value ?? string.Empty;
        if (string.Equals(textComponent.text, resolved, StringComparison.Ordinal))
        {
            return false;
        }

        textComponent.text = resolved;
        return true;
    }

    private static string FormatClock(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{secs:00}"
            : $"{minutes:00}:{secs:00}";
    }

    private IncidentMissionSystem.ObjectivePresentationKind ResolveObjectivePresentationKind(int objectiveIndex)
    {
        if (missionSystem != null &&
            missionSystem.TryGetObjectivePresentationKind(objectiveIndex, out IncidentMissionSystem.ObjectivePresentationKind resolvedKind))
        {
            return resolvedKind;
        }

        return IncidentMissionSystem.ObjectivePresentationKind.Generic;
    }

    private bool HasObjectivePrefabConfiguration()
    {
        return fireObjectiveItemPrefab != null || rescueObjectiveItemPrefab != null || genericObjectiveItemPrefab != null;
    }

    private GameObject ResolveObjectiveItemPrefab(IncidentMissionSystem.ObjectivePresentationKind kind)
    {
        switch (kind)
        {
            case IncidentMissionSystem.ObjectivePresentationKind.Fire:
                return fireObjectiveItemPrefab != null
                    ? fireObjectiveItemPrefab
                    : (genericObjectiveItemPrefab != null ? genericObjectiveItemPrefab : rescueObjectiveItemPrefab);
            case IncidentMissionSystem.ObjectivePresentationKind.Rescue:
                return rescueObjectiveItemPrefab != null
                    ? rescueObjectiveItemPrefab
                    : (genericObjectiveItemPrefab != null ? genericObjectiveItemPrefab : fireObjectiveItemPrefab);
            default:
                if (genericObjectiveItemPrefab != null)
                {
                    return genericObjectiveItemPrefab;
                }

                return rescueObjectiveItemPrefab != null ? rescueObjectiveItemPrefab : fireObjectiveItemPrefab;
        }
    }

    private void RequestLayoutRebuild()
    {
        if (!rebuildLayoutOnChanges)
        {
            return;
        }

        layoutRebuildPending = true;
    }

    private void ProcessPendingLayoutRebuild(bool forceImmediate)
    {
        if (!layoutRebuildPending)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!forceImmediate && now < nextLayoutRebuildTime)
        {
            return;
        }

        RectTransform target = hubDetailRoot != null ? hubDetailRoot : FindRectTransform("HubV2_Detail");
        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target == null)
        {
            layoutRebuildPending = false;
            return;
        }

        Canvas.ForceUpdateCanvases();
        RebuildLayoutRecursive(target);
        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        bool syncedWidth = SyncDetailWidthWithTitle();
        if (syncedWidth)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        }
        Canvas.ForceUpdateCanvases();

        layoutRebuildPending = false;
        nextLayoutRebuildTime = now + Mathf.Max(0f, layoutRebuildThrottleSeconds);
    }

    private static void RebuildLayoutRecursive(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        for (int i = 0; i < rectTransform.childCount; i++)
        {
            if (rectTransform.GetChild(i) is RectTransform childRect)
            {
                RebuildLayoutRecursive(childRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
        }
    }

    private bool SyncDetailWidthWithTitle()
    {
        if (hubDetailRoot == null || hubTitleRoot == null)
        {
            return false;
        }

        float targetWidth = hubTitleRoot.rect.width;
        if (targetWidth <= 0f)
        {
            return false;
        }

        float currentWidth = hubDetailRoot.rect.width;
        if (Mathf.Abs(currentWidth - targetWidth) < 0.1f)
        {
            return false;
        }

        hubDetailRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        return true;
    }
}
