using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ChainSaw : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    [Header("Attack")]
    [SerializeField] private float damagePerTick = 12f;
    [SerializeField] private float tickInterval = 0.1f;
    [SerializeField] private float fallbackRange = 2.5f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool woodOnly = true;

    [Header("Engine")]
    [SerializeField] private bool toggleUse = true;
    [SerializeField] private ParticleSystem bladeParticles;
    [SerializeField] private AudioSource engineAudio;

    [Header("Stamina")]
    [SerializeField] private float staminaCostPerSecond = 12f;

    [Header("Runtime (Debug)")]
    [SerializeField] private bool isRunning;
    [SerializeField] private GameObject activeUser;

    private Rigidbody cachedRigidbody;
    private float nextDamageTime;

    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();

        if (bladeParticles == null)
        {
            bladeParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (engineAudio == null)
        {
            engineAudio = GetComponentInChildren<AudioSource>(true);
        }

        SetRunningState(false);
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        if (!TryConsumeStamina(activeUser, staminaCostPerSecond * Time.deltaTime))
        {
            SetRunningState(false);
            Debug.LogWarning("Not enough stamina to keep ChainSaw running.");
            return;
        }

        if (Time.time < nextDamageTime)
        {
            return;
        }

        nextDamageTime = Time.time + Mathf.Max(0.01f, tickInterval);
        TryDealDamage(activeUser);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
    }

    public void OnDrop(GameObject dropper)
    {
        activeUser = null;
        SetRunningState(false);
    }

    public void Use(GameObject user)
    {
        activeUser = user;

        if (toggleUse)
        {
            if (!isRunning && !CanAffordStart(user))
            {
                Debug.LogWarning("Not enough stamina to start ChainSaw.");
                return;
            }

            SetRunningState(!isRunning);
            return;
        }

        float singleUseCost = staminaCostPerSecond * Mathf.Max(0.01f, tickInterval);
        if (!TryConsumeStamina(user, singleUseCost))
        {
            Debug.LogWarning("Not enough stamina to use ChainSaw.");
            return;
        }

        TryDealDamage(user);
    }

    private void OnDisable()
    {
        SetRunningState(false);
        activeUser = null;
    }

    private void SetRunningState(bool enable)
    {
        if (isRunning == enable)
        {
            return;
        }

        isRunning = enable;
        nextDamageTime = Time.time;

        if (bladeParticles != null)
        {
            if (enable)
            {
                bladeParticles.Play();
            }
            else
            {
                bladeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (engineAudio != null)
        {
            if (enable)
            {
                if (!engineAudio.isPlaying)
                {
                    engineAudio.Play();
                }
            }
            else
            {
                engineAudio.Stop();
            }
        }
    }

    private void TryDealDamage(GameObject user)
    {
        if (damagePerTick <= 0f)
        {
            return;
        }

        float range = GetAttackRange(user);
        if (range <= 0f)
        {
            return;
        }

        Transform aim = GetAimTransform(user);
        if (aim == null)
        {
            return;
        }

        Ray ray = new Ray(aim.position, aim.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        IDamageable damageable = FindDamageable(hit.collider);
        if (damageable == null || !CanDamageTarget(damageable))
        {
            return;
        }

        GameObject source = user != null ? user : gameObject;
        damageable.TakeDamage(damagePerTick, source, hit.point, hit.normal);
    }

    private bool CanDamageTarget(IDamageable damageable)
    {
        if (!woodOnly)
        {
            return true;
        }

        if (damageable is Breakable breakable)
        {
            return breakable.Type == Breakable.BreakableType.Wood;
        }

        return true;
    }

    private IDamageable FindDamageable(Collider collider)
    {
        if (collider.TryGetComponent(out IDamageable direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out IDamageable rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out IDamageable parentDamageable))
        {
            return parentDamageable;
        }

        return null;
    }

    private float GetAttackRange(GameObject user)
    {
        if (user != null && user.TryGetComponent(out StarterAssets.FPSInteractionSystem interaction))
        {
            return interaction.InteractDistance;
        }

        return fallbackRange;
    }

    private Transform GetAimTransform(GameObject user)
    {
        if (user != null)
        {
            Transform cameraRoot = user.transform.Find("PlayerCameraRoot");
            if (cameraRoot != null)
            {
                return cameraRoot;
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam.transform;
        }

        return transform;
    }

    private bool CanAffordStart(GameObject user)
    {
        if (staminaCostPerSecond <= 0f || user == null)
        {
            return true;
        }

        if (!user.TryGetComponent(out PlayerVitals vitals))
        {
            return true;
        }

        float startCost = staminaCostPerSecond * Mathf.Max(0.01f, tickInterval);
        return vitals.CurrentStamina >= startCost;
    }

    private bool TryConsumeStamina(GameObject user, float amount)
    {
        if (amount <= 0f || user == null)
        {
            return true;
        }

        if (!user.TryGetComponent(out PlayerVitals vitals))
        {
            return true;
        }

        return vitals.TryUseStamina(amount);
    }
}
