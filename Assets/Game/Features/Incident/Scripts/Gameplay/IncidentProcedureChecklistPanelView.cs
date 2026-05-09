using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IncidentProcedureChecklistPanelView : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform actionItemsRoot;
    [SerializeField] private Transform actionItemsBodyRoot;
    [SerializeField] private IncidentProcedureChecklistItemView itemViewPrefab;
    [SerializeField] private RectTransform actionItemsRootRect;
    [SerializeField] private RectTransform actionItemsBodyRootRect;
    [SerializeField] private ContentSizeFitter actionItemsContentSizeFitter;
    [SerializeField] private RectTransform panelRootRect;

    public TextMeshProUGUI TitleText => titleText;
    public Transform ActionItemsRoot => actionItemsRoot;
    public Transform ActionItemsBodyRoot => actionItemsBodyRoot;
    public IncidentProcedureChecklistItemView ItemViewPrefab => itemViewPrefab;
    public RectTransform ActionItemsRootRect => actionItemsRootRect;
    public RectTransform ActionItemsBodyRootRect => actionItemsBodyRootRect;
    public ContentSizeFitter ActionItemsContentSizeFitter => actionItemsContentSizeFitter;
    public RectTransform PanelRootRect => panelRootRect;

    private void Reset()
    {
        AutoBind();
    }

    private void OnValidate()
    {
        if (titleText == null || actionItemsRoot == null)
        {
            AutoBind();
        }
    }

    private void AutoBind()
    {
        panelRootRect = transform as RectTransform;

        Transform titleRoot = FindDeepChild(transform, "Tittle");
        if (titleRoot != null)
        {
            Transform titleTextRoot = FindDeepChild(titleRoot, "TittleText");
            titleText = titleTextRoot != null
                ? titleTextRoot.GetComponentInChildren<TextMeshProUGUI>(true)
                : titleRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        actionItemsRoot = FindDeepChild(transform, "Action Items");
        actionItemsRootRect = actionItemsRoot as RectTransform;
        actionItemsContentSizeFitter = actionItemsRoot != null ? actionItemsRoot.GetComponent<ContentSizeFitter>() : null;

        Transform bodyRoot = FindDeepChild(actionItemsRoot != null ? actionItemsRoot : transform, "Body");
        actionItemsBodyRoot = bodyRoot;
        actionItemsBodyRootRect = bodyRoot as RectTransform;
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
