using UnityEngine;

public class FireHose : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    void Start()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("FireHose Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("FireHose Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        Debug.Log("FireHose Dropped!");
    }

    public void Use(GameObject user)
    {
        Debug.Log("FireHose Used!");
    }
}
