using UnityEngine;

[DisallowMultipleComponent]
public class MeshShatterDebugTrigger : MonoBehaviour
{
    [SerializeField] private MeshShatter target;
    [SerializeField] private KeyCode triggerKey = KeyCode.K;
    [SerializeField] private bool triggerOnStart;
    [SerializeField] private float triggerDelay = 0.25f;

    private bool hasTriggered;
    private float remainingDelay;

    private void Awake()
    {
        ResolveTarget();
        remainingDelay = Mathf.Max(0f, triggerDelay);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveTarget();
        }
    }

    private void Update()
    {
        if (hasTriggered)
        {
            return;
        }

        if (triggerOnStart)
        {
            if (remainingDelay > 0f)
            {
                remainingDelay -= Time.deltaTime;
                return;
            }

            TriggerShatter();
            return;
        }

        if (Input.GetKeyDown(triggerKey))
        {
            TriggerShatter();
        }
    }

    [ContextMenu("Trigger Shatter")]
    public void TriggerShatter()
    {
        ResolveTarget();
        if (target == null)
        {
            return;
        }

        target.Shatter();
        hasTriggered = true;
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            target = GetComponent<MeshShatter>();
        }
    }
}
