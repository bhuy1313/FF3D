using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class FireScorchDecalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonoBehaviour fireSourceBehaviour;
    [SerializeField] private Material scorchMaterial;

    [Header("Projection")]
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private float raycastHeight = 1.2f;
    [SerializeField] private float raycastDistance = 4f;
    [SerializeField] private float surfaceOffset = 0.015f;
    [SerializeField] private Vector3 projectorDepth = new Vector3(0f, 0f, 1.2f);

    [Header("Growth")]
    [SerializeField] private float minSize = 0.45f;
    [SerializeField] private float maxSize = 3.2f;
    [SerializeField] private float growthPerSecond = 0.28f;
    [SerializeField] [Range(0f, 1f)] private float maxOpacity = 0.75f;
    [SerializeField] private bool persistAfterExtinguished = true;
    [SerializeField] private float fadeOutDuration = 4f;

    private DecalProjector projector;
    private float scorchAmount;
    private bool wasBurning;
    private bool extinguished;
    private bool warnedMissingMaterial;
    private IFireTarget fireTarget;

    private static Transform decalRoot;

    private void Awake()
    {
        ResolveFireTarget();
        EnsureProjector();
        SyncImmediate();
    }

    private void OnEnable()
    {
        ResolveFireTarget();
        wasBurning = fireTarget != null && fireTarget.IsBurning;
    }

    private void OnDestroy()
    {
        if (projector != null && !persistAfterExtinguished)
        {
            Destroy(projector.gameObject);
        }
    }

    private void Update()
    {
        if (fireTarget == null)
        {
            return;
        }

        bool isBurning = fireTarget.IsBurning;
        float intensity01 = isBurning ? ResolveFireIntensity01() : 0f;
        if (isBurning)
        {
            extinguished = false;
            scorchAmount = Mathf.Clamp01(scorchAmount + Time.deltaTime * growthPerSecond * Mathf.Max(0.2f, intensity01));
            UpdateProjectorPlacement();
            ApplyProjectorVisuals(intensity01);
        }
        else if (!persistAfterExtinguished && projector != null && projector.fadeFactor > 0f)
        {
            float fadeStep = fadeOutDuration > 0f ? Time.deltaTime / fadeOutDuration : 1f;
            projector.fadeFactor = Mathf.MoveTowards(projector.fadeFactor, 0f, fadeStep);
            projector.gameObject.SetActive(projector.fadeFactor > 0.001f);
        }
        else if (projector != null)
        {
            projector.gameObject.SetActive(scorchAmount > 0.001f || extinguished);
        }

        wasBurning = isBurning;
    }

    public void Configure(Material material, LayerMask mask)
    {
        if (material != null)
        {
            scorchMaterial = material;
        }

        surfaceMask = mask;
        if (projector != null)
        {
            projector.material = ResolveMaterial();
        }
    }

    private void SyncImmediate()
    {
        if (fireTarget == null)
        {
            return;
        }

        scorchAmount = fireTarget.IsBurning ? Mathf.Max(scorchAmount, ResolveFireIntensity01() * 0.35f) : scorchAmount;
        UpdateProjectorPlacement();
        ApplyProjectorVisuals(fireTarget.IsBurning ? ResolveFireIntensity01() : 0f);
    }

    private void EnsureProjector()
    {
        if (projector != null)
        {
            return;
        }

        Transform root = ResolveDecalRoot();
        GameObject projectorObject = new GameObject($"{name}_ScorchDecal");
        projectorObject.transform.SetParent(root, true);
        projector = projectorObject.AddComponent<DecalProjector>();
        projector.material = ResolveMaterial();
        projector.drawDistance = 70f;
        projector.fadeScale = 0.35f;
        projector.size = new Vector3(minSize, minSize, Mathf.Max(0.05f, projectorDepth.z));
        projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
        projector.fadeFactor = 0f;
        projectorObject.SetActive(false);
    }

    private Material ResolveMaterial()
    {
        if (scorchMaterial == null && !warnedMissingMaterial)
        {
            warnedMissingMaterial = true;
            Debug.LogWarning($"{nameof(FireScorchDecalController)} needs a URP Decal material assigned to render scorch decals.", this);
        }

        return scorchMaterial;
    }

    private void UpdateProjectorPlacement()
    {
        if (projector == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * raycastHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight + raycastDistance, surfaceMask, triggerInteraction))
        {
            PlaceProjector(hit.point, hit.normal);
            return;
        }

        PlaceProjector(transform.position, Vector3.up);
    }

    private void PlaceProjector(Vector3 surfacePoint, Vector3 surfaceNormal)
    {
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Transform projectorTransform = projector.transform;
        projectorTransform.position = surfacePoint + normal * surfaceOffset;
        projectorTransform.rotation = Quaternion.LookRotation(-normal, ResolveTangent(normal));
    }

    private void ApplyProjectorVisuals(float intensity01)
    {
        if (projector == null)
        {
            return;
        }

        float fireRadius = fireTarget != null ? fireTarget.GetWorldRadius() : 1f;
        float targetSize = Mathf.Lerp(minSize, Mathf.Max(maxSize, fireRadius), Mathf.Clamp01(scorchAmount));
        float opacity = Mathf.Clamp01(Mathf.Max(scorchAmount, intensity01 * 0.25f)) * maxOpacity;

        projector.size = new Vector3(targetSize, targetSize, Mathf.Max(0.05f, projectorDepth.z));
        projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
        projector.fadeFactor = opacity;
        projector.gameObject.SetActive(opacity > 0.001f || (persistAfterExtinguished && scorchAmount > 0.001f));
    }

    private void ResolveFireTarget()
    {
        if (fireSourceBehaviour == null)
        {
            MonoBehaviour[] parentBehaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < parentBehaviours.Length; i++)
            {
                MonoBehaviour candidate = parentBehaviours[i];
                if (candidate is IFireTarget)
                {
                    fireSourceBehaviour = candidate;
                    break;
                }
            }
        }

        fireTarget = fireSourceBehaviour as IFireTarget;
    }

    private float ResolveFireIntensity01()
    {
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            return 0f;
        }

        if (fireSourceBehaviour is IThermalSignatureSource thermalSource)
        {
            return Mathf.Clamp01(thermalSource.GetThermalSignatureStrength());
        }

        return Mathf.Clamp01(fireTarget.GetWorldRadius());
    }

    private static Vector3 ResolveTangent(Vector3 normal)
    {
        Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (tangent.sqrMagnitude <= 0.001f)
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, normal);
        }

        return tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.up;
    }

    private static Transform ResolveDecalRoot()
    {
        if (decalRoot != null)
        {
            return decalRoot;
        }

        GameObject rootObject = GameObject.Find("FireScorchDecals");
        if (rootObject == null)
        {
            rootObject = new GameObject("FireScorchDecals");
        }

        decalRoot = rootObject.transform;
        return decalRoot;
    }
}
