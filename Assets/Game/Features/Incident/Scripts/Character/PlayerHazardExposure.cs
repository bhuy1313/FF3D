using System;
using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHazardExposure : MonoBehaviour
{
    private const float SmokeSubmissionGraceSeconds = 0.1f;
    private const int MaxTrackedFireGlareTargets = 4;
    private static readonly RaycastHit[] FireGlareHitBuffer = new RaycastHit[32];
    private static readonly Vector2 DefaultViewportPosition = new Vector2(0.5f, 0.5f);

    [Header("Smoke")]
    [SerializeField] private float smokeRiseSpeed = 2.5f;
    [SerializeField] private float smokeFallSpeed = 1.75f;
    [SerializeField, Range(0f, 1f)] private float smokeEnterThreshold = 0.08f;
    [SerializeField, Range(0f, 1f)] private float smokeExitThreshold = 0.04f;

    [Header("Fire")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float fireGlareRiseSpeed = 5f;
    [SerializeField] private float fireGlareFallSpeed = 3f;
    [SerializeField] private float fireGlareMaxDistance = 16f;
    [SerializeField, Range(0f, 1f)] private float fireMinimumViewDot = 0.05f;
    [SerializeField] private float fireViewportEdgeFade = 0.12f;
    [SerializeField] private bool requireLineOfSightForFireGlare = true;
    [SerializeField] private LayerMask fireGlareObstacleMask = ~0;
    [SerializeField, Range(1, MaxTrackedFireGlareTargets)] private int simultaneousFireOverlayCount = 2;
    [SerializeField, Min(0.02f)] private float fireGlareScanInterval = 0.1f;

    [Header("Runtime")]
    [SerializeField, Range(0f, 1f)] private float smokeDensity01;
    [SerializeField] private bool isInSmoke;
    [SerializeField] private int inSightFireCount;
    [SerializeField, Range(0f, 1f)] private float[] fireGlareBySlot = new float[MaxTrackedFireGlareTargets];
    [SerializeField] private Vector2[] fireGlareViewportPositions = CreateDefaultFireViewportPositions();

    private float smokeSubmissionDensity;
    private float lastSmokeSubmissionTime = float.NegativeInfinity;
    private float nextFireGlareScanTime;
    private readonly float[] fireTargetIntensities = new float[MaxTrackedFireGlareTargets];
    private readonly float[] fireTargetDistances = new float[MaxTrackedFireGlareTargets];
    private readonly Vector2[] fireTargetPositions = CreateDefaultFireViewportPositions();

    public float SmokeDensity01 => smokeDensity01;
    public bool IsInSmoke => isInSmoke;
    public float FireGlare01 => fireGlareBySlot != null && fireGlareBySlot.Length > 0 ? fireGlareBySlot[0] : 0f;
    public Vector2 FireGlareViewportPosition =>
        fireGlareViewportPositions != null && fireGlareViewportPositions.Length > 0
            ? fireGlareViewportPositions[0]
            : DefaultViewportPosition;
    public int InSightFireCount => inSightFireCount;
    public int SimultaneousFireOverlayCount => Mathf.Clamp(simultaneousFireOverlayCount, 1, MaxTrackedFireGlareTargets);
    public Vector3 SmokeExposureSamplePoint => ResolveSmokeExposureSamplePoint();

    private void OnValidate()
    {
        EnsureFireSlotArrays();
        smokeRiseSpeed = Mathf.Max(0f, smokeRiseSpeed);
        smokeFallSpeed = Mathf.Max(0f, smokeFallSpeed);
        smokeEnterThreshold = Mathf.Clamp01(smokeEnterThreshold);
        smokeExitThreshold = Mathf.Clamp(smokeExitThreshold, 0f, smokeEnterThreshold);
        fireGlareRiseSpeed = Mathf.Max(0f, fireGlareRiseSpeed);
        fireGlareFallSpeed = Mathf.Max(0f, fireGlareFallSpeed);
        fireGlareMaxDistance = Mathf.Max(0.1f, fireGlareMaxDistance);
        fireMinimumViewDot = Mathf.Clamp01(fireMinimumViewDot);
        fireViewportEdgeFade = Mathf.Max(0.01f, fireViewportEdgeFade);
        simultaneousFireOverlayCount = Mathf.Clamp(simultaneousFireOverlayCount, 1, MaxTrackedFireGlareTargets);
        fireGlareScanInterval = Mathf.Max(0.02f, fireGlareScanInterval);
        smokeDensity01 = Mathf.Clamp01(smokeDensity01);
        inSightFireCount = Mathf.Max(0, inSightFireCount);

        for (int i = 0; i < MaxTrackedFireGlareTargets; i++)
        {
            fireGlareBySlot[i] = Mathf.Clamp01(fireGlareBySlot[i]);
            fireGlareViewportPositions[i].x = Mathf.Clamp01(fireGlareViewportPositions[i].x);
            fireGlareViewportPositions[i].y = Mathf.Clamp01(fireGlareViewportPositions[i].y);
        }
    }

    private void Update()
    {
        EnsureCameraResolved();
        UpdateSmokeExposure(Time.deltaTime);
        UpdateFireGlare(Time.deltaTime);
    }

    public void ReportSmokeExposure(float density01)
    {
        if (!HasRecentSmokeSubmission())
        {
            smokeSubmissionDensity = 0f;
        }

        lastSmokeSubmissionTime = Time.time;
        smokeSubmissionDensity = Mathf.Max(smokeSubmissionDensity, Mathf.Clamp01(density01));
    }

    public float GetFireGlare01(int index)
    {
        EnsureFireSlotArrays();

        if (index < 0 || index >= fireGlareBySlot.Length)
        {
            return 0f;
        }

        return fireGlareBySlot[index];
    }

    public Vector2 GetFireGlareViewportPosition(int index)
    {
        EnsureFireSlotArrays();

        if (index < 0 || index >= fireGlareViewportPositions.Length)
        {
            return DefaultViewportPosition;
        }

        return fireGlareViewportPositions[index];
    }

    private void UpdateSmokeExposure(float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        float targetSmoke = HasRecentSmokeSubmission()
            ? smokeSubmissionDensity
            : 0f;
        float speed = targetSmoke >= smokeDensity01 ? smokeRiseSpeed : smokeFallSpeed;
        smokeDensity01 = Mathf.MoveTowards(smokeDensity01, targetSmoke, speed * safeDeltaTime);

        if (isInSmoke)
        {
            isInSmoke = smokeDensity01 > smokeExitThreshold;
        }
        else
        {
            isInSmoke = smokeDensity01 >= smokeEnterThreshold;
        }
    }

    private bool HasRecentSmokeSubmission()
    {
        return Time.time - lastSmokeSubmissionTime <= SmokeSubmissionGraceSeconds;
    }

    private void EnsureCameraResolved()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return;
        }

        targetCamera = GetComponentInChildren<Camera>(true);

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }
    }

    private Vector3 ResolveSmokeExposureSamplePoint()
    {
        EnsureCameraResolved();
        if (targetCamera != null)
        {
            return targetCamera.transform.position;
        }

        return transform.position + Vector3.up * 1.6f;
    }

    private void UpdateFireGlare(float deltaTime)
    {
        EnsureFireSlotArrays();

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        int trackedSlotCount = SimultaneousFireOverlayCount;
        if (ShouldScanFireGlare())
        {
            ScanFireGlareTargets(trackedSlotCount);
        }

        for (int i = 0; i < MaxTrackedFireGlareTargets; i++)
        {
            float targetGlare = i < trackedSlotCount ? fireTargetIntensities[i] : 0f;
            float speed = targetGlare >= fireGlareBySlot[i]
                ? fireGlareRiseSpeed
                : fireGlareFallSpeed;

            fireGlareBySlot[i] = Mathf.MoveTowards(fireGlareBySlot[i], targetGlare, speed * safeDeltaTime);

            if (targetGlare > 0f)
            {
                fireGlareViewportPositions[i] = fireTargetPositions[i];
            }
            else if (fireGlareBySlot[i] <= 0.0001f)
            {
                fireGlareViewportPositions[i] = DefaultViewportPosition;
            }
        }
    }

    private bool ShouldScanFireGlare()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            EnsureCameraResolved();
        }

        if (targetCamera == null)
        {
            ResetFireTargetBuffers();
            inSightFireCount = 0;
            nextFireGlareScanTime = Time.time + fireGlareScanInterval;
            return false;
        }

        if (Time.time < nextFireGlareScanTime)
        {
            return false;
        }

        nextFireGlareScanTime = Time.time + fireGlareScanInterval;
        return true;
    }

    private void ScanFireGlareTargets(int trackedSlotCount)
    {
        ResetFireTargetBuffers();

        int visibleFireCount = 0;
        foreach (IFireTarget fireTarget in BotRuntimeRegistry.ActiveFireTargets)
        {
            bool passes = TryEvaluateFireTarget(
                fireTarget,
                out float candidateIntensity,
                out Vector2 candidateViewportPosition,
                out float candidateDistance);

            if (!passes)
            {
                continue;
            }

            visibleFireCount++;
            InsertFireCandidate(
                trackedSlotCount,
                candidateDistance,
                candidateIntensity,
                candidateViewportPosition,
                fireTargetDistances,
                fireTargetIntensities,
                fireTargetPositions);
        }

        inSightFireCount = visibleFireCount;
    }

    private void ResetFireTargetBuffers()
    {
        for (int i = 0; i < MaxTrackedFireGlareTargets; i++)
        {
            fireTargetIntensities[i] = 0f;
            fireTargetDistances[i] = float.PositiveInfinity;
            fireTargetPositions[i] = DefaultViewportPosition;
        }
    }

    private bool TryEvaluateFireTarget(
        IFireTarget fireTarget,
        out float intensity01,
        out Vector2 viewportPosition,
        out float distanceToPlayer)
    {
        intensity01 = 0f;
        viewportPosition = DefaultViewportPosition;
        distanceToPlayer = float.PositiveInfinity;

        if (targetCamera == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        Vector3 fireWorldPosition = fireTarget.GetWorldPosition();
        Vector3 viewportPoint = targetCamera.WorldToViewportPoint(fireWorldPosition);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        viewportPosition = new Vector2(viewportPoint.x, viewportPoint.y);
        if (viewportPosition.x < 0f ||
            viewportPosition.x > 1f ||
            viewportPosition.y < 0f ||
            viewportPosition.y > 1f)
        {
            return false;
        }

        Vector3 origin = targetCamera.transform.position;
        Vector3 toFire = fireWorldPosition - origin;
        float distance = toFire.magnitude;
        if (distance <= 0.001f || distance > fireGlareMaxDistance)
        {
            return false;
        }

        distanceToPlayer = Vector3.Distance(transform.position, fireWorldPosition);

        Vector3 direction = toFire / distance;
        float viewDot = Vector3.Dot(targetCamera.transform.forward, direction);
        if (viewDot < fireMinimumViewDot)
        {
            return false;
        }

        if (requireLineOfSightForFireGlare)
        {
            float visibilityTolerance = Mathf.Max(0.2f, fireTarget.GetWorldRadius() * 1.25f);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                FireGlareHitBuffer,
                distance,
                fireGlareObstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = FireGlareHitBuffer[i];
                if (ShouldIgnoreFireGlareOccluder(hit, fireTarget, distance, visibilityTolerance))
                {
                    continue;
                }

                return false;
            }
        }

        float distanceFactor = Mathf.Clamp01(1f - distance / fireGlareMaxDistance);
        distanceFactor = Mathf.SmoothStep(0f, 1f, distanceFactor);

        float viewFactor = Mathf.InverseLerp(fireMinimumViewDot, 1f, viewDot);
        viewFactor = Mathf.Lerp(0.6f, 1f, viewFactor);
        float edgeDistance = Mathf.Min(
            Mathf.Min(viewportPosition.x, 1f - viewportPosition.x),
            Mathf.Min(viewportPosition.y, 1f - viewportPosition.y));
        float edgeFactor = Mathf.Clamp01(edgeDistance / fireViewportEdgeFade);
        edgeFactor = Mathf.Lerp(0.55f, 1f, edgeFactor);
        float fireStrength = GetFireTargetStrength(fireTarget);

        intensity01 = Mathf.Clamp01(distanceFactor * viewFactor * edgeFactor * fireStrength * 1.35f);
        return intensity01 > 0f;
    }

    private void EnsureFireSlotArrays()
    {
        if (fireGlareBySlot == null || fireGlareBySlot.Length != MaxTrackedFireGlareTargets)
        {
            fireGlareBySlot = ResizeFireFloatArray(fireGlareBySlot);
        }

        if (fireGlareViewportPositions == null || fireGlareViewportPositions.Length != MaxTrackedFireGlareTargets)
        {
            fireGlareViewportPositions = ResizeFireVector2Array(fireGlareViewportPositions);
        }
    }

    private static float[] ResizeFireFloatArray(float[] source)
    {
        float[] resized = new float[MaxTrackedFireGlareTargets];

        if (source != null)
        {
            int count = Mathf.Min(source.Length, resized.Length);
            Array.Copy(source, resized, count);
        }

        return resized;
    }

    private static Vector2[] ResizeFireVector2Array(Vector2[] source)
    {
        Vector2[] resized = CreateDefaultFireViewportPositions();

        if (source != null)
        {
            int count = Mathf.Min(source.Length, resized.Length);
            Array.Copy(source, resized, count);
        }

        return resized;
    }

    private static Vector2[] CreateDefaultFireViewportPositions()
    {
        Vector2[] positions = new Vector2[MaxTrackedFireGlareTargets];

        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = new Vector2(0.5f, 0.5f);
        }

        return positions;
    }

    private static void InsertFireCandidate(
        int maxSlots,
        float candidateDistance,
        float candidateIntensity,
        Vector2 candidateViewportPosition,
        float[] distances,
        float[] intensities,
        Vector2[] positions)
    {
        int insertIndex = -1;

        for (int i = 0; i < maxSlots; i++)
        {
            bool isCloser = candidateDistance < distances[i] - 0.01f;
            bool isSameDistanceButStronger =
                Mathf.Abs(candidateDistance - distances[i]) <= 0.01f &&
                candidateIntensity > intensities[i];

            if (!isCloser && !isSameDistanceButStronger)
            {
                continue;
            }

            insertIndex = i;
            break;
        }

        if (insertIndex < 0)
        {
            return;
        }

        for (int i = maxSlots - 1; i > insertIndex; i--)
        {
            distances[i] = distances[i - 1];
            intensities[i] = intensities[i - 1];
            positions[i] = positions[i - 1];
        }

        distances[insertIndex] = candidateDistance;
        intensities[insertIndex] = candidateIntensity;
        positions[insertIndex] = candidateViewportPosition;
    }

    private float GetFireTargetStrength(IFireTarget fireTarget)
    {
        if (fireTarget == null)
        {
            return 0f;
        }

        float radiusStrength = Mathf.Clamp01(Mathf.Max(0.5f, fireTarget.GetWorldRadius()));
        float hazardModifier = fireTarget.FireType switch
        {
            FireHazardType.GasFed => 1f,
            FireHazardType.FlammableLiquid => 0.95f,
            FireHazardType.Electrical => 0.85f,
            _ => 0.9f
        };
        return Mathf.Clamp01(radiusStrength * hazardModifier);
    }

    private bool ShouldIgnoreFireGlareOccluder(
        RaycastHit hit,
        IFireTarget fireTarget,
        float targetDistance,
        float visibilityTolerance)
    {
        if (hit.collider == null)
        {
            return true;
        }

        if (hit.distance >= targetDistance - visibilityTolerance)
        {
            return true;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null)
        {
            return false;
        }

        if (hitTransform.IsChildOf(transform))
        {
            return true;
        }

        if (targetCamera != null && hitTransform.IsChildOf(targetCamera.transform))
        {
            return true;
        }

        if (fireTarget is Component fireTargetComponent)
        {
            Transform targetTransform = fireTargetComponent.transform;
            if (targetTransform != null && hitTransform.IsChildOf(targetTransform))
            {
                return true;
            }
        }

        return false;
    }
}
