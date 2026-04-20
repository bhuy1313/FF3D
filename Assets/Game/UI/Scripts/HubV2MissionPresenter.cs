using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HubV2MissionPresenter : MonoBehaviour
{
    private const string FireItemName = "ObjectiveItem_Fire";
    private const string RescueItemName = "ObjectiveItem_Rescue";

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
        public Image Icon;
        public TMP_Text Label;
        public RectTransform ProgressFill;
        public TMP_Text ProgressValueText;
        public TMP_Text RescueCounterText;
        public Image RescueCounterBackground;
        public readonly List<Image> RescueVictimIcons = new List<Image>();

        public bool IsValid => Root != null && Label != null;
        public bool IsFireItem => Root != null && Root.name.StartsWith(FireItemName, StringComparison.Ordinal);
        public bool IsRescueItem => Root != null && Root.name.StartsWith(RescueItemName, StringComparison.Ordinal);

        public void SetActive(bool active)
        {
            if (Root != null && Root.activeSelf != active)
            {
                Root.SetActive(active);
            }
        }
    }

    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text objectiveCounterText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private RectTransform objectivesListRoot;
    [SerializeField] private Image accentBarImage;

    [Header("Display")]
    [SerializeField] private bool hideWhenMissionMissing = true;
    [SerializeField] private bool hideWhenMissionIdle;
    [SerializeField] private bool showObjectiveScores = true;
    [SerializeField] private string fallbackMissionTitle = "OPERATION";
    [SerializeField] private string idleTimerText = "--:--";
    [SerializeField] private string objectiveCounterFormat = "{0}";
    [SerializeField] private string progressTextFormat = "{0}/{1} OBJECTIVES COMPLETED";
    [SerializeField] private string progressValueFormat = "{0:0}%";

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

    private void Awake()
    {
        ResolveReferences();
        RefreshView();
    }

    private void OnEnable()
    {
        ResolveReferences();
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

        SetText(missionTitleText, BuildMissionTitleText());
        SetText(timerText, BuildTimerText());
        if (objectiveCounterText != null)
        {
            SetText(objectiveCounterText, BuildObjectiveCounterText());
        }
        SetText(progressText, BuildProgressText());

        if (accentBarImage != null)
        {
            accentBarImage.color = ResolveAccentColor(missionSystem.State);
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
        if (missionSystem == null)
        {
            return fallbackMissionTitle;
        }

        return string.IsNullOrWhiteSpace(missionSystem.MissionOperationTitle)
            ? fallbackMissionTitle
            : missionSystem.MissionOperationTitle.Trim();
    }

    private string BuildTimerText()
    {
        if (missionSystem == null)
        {
            return idleTimerText;
        }

        float seconds = missionSystem.TimeLimitSeconds > 0f
            ? missionSystem.RemainingTimeSeconds
            : missionSystem.ElapsedTime;
        return FormatClock(seconds);
    }

    private string BuildObjectiveCounterText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        return string.Format(objectiveCounterFormat, ResolveActiveObjectiveNumber());
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
        return string.Format(progressTextFormat, completedObjectives, totalObjectives);
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

        int fireObjectiveIndex = FindObjectiveIndex(IsFireObjectiveStatus);
        int rescueObjectiveIndex = FindObjectiveIndex(IsRescueObjectiveStatus);
        bool[] consumedStatuses = new bool[Mathf.Max(0, missionSystem.ObjectiveStatusCount)];
        bool assignedActiveObjective = false;

        ObjectiveItemView fireView = FindViewByPredicate(static view => view.IsFireItem);
        if (fireView != null)
        {
            if (fireObjectiveIndex >= 0 && missionSystem.TryGetObjectiveStatus(fireObjectiveIndex, out MissionObjectiveStatusSnapshot fireStatus))
            {
                ObjectiveVisualState fireState = ResolveObjectiveVisualState(fireStatus, ref assignedActiveObjective);
                BindObjectiveView(fireView, fireStatus, fireState);
                consumedStatuses[fireObjectiveIndex] = true;
            }
            else
            {
                fireView.SetActive(false);
            }
        }

        ObjectiveItemView rescueView = FindViewByPredicate(static view => view.IsRescueItem);
        if (rescueView != null)
        {
            if (rescueObjectiveIndex >= 0 && missionSystem.TryGetObjectiveStatus(rescueObjectiveIndex, out MissionObjectiveStatusSnapshot rescueStatus))
            {
                ObjectiveVisualState rescueState = ResolveObjectiveVisualState(rescueStatus, ref assignedActiveObjective);
                BindObjectiveView(rescueView, rescueStatus, rescueState);
                consumedStatuses[rescueObjectiveIndex] = true;
            }
            else
            {
                rescueView.SetActive(false);
            }
        }

        int visibleCount = 0;
        for (int i = 0; i < objectiveViews.Count; i++)
        {
            if (objectiveViews[i].IsFireItem || objectiveViews[i].IsRescueItem)
            {
                visibleCount++;
            }
        }

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (i < consumedStatuses.Length && consumedStatuses[i])
            {
                continue;
            }

            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            EnsureObjectiveViewCapacity(visibleCount + 1);
            while (visibleCount < objectiveViews.Count &&
                   (objectiveViews[visibleCount].IsFireItem || objectiveViews[visibleCount].IsRescueItem))
            {
                visibleCount++;
                EnsureObjectiveViewCapacity(visibleCount + 1);
            }

            if (visibleCount >= objectiveViews.Count)
            {
                break;
            }

            ObjectiveVisualState visualState = ResolveObjectiveVisualState(status, ref assignedActiveObjective);
            BindObjectiveView(objectiveViews[visibleCount], status, visualState);
            visibleCount++;
        }

        for (int i = visibleCount; i < objectiveViews.Count; i++)
        {
            if (!objectiveViews[i].IsFireItem && !objectiveViews[i].IsRescueItem)
            {
                objectiveViews[i].SetActive(false);
            }
        }
    }

    private void EnsureObjectiveViewsInitialized()
    {
        if (objectiveViewsInitialized)
        {
            return;
        }

        objectiveViews.Clear();
        for (int i = 0; i < objectivesListRoot.childCount; i++)
        {
            RegisterObjectiveView(objectivesListRoot.GetChild(i).gameObject);
        }

        objectiveViewsInitialized = true;
    }

    private void EnsureObjectiveViewCapacity(int count)
    {
        EnsureObjectiveViewsInitialized();
        if (objectivesListRoot == null || objectivesListRoot.childCount <= 0)
        {
            return;
        }

        GameObject template = objectiveViews.Count > 0
            ? objectiveViews[objectiveViews.Count - 1].Root
            : objectivesListRoot.GetChild(0).gameObject;

        while (objectiveViews.Count < count && template != null)
        {
            GameObject clone = Instantiate(template, objectivesListRoot, false);
            clone.name = $"{template.name}_Clone{objectiveViews.Count}";
            RegisterObjectiveView(clone);
        }
    }

    private void RegisterObjectiveView(GameObject itemRoot)
    {
        if (itemRoot == null)
        {
            return;
        }

        ObjectiveItemView view = new ObjectiveItemView
        {
            Root = itemRoot,
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
            objectiveViews.Add(view);
        }
    }

    private void BindObjectiveView(ObjectiveItemView view, MissionObjectiveStatusSnapshot status, ObjectiveVisualState visualState)
    {
        if (view == null || !view.IsValid)
        {
            return;
        }

        view.SetActive(true);
        SetText(view.Label, BuildObjectiveText(status));

        Color color = ResolveObjectiveColor(visualState);
        view.Label.color = color;
        if (view.Icon != null)
        {
            view.Icon.color = color;
        }

        float normalized = ResolveObjectiveNormalizedProgress(view, status);
        if (view.ProgressFill != null)
        {
            SetHorizontalFill(view.ProgressFill, normalized);
        }

        if (view.ProgressValueText != null)
        {
            view.ProgressValueText.text = string.Format(progressValueFormat, normalized * 100f);
        }

        if (view.IsRescueItem)
        {
            BindRescueObjectiveView(view);
        }
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
        string text = !string.IsNullOrWhiteSpace(status.Summary) ? status.Summary : status.Title;
        if (!showObjectiveScores || status.MaxScore <= 0)
        {
            return text;
        }

        return $"{text} ({status.Score}/{status.MaxScore})";
    }

    private int FindObjectiveIndex(Func<MissionObjectiveStatusSnapshot, bool> predicate)
    {
        if (missionSystem == null || predicate == null)
        {
            return -1;
        }

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status) && predicate(status))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsFireObjectiveStatus(MissionObjectiveStatusSnapshot status)
    {
        return ContainsAnyKeyword(status.Title, "fire", "extinguish");
    }

    private static bool IsRescueObjectiveStatus(MissionObjectiveStatusSnapshot status)
    {
        return ContainsAnyKeyword(status.Title, "rescue", "victim");
    }

    private static bool ContainsAnyKeyword(string source, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source) || keywords == null)
        {
            return false;
        }

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) &&
                source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private ObjectiveItemView FindViewByPredicate(Func<ObjectiveItemView, bool> predicate)
    {
        if (predicate == null)
        {
            return null;
        }

        for (int i = 0; i < objectiveViews.Count; i++)
        {
            ObjectiveItemView view = objectiveViews[i];
            if (view != null && predicate(view))
            {
                return view;
            }
        }

        return null;
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

    private static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
        {
            textComponent.text = value ?? string.Empty;
        }
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
}
