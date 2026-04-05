using UnityEngine;

public class WoodenCrate : MonoBehaviour, IGrabbable, IInteractable, IMovementWeightSource
{
    [SerializeField] private float movementWeightKg = 15f;

    private Rigidbody rb;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing from Crate object.");
        }
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
    }

    void Start()
    {
        //None
    }

    void Update()
    {
        //None
    }

    public void Interact(GameObject interactor)
    {
        //None
    }
}
