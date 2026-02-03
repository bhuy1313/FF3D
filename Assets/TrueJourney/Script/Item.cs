using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("Item Info")]
    [SerializeField] private string itemName = "";
    public string ItemName => itemName;
    [SerializeField] private string itemDescription = "";
    public string ItemDescription => itemDescription;
    [SerializeField] private Sprite itemIcon;
    public Sprite ItemIcon => itemIcon;
}
