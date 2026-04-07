using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Ladder))]
[RequireComponent(typeof(NavMeshLink))]
public class RopeLadderDeployed : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float ladderWidth = 0.45f;
    [SerializeField] private float rungSpacing = 0.35f;
    [SerializeField] private float railThickness = 0.04f;
    [SerializeField] private float rungThickness = 0.03f;
    [SerializeField] private float colliderDepth = 0.2f;
    [SerializeField] private float topExitOffset = 0.15f;

    [Header("Navigation")]
    [SerializeField] private bool enableNavMeshLink = true;
    [SerializeField] private float navMeshLinkWidthPadding = 0.2f;
    [SerializeField] private float navMeshLinkBottomOffset = 0.15f;
    [SerializeField] private float navMeshLinkTopPlatformOffset = 0.6f;
    [SerializeField] private bool autoSnapLinkEndpointsToNavMesh = true;
    [SerializeField] private float navMeshEndpointSnapDistance = 1f;
    [SerializeField] private float navMeshEndpointVerticalTolerance = 1f;

    [Header("Runtime")]
    [SerializeField] private Vector3 topAnchorWorld;
    [SerializeField] private float ladderHeight;
    [SerializeField] private bool previewMode;

    private BoxCollider climbCollider;
    private Ladder ladder;
    private NavMeshLink navMeshLink;
    private Transform visualsRoot;
    private Material previewMaterial;
    private Color previewTint = new Color(0.45f, 0.95f, 0.65f, 0.4f);

    public float LadderHeight => ladderHeight;

    private void Awake()
    {
        EnsureReferences();
    }

    public void ApplyDimensions(
        float width,
        float spacing,
        float railSize,
        float rungSize,
        float depth,
        float exitOffset)
    {
        ladderWidth = Mathf.Max(0.1f, width);
        rungSpacing = Mathf.Max(0.1f, spacing);
        railThickness = Mathf.Max(0.01f, railSize);
        rungThickness = Mathf.Max(0.01f, rungSize);
        colliderDepth = Mathf.Max(0.05f, depth);
        topExitOffset = Mathf.Max(0f, exitOffset);
    }

    public void ApplyNavigationSettings(
        bool linksEnabled,
        float widthPadding,
        float bottomOffset,
        float topPlatformOffset,
        bool autoSnapEndpoints,
        float endpointSnapDistance,
        float endpointVerticalTolerance)
    {
        enableNavMeshLink = linksEnabled;
        navMeshLinkWidthPadding = Mathf.Max(0f, widthPadding);
        navMeshLinkBottomOffset = Mathf.Max(0f, bottomOffset);
        navMeshLinkTopPlatformOffset = Mathf.Max(0f, topPlatformOffset);
        autoSnapLinkEndpointsToNavMesh = autoSnapEndpoints;
        navMeshEndpointSnapDistance = Mathf.Max(0.05f, endpointSnapDistance);
        navMeshEndpointVerticalTolerance = Mathf.Max(0.05f, endpointVerticalTolerance);
    }

    public void Configure(Vector3 topAnchor, Vector3 groundPoint, Vector3 outward)
    {
        EnsureReferences();

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            flatOutward = Vector3.forward;
        }

        topAnchorWorld = topAnchor;
        ladderHeight = Mathf.Max(0.5f, topAnchor.y - groundPoint.y);

        Vector3 bottomAnchor = new Vector3(topAnchor.x, topAnchor.y - ladderHeight, topAnchor.z);
        transform.position = Vector3.Lerp(topAnchor, bottomAnchor, 0.5f);
        transform.rotation = Quaternion.LookRotation(-flatOutward.normalized, Vector3.up);

        climbCollider.center = Vector3.zero;
        climbCollider.size = new Vector3(ladderWidth, ladderHeight, colliderDepth);

        ladder.SetTopHeightOffset(topExitOffset);
        ConfigureNavMeshLink(topAnchor, groundPoint, flatOutward.normalized);
        RebuildVisuals();
        ApplyPreviewState();
    }

    public void SetPreviewMode(bool enabled, Color tint)
    {
        previewMode = enabled;
        previewTint = tint;
        EnsureReferences();
        ApplyPreviewState();
    }

    private void EnsureReferences()
    {
        climbCollider ??= GetComponent<BoxCollider>();
        ladder ??= GetComponent<Ladder>();
        navMeshLink ??= GetComponent<NavMeshLink>();

        if (visualsRoot == null)
        {
            Transform existing = transform.Find("Visuals");
            if (existing != null)
            {
                visualsRoot = existing;
            }
            else
            {
                GameObject visuals = new GameObject("Visuals");
                visualsRoot = visuals.transform;
                visualsRoot.SetParent(transform, false);
            }
        }
    }

    private void ConfigureNavMeshLink(Vector3 topAnchor, Vector3 groundPoint, Vector3 outward)
    {
        if (navMeshLink == null)
        {
            return;
        }

        Vector3 horizontalOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (horizontalOutward.sqrMagnitude <= 0.0001f)
        {
            horizontalOutward = transform.forward;
        }

        horizontalOutward.Normalize();

        float topPlatformOffset = Mathf.Max(colliderDepth * 0.5f, navMeshLinkTopPlatformOffset);
        float bottomOffset = Mathf.Max(0f, navMeshLinkBottomOffset);

        Vector3 bottomWorld = groundPoint + horizontalOutward * bottomOffset;
        Vector3 topWorld = topAnchor - horizontalOutward * topPlatformOffset;
        bottomWorld = ResolveClosestNavMeshEndpoint(bottomWorld, groundPoint.y);
        topWorld = ResolveClosestNavMeshEndpoint(topWorld, topAnchor.y);

        navMeshLink.agentTypeID = 0;
        navMeshLink.bidirectional = true;
        navMeshLink.width = ladderWidth + Mathf.Max(0f, navMeshLinkWidthPadding);
        navMeshLink.autoUpdate = true;
        navMeshLink.startTransform = null;
        navMeshLink.endTransform = null;
        navMeshLink.startPoint = transform.InverseTransformPoint(bottomWorld);
        navMeshLink.endPoint = transform.InverseTransformPoint(topWorld);
        navMeshLink.activated = enableNavMeshLink && !previewMode;
        navMeshLink.UpdateLink();
    }

    private Vector3 ResolveClosestNavMeshEndpoint(Vector3 fallbackWorldPoint, float expectedSurfaceY)
    {
        if (!enableNavMeshLink || !autoSnapLinkEndpointsToNavMesh)
        {
            return fallbackWorldPoint;
        }

        float sampleDistance = Mathf.Max(0.05f, navMeshEndpointSnapDistance);
        if (!NavMesh.SamplePosition(fallbackWorldPoint, out NavMeshHit navMeshHit, sampleDistance, NavMesh.AllAreas))
        {
            return fallbackWorldPoint;
        }

        if (Mathf.Abs(navMeshHit.position.y - expectedSurfaceY) > Mathf.Max(0.05f, navMeshEndpointVerticalTolerance))
        {
            return fallbackWorldPoint;
        }

        return navMeshHit.position;
    }

    private void RebuildVisuals()
    {
        ClearVisuals();

        float halfHeight = ladderHeight * 0.5f;
        float railInset = Mathf.Max(railThickness * 0.5f, ladderWidth * 0.5f - railThickness * 0.5f);

        CreateVisualBlock(
            "RailLeft",
            new Vector3(-railInset, 0f, 0f),
            new Vector3(railThickness, ladderHeight, railThickness));
        CreateVisualBlock(
            "RailRight",
            new Vector3(railInset, 0f, 0f),
            new Vector3(railThickness, ladderHeight, railThickness));

        int rungCount = Mathf.Max(2, Mathf.FloorToInt(ladderHeight / rungSpacing) + 1);
        float usableHeight = Mathf.Max(0f, ladderHeight - rungThickness);
        float step = rungCount > 1 ? usableHeight / (rungCount - 1) : 0f;
        float startY = halfHeight - rungThickness * 0.5f;

        for (int i = 0; i < rungCount; i++)
        {
            float y = startY - step * i;
            CreateVisualBlock(
                $"Rung_{i}",
                new Vector3(0f, y, 0f),
                new Vector3(ladderWidth - railThickness, rungThickness, rungThickness));
        }
    }

    private void CreateVisualBlock(string objectName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(visualsRoot, false);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = localScale;

        Collider blockCollider = block.GetComponent<Collider>();
        if (blockCollider != null)
        {
            DestroyRuntimeSafe(blockCollider);
        }

        MeshRenderer renderer = block.GetComponent<MeshRenderer>();
        if (renderer != null && previewMode)
        {
            renderer.sharedMaterial = GetPreviewMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void ClearVisuals()
    {
        if (visualsRoot == null)
        {
            return;
        }

        for (int i = visualsRoot.childCount - 1; i >= 0; i--)
        {
            DestroyRuntimeSafe(visualsRoot.GetChild(i).gameObject);
        }
    }

    private static void DestroyRuntimeSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void ApplyPreviewState()
    {
        if (climbCollider != null)
        {
            climbCollider.enabled = !previewMode;
        }

        if (ladder != null)
        {
            ladder.enabled = !previewMode;
        }

        if (navMeshLink != null)
        {
            navMeshLink.activated = enableNavMeshLink && !previewMode;
        }

        if (visualsRoot == null)
        {
            return;
        }

        MeshRenderer[] renderers = visualsRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !previewMode)
            {
                continue;
            }

            renderer.sharedMaterial = GetPreviewMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private Material GetPreviewMaterial()
    {
        if (previewMaterial != null)
        {
            previewMaterial.color = previewTint;
            if (previewMaterial.HasProperty("_BaseColor"))
            {
                previewMaterial.SetColor("_BaseColor", previewTint);
            }

            return previewMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        previewMaterial = new Material(shader)
        {
            name = "RopeLadderPreviewMaterial",
            color = previewTint
        };

        if (previewMaterial.HasProperty("_BaseColor"))
        {
            previewMaterial.SetColor("_BaseColor", previewTint);
        }

        if (previewMaterial.HasProperty("_Surface"))
        {
            previewMaterial.SetFloat("_Surface", 1f);
        }

        if (previewMaterial.HasProperty("_Blend"))
        {
            previewMaterial.SetFloat("_Blend", 0f);
        }

        previewMaterial.SetOverrideTag("RenderType", "Transparent");
        previewMaterial.renderQueue = (int)RenderQueue.Transparent;
        previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return previewMaterial;
    }

    private void OnDestroy()
    {
        if (previewMaterial != null)
        {
            DestroyRuntimeSafe(previewMaterial);
            previewMaterial = null;
        }
    }
}
