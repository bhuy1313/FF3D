using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TMPFontSizeBatchTool : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private TMP_FontAsset targetFontAsset;
    [SerializeField] private float targetFontSize = 36f;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool applyFontAsset = true;
    [SerializeField] private bool applyFontSize = true;

    public TMP_FontAsset TargetFontAsset
    {
        get => targetFontAsset;
        set => targetFontAsset = value;
    }

    public float TargetFontSize
    {
        get => targetFontSize;
        set => targetFontSize = value;
    }

    public bool IncludeInactive
    {
        get => includeInactive;
        set => includeInactive = value;
    }

    public bool ApplyFontAsset
    {
        get => applyFontAsset;
        set => applyFontAsset = value;
    }

    public bool ApplyFontSize
    {
        get => applyFontSize;
        set => applyFontSize = value;
    }

    public int ApplyToChildren()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(includeInactive);
        int changedCount = 0;

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            bool changed = false;

            if (applyFontAsset && targetFontAsset != null && text.font != targetFontAsset)
            {
                text.font = targetFontAsset;
                changed = true;
            }

            if (applyFontSize && !Mathf.Approximately(text.fontSize, targetFontSize))
            {
                text.fontSize = targetFontSize;
                changed = true;
            }

            if (changed)
            {
                changedCount++;
            }
        }

        return changedCount;
    }
}
