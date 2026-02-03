using UnityEngine;

public class WoodenCrate : MonoBehaviour, IGrabbable, IInteractable
{
    private Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing from Crate object.");
        }
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
