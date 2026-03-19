using UnityEngine;

public class Cube : MonoBehaviour, IPickupable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }
    
    public void OnPickup(GameObject picker)
    {
    }
    public void OnDrop(GameObject dropper)
    {
    }
}
