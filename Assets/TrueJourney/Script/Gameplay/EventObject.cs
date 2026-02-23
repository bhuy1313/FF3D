using UnityEngine;

public class EventObject : MonoBehaviour, IEventListener
{
    Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
    }

    private void rbIsKinematic()
    {
        if (rb != null)
        {
            rb.isKinematic = !rb.isKinematic;
        }
    }

    public void OnEventTriggered(GameObject eventSource, GameObject instigator)
    {
        rbIsKinematic();
    }
}
