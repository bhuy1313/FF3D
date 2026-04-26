using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class KeyHintItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text nameText;

    private RectTransform rootRect;

    private void Awake()
    {
        rootRect = transform as RectTransform;
    }

    public void Set(string key, string name)
    {
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
    }
}
