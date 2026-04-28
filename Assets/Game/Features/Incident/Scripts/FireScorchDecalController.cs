using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class FireScorchDecalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Fire fire;
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

    private static Transform decalRoot;

    private void Awake()
    {
        if (fire == null)
        {
            fire = GetComponentInParent<Fire>();
        }

        EnsureProjector();
        SyncImmediate();
    }

    private void OnEnable()
    {
        if (fire == null)
        {
            fire = GetComponentInParent<Fire>();
        }

        if (fire != null)
        {
            fire.BurningStateChanged += HandleBurningStateChanged;
            fire.Extinguished += HandleExtinguished;
            wasBurning = fire.IsBurning;
        }
    }

    private void OnDisable()
    {
        if (fire != null)
        {
            fire.BurningStateChanged -= HandleBurningStateChanged;
            fire.Extinguished -= HandleExtinguished;
        }
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
        if (fire == null)
        {
            return;
        }

        bool isBurning = fire.IsBurning;
        float intensity01 = isBurning ? fire.NormalizedHp : 0f;
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

    private void HandleBurningStateChanged(bool isBurning)
    {
        wasBurning = isBurning;
    }

    private void HandleExtinguished()
    {
        extinguished = true;
        if (projector != null && persistAfterExtinguished)
        {
            projector.gameObject.SetActive(scorchAmount > 0.001f);
        }
    }

    private void SyncImmediate()
    {
        if (fire == null)
        {
            return;
        }

        scorchAmount = fire.IsBurning ? Mathf.Max(scorchAmount, fire.NormalizedHp * 0.35f) : scorchAmount;
        UpdateProjectorPlacement();
        ApplyProjectorVisuals(fire.IsBurning ? fire.NormalizedHp : 0f);
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

        float fireRadius = fire != null ? fire.GetWorldRadius() : 1f;
        float targetSize = Mathf.Lerp(minSize, Mathf.Max(maxSize, fireRadius), Mathf.Clamp01(scorchAmount));
        float opacity = Mathf.Clamp01(Mathf.Max(scorchAmount, intensity01 * 0.25f)) * maxOpacity;

        projector.size = new Vector3(targetSize, targetSize, Mathf.Max(0.05f, projectorDepth.z));
        projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
        projector.fadeFactor = opacity;
        projector.gameObject.SetActive(opacity > 0.001f || (persistAfterExtinguished && scorchAmount > 0.001f));
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
