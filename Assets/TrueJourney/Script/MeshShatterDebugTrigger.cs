using UnityEngine;

[DisallowMultipleComponent]
public class MeshShatterDebugTrigger : MonoBehaviour
{
    [SerializeField] private MeshShatter target;
    [SerializeField] private KeyCode triggerKey = KeyCode.K;
    [SerializeField] private bool triggerOnStart;
    [SerializeField] private float triggerDelay = 0.25f;
    [SerializeField] private float forceMultiplier = 1f;
    [SerializeField] private Vector3 localImpactDirection = Vector3.forward;

    private bool hasTriggered;
    private float remainingDelay;

    private void Awake()
    {
        ResolveTarget();
        remainingDelay = Mathf.Max(0f, triggerDelay);
    }

    private void OnValidate()
    {
        forceMultiplier = Mathf.Max(0f, forceMultiplier);

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

        Vector3 impactDirection = transform.TransformDirection(localImpactDirection);
        if (impactDirection.sqrMagnitude <= 0.001f)
        {
            impactDirection = transform.forward;
        }

        target.Shatter(transform.position, impactDirection.normalized, forceMultiplier);
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
