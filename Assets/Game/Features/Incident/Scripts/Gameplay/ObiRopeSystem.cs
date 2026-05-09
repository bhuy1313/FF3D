using UnityEngine;
using Obi;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class ObiRopeSystem : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;

    [Header("Obi")]
    [SerializeField] private ObiSolver solver;
    [SerializeField] private ObiRopeBlueprint blueprintAsset;
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private ObiRopeSection ropeSection;

    [Header("Shape")]
    [SerializeField] private float thickness = 0.08f;
    [SerializeField] private float resolution = 1f;
    [SerializeField] private float uvScale = 1f;
    [SerializeField] private bool fixedParticleCount = true;
    [SerializeField] private int particleCount = 24;

    [Header("Simulation")]
    [SerializeField] private bool selfCollisions;
    [SerializeField] private float stretchingScale = 1f;
    [SerializeField] private float stretchCompliance;
    [SerializeField] private float bendCompliance = 0.05f;
    [SerializeField] private float maxBending = 0.02f;

    [Header("Rebuild")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool rebuildOnValidate = true;
    [SerializeField] private bool autoRebuildInPlayMode;
    [SerializeField] private bool autoCreateBlueprintAsset = true;

    [Header("Runtime")]
    [SerializeField] private ObiRope rope;
    [SerializeField] private ObiRopeExtrudedRenderer extrudedRenderer;
    [SerializeField] private ObiParticleAttachment startAttachment;
    [SerializeField] private ObiParticleAttachment endAttachment;

    private ObiRopeBlueprint runtimeBlueprint;
    private bool isRebuildingInEditor;
    private Vector3 lastStartPosition;
    private Vector3 lastEndPosition;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (rebuildOnEnable)
        {
            Rebuild();
        }
    }

    private void OnValidate()
    {
        if (isRebuildingInEditor)
        {
            return;
        }

        ClampSettings();
        ResolveReferences();

        if (rebuildOnValidate)
        {
            Rebuild();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeBlueprint();
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoRebuildInPlayMode)
        {
            return;
        }

        if (startAnchor == null || endAnchor == null)
        {
            return;
        }

        if (startAnchor.position != lastStartPosition || endAnchor.position != lastEndPosition)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild Obi Rope")]
    public void Rebuild()
    {
        ClampSettings();

        if (startAnchor == null || endAnchor == null)
        {
            return;
        }

        ResolveReferences();
        EnsureComponents();
        if (rope == null || extrudedRenderer == null || startAttachment == null || endAttachment == null)
        {
            return;
        }

        if (solver == null)
        {
            return;
        }

        GenerateBlueprintForCurrentMode();
        ConfigureRope();
        ConfigureRenderer();
        ConfigureAttachments();
        ParentUnderSolver();
        CacheAnchorPositions();
    }

    private void ResolveReferences()
    {
        rope ??= GetComponent<ObiRope>();
        extrudedRenderer ??= GetComponent<ObiRopeExtrudedRenderer>();

        ObiParticleAttachment[] attachments = GetComponents<ObiParticleAttachment>();
        if (attachments.Length > 0)
        {
            startAttachment ??= attachments[0];
        }

        if (attachments.Length > 1)
        {
            endAttachment ??= attachments[1];
        }

        solver ??= GetComponentInParent<ObiSolver>();
        if (ropeSection == null)
        {
            ropeSection = Resources.Load<ObiRopeSection>("DefaultRopeSection");
        }
    }

    private void EnsureComponents()
    {
        if (solver == null)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying && !CanCreateEditorAssetsAndObjects())
            {
                return;
            }
            #endif

            GameObject solverObject = new GameObject("ObiSolver");
            solverObject.transform.SetParent(transform.parent, false);
            solverObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            solver = solverObject.AddComponent<ObiSolver>();
        }

        rope ??= gameObject.GetComponent<ObiRope>();
        if (rope == null)
        {
            rope = gameObject.AddComponent<ObiRope>();
        }

        extrudedRenderer ??= gameObject.GetComponent<ObiRopeExtrudedRenderer>();
        if (extrudedRenderer == null)
        {
            extrudedRenderer = gameObject.AddComponent<ObiRopeExtrudedRenderer>();
        }

        ObiParticleAttachment[] attachments = GetComponents<ObiParticleAttachment>();
        if (attachments.Length == 0)
        {
            startAttachment = gameObject.AddComponent<ObiParticleAttachment>();
            endAttachment = gameObject.AddComponent<ObiParticleAttachment>();
        }
        else if (attachments.Length == 1)
        {
            startAttachment = attachments[0];
            endAttachment = gameObject.AddComponent<ObiParticleAttachment>();
        }
        else
        {
            startAttachment = attachments[0];
            endAttachment = attachments[1];
        }
    }

    private void GenerateBlueprintForCurrentMode()
    {
        if (Application.isPlaying)
        {
            GenerateRuntimeBlueprint();
            return;
        }

        #if UNITY_EDITOR
        GenerateEditorBlueprintAsset();
        #endif
    }

    private void GenerateRuntimeBlueprint()
    {
        DestroyRuntimeBlueprint();

        if (blueprintAsset != null)
        {
            runtimeBlueprint = Instantiate(blueprintAsset);
            runtimeBlueprint.name = blueprintAsset.name + " (Runtime)";
        }
        else
        {
            runtimeBlueprint = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
        }

        RebuildBlueprintShape(runtimeBlueprint);
    }

    #if UNITY_EDITOR
    private void GenerateEditorBlueprintAsset()
    {
        if (isRebuildingInEditor)
        {
            return;
        }

        isRebuildingInEditor = true;
        try
        {
            if (blueprintAsset == null)
            {
                if (!autoCreateBlueprintAsset)
                {
                    return;
                }

                blueprintAsset = CreateBlueprintAsset();
                if (blueprintAsset == null)
                {
                    return;
                }
            }

            RebuildBlueprintShape(blueprintAsset);
            EditorUtility.SetDirty(blueprintAsset);
            AssetDatabase.SaveAssetIfDirty(blueprintAsset);
        }
        finally
        {
            isRebuildingInEditor = false;
        }
    }
    #endif

    private void RebuildBlueprintShape(ObiRopeBlueprint blueprint)
    {
        if (blueprint == null)
        {
            return;
        }

        blueprint.ClearParticleGroups(false, false);
        blueprint.path.Clear();
        blueprint.thickness = thickness;
        blueprint.resolution = resolution;

        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;
        transform.position = (startAnchor.position + endAnchor.position) * 0.5f;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, endAnchor.position - startAnchor.position);

        Vector3 startLocal = transform.InverseTransformPoint(startAnchor.position);
        Vector3 endLocal = transform.InverseTransformPoint(endAnchor.position);
        Vector3 tangentLocal = (endLocal - startLocal).normalized;
        int filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);

        blueprint.path.AddControlPoint(
            startLocal,
            -tangentLocal,
            tangentLocal,
            Vector3.up,
            thickness,
            thickness,
            1,
            filter,
            Color.white,
            "start");
        blueprint.path.AddControlPoint(
            endLocal,
            -tangentLocal,
            tangentLocal,
            Vector3.up,
            thickness,
            thickness,
            1,
            filter,
            Color.white,
            "end");
        blueprint.path.FlushEvents();

        if (fixedParticleCount)
        {
            float pathLength = Mathf.Max(0.001f, blueprint.path.Length);
            blueprint.resolution = particleCount / Mathf.Max(0.001f, pathLength / Mathf.Max(0.001f, thickness));
        }

        blueprint.GenerateImmediate();
        transform.SetPositionAndRotation(originalPosition, originalRotation);
    }

    private void ConfigureRope()
    {
        ObiRopeBlueprint activeBlueprint = Application.isPlaying ? runtimeBlueprint : blueprintAsset;
        if (activeBlueprint == null)
        {
            return;
        }

        rope.ropeBlueprint = activeBlueprint;
        rope.selfCollisions = selfCollisions;
        rope.stretchingScale = stretchingScale;
        rope.stretchCompliance = stretchCompliance;
        rope.bendCompliance = bendCompliance;
        rope.maxBending = maxBending;
    }

    private void ConfigureRenderer()
    {
        extrudedRenderer.section = ropeSection;
        extrudedRenderer.uvScale = new Vector2(Mathf.Max(0.01f, uvScale), 1f);

        if (ropeMaterial != null)
        {
            extrudedRenderer.material = ropeMaterial;
        }
    }

    private void ConfigureAttachments()
    {
        ObiRopeBlueprint activeBlueprint = Application.isPlaying ? runtimeBlueprint : blueprintAsset;
        if (activeBlueprint == null)
        {
            return;
        }

        if (activeBlueprint.groups.Count < 2)
        {
            Debug.LogWarning("Obi rope blueprint did not generate endpoint particle groups.", this);
            return;
        }

        startAttachment.target = startAnchor;
        startAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
        startAttachment.particleGroup = activeBlueprint.groups[0];

        endAttachment.target = endAnchor;
        endAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
        endAttachment.particleGroup = activeBlueprint.groups[1];
    }

    private void ParentUnderSolver()
    {
        if (solver != null && transform.parent != solver.transform)
        {
            transform.SetParent(solver.transform, true);
        }
    }

    private void ClampSettings()
    {
        thickness = Mathf.Max(0.005f, thickness);
        resolution = Mathf.Max(0.05f, resolution);
        uvScale = Mathf.Max(0.01f, uvScale);
        particleCount = Mathf.Max(4, particleCount);
        stretchingScale = Mathf.Max(0.01f, stretchingScale);
        stretchCompliance = Mathf.Max(0f, stretchCompliance);
        bendCompliance = Mathf.Max(0f, bendCompliance);
        maxBending = Mathf.Clamp(maxBending, 0f, 0.5f);
    }

    private void CacheAnchorPositions()
    {
        lastStartPosition = startAnchor != null ? startAnchor.position : Vector3.zero;
        lastEndPosition = endAnchor != null ? endAnchor.position : Vector3.zero;
    }

    private void DestroyRuntimeBlueprint()
    {
        if (runtimeBlueprint == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeBlueprint);
        }
        else
        {
            DestroyImmediate(runtimeBlueprint);
        }

        runtimeBlueprint = null;
    }

    #if UNITY_EDITOR
    private ObiRopeBlueprint CreateBlueprintAsset()
    {
        string ownerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        if (string.IsNullOrEmpty(ownerPath))
        {
            ownerPath = AssetDatabase.GetAssetPath(gameObject);
        }

        if (string.IsNullOrEmpty(ownerPath))
        {
            SceneAsset sceneAsset = gameObject.scene.IsValid() ? AssetDatabase.LoadAssetAtPath<SceneAsset>(gameObject.scene.path) : null;
            ownerPath = sceneAsset != null ? AssetDatabase.GetAssetPath(sceneAsset) : string.Empty;
        }

        if (string.IsNullOrEmpty(ownerPath))
        {
            Debug.LogWarning("ObiRopeSystem could not determine an asset path for blueprint creation. Save the scene or prefab first, or assign a blueprint asset manually.", this);
            return null;
        }

        string directory = System.IO.Path.GetDirectoryName(ownerPath);
        string safeName = string.IsNullOrWhiteSpace(gameObject.name) ? "ObiRope" : gameObject.name;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(directory, safeName + "_ObiRopeBlueprint.asset"));

        ObiRopeBlueprint asset = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        return asset;
    }

    private bool CanCreateEditorAssetsAndObjects()
    {
        string ownerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        if (!string.IsNullOrEmpty(ownerPath))
        {
            return true;
        }

        if (gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.path))
        {
            return true;
        }

        return false;
    }
    #endif
}
