using UnityEngine;

public class FireAxe : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    void Start()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("FireAxe Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("FireAxe Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        Debug.Log("FireAxe Dropped!");
    }

    public void Use(GameObject user)
    {
        Debug.Log("FireAxe Used!");
    }


}
