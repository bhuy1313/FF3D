using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FireHose : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private enum SprayPattern
    {
        Concentrated,
        Wide
    }

    private struct SprayPatternConfig
    {
        public float rangeMultiplier;
        public float radiusMultiplier;
        public float effectivenessMultiplier;
        public float vfxSpeedMultiplier;
        public float vfxSpreadMultiplier;
    }

    [Header("Water Supply")]
    [SerializeField] private float maxWater = 0f;
    [SerializeField] private float dischargePerSecond = 2f;
    [SerializeField] private float rechargePerSecond = 0f;
    [SerializeField] private float minWaterToUse = 0.05f;
    [SerializeField] private bool toggleUse = true;

    [Header("Spray (Base)")]
    [SerializeField] private Transform sprayOrigin;
    [SerializeField] private float sprayRange = 12f;
    [SerializeField] private float sprayRadius = 0.35f;
    [SerializeField] private LayerMask sprayMask = ~0;
    [SerializeField] private float applyWaterPerSecond = 1.5f;

    [Header("Spray Control")]
    [SerializeField] private SprayPattern sprayPattern = SprayPattern.Concentrated;
    [SerializeField] private float pressureMultiplier = 1f;
    [SerializeField] private float minPressureMultiplier = 0.5f;
    [SerializeField] private float maxPressureMultiplier = 1.75f;
    [SerializeField] private float pressureStep = 0.25f;
    [SerializeField] private bool allowRuntimeTuning = true;
    [SerializeField] private bool tuningOnlyWhileSpraying = true;
    [SerializeField] private bool showTuningLogs = false;

    [Header("Concentrated Pattern")]
    [SerializeField] private float concentratedRangeMultiplier = 1.25f;
    [SerializeField] private float concentratedRadiusMultiplier = 0.6f;
    [SerializeField] private float concentratedEffectivenessMultiplier = 1.35f;
    [SerializeField] private float concentratedVfxSpeedMultiplier = 1.2f;
    [SerializeField] private float concentratedVfxSpreadMultiplier = 0.5f;

    [Header("Wide Pattern")]
    [SerializeField] private float wideRangeMultiplier = 0.8f;
    [SerializeField] private float wideRadiusMultiplier = 1.7f;
    [SerializeField] private float wideEffectivenessMultiplier = 0.75f;
    [SerializeField] private float wideVfxSpeedMultiplier = 0.9f;
    [SerializeField] private float wideVfxSpreadMultiplier = 1.45f;

    [Header("VFX/SFX")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private AudioSource sprayAudio;
    [SerializeField] private float particleGravityModifier = 0.35f;
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private bool setWaterTag = true;
    [SerializeField] private bool removeParticleBounce = true;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentWater;
    [SerializeField] private bool isSpraying;
    [SerializeField] private float currentDischargeRate;
    [SerializeField] private float currentApplyWaterRate;
    [SerializeField] private float currentSprayRange;
    [SerializeField] private float currentSprayRadius;

    private Rigidbody cachedRigidbody;
    private bool particleDefaultsCached;
    private float baseParticleStartSpeedMultiplier = 1f;
    private float baseParticleShapeAngle = 25f;

    public Rigidbody Rigidbody => cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (waterParticles == null && sprayOrigin != null)
        {
            waterParticles = sprayOrigin.GetComponentInChildren<ParticleSystem>(true);
        }

        if (waterParticles == null)
        {
            waterParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (sprayOrigin == null && waterParticles != null)
        {
            sprayOrigin = waterParticles.transform;
        }

        AlignWaterVfxToSprayOrigin();
        if (waterParticles == null)
        {
            Debug.LogWarning("FireHose has no water ParticleSystem assigned/found.", this);
        }
        else
        {
            ConfigureWaterParticleCollision();
        }

        if (sprayAudio == null)
        {
            sprayAudio = GetComponentInChildren<AudioSource>();
        }

        if (maxWater > 0f)
        {
            currentWater = Mathf.Clamp(currentWater <= 0f ? maxWater : currentWater, 0f, maxWater);
        }

        ClampSpraySettings();
        RecalculateSprayRuntimeValues();
        CacheWaterParticleDefaults();
        ApplySprayTuningToVfx();

        SetSprayState(false);
        TryApplyWaterTag();
    }

    private void Update()
    {
        HandleRuntimeTuningInput();
        RecalculateSprayRuntimeValues();

        if (isSpraying)
        {
            if (maxWater > 0f)
            {
                currentWater = Mathf.Max(0f, currentWater - currentDischargeRate * Time.deltaTime);
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
        AlignWaterVfxToSprayOrigin();
        ConfigureWaterParticleCollision();
        CacheWaterParticleDefaults();
        ApplySprayTuningToVfx();
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

    private void OnValidate()
    {
        ClampSpraySettings();
        RecalculateSprayRuntimeValues();

        if (!Application.isPlaying)
        {
            return;
        }

        if (!particleDefaultsCached)
        {
            CacheWaterParticleDefaults();
        }

        ApplySprayTuningToVfx();
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
                AlignWaterVfxToSprayOrigin();
                ApplySprayTuningToVfx();
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
        if (currentApplyWaterRate <= 0f)
        {
            return;
        }

        Transform origin = sprayOrigin != null ? sprayOrigin : transform;
        Vector3 position = origin.position;
        Vector3 direction = origin.forward;

        float amount = currentApplyWaterRate * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(
            position,
            currentSprayRadius,
            direction,
            currentSprayRange,
            sprayMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            ApplyWaterToCollider(hits[i].collider, amount);
        }
    }

    private void AlignWaterVfxToSprayOrigin()
    {
        if (sprayOrigin == null || waterParticles == null)
        {
            return;
        }

        Transform particleTransform = waterParticles.transform;
        if (particleTransform.parent != sprayOrigin)
        {
            particleTransform.SetParent(sprayOrigin, false);
        }

        particleTransform.localPosition = Vector3.zero;
        particleTransform.localRotation = Quaternion.identity;
    }

    private void ConfigureWaterParticleCollision()
    {
        if (!removeParticleBounce || waterParticles == null)
        {
            return;
        }

        ParticleSystem.CollisionModule collision = waterParticles.collision;
        if (!collision.enabled)
        {
            return;
        }

        collision.bounce = 0f;
        collision.dampen = 0f;
        collision.lifetimeLoss = 1f;
    }

    private void ClampSpraySettings()
    {
        sprayRange = Mathf.Max(0.1f, sprayRange);
        sprayRadius = Mathf.Max(0.01f, sprayRadius);
        applyWaterPerSecond = Mathf.Max(0f, applyWaterPerSecond);
        dischargePerSecond = Mathf.Max(0f, dischargePerSecond);
        particleGravityModifier = Mathf.Max(0f, particleGravityModifier);
        pressureStep = Mathf.Max(0.01f, pressureStep);
        minPressureMultiplier = Mathf.Max(0.1f, minPressureMultiplier);
        maxPressureMultiplier = Mathf.Max(minPressureMultiplier, maxPressureMultiplier);
        pressureMultiplier = Mathf.Clamp(pressureMultiplier, minPressureMultiplier, maxPressureMultiplier);

        concentratedRangeMultiplier = Mathf.Max(0.1f, concentratedRangeMultiplier);
        concentratedRadiusMultiplier = Mathf.Max(0.05f, concentratedRadiusMultiplier);
        concentratedEffectivenessMultiplier = Mathf.Max(0f, concentratedEffectivenessMultiplier);
        concentratedVfxSpeedMultiplier = Mathf.Max(0.1f, concentratedVfxSpeedMultiplier);
        concentratedVfxSpreadMultiplier = Mathf.Max(0.05f, concentratedVfxSpreadMultiplier);

        wideRangeMultiplier = Mathf.Max(0.1f, wideRangeMultiplier);
        wideRadiusMultiplier = Mathf.Max(0.05f, wideRadiusMultiplier);
        wideEffectivenessMultiplier = Mathf.Max(0f, wideEffectivenessMultiplier);
        wideVfxSpeedMultiplier = Mathf.Max(0.1f, wideVfxSpeedMultiplier);
        wideVfxSpreadMultiplier = Mathf.Max(0.05f, wideVfxSpreadMultiplier);
    }

    private void RecalculateSprayRuntimeValues()
    {
        ClampSpraySettings();
        SprayPatternConfig config = GetCurrentPatternConfig();

        float pressureT = GetPressure01();
        float pressureRangeMultiplier = Mathf.Lerp(0.85f, 1.2f, pressureT);
        float pressureRadiusMultiplier = Mathf.Lerp(1.15f, 0.85f, pressureT);

        currentDischargeRate = dischargePerSecond * pressureMultiplier;
        currentApplyWaterRate = applyWaterPerSecond * config.effectivenessMultiplier * pressureMultiplier;
        currentSprayRange = sprayRange * config.rangeMultiplier * pressureRangeMultiplier;
        currentSprayRadius = sprayRadius * config.radiusMultiplier * pressureRadiusMultiplier;
    }

    private SprayPatternConfig GetCurrentPatternConfig()
    {
        if (sprayPattern == SprayPattern.Wide)
        {
            return new SprayPatternConfig
            {
                rangeMultiplier = wideRangeMultiplier,
                radiusMultiplier = wideRadiusMultiplier,
                effectivenessMultiplier = wideEffectivenessMultiplier,
                vfxSpeedMultiplier = wideVfxSpeedMultiplier,
                vfxSpreadMultiplier = wideVfxSpreadMultiplier
            };
        }

        return new SprayPatternConfig
        {
            rangeMultiplier = concentratedRangeMultiplier,
            radiusMultiplier = concentratedRadiusMultiplier,
            effectivenessMultiplier = concentratedEffectivenessMultiplier,
            vfxSpeedMultiplier = concentratedVfxSpeedMultiplier,
            vfxSpreadMultiplier = concentratedVfxSpreadMultiplier
        };
    }

    private float GetPressure01()
    {
        if (Mathf.Approximately(minPressureMultiplier, maxPressureMultiplier))
        {
            return 1f;
        }

        return Mathf.InverseLerp(minPressureMultiplier, maxPressureMultiplier, pressureMultiplier);
    }

    private void HandleRuntimeTuningInput()
    {
        if (!allowRuntimeTuning)
        {
            return;
        }

        if (tuningOnlyWhileSpraying && !isSpraying)
        {
            return;
        }

        bool changed = false;

        if (WasIncreasePressurePressedThisFrame())
        {
            pressureMultiplier = Mathf.Clamp(
                pressureMultiplier + pressureStep,
                minPressureMultiplier,
                maxPressureMultiplier);
            changed = true;
        }

        if (WasDecreasePressurePressedThisFrame())
        {
            pressureMultiplier = Mathf.Clamp(
                pressureMultiplier - pressureStep,
                minPressureMultiplier,
                maxPressureMultiplier);
            changed = true;
        }

        if (WasTogglePatternPressedThisFrame())
        {
            sprayPattern = sprayPattern == SprayPattern.Concentrated
                ? SprayPattern.Wide
                : SprayPattern.Concentrated;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RecalculateSprayRuntimeValues();
        ApplySprayTuningToVfx();

        if (showTuningLogs)
        {
            Debug.Log(
                $"FireHose tune -> Pattern: {sprayPattern}, Pressure: {pressureMultiplier:0.00}x",
                this);
        }
    }

    private static bool WasTogglePatternPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.V))
        {
            return true;
        }
#endif

        return false;
    }

    private static bool WasIncreasePressurePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.rightBracketKey.wasPressedThisFrame || keyboard.equalsKey.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals))
        {
            return true;
        }
#endif

        return false;
    }

    private static bool WasDecreasePressurePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.leftBracketKey.wasPressedThisFrame || keyboard.minusKey.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus))
        {
            return true;
        }
#endif

        return false;
    }

    private void CacheWaterParticleDefaults()
    {
        particleDefaultsCached = false;
        if (waterParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = waterParticles.main;
        baseParticleStartSpeedMultiplier = Mathf.Max(0.01f, main.startSpeedMultiplier);

        ParticleSystem.ShapeModule shape = waterParticles.shape;
        baseParticleShapeAngle = Mathf.Max(0.01f, shape.angle);
        particleDefaultsCached = true;
    }

    private void ApplySprayTuningToVfx()
    {
        if (waterParticles == null || !particleDefaultsCached)
        {
            return;
        }

        SprayPatternConfig config = GetCurrentPatternConfig();
        float pressureT = GetPressure01();
        float pressureSpeedMultiplier = Mathf.Lerp(0.8f, 1.25f, pressureT);
        float pressureSpreadMultiplier = Mathf.Lerp(1.1f, 0.85f, pressureT);

        ParticleSystem.MainModule main = waterParticles.main;
        main.startSpeedMultiplier =
            baseParticleStartSpeedMultiplier * config.vfxSpeedMultiplier * pressureSpeedMultiplier;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(particleGravityModifier);

        ParticleSystem.ShapeModule shape = waterParticles.shape;
        shape.angle = baseParticleShapeAngle * config.vfxSpreadMultiplier * pressureSpreadMultiplier;
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
