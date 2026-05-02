using System.Collections.Generic;
using TrueJourney.BotBehavior;
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
    [SerializeField] private MonoBehaviour fireSourceBehaviour;
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private MeshRenderer targetMeshRenderer;
    [SerializeField] private bool autoResolveChildFire = true;
    [SerializeField] private bool autoResolveMeshTarget = true;
    [SerializeField] private float defaultFireContactDamagePerSecond = 10f;

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
    private IFireTarget fireSourceTarget;

    public float CurrentFireContactDamagePerSecond => ResolveCurrentFireContactDamagePerSecond();
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
        RefreshVisualState();
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

    private void RefreshVisualState()
    {
        currentStage = EvaluateStage(Mathf.Clamp01(burnProgress));
        ApplyStageMesh(currentStage);
        ApplyScorchVisuals(Mathf.Clamp01(Mathf.Max(burnProgress, GetCurrentFireIntensity())));
    }

    private void AutoAssignReferences()
    {
        if (autoResolveChildFire && fireSourceBehaviour == null)
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour candidate = behaviours[i];
                if (candidate is IFireTarget)
                {
                    fireSourceBehaviour = candidate;
                    break;
                }
            }
        }

        fireSourceTarget = fireSourceBehaviour as IFireTarget;

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

                if (fireSourceBehaviour != null && candidate.transform.IsChildOf(fireSourceBehaviour.transform))
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
        if (fireSourceTarget == null || !fireSourceTarget.IsBurning)
        {
            return 0f;
        }

        if (fireSourceBehaviour is IThermalSignatureSource thermalSource)
        {
            return Mathf.Clamp01(thermalSource.GetThermalSignatureStrength());
        }

        return Mathf.Clamp01(fireSourceTarget.GetWorldRadius());
    }

    private float ResolveCurrentFireContactDamagePerSecond()
    {
        if (fireSourceTarget == null || !fireSourceTarget.IsBurning)
        {
            return 0f;
        }

        float intensity01 = GetCurrentFireIntensity();
        return Mathf.Max(0f, defaultFireContactDamagePerSecond) * Mathf.Max(0.25f, intensity01);
    }

    [ContextMenu("Setup Demo Fire Child")]
    private void SetupDemoFireChild()
    {
#if UNITY_EDITOR
        Debug.LogWarning(
            $"{nameof(BurnableObject)} demo setup no longer creates legacy fire children. " +
            $"Prefer authoring node-based fire simulation content instead.",
            this);
#endif
    }
}
