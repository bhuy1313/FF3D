using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IncidentProcedureChecklistPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private IncidentProcedureChecklistPanelView panelView;
    [SerializeField] [Min(1)] private int layoutRebuildPasses = 3;

    [Header("Debug")]
    [SerializeField] private bool createRuntimeRebuildButton = true;
    [SerializeField] private Vector2 rebuildButtonAnchorPosition = new Vector2(-170f, -28f);
    [SerializeField] private Vector2 rebuildButtonSize = new Vector2(150f, 42f);

    private IncidentProcedureDefinition lastBoundDefinition;
    private Coroutine layoutRebuildRoutine;
    private Button runtimeRebuildButton;
    private TextMeshProUGUI runtimeRebuildButtonLabel;
    private int manualRebuildCount;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureRuntimeRebuildButton();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureRuntimeRebuildButton();
        RefreshChecklist();
    }

    private void Update()
    {
        if (missionSystem == null || panelView == null)
        {
            ResolveReferences();
            return;
        }

        IncidentProcedureDefinition definition = missionSystem.ActiveProcedureDefinition;
        if (definition != lastBoundDefinition)
        {
            RefreshChecklist();
        }
    }

    public void RefreshChecklist()
    {
        ResolveReferences();
        if (missionSystem == null || panelView == null)
        {
            return;
        }

        IncidentProcedureDefinition definition = missionSystem.ActiveProcedureDefinition;
        lastBoundDefinition = definition;

        if (panelView.TitleText != null)
        {
            panelView.TitleText.text = definition != null ? definition.Title : string.Empty;
        }

        Transform root = panelView.ActionItemsBodyRoot != null ? panelView.ActionItemsBodyRoot : panelView.ActionItemsRoot;
        if (root == null)
        {
            return;
        }

        if (definition == null || definition.ChecklistItems == null)
        {
            SetAllChildrenActive(root, false);
            RebuildChecklistLayout();
            return;
        }

        int itemCount = definition.ChecklistItems.Count;
        int childCount = root.childCount;
        int usableCount = Mathf.Min(itemCount, childCount);

        for (int i = 0; i < usableCount; i++)
        {
            IncidentProcedureChecklistItem item = definition.ChecklistItems[i];
            if (item == null)
            {
                continue;
            }

            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            IncidentProcedureChecklistItemView instance = child.GetComponent<IncidentProcedureChecklistItemView>();
            if (instance == null)
            {
                if (panelView.ItemViewPrefab == null)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                instance = Instantiate(panelView.ItemViewPrefab, root);
                instance.transform.SetSiblingIndex(i);
                child = instance.transform;
            }

            child.gameObject.SetActive(true);
            child.name = $"ChecklistItem_{i + 1}";
            instance.Bind(item);
        }

        for (int i = usableCount; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        RebuildChecklistLayout();
    }

    public void RebuildChecklistLayoutNow()
    {
        manualRebuildCount++;
        Debug.Log($"{nameof(IncidentProcedureChecklistPresenter)} manual rebuild requested. Count={manualRebuildCount}", this);

        if (layoutRebuildRoutine != null)
        {
            StopCoroutine(layoutRebuildRoutine);
            layoutRebuildRoutine = null;
        }

        if (runtimeRebuildButtonLabel != null)
        {
            runtimeRebuildButtonLabel.text = $"REBUILDING {manualRebuildCount}";
        }

        layoutRebuildRoutine = StartCoroutine(RebuildChecklistLayoutRoutine());
    }

    private void ResolveReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }

        if (panelView == null)
        {
            panelView = GetComponent<IncidentProcedureChecklistPanelView>();
            if (panelView == null)
            {
                panelView = FindAnyObjectByType<IncidentProcedureChecklistPanelView>(FindObjectsInactive.Include);
            }
        }
    }

    private void EnsureRuntimeRebuildButton()
    {
        if (!createRuntimeRebuildButton || runtimeRebuildButton != null)
        {
            return;
        }

        if (panelView == null || panelView.PanelRootRect == null)
        {
            ResolveReferences();
        }

        RectTransform parent = panelView != null
            ? panelView.PanelRootRect != null
                ? panelView.PanelRootRect
                : panelView.transform as RectTransform
            : transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        GameObject buttonObject = new GameObject("RuntimeRebuildLayoutButton");
        buttonObject.transform.SetParent(parent, false);
        buttonObject.transform.SetAsLastSibling();

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = rebuildButtonAnchorPosition;
        rect.sizeDelta = rebuildButtonSize;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        image.raycastTarget = true;

        runtimeRebuildButton = buttonObject.AddComponent<Button>();
        runtimeRebuildButton.targetGraphic = image;
        runtimeRebuildButton.onClick.AddListener(RebuildChecklistLayoutNow);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        runtimeRebuildButtonLabel = labelObject.AddComponent<TextMeshProUGUI>();
        runtimeRebuildButtonLabel.text = "REBUILD";
        runtimeRebuildButtonLabel.fontSize = 18f;
        runtimeRebuildButtonLabel.alignment = TextAlignmentOptions.Center;
        runtimeRebuildButtonLabel.color = Color.white;
        runtimeRebuildButtonLabel.raycastTarget = false;
    }

    private void RebuildChecklistLayout()
    {
        if (layoutRebuildRoutine != null)
        {
            StopCoroutine(layoutRebuildRoutine);
        }

        layoutRebuildRoutine = StartCoroutine(RebuildChecklistLayoutRoutine());
    }

    private IEnumerator RebuildChecklistLayoutRoutine()
    {
        int passCount = Mathf.Max(1, layoutRebuildPasses);
        for (int i = 0; i < passCount; i++)
        {
            ForceChecklistLayoutPass();
            yield return new WaitForEndOfFrame();
        }

        ForceChecklistLayoutPass();
        if (runtimeRebuildButtonLabel != null)
        {
            runtimeRebuildButtonLabel.text = manualRebuildCount > 0 ? $"REBUILD {manualRebuildCount}" : "REBUILD";
        }

        layoutRebuildRoutine = null;
    }

    private void ForceChecklistLayoutPass()
    {
        RectTransform rebuildRoot = ResolveRebuildRoot();
        ForceTextMeshUpdates(rebuildRoot);
        MarkLayoutsForRebuild(rebuildRoot);
        ToggleContentSizeFitter(panelView.ActionItemsContentSizeFitter);

        RebuildActiveItemLayouts();

        if (panelView.ActionItemsBodyRootRect != null)
        {
            NudgeWidthForImmediateRebuild(panelView.ActionItemsBodyRootRect);
            RebuildLayout(panelView.ActionItemsBodyRootRect);
        }

        if (panelView.ActionItemsRootRect != null)
        {
            RebuildLayout(panelView.ActionItemsRootRect);
        }

        if (panelView.PanelRootRect != null)
        {
            RebuildLayout(panelView.PanelRootRect);
        }

        RebuildLayoutBottomUp(rebuildRoot);
        Canvas.ForceUpdateCanvases();
    }

    private RectTransform ResolveRebuildRoot()
    {
        if (panelView != null && panelView.PanelRootRect != null)
        {
            return panelView.PanelRootRect;
        }

        if (panelView != null)
        {
            return panelView.transform as RectTransform;
        }

        return transform as RectTransform;
    }

    private void RebuildActiveItemLayouts()
    {
        Transform root = panelView.ActionItemsBodyRoot != null ? panelView.ActionItemsBodyRoot : panelView.ActionItemsRoot;
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            RebuildLayout(child as RectTransform);
        }
    }

    private static void RebuildLayout(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        Canvas.ForceUpdateCanvases();
    }

    private static void ForceTextMeshUpdates(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }
        }
    }

    private static void MarkLayoutsForRebuild(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            if (rectTransforms[i] != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rectTransforms[i]);
            }
        }

        LayoutRebuilder.MarkLayoutForRebuild(root);
    }

    private static void RebuildLayoutBottomUp(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        List<RectTransform> rectTransforms = new List<RectTransform>();
        root.GetComponentsInChildren(includeInactive: true, rectTransforms);
        rectTransforms.Sort((a, b) => GetDepth(b).CompareTo(GetDepth(a)));

        for (int i = 0; i < rectTransforms.Count; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null && rectTransform.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        if (root.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        while (transform != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private static void NudgeWidthForImmediateRebuild(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Vector2 size = root.sizeDelta;
        root.sizeDelta = new Vector2(size.x + 1f, size.y);
        root.sizeDelta = size;
    }

    private static void ToggleContentSizeFitter(ContentSizeFitter fitter)
    {
        if (fitter == null)
        {
            return;
        }

        bool wasEnabled = fitter.enabled;
        fitter.enabled = false;
        Canvas.ForceUpdateCanvases();
        fitter.enabled = wasEnabled;
        Canvas.ForceUpdateCanvases();
    }

    private static void SetAllChildrenActive(Transform root, bool active)
    {
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
