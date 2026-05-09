using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IncidentProcedureChecklistItemView : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private TextMeshProUGUI tagText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI subDescText;
    [SerializeField] private LayoutElement layoutElement;

    public void Bind(IncidentProcedureChecklistItem item)
    {
        if (item == null)
        {
            SetText(tagText, string.Empty);
            SetText(descText, string.Empty);
            SetText(subDescText, string.Empty);
            return;
        }

        SetText(tagText, item.Title);
        SetText(descText, item.Description);
        SetText(subDescText, $"{item.ItemType} | {item.Priority}");
        if (layoutElement != null)
        {
            layoutElement.minHeight = 120f;
            layoutElement.preferredHeight = -1f;
            layoutElement.flexibleHeight = 0f;
        }
    }

    private void Reset()
    {
        AutoBind();
    }

    private void OnValidate()
    {
        if (tagText == null || descText == null || subDescText == null)
        {
            AutoBind();
        }
    }

    private void AutoBind()
    {
        tagText = FindText("Tag");
        descText = FindText("Desc");
        subDescText = FindText("SubDesc");
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
    }

    private TextMeshProUGUI FindText(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponentInChildren<TextMeshProUGUI>(true) : null;
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

    private static void SetText(TextMeshProUGUI textComponent, string value)
    {
        if (textComponent != null)
        {
            textComponent.text = value ?? string.Empty;
        }
    }
}
