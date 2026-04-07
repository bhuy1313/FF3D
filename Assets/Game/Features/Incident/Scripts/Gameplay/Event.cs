using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class Event : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Collider triggerZone;
    public Collider TriggerZone => triggerZone;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string requiredTag = "Player";
    [SerializeField] private bool isTriggered;
    public bool IsTriggered => isTriggered;

    [Header("Actions")]
    [SerializeField] private UnityEvent onTriggered;

    private void Awake()
    {
        if (triggerZone == null)
        {
            triggerZone = GetComponent<Collider>();
        }

        if (triggerZone != null && !triggerZone.isTrigger)
        {
            triggerZone.isTrigger = true;
        }
    }

    private void Reset()
    {
        triggerZone = GetComponent<Collider>();
        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        if (triggerZone == null)
        {
            triggerZone = GetComponent<Collider>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered && triggerOnce)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }

        isTriggered = true;

        TriggerChildren(other.gameObject);
        onTriggered?.Invoke();
    }

    private void TriggerChildren(GameObject instigator)
    {
        IEventListener[] listeners = GetComponentsInChildren<IEventListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].OnEventTriggered(gameObject, instigator);
        }
    }
}
