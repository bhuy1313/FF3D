using UnityEngine;

public class FireExtinguisher : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    [Header("Extinguisher")]
    [SerializeField] private float maxCharge = 10f;
    [SerializeField] private float dischargePerSecond = 1.5f;
    [SerializeField] private float rechargePerSecond = 0f;
    [SerializeField] private float minChargeToUse = 0.05f;
    [SerializeField] private bool toggleUse = true;

    [Header("VFX/SFX")]
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private AudioSource sprayAudio;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentCharge;
    [SerializeField] private bool isSpraying;

    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (sprayParticles == null)
        {
            sprayParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (sprayAudio == null)
        {
            sprayAudio = GetComponentInChildren<AudioSource>();
        }

        currentCharge = Mathf.Clamp(currentCharge <= 0f ? maxCharge : currentCharge, 0f, maxCharge);
        SetSprayState(false);
    }

    private void Update()
    {
        if (isSpraying)
        {
            currentCharge = Mathf.Max(0f, currentCharge - dischargePerSecond * Time.deltaTime);
            if (currentCharge <= 0f)
            {
                SetSprayState(false);
            }
        }
        else if (rechargePerSecond > 0f && currentCharge < maxCharge)
        {
            currentCharge = Mathf.Min(maxCharge, currentCharge + rechargePerSecond * Time.deltaTime);
        }
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("FireExtinguisher Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("FireExtinguisher Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        SetSprayState(false);
        Debug.Log("FireExtinguisher Dropped!");
    }

    public void Use(GameObject user)
    {
        if (toggleUse)
        {
            if (isSpraying)
            {
                SetSprayState(false);
                return;
            }

            if (currentCharge >= minChargeToUse)
            {
                SetSprayState(true);
            }

            return;
        }

        if (currentCharge >= minChargeToUse)
        {
            SetSprayState(true);
        }
    }

    private void OnDisable()
    {
        SetSprayState(false);
    }

    private void SetSprayState(bool enable)
    {
        if (isSpraying == enable)
        {
            return;
        }

        isSpraying = enable;

        if (sprayParticles != null)
        {
            if (enable)
            {
                sprayParticles.Play();
            }
            else
            {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
}
