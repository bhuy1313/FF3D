using UnityEngine;

public class FireHose : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    [Header("Water Supply")]
    [SerializeField] private float maxWater = 0f;
    [SerializeField] private float dischargePerSecond = 2f;
    [SerializeField] private float rechargePerSecond = 0f;
    [SerializeField] private float minWaterToUse = 0.05f;
    [SerializeField] private bool toggleUse = true;

    [Header("Spray")]
    [SerializeField] private Transform sprayOrigin;
    [SerializeField] private float sprayRange = 12f;
    [SerializeField] private float sprayRadius = 0.35f;
    [SerializeField] private LayerMask sprayMask = ~0;
    [SerializeField] private float applyWaterPerSecond = 1.5f;

    [Header("VFX/SFX")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private AudioSource sprayAudio;
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private bool setWaterTag = true;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentWater;
    [SerializeField] private bool isSpraying;

    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (waterParticles == null)
        {
            waterParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (sprayOrigin == null && waterParticles != null)
        {
            sprayOrigin = waterParticles.transform;
        }

        if (sprayAudio == null)
        {
            sprayAudio = GetComponentInChildren<AudioSource>();
        }

        if (maxWater > 0f)
        {
            currentWater = Mathf.Clamp(currentWater <= 0f ? maxWater : currentWater, 0f, maxWater);
        }

        SetSprayState(false);
        TryApplyWaterTag();
    }

    private void Update()
    {
        if (isSpraying)
        {
            if (maxWater > 0f)
            {
                currentWater = Mathf.Max(0f, currentWater - dischargePerSecond * Time.deltaTime);
                if (currentWater <= 0f)
                {
                    SetSprayState(false);
                    return;
                }
            }

            SprayWater();
        }
        else if (maxWater > 0f && rechargePerSecond > 0f && currentWater < maxWater)
        {
            currentWater = Mathf.Min(maxWater, currentWater + rechargePerSecond * Time.deltaTime);
        }
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("FireHose Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("FireHose Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        SetSprayState(false);
        Debug.Log("FireHose Dropped!");
    }

    public void Use(GameObject user)
    {
        Debug.Log("FireHose Used!");

        if (toggleUse)
        {
            if (isSpraying)
            {
                SetSprayState(false);
                return;
            }

            if (HasWaterToUse())
            {
                SetSprayState(true);
            }

            return;
        }

        if (HasWaterToUse())
        {
            SetSprayState(true);
        }
    }

    private void OnDisable()
    {
        SetSprayState(false);
    }

    private bool HasWaterToUse()
    {
        if (maxWater <= 0f)
        {
            return true;
        }

        return currentWater >= minWaterToUse;
    }

    private void SetSprayState(bool enable)
    {
        if (isSpraying == enable)
        {
            return;
        }

        isSpraying = enable;

        if (waterParticles != null)
        {
            if (enable)
            {
                waterParticles.Play();
            }
            else
            {
                waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (sprayAudio != null)
        {
            if (enable)
            {
                if (!sprayAudio.isPlaying)
                {
                    sprayAudio.Play();
                }
            }
            else
            {
                sprayAudio.Stop();
            }
        }
    }

    private void SprayWater()
    {
        if (applyWaterPerSecond <= 0f)
        {
            return;
        }

        Transform origin = sprayOrigin != null ? sprayOrigin : transform;
        Vector3 position = origin.position;
        Vector3 direction = origin.forward;

        float amount = applyWaterPerSecond * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(
            position,
            sprayRadius,
            direction,
            sprayRange,
            sprayMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            ApplyWaterToCollider(hits[i].collider, amount);
        }
    }

    private static void ApplyWaterToCollider(Collider collider, float amount)
    {
        if (collider == null)
        {
            return;
        }

        Fire fire = FindFire(collider);
        if (fire != null)
        {
            fire.ApplyWater(amount);
        }

        FireParticleSystem particleFire = FindFireParticleSystem(collider);
        if (particleFire != null)
        {
            particleFire.ApplyWater(amount);
        }
    }

    private static Fire FindFire(Collider collider)
    {
        if (collider.TryGetComponent(out Fire direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out Fire rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out Fire parentFire))
        {
            return parentFire;
        }

        return null;
    }

    private static FireParticleSystem FindFireParticleSystem(Collider collider)
    {
        if (collider.TryGetComponent(out FireParticleSystem direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out FireParticleSystem rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out FireParticleSystem parentFire))
        {
            return parentFire;
        }

        return null;
    }

    private void TryApplyWaterTag()
    {
        if (!setWaterTag || waterParticles == null || string.IsNullOrEmpty(waterTag))
        {
            return;
        }

        if (waterParticles.CompareTag(waterTag))
        {
            return;
        }

        try
        {
            waterParticles.gameObject.tag = waterTag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{waterTag}' not found. Add it in Tag Manager to enable water detection.", this);
        }
    }
}
