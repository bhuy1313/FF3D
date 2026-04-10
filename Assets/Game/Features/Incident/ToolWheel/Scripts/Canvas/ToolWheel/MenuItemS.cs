using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuItemS : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemLabel;

    private void Awake()
    {
        if (itemImage == null)
        {
            Transform backgroundTransform = transform.Find("BgW");
            if (backgroundTransform != null)
            {
                itemImage = backgroundTransform.GetComponent<Image>();
            }

            if (itemImage == null)
            {
                Debug.LogError("No Image component found on 'BgW' GameObject!");
            }
        }

        if (itemLabel == null)
        {
            itemLabel = GetComponentInChildren<TMP_Text>(true);
        }
    }

    public void Select()
    {
        if (itemImage != null)
        {
            Color color = itemImage.color;
            color.a = 1f;
            itemImage.color = color;
        }
    }

    public void Deselect()
    {
        if (itemImage != null)
        {
            Color color = itemImage.color;
            color.a = 0.5f;
            itemImage.color = color;
        }
    }

    public void SetDisplayLabel(string label)
    {
        if (itemLabel == null)
        {
            itemLabel = GetComponentInChildren<TMP_Text>(true);
        }

        if (itemLabel != null)
        {
            itemLabel.text = string.IsNullOrWhiteSpace(label) ? "-" : label;
        }
    }
}
