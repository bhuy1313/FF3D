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

    private IncidentProcedureDefinition lastBoundDefinition;
    private Coroutine layoutRebuildRoutine;
    private int lastObservedBodyChildCount = -1;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SyncObservedBodyChildCount();
    }

    private void Start()
    {
        ResolveReferences();
        SyncObservedBodyChildCount();
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

        if (TryConsumeBodyChildCountChange())
        {
            RebuildChecklistLayout();
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
            DestroyAllChildren(root);
            SyncObservedBodyChildCount();
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
                Destroy(child.gameObject);
            }
        }

        SyncObservedBodyChildCount();
        RebuildChecklistLayout();
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

    private bool TryConsumeBodyChildCountChange()
    {
        Transform bodyRoot = panelView != null ? panelView.ActionItemsBodyRoot : null;
        if (bodyRoot == null)
        {
            return false;
        }

        int currentChildCount = bodyRoot.childCount;
        if (lastObservedBodyChildCount < 0)
        {
            lastObservedBodyChildCount = currentChildCount;
            return false;
        }

        if (currentChildCount == lastObservedBodyChildCount)
        {
            return false;
        }

        lastObservedBodyChildCount = currentChildCount;
        return true;
    }

    private void SyncObservedBodyChildCount()
    {
        Transform bodyRoot = panelView != null ? panelView.ActionItemsBodyRoot : null;
        lastObservedBodyChildCount = bodyRoot != null ? bodyRoot.childCount : -1;
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

    private static void DestroyAllChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
