using UnityEngine;

public class Cube : MonoBehaviour, IPickupable, IMovementWeightSource
{
    [SerializeField] private float movementWeightKg = 5f;

    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
    }
    
    public void OnPickup(GameObject picker)
    {
    }
    public void OnDrop(GameObject dropper)
    {
    }
}
