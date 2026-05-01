using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class KeyHintItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text nameText;

    private RectTransform rootRect;
    private string currentKey = string.Empty;
    private string currentName = string.Empty;

    private void Awake()
    {
        rootRect = transform as RectTransform;
    }

    public bool Set(string key, string name)
    {
        key ??= string.Empty;
        name ??= string.Empty;

        if (currentKey == key && currentName == name)
        {
            return false;
        }

        currentKey = key;
        currentName = name;

        if (keyText) keyText.text = key;
        if (nameText) nameText.text = name;

        if (keyText != null)
        {
            keyText.ForceMeshUpdate();
        }

        if (nameText != null)
        {
            nameText.ForceMeshUpdate();
        }

        if (rootRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        return true;
    }
}
