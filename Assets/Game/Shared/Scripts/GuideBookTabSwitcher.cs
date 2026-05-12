using System;
using System.Collections.Generic;
using UnityEngine.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GuideBookTabSwitcher : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform tocRoot;
    [SerializeField] private Transform contentRoot;

    [Header("Optional Explicit References")]
    [SerializeField] private Button[] tocButtons = new Button[6];
    [SerializeField] private GameObject[] contentTabs = new GameObject[6];

    [Header("Tab 5 Subtabs")]
    [SerializeField] private Transform tab5SubTabRoot;
    [SerializeField] private Button[] tab5SubTabButtons = Array.Empty<Button>();
    [SerializeField] private GameObject[] tab5SubTabContents = Array.Empty<GameObject>();
    [SerializeField] private int defaultTab5SubTabIndex = 0;

    [Header("Visuals")]
    [SerializeField] private Color activeTextColor = new Color(0.4f, 0.16f, 0.08f, 1f);
    [SerializeField] private Color inactiveTextColor = new Color(0.18f, 0.14f, 0.11f, 0.82f);
    [SerializeField] private FontStyles activeTextStyle = FontStyles.Bold;
    [SerializeField] private FontStyles inactiveTextStyle = FontStyles.Normal;
    [SerializeField] private int defaultTabIndex = 0;

    private int currentTabIndex = -1;
    private int currentTab5SubTabIndex = -1;
    private readonly UnityAction[] tocButtonActions = new UnityAction[6];
    private UnityAction[] tab5SubTabOpenActions = Array.Empty<UnityAction>();
    private UnityAction[] tab5SubTabSelectActions = Array.Empty<UnityAction>();

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        ShowTab(Mathf.Clamp(defaultTabIndex, 0, 5));
    }

    [ContextMenu("Rebind Tabs")]
    public void EnsureInitialized()
    {
        ResolveRoots();
        ResolveButtons();
        ResolveTabs();
        ResolveTab5SubTabs();
        EnsureButtonVisuals();
        BindButtons();
        BindTab5SubTabButtons();
    }

    public void ShowTab(int tabIndex)
    {
        if (contentTabs == null || contentTabs.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(tabIndex, 0, contentTabs.Length - 1);
        currentTabIndex = clampedIndex;

        for (int i = 0; i < contentTabs.Length; i++)
        {
            GameObject tab = contentTabs[i];
            if (tab == null)
            {
                continue;
            }

            bool isActive = i == clampedIndex && i != 4;
            tab.SetActive(isActive);
        }

        if (clampedIndex == 4)
        {
            SetTab5SubTabsVisible(true);
            ShowTab5SubTab(Mathf.Clamp(defaultTab5SubTabIndex, 0, tab5SubTabContents.Length - 1));
        }
        else
        {
            SetTab5SubTabsVisible(false);
            HideTab5SubTabContents();
        }

        RefreshButtonVisuals();
    }

    public void ShowTab5SubTab(int subTabIndex)
    {
        if (tab5SubTabContents == null || tab5SubTabContents.Length == 0)
        {
            return;
        }

        currentTab5SubTabIndex = Mathf.Clamp(subTabIndex, 0, tab5SubTabContents.Length - 1);

        for (int i = 0; i < tab5SubTabContents.Length; i++)
        {
            GameObject content = tab5SubTabContents[i];
            if (content == null)
            {
                continue;
            }

            content.SetActive(i == currentTab5SubTabIndex);
        }

        RefreshTab5SubTabButtonVisuals();
    }

    private void ResolveRoots()
    {
        if (tocRoot == null)
        {
            tocRoot = FindChildByName(transform, "TOC") ??
                      FindChildByName(transform, "TableOfContents") ??
                      FindChildByName(transform, "Guide") ??
                      transform;
        }

        if (contentRoot == null)
        {
            contentRoot = FindChildByName(transform, "Content") ??
                          FindChildByName(transform, "Contents") ??
                          transform;
        }
    }

    private void ResolveButtons()
    {
        EnsureArraySize(ref tocButtons, 6);

        for (int i = 0; i < tocButtons.Length; i++)
        {
            if (tocButtons[i] != null)
            {
                continue;
            }

            string objectName = (i + 1).ToString();
            Transform candidate = tocRoot != null ? FindChildByName(tocRoot, objectName) : null;
            if (candidate == null)
            {
                continue;
            }

            tocButtons[i] = candidate.GetComponent<Button>();
            if (tocButtons[i] == null)
            {
                tocButtons[i] = candidate.GetComponentInChildren<Button>(true);
            }
        }
    }

    private void ResolveTabs()
    {
        EnsureArraySize(ref contentTabs, 6);

        for (int i = 0; i < contentTabs.Length; i++)
        {
            if (contentTabs[i] != null)
            {
                continue;
            }

            string numberName = (i + 1).ToString();
            string tabName = $"Tab{i + 1}";
            string contentName = $"Tab{i + 1}Content";

            Transform candidate = contentRoot != null ? FindChildByName(contentRoot, contentName) : null;
            candidate ??= contentRoot != null ? FindChildByName(contentRoot, tabName) : null;
            candidate ??= contentRoot != null ? FindChildByName(contentRoot, numberName) : null;

            if (candidate != null)
            {
                contentTabs[i] = candidate.gameObject;
            }
        }
    }

    private void ResolveTab5SubTabs()
    {
        if (tab5SubTabRoot == null && tocButtons.Length > 4 && tocButtons[4] != null)
        {
            tab5SubTabRoot = tocButtons[4].transform;
        }

        if (tab5SubTabRoot != null)
        {
            List<Button> discoveredButtons = new List<Button>();
            CollectTab5SubTabButtons(tab5SubTabRoot, discoveredButtons);
            if (discoveredButtons.Count > 0)
            {
                discoveredButtons.Sort((a, b) => CompareIndexedNames(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
                if (tab5SubTabButtons == null || tab5SubTabButtons.Length < discoveredButtons.Count)
                {
                    tab5SubTabButtons = discoveredButtons.ToArray();
                }
            }
        }

        if (contentRoot != null)
        {
            List<GameObject> discoveredContents = new List<GameObject>();
            CollectTab5SubTabContents(contentRoot, discoveredContents);
            if (discoveredContents.Count > 0)
            {
                discoveredContents.Sort((a, b) => CompareIndexedNames(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
                if (tab5SubTabContents == null || tab5SubTabContents.Length < discoveredContents.Count)
                {
                    tab5SubTabContents = discoveredContents.ToArray();
                }
            }
        }

        EnsureArraySize(ref tab5SubTabOpenActions, tab5SubTabButtons != null ? tab5SubTabButtons.Length : 0);
        EnsureArraySize(ref tab5SubTabSelectActions, tab5SubTabButtons != null ? tab5SubTabButtons.Length : 0);
    }

    private void BindButtons()
    {
        for (int i = 0; i < tocButtons.Length; i++)
        {
            Button button = tocButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            if (tocButtonActions[i] == null)
            {
                tocButtonActions[i] = () => ShowTab(capturedIndex);
            }

            button.onClick.RemoveListener(tocButtonActions[i]);
            button.onClick.AddListener(tocButtonActions[i]);
        }
    }

    private void BindTab5SubTabButtons()
    {
        if (tab5SubTabButtons == null)
        {
            return;
        }

        for (int i = 0; i < tab5SubTabButtons.Length; i++)
        {
            Button button = tab5SubTabButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            if (tab5SubTabOpenActions[i] == null)
            {
                tab5SubTabOpenActions[i] = () => ShowTab(4);
            }

            if (tab5SubTabSelectActions[i] == null)
            {
                tab5SubTabSelectActions[i] = () => ShowTab5SubTab(capturedIndex);
            }

            button.onClick.RemoveListener(tab5SubTabOpenActions[i]);
            button.onClick.RemoveListener(tab5SubTabSelectActions[i]);
            button.onClick.AddListener(tab5SubTabOpenActions[i]);
            button.onClick.AddListener(tab5SubTabSelectActions[i]);
        }
    }

    private void EnsureButtonVisuals()
    {
        EnsureButtonVisuals(tocButtons);
        EnsureButtonVisuals(tab5SubTabButtons);
    }

    private static void EnsureButtonVisuals(Button[] buttons)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            GuideBookListButtonVisual visual = button.GetComponent<GuideBookListButtonVisual>();
            if (visual == null)
            {
                visual = button.gameObject.AddComponent<GuideBookListButtonVisual>();
            }

            visual.RefreshBindings();
        }
    }

    private void RefreshButtonVisuals()
    {
        for (int i = 0; i < tocButtons.Length; i++)
        {
            Button button = tocButtons[i];
            if (button == null)
            {
                continue;
            }

            bool isActive = i == currentTabIndex;
            GuideBookListButtonVisual visual = button.GetComponent<GuideBookListButtonVisual>();
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                TMP_Text text = texts[j];
                text.color = isActive ? activeTextColor : inactiveTextColor;
                text.fontStyle = isActive ? activeTextStyle : inactiveTextStyle;
            }

            if (visual != null)
            {
                visual.RefreshLabelBindings();
                visual.SetSelected(isActive);
            }
        }
    }

    private void RefreshTab5SubTabButtonVisuals()
    {
        if (tab5SubTabButtons == null)
        {
            return;
        }

        for (int i = 0; i < tab5SubTabButtons.Length; i++)
        {
            Button button = tab5SubTabButtons[i];
            if (button == null)
            {
                continue;
            }

            bool isActive = i == currentTab5SubTabIndex;
            GuideBookListButtonVisual visual = button.GetComponent<GuideBookListButtonVisual>();
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                TMP_Text text = texts[j];
                text.color = isActive ? activeTextColor : inactiveTextColor;
                text.fontStyle = isActive ? activeTextStyle : inactiveTextStyle;
            }

            if (visual != null)
            {
                visual.RefreshLabelBindings();
                visual.SetSelected(isActive);
            }
        }
    }

    private void SetTab5SubTabsVisible(bool isVisible)
    {
        if (tab5SubTabButtons == null)
        {
            return;
        }

        for (int i = 0; i < tab5SubTabButtons.Length; i++)
        {
            if (tab5SubTabButtons[i] != null)
            {
                tab5SubTabButtons[i].gameObject.SetActive(isVisible);
            }
        }
    }

    private void HideTab5SubTabContents()
    {
        if (tab5SubTabContents == null)
        {
            return;
        }

        for (int i = 0; i < tab5SubTabContents.Length; i++)
        {
            if (tab5SubTabContents[i] != null)
            {
                tab5SubTabContents[i].SetActive(false);
            }
        }
    }

    private static void CollectTab5SubTabButtons(Transform root, List<Button> results)
    {
        if (root == null || results == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.StartsWith("SubTab5_1", StringComparison.OrdinalIgnoreCase))
            {
                Button button = child.GetComponent<Button>() ?? child.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    results.Add(button);
                }
            }
        }
    }

    private static void CollectTab5SubTabContents(Transform root, List<GameObject> results)
    {
        if (root == null || results == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.StartsWith("subTab5Content", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(child.gameObject);
            }

            CollectTab5SubTabContents(child, results);
        }
    }

    private static int CompareIndexedNames(string left, string right)
    {
        int leftIndex = ExtractTrailingIndex(left);
        int rightIndex = ExtractTrailingIndex(right);
        if (leftIndex != rightIndex)
        {
            return leftIndex.CompareTo(rightIndex);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractTrailingIndex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue;
        }

        int openParen = value.LastIndexOf('(');
        int closeParen = value.LastIndexOf(')');
        if (openParen >= 0 && closeParen > openParen)
        {
            string numberText = value.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            if (int.TryParse(numberText, out int parsed))
            {
                return parsed + 1;
            }
        }

        return 0;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void EnsureArraySize<T>(ref T[] array, int size)
    {
        if (array != null && array.Length == size)
        {
            return;
        }

        T[] resized = new T[size];
        if (array != null)
        {
            Array.Copy(array, resized, Mathf.Min(array.Length, size));
        }

        array = resized;
    }
}
