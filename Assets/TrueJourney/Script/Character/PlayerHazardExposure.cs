using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHazardExposure : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform viewTransform;

    [Header("Smoke")]
    [SerializeField] private float smokeRiseSpeed = 2.5f;
    [SerializeField] private float smokeFallSpeed = 1.75f;
    [SerializeField, Range(0f, 1f)] private float smokeEnterThreshold = 0.08f;
    [SerializeField, Range(0f, 1f)] private float smokeExitThreshold = 0.04f;

    [Header("Fire Glare")]
    [SerializeField] private float fireGlareMaxDistance = 10f;
    [SerializeField, Range(0f, 1f)] private float fireGlareMinViewDot = 0.6f;
    [SerializeField] private float fireGlareRiseSpeed = 4f;
    [SerializeField] private float fireGlareFallSpeed = 5.5f;
    [SerializeField] private float fireViewOriginOffset = 0.05f;
    [SerializeField, Range(0f, 0.5f)] private float fireViewportPadding = 0.15f;
    [SerializeField] private LayerMask fireOcclusionMask = ~0;

    [Header("Runtime")]
    [SerializeField, Range(0f, 1f)] private float smokeDensity01;
    [SerializeField, Range(0f, 1f)] private float fireGlare01;
    [SerializeField] private bool isInSmoke;
    [SerializeField] private bool isLookingAtFire;
    [SerializeField] private Vector2 fireGlareViewportPosition = new Vector2(0.5f, 0.5f);

    private int smokeSubmissionFrame = -1;
    private float smokeSubmissionDensity;

    public float SmokeDensity01 => smokeDensity01;
    public float FireGlare01 => fireGlare01;
    public bool IsInSmoke => isInSmoke;
    public bool IsLookingAtFire => isLookingAtFire;
    public Vector2 FireGlareViewportPosition => fireGlareViewportPosition;

    private void Awake()
    {
        ResolveViewReferences();
    }

    private void OnValidate()
    {
        smokeRiseSpeed = Mathf.Max(0f, smokeRiseSpeed);
        smokeFallSpeed = Mathf.Max(0f, smokeFallSpeed);
        smokeEnterThreshold = Mathf.Clamp01(smokeEnterThreshold);
        smokeExitThreshold = Mathf.Clamp(smokeExitThreshold, 0f, smokeEnterThreshold);
        fireGlareMaxDistance = Mathf.Max(0.1f, fireGlareMaxDistance);
        fireGlareMinViewDot = Mathf.Clamp01(fireGlareMinViewDot);
        fireGlareRiseSpeed = Mathf.Max(0f, fireGlareRiseSpeed);
        fireGlareFallSpeed = Mathf.Max(0f, fireGlareFallSpeed);
        fireViewOriginOffset = Mathf.Max(0f, fireViewOriginOffset);
        fireViewportPadding = Mathf.Clamp(fireViewportPadding, 0f, 0.5f);
        smokeDensity01 = Mathf.Clamp01(smokeDensity01);
        fireGlare01 = Mathf.Clamp01(fireGlare01);
        ResolveViewReferences();
    }

    private void Update()
    {
        ResolveViewReferences();
        UpdateSmokeExposure(Time.deltaTime);
        UpdateFireGlare(Time.deltaTime);
    }

    public void ReportSmokeExposure(float density01)
    {
        if (smokeSubmissionFrame != Time.frameCount)
        {
            smokeSubmissionFrame = Time.frameCount;
            smokeSubmissionDensity = 0f;
        }

        smokeSubmissionDensity = Mathf.Max(smokeSubmissionDensity, Mathf.Clamp01(density01));
    }

    private void ResolveViewReferences()
    {
        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }
        }

        if (viewTransform == null)
        {
            Transform cameraRoot = transform.Find("PlayerCameraRoot");
            viewTransform = cameraRoot != null
                ? cameraRoot
                : viewCamera != null ? viewCamera.transform : transform;
        }
    }

    private void UpdateSmokeExposure(float deltaTime)
    {
        float targetSmoke = smokeSubmissionFrame == Time.frameCount
            ? smokeSubmissionDensity
            : 0f;
        float speed = targetSmoke >= smokeDensity01 ? smokeRiseSpeed : smokeFallSpeed;
        smokeDensity01 = Mathf.MoveTowards(smokeDensity01, targetSmoke, speed * Mathf.Max(0f, deltaTime));

        if (isInSmoke)
        {
            isInSmoke = smokeDensity01 > smokeExitThreshold;
        }
        else
        {
            isInSmoke = smokeDensity01 >= smokeEnterThreshold;
        }
    }

    private void UpdateFireGlare(float deltaTime)
    {
        if (viewTransform == null)
        {
            fireGlare01 = Mathf.MoveTowards(fireGlare01, 0f, fireGlareFallSpeed * Mathf.Max(0f, deltaTime));
            isLookingAtFire = false;
            return;
        }

        Vector3 origin = viewTransform.position + viewTransform.forward * fireViewOriginOffset;
        float bestIntensity = 0f;
        Vector2 bestViewportPosition = fireGlareViewportPosition;

        foreach (IFireTarget fireTarget in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (!TryEvaluateFireGlare(fireTarget, origin, out float intensity, out Vector2 viewportPosition))
            {
                continue;
            }

            if (intensity <= bestIntensity)
            {
                continue;
            }

            bestIntensity = intensity;
            bestViewportPosition = viewportPosition;
        }

        float speed = bestIntensity >= fireGlare01 ? fireGlareRiseSpeed : fireGlareFallSpeed;
        fireGlare01 = Mathf.MoveTowards(fireGlare01, bestIntensity, speed * Mathf.Max(0f, deltaTime));
        if (bestIntensity > 0.001f)
        {
            fireGlareViewportPosition = bestViewportPosition;
        }

        isLookingAtFire = fireGlare01 > 0.025f;
    }

    private bool TryEvaluateFireGlare(IFireTarget fireTarget, Vector3 origin, out float intensity, out Vector2 viewportPosition)
    {
        intensity = 0f;
        viewportPosition = new Vector2(0.5f, 0.5f);

        if (fireTarget == null || !fireTarget.IsBurning || !(fireTarget is Component fireComponent) || fireComponent == null)
        {
            return false;
        }

        Vector3 firePosition = fireTarget.GetWorldPosition();
        Vector3 toFire = firePosition - origin;
        float distance = toFire.magnitude;
        if (distance <= 0.001f || distance > fireGlareMaxDistance)
        {
            return false;
        }

        Vector3 direction = toFire / distance;
        float viewDot = Vector3.Dot(viewTransform.forward, direction);
        if (viewDot < fireGlareMinViewDot)
        {
            return false;
        }

        if (IsFireOccluded(origin, direction, distance, fireComponent))
        {
            return false;
        }

        if (viewCamera != null)
        {
            Vector3 viewport = viewCamera.WorldToViewportPoint(firePosition);
            if (viewport.z <= 0f)
            {
                return false;
            }

            if (viewport.x < -fireViewportPadding || viewport.x > 1f + fireViewportPadding ||
                viewport.y < -fireViewportPadding || viewport.y > 1f + fireViewportPadding)
            {
                return false;
            }

            viewportPosition = new Vector2(viewport.x, viewport.y);
        }

        float normalizedIntensity = fireTarget is Fire fire
            ? Mathf.Clamp01(fire.NormalizedHp)
            : 1f;
        float viewFactor = Mathf.InverseLerp(fireGlareMinViewDot, 1f, viewDot);
        float distanceFactor = 1f - Mathf.Clamp01((distance - fireTarget.GetWorldRadius()) / fireGlareMaxDistance);
        float screenCenterFactor = 1f - Mathf.Clamp01(Vector2.Distance(viewportPosition, new Vector2(0.5f, 0.5f)) / 0.75f);

        intensity = Mathf.Clamp01(normalizedIntensity * viewFactor * distanceFactor * Mathf.Lerp(0.65f, 1f, screenCenterFactor));
        return intensity > 0.001f;
    }

    private bool IsFireOccluded(Vector3 origin, Vector3 direction, float distance, Component fireComponent)
    {
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, fireOcclusionMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider == null || !hit.collider.transform.IsChildOf(fireComponent.transform);
    }
}
