using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

[DisallowMultipleComponent]
public class BurnableObject : MonoBehaviour, IBurnable
{
    public enum BurnVisualStage
    {
        Intact = 0,
        Scorched = 1,
        HeavilyBurned = 2,
        Destroyed = 3
    }

    [Header("References")]
    [SerializeField] private Fire fireSource;
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private MeshRenderer targetMeshRenderer;
    [SerializeField] private bool autoResolveChildFire = true;
    [SerializeField] private bool autoResolveMeshTarget = true;

    [Header("Burn Progress")]
    [SerializeField] private float burnProgress;
    [SerializeField] private float burnAccumulationPerSecond = 0.15f;
    [SerializeField] private bool accumulateOnlyWhileBurning = true;

    [Header("Stage Thresholds")]
    [SerializeField, Range(0f, 1f)] private float scorchedStageThreshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float heavilyBurnedStageThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float destroyedStageThreshold = 0.9f;
    [SerializeField] private BurnVisualStage currentStage;

    [Header("Optional Mesh Swaps")]
    [SerializeField] private Mesh intactMesh;
    [SerializeField] private Mesh scorchedMesh;
    [SerializeField] private Mesh heavilyBurnedMesh;
    [SerializeField] private Mesh destroyedMesh;

    [Header("Scorch")]
    [SerializeField] private bool scorchMaterials = true;
    [SerializeField] private Color scorchColor = new Color(0.11f, 0.09f, 0.07f, 1f);
    [SerializeField, Range(0f, 1f)] private float scorchColorStrength = 0.85f;
    [SerializeField] private string burnAmountShaderProperty = "_BurnAmount";

    private readonly List<Color> baseMaterialColors = new List<Color>();
    private readonly List<int> colorPropertyIds = new List<int>();
    private MaterialPropertyBlock propertyBlock;
    private BurnVisualStage appliedStage = BurnVisualStage.Intact;
    private bool visualStateCached;

    public Fire FireSource => fireSource;
    public float BurnProgress => burnProgress;
    public bool HasDeformableMesh => targetMeshFilter != null;

    private void Awake()
    {
        AutoAssignReferences();
        CacheInitialVisualState();
        CacheRendererState();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        CacheInitialVisualState();
        CacheRendererState();
        SubscribeToFireEvents(true);
        RefreshVisualState();
    }

    private void OnDisable()
    {
        SubscribeToFireEvents(false);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        burnAccumulationPerSecond = Mathf.Max(0f, burnAccumulationPerSecond);
        scorchedStageThreshold = Mathf.Clamp01(scorchedStageThreshold);
        heavilyBurnedStageThreshold = Mathf.Clamp(heavilyBurnedStageThreshold, scorchedStageThreshold, 1f);
        destroyedStageThreshold = Mathf.Clamp(destroyedStageThreshold, heavilyBurnedStageThreshold, 1f);
        currentStage = EvaluateStage(Mathf.Clamp01(burnProgress));
    }

    private void Update()
    {
        float fireIntensity = GetCurrentFireIntensity();
        if (fireIntensity > 0f || !accumulateOnlyWhileBurning)
        {
            float accumulation = accumulateOnlyWhileBurning
                ? fireIntensity
                : Mathf.Max(fireIntensity, 1f);
            burnProgress = Mathf.Clamp01(burnProgress + accumulation * burnAccumulationPerSecond * Time.deltaTime);
        }

        RefreshVisualState();
    }

    public void ForceRefresh()
    {
        RefreshVisualState();
    }

    private void HandleFireStateChanged(bool _)
    {
        RefreshVisualState();
    }

    private void HandleFireStateChanged()
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        currentStage = EvaluateStage(Mathf.Clamp01(burnProgress));
        ApplyStageMesh(currentStage);
        ApplyScorchVisuals(Mathf.Clamp01(Mathf.Max(burnProgress, GetCurrentFireIntensity())));
    }

    private void AutoAssignReferences()
    {
        if (autoResolveChildFire && fireSource == null)
        {
            fireSource = GetComponentInChildren<Fire>(true);
        }

        if (!autoResolveMeshTarget)
        {
            if (targetMeshRenderer == null && targetMeshFilter != null)
            {
                targetMeshRenderer = targetMeshFilter.GetComponent<MeshRenderer>();
            }

            return;
        }

        if (targetMeshFilter == null)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter candidate = meshFilters[i];
                if (candidate == null || candidate.sharedMesh == null)
                {
                    continue;
                }

                if (fireSource != null && candidate.GetComponentInParent<Fire>() == fireSource)
                {
                    continue;
                }

                targetMeshFilter = candidate;
                break;
            }
        }

        if (targetMeshRenderer == null && targetMeshFilter != null)
        {
            targetMeshRenderer = targetMeshFilter.GetComponent<MeshRenderer>();
        }
    }

    private void CacheInitialVisualState()
    {
        if (visualStateCached)
        {
            return;
        }

        if (targetMeshFilter != null && intactMesh == null)
        {
            intactMesh = targetMeshFilter.sharedMesh;
        }

        visualStateCached = true;
    }

    private void SubscribeToFireEvents(bool subscribe)
    {
        if (fireSource == null)
        {
            return;
        }

        if (subscribe)
        {
            fireSource.Ignited -= HandleFireStateChanged;
            fireSource.Extinguished -= HandleFireStateChanged;
            fireSource.BurningStateChanged -= HandleFireStateChanged;
            fireSource.Ignited += HandleFireStateChanged;
            fireSource.Extinguished += HandleFireStateChanged;
            fireSource.BurningStateChanged += HandleFireStateChanged;
            return;
        }

        fireSource.Ignited -= HandleFireStateChanged;
        fireSource.Extinguished -= HandleFireStateChanged;
        fireSource.BurningStateChanged -= HandleFireStateChanged;
    }

    private void CacheRendererState()
    {
        if (targetMeshRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        baseMaterialColors.Clear();
        colorPropertyIds.Clear();

        Material[] sharedMaterials = targetMeshRenderer.sharedMaterials;
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material sharedMaterial = sharedMaterials[i];
            if (sharedMaterial == null)
            {
                baseMaterialColors.Add(Color.white);
                colorPropertyIds.Add(0);
                continue;
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                colorPropertyIds.Add(Shader.PropertyToID("_BaseColor"));
                baseMaterialColors.Add(sharedMaterial.GetColor("_BaseColor"));
                continue;
            }

            if (sharedMaterial.HasProperty("_Color"))
            {
                colorPropertyIds.Add(Shader.PropertyToID("_Color"));
                baseMaterialColors.Add(sharedMaterial.GetColor("_Color"));
                continue;
            }

            colorPropertyIds.Add(0);
            baseMaterialColors.Add(Color.white);
        }
    }

    private BurnVisualStage EvaluateStage(float progress01)
    {
        if (progress01 >= destroyedStageThreshold)
        {
            return BurnVisualStage.Destroyed;
        }

        if (progress01 >= heavilyBurnedStageThreshold)
        {
            return BurnVisualStage.HeavilyBurned;
        }

        if (progress01 >= scorchedStageThreshold)
        {
            return BurnVisualStage.Scorched;
        }

        return BurnVisualStage.Intact;
    }

    private void ApplyStageMesh(BurnVisualStage stage)
    {
        if (targetMeshFilter == null)
        {
            appliedStage = stage;
            return;
        }

        if (appliedStage == stage)
        {
            return;
        }

        Mesh targetMesh = ResolveMeshForStage(stage);
        if (targetMesh != null)
        {
            targetMeshFilter.sharedMesh = targetMesh;
        }

        appliedStage = stage;
    }

    private Mesh ResolveMeshForStage(BurnVisualStage stage)
    {
        switch (stage)
        {
            case BurnVisualStage.Destroyed:
                return destroyedMesh != null
                    ? destroyedMesh
                    : heavilyBurnedMesh != null
                        ? heavilyBurnedMesh
                        : scorchedMesh != null
                            ? scorchedMesh
                            : intactMesh;

            case BurnVisualStage.HeavilyBurned:
                return heavilyBurnedMesh != null
                    ? heavilyBurnedMesh
                    : scorchedMesh != null
                        ? scorchedMesh
                        : intactMesh;

            case BurnVisualStage.Scorched:
                return scorchedMesh != null ? scorchedMesh : intactMesh;

            default:
                return intactMesh;
        }
    }

    private void ApplyScorchVisuals(float visualBurnAmount)
    {
        if (!scorchMaterials || targetMeshRenderer == null)
        {
            return;
        }

        float clampedBurn = Mathf.Clamp01(visualBurnAmount);
        propertyBlock ??= new MaterialPropertyBlock();

        int sharedMaterialCount = targetMeshRenderer.sharedMaterials.Length;
        for (int i = 0; i < sharedMaterialCount; i++)
        {
            propertyBlock.Clear();

            if (i < colorPropertyIds.Count && colorPropertyIds[i] != 0 && i < baseMaterialColors.Count)
            {
                Color targetColor = Color.Lerp(baseMaterialColors[i], scorchColor, clampedBurn * scorchColorStrength);
                propertyBlock.SetColor(colorPropertyIds[i], targetColor);
            }

            if (!string.IsNullOrWhiteSpace(burnAmountShaderProperty))
            {
                propertyBlock.SetFloat(burnAmountShaderProperty, clampedBurn);
            }

            targetMeshRenderer.SetPropertyBlock(propertyBlock, i);
        }
    }

    private float GetCurrentFireIntensity()
    {
        return fireSource != null && fireSource.IsBurning
            ? Mathf.Clamp01(fireSource.NormalizedHp)
            : 0f;
    }

    [ContextMenu("Setup Demo Fire Child")]
    private void SetupDemoFireChild()
    {
        if (fireSource != null)
        {
            return;
        }

        GameObject fireObject = new GameObject("Fire");
        fireObject.transform.SetParent(transform, false);
        fireObject.AddComponent<SphereCollider>().isTrigger = true;
        fireObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        fireSource = fireObject.AddComponent<Fire>();
    }
}
