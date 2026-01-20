using UnityEngine;

public class Cube : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("Cube Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("Cube Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        Debug.Log("Cube Dropped!");
    }

    public void Use(GameObject user)
    {
        Debug.Log("Cube Used!");
    }
}
