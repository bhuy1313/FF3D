using UnityEngine;

public class Vent : MonoBehaviour, IInteractable, IGrabbable
{
    private Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing from Vent object.");
        }
    }

    void Update()
    {
        //None
    }

    public void Interact(GameObject interactor)
    {
        rb.isKinematic = false;
    }
}
