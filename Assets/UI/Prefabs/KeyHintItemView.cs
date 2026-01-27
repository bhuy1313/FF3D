using TMPro;
using UnityEngine;


public class KeyHintItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text nameText;

    public void Set(string key, string name)
    {
        if (keyText) keyText.text = key;
        if (nameText) nameText.text = name;
    }
}
