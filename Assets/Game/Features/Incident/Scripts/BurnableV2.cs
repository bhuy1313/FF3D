using System.Collections.Generic;
using RayFire;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RayfireRigid))]
public sealed class BurnableV2 : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BurnAmountId = Shader.PropertyToID("_BurnAmount");

    private enum BurnStage
    {
        Intact = 0,
        Heating = 1,
        Demolished = 2,
        Consumed = 3
    }

    private sealed class ScorchRendererState
    {
        public Renderer Renderer;
        public int ColorPropertyId;
        public Color InitialColor;
    }

    [Header("References")]
    [SerializeField] private RayfireRigid rayfireRigid;
    [SerializeField] private FireSimulationManager fireSimulationManager;
    [SerializeField] private Collider[] exposureColliders;
    [SerializeField] private Renderer[] scorchRenderers;

    [Header("Exposure")]
    [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
    [SerializeField, Min(0f)] private float fireExposureBoundsPadding = 0.6f;
    [SerializeField, Min(0f)] private float fallbackNearestFireSearchRadius = 1.75f;
    [SerializeField, Min(0)] private int minimumBurningNodeCount = 1;
    [SerializeField, Range(0f, 1f)] private float minimumAverageIntensity01 = 0.12f;
    [SerializeField, Min(0f)] private float progressPerBurningNodePerSecond = 0.08f;
    [SerializeField, Min(0f)] private float progressPerIntensityUnitPerSecond = 0.16f;
    [SerializeField, Min(0f)] private float cooldownPerSecond = 0.035f;

    [Header("RayFire")]
    [SerializeField] private bool autoConfigureRayfireDamage = true;
    [SerializeField, Min(1f)] private float rayfireDamageThreshold = 100f;
    [SerializeField, Min(0.1f)] private float rayfireDamagePerPulse = 34f;
    [SerializeField, Range(0.05f, 1f)] private float burnProgressPerPulse = 0.22f;
    [SerializeField] private bool forceDemolishWhenFullyConsumed = true;
    [SerializeField] private bool consumeFragmentsAfterDemolition = true;
    [SerializeField, Range(0.02f, 1f)] private float burnProgressPerFragmentConsume = 0.08f;
    [SerializeField] private FadeType consumedFragmentFadeType = FadeType.ScaleDown;
    [SerializeField, Min(0f)] private float consumedFragmentFadeTime = 1.1f;
    [SerializeField, Min(0f)] private float consumedFragmentLifeTime = 0f;

    [Header("Visuals")]
    [SerializeField] private bool updateScorchVisuals = true;
    [SerializeField] private Color scorchColor = new Color(0.12f, 0.1f, 0.08f, 1f);
    [SerializeField, Range(0f, 1f)] private float scorchBlend = 0.85f;
    [SerializeField, Min(0f)] private float burnAmountScale = 1f;

    [Header("Runtime")]
    [SerializeField, Range(0f, 1f)] private float burnProgress;
    [SerializeField] private BurnStage burnStage = BurnStage.Intact;
    [SerializeField] private int lastBurningNodeCount;
    [SerializeField, Range(0f, 1f)] private float lastAverageIntensity01;
    [SerializeField] private float accumulatedRayfireDamage;
    [SerializeField] private int consumedFragmentCount;

    private readonly List<ScorchRendererState> scorchStates = new List<ScorchRendererState>();
    private readonly List<FireRuntimeNode> burningNodesBuffer = new List<FireRuntimeNode>();

    private MaterialPropertyBlock propertyBlock;

    private float nextUpdateTime;
    private float nextBurnProgressPulse;
    private float nextFragmentConsumeProgress;
    private bool runtimeStateInitialized;

    public float BurnProgress => burnProgress;
    public bool IsBurningAway => burnStage != BurnStage.Intact;
    public bool IsFullyConsumed => burnStage == BurnStage.Consumed;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveReferences();
        CaptureScorchRendererState();
        ConfigureRayfireDamageIfNeeded();
        SyncBurnStageFromRayfire();
        ApplyScorchVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureRayfireDamageIfNeeded();
        SyncBurnStageFromRayfire();
        nextUpdateTime = Time.time;
    }

    private void OnValidate()
    {
        updateInterval = Mathf.Max(0.05f, updateInterval);
        fireExposureBoundsPadding = Mathf.Max(0f, fireExposureBoundsPadding);
        fallbackNearestFireSearchRadius = Mathf.Max(0f, fallbackNearestFireSearchRadius);
        minimumBurningNodeCount = Mathf.Max(1, minimumBurningNodeCount);
        progressPerBurningNodePerSecond = Mathf.Max(0f, progressPerBurningNodePerSecond);
        progressPerIntensityUnitPerSecond = Mathf.Max(0f, progressPerIntensityUnitPerSecond);
        cooldownPerSecond = Mathf.Max(0f, cooldownPerSecond);
        rayfireDamageThreshold = Mathf.Max(1f, rayfireDamageThreshold);
        rayfireDamagePerPulse = Mathf.Max(0.1f, rayfireDamagePerPulse);
        consumedFragmentFadeTime = Mathf.Max(0f, consumedFragmentFadeTime);
        consumedFragmentLifeTime = Mathf.Max(0f, consumedFragmentLifeTime);
        burnAmountScale = Mathf.Max(0f, burnAmountScale);
    }

    private void Update()
    {
        if (Time.time < nextUpdateTime)
        {
            return;
        }

        float deltaTime = runtimeStateInitialized
            ? Mathf.Max(0.01f, Time.time - (nextUpdateTime - updateInterval))
            : updateInterval;
        runtimeStateInitialized = true;
        nextUpdateTime = Time.time + updateInterval;

        TickBurnState(deltaTime);
    }

    private void TickBurnState(float deltaTime)
    {
        SyncBurnStageFromRayfire();

        Bounds exposureBounds = ResolveExposureBounds();
        bool isExposedToFire = TrySampleFireExposure(exposureBounds, out int burningNodeCount, out float averageIntensity01, out Vector3 burnOrigin);

        lastBurningNodeCount = burningNodeCount;
        lastAverageIntensity01 = averageIntensity01;

        if (isExposedToFire)
        {
            float gain = (burningNodeCount * progressPerBurningNodePerSecond)
                + (averageIntensity01 * progressPerIntensityUnitPerSecond);
            burnProgress = Mathf.Clamp01(burnProgress + gain * deltaTime);

            if (burnStage == BurnStage.Intact)
            {
                burnStage = BurnStage.Heating;
            }

            TryApplyRayfirePulse(burnOrigin, exposureBounds);
            TryForceDemolishAtFullProgress(burnOrigin);
            TryConsumeDemolishedFragments();
        }
        else
        {
            burnProgress = Mathf.MoveTowards(burnProgress, 0f, cooldownPerSecond * deltaTime);
        }

        ApplyScorchVisuals();
    }

    private void TryApplyRayfirePulse(Vector3 burnOrigin, Bounds exposureBounds)
    {
        if (rayfireRigid == null || burnStage >= BurnStage.Demolished)
        {
            return;
        }

        if (burnProgress + 0.0001f < nextBurnProgressPulse)
        {
            return;
        }

        float radius = Mathf.Max(0.15f, exposureBounds.extents.magnitude);
        accumulatedRayfireDamage += rayfireDamagePerPulse;
        RFDamage.ApplyDamage(rayfireRigid, rayfireDamagePerPulse, burnOrigin, radius, null);
        nextBurnProgressPulse += Mathf.Max(0.01f, burnProgressPerPulse);
        SyncBurnStageFromRayfire();
    }

    private void TryForceDemolishAtFullProgress(Vector3 burnOrigin)
    {
        if (!forceDemolishWhenFullyConsumed || rayfireRigid == null || burnProgress < 0.999f)
        {
            return;
        }

        if (!rayfireRigid.limitations.demolished)
        {
            Vector3 direction = transform.position - burnOrigin;
            RayfireBreakImpact.DemolishWithImpact(rayfireRigid, null, burnOrigin, direction, false, RayfireBreakImpact.DirectionMode.ImpactDirection);
        }

        SyncBurnStageFromRayfire();
    }

    private void TryConsumeDemolishedFragments()
    {
        if (!consumeFragmentsAfterDemolition || rayfireRigid == null || burnStage < BurnStage.Demolished)
        {
            return;
        }

        while (burnProgress + 0.0001f >= nextFragmentConsumeProgress)
        {
            if (!TryConsumeSingleFragment())
            {
                burnStage = BurnStage.Consumed;
                break;
            }

            consumedFragmentCount++;
            nextFragmentConsumeProgress += Mathf.Max(0.01f, burnProgressPerFragmentConsume);
        }
    }

    private bool TryConsumeSingleFragment()
    {
        if (rayfireRigid.fragments == null || rayfireRigid.fragments.Count == 0)
        {
            return false;
        }

        RayfireRigid selectedFragment = null;
        float bestDistance = float.PositiveInfinity;
        Vector3 center = transform.position;

        for (int i = 0; i < rayfireRigid.fragments.Count; i++)
        {
            RayfireRigid fragment = rayfireRigid.fragments[i];
            if (fragment == null || !fragment.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (fragment.fading.state > 0)
            {
                continue;
            }

            float distance = (fragment.transform.position - center).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            selectedFragment = fragment;
        }

        if (selectedFragment == null)
        {
            return false;
        }

        selectedFragment.fading.fadeType = consumedFragmentFadeType;
        selectedFragment.fading.lifeType = RFFadeLifeType.ByLifeTime;
        selectedFragment.fading.lifeTime = consumedFragmentLifeTime;
        selectedFragment.fading.fadeTime = consumedFragmentFadeTime;
        selectedFragment.fading.sizeFilter = 0f;
        RFFade.FadeRigid(selectedFragment);
        return true;
    }

    private bool TrySampleFireExposure(Bounds exposureBounds, out int burningNodeCount, out float averageIntensity01, out Vector3 burnOrigin)
    {
        burningNodeCount = 0;
        averageIntensity01 = 0f;
        burnOrigin = transform.position;

        if (fireSimulationManager == null)
        {
            fireSimulationManager = FindAnyObjectByType<FireSimulationManager>();
        }

        if (fireSimulationManager == null)
        {
            return false;
        }

        Bounds paddedBounds = ExpandBounds(exposureBounds, fireExposureBoundsPadding);
        fireSimulationManager.GetBurningTrackedStats(paddedBounds, out burningNodeCount, out float intensitySum);
        if (burningNodeCount > 0)
        {
            averageIntensity01 = Mathf.Clamp01(intensitySum / Mathf.Max(1, burningNodeCount));
            if (burningNodeCount >= minimumBurningNodeCount && averageIntensity01 >= minimumAverageIntensity01)
            {
                burnOrigin = fireSimulationManager.GetClosestBurningNodePosition(paddedBounds, transform.position, transform.position);
                return true;
            }
        }

        if (fallbackNearestFireSearchRadius <= 0f)
        {
            return false;
        }

        Bounds fallbackBounds = new Bounds(transform.position, Vector3.one * fallbackNearestFireSearchRadius * 2f);
        fireSimulationManager.GetBurningNodes(fallbackBounds, burningNodesBuffer);
        if (burningNodesBuffer.Count == 0)
        {
            return false;
        }

        float bestDistanceSqr = fallbackNearestFireSearchRadius * fallbackNearestFireSearchRadius;
        FireRuntimeNode closestNode = null;
        for (int i = 0; i < burningNodesBuffer.Count; i++)
        {
            FireRuntimeNode node = burningNodesBuffer[i];
            if (node == null || !node.IsBurning)
            {
                continue;
            }

            float distanceSqr = (node.Position - transform.position).sqrMagnitude;
            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            closestNode = node;
        }

        if (closestNode == null)
        {
            return false;
        }

        burningNodeCount = 1;
        averageIntensity01 = Mathf.Clamp01(closestNode.Heat / Mathf.Max(0.01f, closestNode.IgnitionThreshold));
        if (averageIntensity01 < minimumAverageIntensity01)
        {
            return false;
        }

        burnOrigin = closestNode.Position;
        return true;
    }

    private Bounds ResolveExposureBounds()
    {
        if (exposureColliders == null || exposureColliders.Length == 0)
        {
            exposureColliders = GetComponentsInChildren<Collider>(true);
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.5f);

        if (exposureColliders != null)
        {
            for (int i = 0; i < exposureColliders.Length; i++)
            {
                Collider collider = exposureColliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(transform.position, Vector3.one);
        }

        return bounds;
    }

    private static Bounds ExpandBounds(Bounds bounds, float padding)
    {
        if (padding <= 0f)
        {
            return bounds;
        }

        bounds.Expand(padding * 2f);
        return bounds;
    }

    private void ResolveReferences()
    {
        if (rayfireRigid == null)
        {
            rayfireRigid = GetComponent<RayfireRigid>();
        }

        if (fireSimulationManager == null)
        {
            fireSimulationManager = FindAnyObjectByType<FireSimulationManager>();
        }
    }

    private void ConfigureRayfireDamageIfNeeded()
    {
        if (!autoConfigureRayfireDamage || rayfireRigid == null)
        {
            return;
        }

        rayfireRigid.damage.en = true;
        rayfireRigid.damage.max = rayfireDamageThreshold;
        rayfireRigid.damage.cur = Mathf.Clamp(rayfireRigid.damage.cur, 0f, rayfireDamageThreshold);
    }

    private void SyncBurnStageFromRayfire()
    {
        if (rayfireRigid == null)
        {
            return;
        }

        if (rayfireRigid.limitations.demolished || rayfireRigid.HasFragments)
        {
            burnStage = burnStage == BurnStage.Consumed ? BurnStage.Consumed : BurnStage.Demolished;
            nextFragmentConsumeProgress = Mathf.Max(nextFragmentConsumeProgress, Mathf.Max(0.01f, burnProgressPerFragmentConsume));
        }
        else if (burnProgress > 0.001f)
        {
            burnStage = BurnStage.Heating;
            nextBurnProgressPulse = Mathf.Max(nextBurnProgressPulse, Mathf.Max(0.01f, burnProgressPerPulse));
        }
        else
        {
            burnStage = BurnStage.Intact;
            nextBurnProgressPulse = Mathf.Max(0.01f, burnProgressPerPulse);
            nextFragmentConsumeProgress = Mathf.Max(0.01f, burnProgressPerFragmentConsume);
        }
    }

    private void CaptureScorchRendererState()
    {
        scorchStates.Clear();
        if (!updateScorchVisuals)
        {
            return;
        }

        if (scorchRenderers == null || scorchRenderers.Length == 0)
        {
            scorchRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (scorchRenderers == null)
        {
            return;
        }

        for (int i = 0; i < scorchRenderers.Length; i++)
        {
            Renderer renderer = scorchRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            int colorPropertyId = 0;
            if (sharedMaterial.HasProperty(BaseColorId))
            {
                colorPropertyId = BaseColorId;
            }
            else if (sharedMaterial.HasProperty(ColorId))
            {
                colorPropertyId = ColorId;
            }

            Color initialColor = Color.white;
            if (colorPropertyId != 0)
            {
                initialColor = sharedMaterial.GetColor(colorPropertyId);
            }

            scorchStates.Add(new ScorchRendererState
            {
                Renderer = renderer,
                ColorPropertyId = colorPropertyId,
                InitialColor = initialColor
            });
        }
    }

    private void ApplyScorchVisuals()
    {
        if (!updateScorchVisuals || scorchStates.Count == 0)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        float scorch01 = Mathf.Clamp01(burnProgress);
        for (int i = 0; i < scorchStates.Count; i++)
        {
            ScorchRendererState state = scorchStates[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.GetPropertyBlock(propertyBlock);

            if (state.ColorPropertyId != 0)
            {
                Color nextColor = Color.Lerp(state.InitialColor, scorchColor, scorch01 * scorchBlend);
                propertyBlock.SetColor(state.ColorPropertyId, nextColor);
            }

            propertyBlock.SetFloat(BurnAmountId, scorch01 * burnAmountScale);
            state.Renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Bounds bounds = ResolveExposureBounds();
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
