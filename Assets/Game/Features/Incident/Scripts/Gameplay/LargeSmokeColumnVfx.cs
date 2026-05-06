using System.Collections;
using StarterAssets;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LargeSmokeColumnVfx : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField, Min(0.1f)] private float columnScale = 0.65f;
    [SerializeField, Min(0f)] private float emissionStrength = 1f;
    [SerializeField, Min(0.05f)] private float particleBudgetMultiplier = 1f;
    [SerializeField] private Vector3 windDirection = new Vector3(0.35f, 0f, 0.12f);
    [SerializeField, Min(0f)] private float windSpeed = 1.25f;

    [Header("Incident Placement")]
    [SerializeField] private bool autoPlaceFromIncident = true;
    [SerializeField] private bool placeAtAnchorBoundsCenter;
    [SerializeField] private Vector3 exteriorDirection = new Vector3(0f, 0f, -1f);
    [SerializeField, Min(0f)] private float exteriorOffset = 2.5f;
    [SerializeField, Range(0f, 1f)] private float verticalBoundsBias = 0.72f;
    [SerializeField] private Vector3 placementOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField, Min(0f)] private float placementRetryDuration = 4f;

    [Header("Smoke")]
    [SerializeField] private Material smokeMaterial;
    [SerializeField] private Material coreOcclusionMaterial;
    [SerializeField] private bool includeCoreOcclusion = true;
    [SerializeField] private bool alignParticleShapeToLocalY;
    [SerializeField, Min(0.05f)] private float smokeLifetimeMultiplier = 1f;
    [SerializeField, Min(0f)] private float smokeRiseMultiplier = 1f;
    [SerializeField] private bool straightenWindOverLifetime;
    [SerializeField, Range(0f, 1f)] private float windEndStrength = 0.15f;
    [SerializeField] private bool linkToSmokeDensity = true;
    [SerializeField, Range(0f, 1f)] private float smokeDensityVisibleThreshold = 0.08f;
    [SerializeField, Range(0.01f, 1f)] private float fullStrengthSmokeDensity = 0.65f;
    [SerializeField, Range(0f, 1f)] private float minimumVisibleEmissionScale = 0.18f;
    [SerializeField] private bool includeWindTrail;
    [SerializeField] private Color baseSmokeColor = new Color(0.02f, 0.02f, 0.018f, 0.96f);
    [SerializeField] private Color coreSmokeColor = new Color(0.005f, 0.005f, 0.004f, 1f);
    [SerializeField] private Color upperSmokeColor = new Color(0.045f, 0.045f, 0.04f, 0.86f);
    [SerializeField] private Color trailSmokeColor = new Color(0.075f, 0.075f, 0.07f, 0.68f);
    [SerializeField, Range(0.05f, 0.95f)] private float coreAlphaCutoff = 0.36f;

    [Header("Fire Detail")]
    [SerializeField] private bool includeBaseGlow;
    [SerializeField] private bool includeEmbers;
    [SerializeField] private Color glowColor = new Color(1f, 0.34f, 0.08f, 0.55f);
    [SerializeField] private Color emberColor = new Color(1f, 0.42f, 0.08f, 1f);

    [Header("Optimization")]
    [SerializeField] private bool disableParticlesWhenViewerNear;
    [SerializeField, Min(0f)] private float nearViewerDisableDistance = 18f;
    [SerializeField, Min(0f)] private float nearViewerEnableDistance = 24f;
    [SerializeField, Min(0.05f)] private float playerDistanceCheckInterval = 1f;

    [Header("Runtime")]
    [SerializeField] private ParticleSystem coreOcclusionSmoke;
    [SerializeField] private ParticleSystem baseDenseSmoke;
    [SerializeField] private ParticleSystem upperBillowsSmoke;
    [SerializeField] private ParticleSystem windTrailSmoke;
    [SerializeField] private ParticleSystem emberSparks;
    [SerializeField] private Light baseGlowLight;

    private Material runtimeSmokeMaterial;
    private Material runtimeCoreOcclusionMaterial;
    private Texture2D runtimeSmokeTexture;
    private Coroutine placementRoutine;
    private SmokeHazard linkedSmokeHazard;
    private ISmokeVentPoint linkedVentPoint;
    private bool densityDrivenParticlesPlaying = true;
    private Transform trackedPlayer;
    private float nextPlayerDistanceCheckTime;
    private bool nearPlayerCulled;

    private void OnEnable()
    {
        BuildOrRefresh();
    }

    private void Start()
    {
        if (!Application.isPlaying || !autoPlaceFromIncident)
        {
            return;
        }

        placementRoutine = StartCoroutine(PlaceFromIncidentWhenReady());
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateNearPlayerCulling();

        if (linkToSmokeDensity)
        {
            ApplySmokeDensity(linkedSmokeHazard != null ? linkedSmokeHazard.CurrentSmokeDensity : 0f);
        }
    }

    private void OnValidate()
    {
        columnScale = Mathf.Max(0.1f, columnScale);
        emissionStrength = Mathf.Max(0f, emissionStrength);
        particleBudgetMultiplier = Mathf.Max(0.05f, particleBudgetMultiplier);
        smokeLifetimeMultiplier = Mathf.Max(0.05f, smokeLifetimeMultiplier);
        smokeRiseMultiplier = Mathf.Max(0f, smokeRiseMultiplier);
        windEndStrength = Mathf.Clamp01(windEndStrength);
        nearViewerDisableDistance = Mathf.Max(0f, nearViewerDisableDistance);
        nearViewerEnableDistance = Mathf.Max(nearViewerDisableDistance, nearViewerEnableDistance);
        playerDistanceCheckInterval = Mathf.Max(0.05f, playerDistanceCheckInterval);
        windSpeed = Mathf.Max(0f, windSpeed);
        coreAlphaCutoff = Mathf.Clamp(coreAlphaCutoff, 0.05f, 0.95f);
        smokeDensityVisibleThreshold = Mathf.Clamp01(smokeDensityVisibleThreshold);
        fullStrengthSmokeDensity = Mathf.Clamp(fullStrengthSmokeDensity, 0.01f, 1f);
        minimumVisibleEmissionScale = Mathf.Clamp01(minimumVisibleEmissionScale);
        exteriorOffset = Mathf.Max(0f, exteriorOffset);
        verticalBoundsBias = Mathf.Clamp01(verticalBoundsBias);
        placementRetryDuration = Mathf.Max(0f, placementRetryDuration);

        if (isActiveAndEnabled)
        {
            BuildOrRefresh();
        }
    }

    private void OnDisable()
    {
        if (placementRoutine != null)
        {
            StopCoroutine(placementRoutine);
            placementRoutine = null;
        }

        ReleaseRuntimeAssets();
    }

    [ContextMenu("Rebuild Smoke Column")]
    public void BuildOrRefresh()
    {
        Material resolvedSmokeMaterial = ResolveSmokeMaterial();

        if (includeCoreOcclusion)
        {
            coreOcclusionSmoke = EnsureParticleSystem("Core Occlusion Smoke", coreOcclusionSmoke);
            ConfigureSmokeParticleSystem(
                coreOcclusionSmoke,
                ResolveCoreOcclusionMaterial(),
                coreSmokeColor,
                lifetime: new Vector2(7f, 12f),
                speed: new Vector2(4.5f, 7f),
                size: new Vector2(5.5f, 11.5f),
                rate: 10f,
                shapeRadius: 2.6f,
                shapeAngle: 8f,
                upwardMultiplier: 0.85f,
                windMultiplier: 0.18f,
                maxParticles: 180);
        }
        else if (coreOcclusionSmoke != null)
        {
            coreOcclusionSmoke.gameObject.SetActive(false);
        }

        baseDenseSmoke = EnsureParticleSystem("Base Dense Smoke", baseDenseSmoke);
        upperBillowsSmoke = EnsureParticleSystem("Upper Billows Smoke", upperBillowsSmoke);
        if (includeWindTrail)
        {
            windTrailSmoke = EnsureParticleSystem("Wind Trail Smoke", windTrailSmoke);
        }
        else if (windTrailSmoke != null)
        {
            windTrailSmoke.gameObject.SetActive(false);
        }

        ConfigureSmokeParticleSystem(
            baseDenseSmoke,
            resolvedSmokeMaterial,
            baseSmokeColor,
            lifetime: new Vector2(8f, 14f),
            speed: new Vector2(5.5f, 9f),
            size: new Vector2(7f, 15f),
            rate: 18f,
            shapeRadius: 4f,
            shapeAngle: 13f,
            upwardMultiplier: 1f,
            windMultiplier: 0.35f,
            maxParticles: 360);

        ConfigureSmokeParticleSystem(
            upperBillowsSmoke,
            resolvedSmokeMaterial,
            upperSmokeColor,
            lifetime: new Vector2(16f, 26f),
            speed: new Vector2(3.5f, 6f),
            size: new Vector2(18f, 38f),
            rate: 5.5f,
            shapeRadius: 8f,
            shapeAngle: 20f,
            upwardMultiplier: 0.8f,
            windMultiplier: 0.9f,
            maxParticles: 180);

        if (includeWindTrail)
        {
            ConfigureSmokeParticleSystem(
                windTrailSmoke,
                resolvedSmokeMaterial,
                trailSmokeColor,
                lifetime: new Vector2(20f, 34f),
                speed: new Vector2(2f, 4.5f),
                size: new Vector2(28f, 58f),
                rate: 2.4f,
                shapeRadius: 10f,
                shapeAngle: 28f,
                upwardMultiplier: 0.45f,
                windMultiplier: 1.65f,
                maxParticles: 110);
        }

        if (includeEmbers)
        {
            emberSparks = EnsureParticleSystem("Ember Sparks", emberSparks);
            ConfigureEmbers(emberSparks);
        }
        else if (emberSparks != null)
        {
            emberSparks.gameObject.SetActive(false);
        }

        if (includeBaseGlow)
        {
            baseGlowLight = EnsureBaseGlowLight();
            ConfigureBaseGlowLight(baseGlowLight);
        }
        else if (baseGlowLight != null)
        {
            baseGlowLight.gameObject.SetActive(false);
        }
    }

    private ParticleSystem EnsureParticleSystem(string childName, ParticleSystem existing)
    {
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            child = childObject.transform;
        }

        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            particleSystem = child.gameObject.AddComponent<ParticleSystem>();
        }

        if (child.GetComponent<ParticleSystemRenderer>() == null)
        {
            child.gameObject.AddComponent<ParticleSystemRenderer>();
        }

        child.gameObject.SetActive(true);
        return particleSystem;
    }

    private void ConfigureSmokeParticleSystem(
        ParticleSystem particleSystem,
        Material material,
        Color color,
        Vector2 lifetime,
        Vector2 speed,
        Vector2 size,
        float rate,
        float shapeRadius,
        float shapeAngle,
        float upwardMultiplier,
        float windMultiplier,
        int maxParticles)
    {
        if (particleSystem == null)
        {
            return;
        }

        AlignParticleShapeTransform(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            lifetime.x * smokeLifetimeMultiplier,
            lifetime.y * smokeLifetimeMultiplier);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x * columnScale, speed.y * columnScale);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x * columnScale, size.y * columnScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = -0.04f;
        main.maxParticles = Mathf.Max(
            8,
            Mathf.RoundToInt(maxParticles * Mathf.Max(0.1f, emissionStrength) * particleBudgetMultiplier));

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = rate * emissionStrength;
        emission.rateOverDistance = 0f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = shapeAngle;
        shape.radius = shapeRadius * columnScale;
        shape.radiusThickness = 0.72f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        Vector3 wind = ResolveWindVector() * windMultiplier;
        float yMin = 2.5f * columnScale * upwardMultiplier * smokeRiseMultiplier;
        float yMax = 5.5f * columnScale * upwardMultiplier * smokeRiseMultiplier;
        if (straightenWindOverLifetime)
        {
            velocity.x = CreateWindStraighteningVelocityCurve(wind.x * 0.65f, wind.x * 1.35f);
            velocity.y = CreateConstantVelocityCurve(yMin, yMax);
            velocity.z = CreateWindStraighteningVelocityCurve(wind.z * 0.65f, wind.z * 1.35f);
        }
        else
        {
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.65f, wind.x * 1.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.65f, wind.z * 1.35f);
        }

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 1.65f * columnScale;
        noise.frequency = 0.11f;
        noise.scrollSpeed = 0.23f;
        noise.octaveCount = 3;
        noise.quality = ParticleSystemNoiseQuality.High;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.2f, 0.9f),
            new Keyframe(0.78f, 1.35f),
            new Keyframe(1f, 1.65f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f), 0f),
                new GradientColorKey(color, 0.45f),
                new GradientColorKey(new Color(color.r * 1.18f, color.g * 1.18f, color.b * 1.18f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.08f),
                new GradientAlphaKey(color.a * 0.92f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.material = material;

        if (!particleSystem.isPlaying && Application.isPlaying)
        {
            particleSystem.Play(true);
        }
    }

    private void ConfigureEmbers(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.gameObject.SetActive(true);
        AlignParticleShapeTransform(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f * columnScale, 18f * columnScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * columnScale, 0.24f * columnScale);
        main.startColor = emberColor;
        main.gravityModifier = 0.18f;
        main.maxParticles = Mathf.RoundToInt(90 * Mathf.Max(0.1f, emissionStrength) * particleBudgetMultiplier);

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f * emissionStrength;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 2f * columnScale;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        Vector3 wind = ResolveWindVector() * 0.4f;
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.85f, wind.x * 1.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 2.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.85f, wind.z * 1.15f);

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.material = ResolveEmberMaterial();

        if (!particleSystem.isPlaying && Application.isPlaying)
        {
            particleSystem.Play(true);
        }
    }

    private Light EnsureBaseGlowLight()
    {
        if (baseGlowLight != null)
        {
            baseGlowLight.gameObject.SetActive(true);
            return baseGlowLight;
        }

        Transform child = transform.Find("Base Fire Glow");
        if (child == null)
        {
            GameObject lightObject = new GameObject("Base Fire Glow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f * columnScale, 0f);
            child = lightObject.transform;
        }

        Light targetLight = child.GetComponent<Light>();
        if (targetLight == null)
        {
            targetLight = child.gameObject.AddComponent<Light>();
        }

        targetLight.gameObject.SetActive(true);
        return targetLight;
    }

    private void ConfigureBaseGlowLight(Light targetLight)
    {
        if (targetLight == null)
        {
            return;
        }

        targetLight.type = LightType.Point;
        targetLight.color = glowColor;
        targetLight.intensity = 2.8f * emissionStrength;
        targetLight.range = 12f * columnScale;
        targetLight.shadows = LightShadows.None;
    }

    private Vector3 ResolveWindVector()
    {
        Vector3 direction = windDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.right;
        }

        return direction.normalized * windSpeed * columnScale;
    }

    private ParticleSystem.MinMaxCurve CreateWindStraighteningVelocityCurve(float minStart, float maxStart)
    {
        return new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, minStart),
                new Keyframe(0.28f, minStart * 0.82f),
                new Keyframe(1f, minStart * windEndStrength)),
            new AnimationCurve(
                new Keyframe(0f, maxStart),
                new Keyframe(0.28f, maxStart * 0.82f),
                new Keyframe(1f, maxStart * windEndStrength)));
    }

    private static ParticleSystem.MinMaxCurve CreateConstantVelocityCurve(float minValue, float maxValue)
    {
        return new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.Constant(0f, 1f, minValue),
            AnimationCurve.Constant(0f, 1f, maxValue));
    }

    private void AlignParticleShapeTransform(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.transform.localRotation = alignParticleShapeToLocalY
            ? Quaternion.FromToRotation(Vector3.forward, Vector3.up)
            : Quaternion.identity;
    }

    private IEnumerator PlaceFromIncidentWhenReady()
    {
        float timeoutAt = Time.unscaledTime + placementRetryDuration;
        while (Time.unscaledTime <= timeoutAt)
        {
            if (TryPlaceFromResolvedIncident())
            {
                placementRoutine = null;
                yield break;
            }

            yield return null;
        }

        TryPlaceFromPendingPayload();
        placementRoutine = null;
    }

    private bool TryPlaceFromResolvedIncident()
    {
        IncidentMapSetupRoot setupRoot = FindAnyObjectByType<IncidentMapSetupRoot>(FindObjectsInactive.Include);
        if (setupRoot == null || setupRoot.LastResolvedAnchor == null)
        {
            return false;
        }

        PlaceNearAnchor(setupRoot.LastResolvedAnchor);
        return true;
    }

    private bool TryPlaceFromPendingPayload()
    {
        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
            return false;
        }

        IncidentPayloadAnchor anchor = IncidentAnchorHazardMapSetupTask.ResolveBestAnchor(
            payload,
            FindObjectsByType<IncidentPayloadAnchor>(FindObjectsInactive.Include));
        if (anchor == null)
        {
            return false;
        }

        PlaceNearAnchor(anchor);
        return true;
    }

    public void PlaceNearAnchor(IncidentPayloadAnchor anchor)
    {
        if (anchor == null)
        {
            return;
        }

        Bounds bounds = ResolveAnchorBounds(anchor);
        Vector3 position = bounds.center;
        position.y = Mathf.Lerp(bounds.min.y, bounds.max.y, verticalBoundsBias);
        if (!placeAtAnchorBoundsCenter)
        {
            Vector3 direction = ResolveExteriorDirection();
            Vector3 halfExtents = bounds.extents;
            float directionalExtent = Mathf.Abs(direction.x) * halfExtents.x + Mathf.Abs(direction.z) * halfExtents.z;
            position += direction * (directionalExtent + exteriorOffset);
        }

        position += placementOffset;
        transform.position = position;
    }

    public void PlaceAtVentPoint(Component ventPoint, Vector3 localDirection, float upwardBias, float surfaceOffset)
    {
        if (ventPoint == null)
        {
            return;
        }

        Transform sourceTransform = ventPoint.transform;
        Vector3 outwardDirection = sourceTransform.TransformDirection(localDirection);
        if (outwardDirection.sqrMagnitude <= 0.0001f)
        {
            outwardDirection = sourceTransform.forward;
        }

        if (outwardDirection.sqrMagnitude <= 0.0001f)
        {
            outwardDirection = Vector3.forward;
        }

        outwardDirection.Normalize();
        Vector3 direction = Vector3.Lerp(outwardDirection, Vector3.up, Mathf.Clamp01(upwardBias));
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.up;
        }

        direction.Normalize();
        Renderer renderer = ventPoint.GetComponent<Renderer>();
        Vector3 position = renderer != null ? renderer.bounds.center : sourceTransform.position;
        transform.position = position + direction * Mathf.Max(0f, surfaceOffset);
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public void BindSmokeHazard(SmokeHazard smokeHazard)
    {
        linkedSmokeHazard = smokeHazard;

        if (!Application.isPlaying || !linkToSmokeDensity)
        {
            return;
        }

        ApplySmokeDensity(linkedSmokeHazard != null ? linkedSmokeHazard.CurrentSmokeDensity : 0f);
    }

    public void BindSmokeVentPoint(ISmokeVentPoint ventPoint)
    {
        linkedVentPoint = ventPoint;

        if (!Application.isPlaying || !linkToSmokeDensity)
        {
            return;
        }

        ApplySmokeDensity(linkedSmokeHazard != null ? linkedSmokeHazard.CurrentSmokeDensity : 0f);
    }

    private void ApplySmokeDensity(float density)
    {
        float clampedDensity = Mathf.Clamp01(density);
        float ventScale = linkedVentPoint != null
            ? Mathf.Clamp01(Mathf.Max(0f, linkedVentPoint.SmokeVentilationRelief))
            : 1f;
        float effectiveDensity = clampedDensity * ventScale;
        bool shouldEmit = effectiveDensity >= smokeDensityVisibleThreshold && !nearPlayerCulled;
        if (shouldEmit != densityDrivenParticlesPlaying)
        {
            densityDrivenParticlesPlaying = shouldEmit;
            SetParticleEmissionActive(coreOcclusionSmoke, shouldEmit);
            SetParticleEmissionActive(baseDenseSmoke, shouldEmit);
            SetParticleEmissionActive(upperBillowsSmoke, shouldEmit);
            SetParticleEmissionActive(windTrailSmoke, shouldEmit && includeWindTrail);
            SetParticleEmissionActive(emberSparks, shouldEmit && includeEmbers);
        }

        float strength = shouldEmit
            ? Mathf.Lerp(
                minimumVisibleEmissionScale,
                1f,
                Mathf.InverseLerp(smokeDensityVisibleThreshold, fullStrengthSmokeDensity, effectiveDensity))
            : 0f;

        ApplyEmissionScale(coreOcclusionSmoke, 10f, strength);
        ApplyEmissionScale(baseDenseSmoke, 18f, strength);
        ApplyEmissionScale(upperBillowsSmoke, 5.5f, strength);
        ApplyEmissionScale(windTrailSmoke, 2.4f, includeWindTrail ? strength : 0f);
        ApplyEmissionScale(emberSparks, 8f, strength);

        if (baseGlowLight != null)
        {
            baseGlowLight.enabled = shouldEmit && includeBaseGlow;
            baseGlowLight.intensity = 2.8f * emissionStrength * strength;
        }
    }

    private void UpdateNearPlayerCulling()
    {
        if (!disableParticlesWhenViewerNear)
        {
            if (nearPlayerCulled)
            {
                nearPlayerCulled = false;
            }

            return;
        }

        if (Time.unscaledTime < nextPlayerDistanceCheckTime)
        {
            return;
        }

        nextPlayerDistanceCheckTime = Time.unscaledTime + playerDistanceCheckInterval;

        trackedPlayer = ResolvePlayerTransform();
        if (trackedPlayer == null)
        {
            if (nearPlayerCulled)
            {
                nearPlayerCulled = false;
            }

            return;
        }

        float sqrDistance = (trackedPlayer.position - transform.position).sqrMagnitude;
        float disableDistance = nearViewerDisableDistance;
        float enableDistance = Mathf.Max(disableDistance, nearViewerEnableDistance);
        nearPlayerCulled = nearPlayerCulled
            ? sqrDistance <= enableDistance * enableDistance
            : sqrDistance <= disableDistance * disableDistance;
    }

    private static Transform ResolvePlayerTransform()
    {
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller != null)
        {
            return controller.transform;
        }

        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>(FindObjectsInactive.Exclude);
        if (vitals != null)
        {
            return vitals.transform;
        }

        PlayerHazardExposure exposure = FindAnyObjectByType<PlayerHazardExposure>(FindObjectsInactive.Exclude);
        return exposure != null ? exposure.transform : null;
    }

    private void SetParticleEmissionActive(ParticleSystem particleSystem, bool active)
    {
        if (particleSystem == null)
        {
            return;
        }

        if (active)
        {
            if (!particleSystem.isPlaying)
            {
                particleSystem.Play(true);
            }

            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ApplyEmissionScale(ParticleSystem particleSystem, float baseRate, float densityScale)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = baseRate * emissionStrength * Mathf.Clamp01(densityScale);
    }

    private Bounds ResolveAnchorBounds(IncidentPayloadAnchor anchor)
    {
        Collider anchorCollider = anchor.GetComponent<Collider>();
        if (anchorCollider != null)
        {
            return anchorCollider.bounds;
        }

        Collider childCollider = anchor.GetComponentInChildren<Collider>(true);
        if (childCollider != null)
        {
            return childCollider.bounds;
        }

        return new Bounds(anchor.transform.position, anchor.RuntimeZoneSize);
    }

    private Vector3 ResolveExteriorDirection()
    {
        Vector3 direction = exteriorDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.back;
        }

        return direction.normalized;
    }

    private Material ResolveSmokeMaterial()
    {
        if (smokeMaterial != null)
        {
            return smokeMaterial;
        }

        if (runtimeSmokeMaterial == null)
        {
            Shader shader = ResolveParticleShader();

            runtimeSmokeMaterial = new Material(shader)
            {
                name = "Runtime Large Smoke Material",
                hideFlags = HideFlags.DontSave
            };
            runtimeSmokeTexture = CreateSmokeTexture();
            ApplyTexture(runtimeSmokeMaterial, runtimeSmokeTexture);
            ConfigureTransparentParticleMaterial(runtimeSmokeMaterial, Color.white);
        }

        return runtimeSmokeMaterial;
    }

    private Material ResolveCoreOcclusionMaterial()
    {
        if (coreOcclusionMaterial != null)
        {
            return coreOcclusionMaterial;
        }

        if (runtimeCoreOcclusionMaterial == null)
        {
            Shader shader = ResolveParticleShader();

            runtimeCoreOcclusionMaterial = new Material(shader)
            {
                name = "Runtime Core Smoke Occlusion Material",
                hideFlags = HideFlags.DontSave
            };

            if (runtimeSmokeTexture == null)
            {
                runtimeSmokeTexture = CreateSmokeTexture();
            }

            ApplyTexture(runtimeCoreOcclusionMaterial, runtimeSmokeTexture);
            ConfigureCutoutParticleMaterial(runtimeCoreOcclusionMaterial, coreSmokeColor, coreAlphaCutoff);
        }

        return runtimeCoreOcclusionMaterial;
    }

    private Material ResolveEmberMaterial()
    {
        Shader shader = ResolveParticleShader();

        Material material = new Material(shader)
        {
            name = "Runtime Ember Material",
            hideFlags = HideFlags.DontSave
        };
        ConfigureTransparentParticleMaterial(material, emberColor);
        return material;
    }

    private static Shader ResolveParticleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }

    private static void ConfigureTransparentParticleMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");

        SetMaterialFloat(material, "_Surface", 1f);
        SetMaterialFloat(material, "_Blend", 0f);
        SetMaterialFloat(material, "_AlphaClip", 0f);
        SetMaterialFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetMaterialFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetMaterialFloat(material, "_ZWrite", 0f);
        SetMaterialFloat(material, "_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void ConfigureCutoutParticleMaterial(Material material, Color color, float cutoff)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.SetOverrideTag("Queue", "AlphaTest");

        SetMaterialFloat(material, "_Surface", 0f);
        SetMaterialFloat(material, "_AlphaClip", 1f);
        SetMaterialFloat(material, "_Cutoff", cutoff);
        SetMaterialFloat(material, "_AlphaCutoff", cutoff);
        SetMaterialFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetMaterialFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        SetMaterialFloat(material, "_ZWrite", 1f);
        SetMaterialFloat(material, "_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void ApplyTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static Texture2D CreateSmokeTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Runtime Soft Smoke Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float softEdge = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.62f, 1f, distance));
                float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.18f, 0.72f, distance));
                float noise = Mathf.PerlinNoise(u * 6.5f + 17.3f, v * 6.5f + 41.7f);
                float alpha = Mathf.Clamp01(Mathf.Lerp(softEdge * 0.62f, core, 0.72f) * Mathf.Lerp(0.82f, 1f, noise));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(true, true);
        return texture;
    }

    private void ReleaseRuntimeAssets()
    {
        if (Application.isPlaying)
        {
            if (runtimeSmokeMaterial != null)
            {
                Destroy(runtimeSmokeMaterial);
            }

            if (runtimeCoreOcclusionMaterial != null)
            {
                Destroy(runtimeCoreOcclusionMaterial);
            }

            if (runtimeSmokeTexture != null)
            {
                Destroy(runtimeSmokeTexture);
            }
        }
        else
        {
            if (runtimeSmokeMaterial != null)
            {
                DestroyImmediate(runtimeSmokeMaterial);
            }

            if (runtimeCoreOcclusionMaterial != null)
            {
                DestroyImmediate(runtimeCoreOcclusionMaterial);
            }

            if (runtimeSmokeTexture != null)
            {
                DestroyImmediate(runtimeSmokeTexture);
            }
        }

        runtimeSmokeMaterial = null;
        runtimeCoreOcclusionMaterial = null;
        runtimeSmokeTexture = null;
    }
}
