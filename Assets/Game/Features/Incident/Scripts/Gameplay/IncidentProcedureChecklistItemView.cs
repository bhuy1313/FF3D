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
    [SerializeField] private Button toggleButton;
    [SerializeField] private Image toggleImage;
    [SerializeField] private Color checkedColor = new Color(0.2f, 0.75f, 0.3f, 1f);
    [SerializeField] private Color uncheckedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioClip checkSound;
    [SerializeField, Range(0f, 1f)] private float checkSoundVolume = 1f;

    private IncidentMissionSystem missionSystem;
    private string boundItemId;
    private bool boundIsCompleted;

    public void Bind(IncidentProcedureChecklistItem item)
    {
        if (item == null)
        {
            SetText(tagText, string.Empty);
            SetText(descText, string.Empty);
            SetText(subDescText, string.Empty);
            boundItemId = null;
            boundIsCompleted = false;
            ApplyToggleVisual(false);
            SetToggleInteractable(false);
            return;
        }

        ResolveRuntimeReferences();
        SetText(tagText, item.Title);
        SetText(descText, item.Description);
        SetText(subDescText, $"{item.ItemType} | {item.Priority}");
        boundItemId = item.ItemId;

        bool isCompleted = item.DefaultChecked;
        if (missionSystem != null &&
            missionSystem.TryGetProcedureChecklistStatus(item.ItemId, out bool runtimeCompleted, out bool isContradicted, out bool isRelevant))
        {
            isCompleted = runtimeCompleted && !isContradicted && isRelevant;
        }

        boundIsCompleted = isCompleted;
        ApplyToggleVisual(boundIsCompleted);
        SetToggleInteractable(missionSystem != null);

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
        Transform imageRoot = FindDeepChild(transform, "Image");
        toggleButton = imageRoot != null ? imageRoot.GetComponent<Button>() : null;
        toggleImage = imageRoot != null ? imageRoot.GetComponent<Image>() : null;
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        BindToggleButton();
    }

    private void Awake()
    {
        AutoBind();
        ResolveRuntimeReferences();
        BindToggleButton();
    }

    private void ResolveRuntimeReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    private void BindToggleButton()
    {
        if (toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(HandleToggleClicked);
        toggleButton.onClick.AddListener(HandleToggleClicked);
    }

    private void HandleToggleClicked()
    {
        if (missionSystem == null || string.IsNullOrWhiteSpace(boundItemId))
        {
            ResolveRuntimeReferences();
        }

        if (missionSystem == null || string.IsNullOrWhiteSpace(boundItemId))
        {
            return;
        }

        bool nextValue = !boundIsCompleted;
        if (!missionSystem.SetProcedureChecklistCompleted(boundItemId, nextValue))
        {
            return;
        }

        boundIsCompleted = nextValue;
        ApplyToggleVisual(boundIsCompleted);
        if (boundIsCompleted && checkSound != null)
        {
            AudioService.PlayClip2D(checkSound, AudioBus.Ui, checkSoundVolume);
        }
    }

    private void ApplyToggleVisual(bool isCompleted)
    {
        if (toggleImage != null)
        {
            toggleImage.color = isCompleted ? checkedColor : uncheckedColor;
        }
    }

    private void SetToggleInteractable(bool interactable)
    {
        if (toggleButton != null)
        {
            toggleButton.interactable = interactable;
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
