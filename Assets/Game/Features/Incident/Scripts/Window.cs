using System.Collections;
using System.Collections.Generic;
using RayFire;
using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
public class Window : MonoBehaviour, IInteractable, IOpenable, ISmokeVentPoint, IDamageable
{
    private sealed class SashRuntime
    {
        public Transform Transform;
        public Quaternion ClosedLocalRotation;
        public Quaternion OpenLocalRotation;
        public Quaternion TargetLocalRotation;
        public bool IsOpen;
        public bool IsBroken;
    }

    [Header("Single Sash Fallback")]
    [SerializeField] private Transform singleSashTransform;
    [SerializeField] private Vector3 openLocalEulerOffset = new Vector3(0f, 0f, -65f);

    [Header("Double Sash")]
    [SerializeField] private Transform leftSashTransform;
    [SerializeField] private Vector3 leftOpenLocalEulerOffset = new Vector3(0f, 0f, 65f);
    [SerializeField] private Transform rightSashTransform;
    [SerializeField] private Vector3 rightOpenLocalEulerOffset = new Vector3(0f, 0f, -65f);

    [Header("Interaction")]
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool startsBroken;
    [SerializeField] private bool allowToggle = true;
    [SerializeField] private bool breakOnInteract;
    [SerializeField] private bool isLocked;

    [Header("Ventilation")]
    [SerializeField] private float smokeVentilationReliefWhenOpen = 0.28f;
    [SerializeField] private float smokeVentilationReliefWhenBroken = 0.48f;
    [SerializeField] private float fireDraftRiskWhenOpen = 0.08f;
    [SerializeField] private float fireDraftRiskWhenBroken = 0.2f;

    [Header("Damage")]
    [SerializeField] private float breakDamageThreshold = 0.1f;

    [Header("Shatter Targets")]
    [Tooltip("Glass panels to shatter when single sash breaks.")]
    [SerializeField] private GameObject[] singleSashShatterTargets;
    [Tooltip("Glass panels to shatter when left sash breaks.")]
    [SerializeField] private GameObject[] leftSashShatterTargets;
    [Tooltip("Glass panels to shatter when right sash breaks.")]
    [SerializeField] private GameObject[] rightSashShatterTargets;

    [Header("Fragment Fading")]
    [Tooltip("Seconds before fragments start fading.")]
    [SerializeField] private float fragmentLifeTime = 5f;
    [Tooltip("Seconds for the fade animation.")]
    [SerializeField] private float fragmentFadeTime = 1.5f;
    [Tooltip("How fragments disappear.")]
    [SerializeField] private FadeType fragmentFadeType = FadeType.ScaleDown;

    [Header("Climb Over")]
    [SerializeField] private bool enableClimbOver = true;
    [SerializeField] private Transform climbSideAAnchor;
    [SerializeField] private Transform climbSideBAnchor;
    [SerializeField] private float climbAnchorForwardOffset = 0.9f;
    [SerializeField] private float climbProbeOriginHeight = 1.1f;
    [SerializeField] private float climbProbeDownwardOffset = 1.8f;
    [SerializeField] private float climbProbeMaxSlopeAngle = 45f;
    [SerializeField] private LayerMask climbGroundMask = ~0;
    [SerializeField] private float climbApproachDuration = 0.12f;
    [SerializeField] private float climbApproachArcHeight = 0.04f;
    [SerializeField] private float climbTraverseDuration = 0.55f;
    [SerializeField] private float climbTraverseArcHeight = 0.35f;
    [SerializeField] private float climbTraverseTiltAngle = 8f;
    [SerializeField] private bool drawClimbDebug;

    [Header("Runtime")]
    [SerializeField] private bool isOpen;
    [SerializeField] private bool isBroken;
    [SerializeField] private int openSashCount;
    [SerializeField] private int brokenSashCount;

    private readonly List<SashRuntime> sashes = new List<SashRuntime>(2);
    private bool initialized;
    private Coroutine activeClimbRoutine;

    public bool IsOpen => CountOpenSashes() > 0;
    public bool IsBroken => CountBrokenSashes() > 0;
    public bool IsLocked => isLocked;
    public bool IsDoubleSash => sashes.Count > 1;
    public float SmokeVentilationRelief => GetVentilationContribution(smokeVentilationReliefWhenOpen, smokeVentilationReliefWhenBroken);
    public float FireDraftRisk => GetVentilationContribution(fireDraftRiskWhenOpen, fireDraftRiskWhenBroken);

    private void Awake()
    {
        InitializeState();
    }

    private void OnValidate()
    {
        animationSpeed = Mathf.Max(0f, animationSpeed);
        smokeVentilationReliefWhenOpen = Mathf.Max(0f, smokeVentilationReliefWhenOpen);
        smokeVentilationReliefWhenBroken = Mathf.Max(smokeVentilationReliefWhenOpen, smokeVentilationReliefWhenBroken);
        fireDraftRiskWhenOpen = Mathf.Max(0f, fireDraftRiskWhenOpen);
        fireDraftRiskWhenBroken = Mathf.Max(fireDraftRiskWhenOpen, fireDraftRiskWhenBroken);
        breakDamageThreshold = Mathf.Max(0f, breakDamageThreshold);
        climbAnchorForwardOffset = Mathf.Max(0.1f, climbAnchorForwardOffset);
        climbProbeOriginHeight = Mathf.Max(0f, climbProbeOriginHeight);
        climbProbeDownwardOffset = Mathf.Max(0.1f, climbProbeDownwardOffset);
        climbProbeMaxSlopeAngle = Mathf.Clamp(climbProbeMaxSlopeAngle, 0f, 89f);
        climbApproachDuration = Mathf.Max(0f, climbApproachDuration);
        climbApproachArcHeight = Mathf.Max(0f, climbApproachArcHeight);
        climbTraverseDuration = Mathf.Max(0.01f, climbTraverseDuration);
        climbTraverseArcHeight = Mathf.Max(0f, climbTraverseArcHeight);
        EnsureShatterTargetComponents(singleSashShatterTargets);
        EnsureShatterTargetComponents(leftSashShatterTargets);
        EnsureShatterTargetComponents(rightSashShatterTargets);
    }

    private void Update()
    {
        if (!initialized || sashes.Count == 0)
            return;

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash?.Transform == null)
                continue;

            sash.Transform.localRotation = Quaternion.Slerp(
                sash.Transform.localRotation,
                sash.TargetLocalRotation,
                t);
        }

        SyncRuntimeState();
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
            InitializeState();

        if (sashes.Count == 0)
            return;

        if (isLocked)
            return;

        if (breakOnInteract)
        {
            Vector3 impactPoint = interactor != null ? interactor.transform.position : transform.position;
            BreakPreferredSash(impactPoint, ResolveImpactDirection(interactor, impactPoint), interactor);
            return;
        }

        bool shouldOpen = HasClosedUnbrokenSash();
        if (!shouldOpen && !allowToggle)
            return;

        SetAllUnbrokenSashesOpenState(shouldOpen);
    }

    public void BreakWindow()
    {
        if (!initialized)
            InitializeState();

        for (int i = 0; i < sashes.Count; i++)
            BreakSash(
                sashes[i],
                sashes[i]?.Transform != null ? sashes[i].Transform.position : transform.position,
                transform.forward,
                null);
    }

    public void SetOpenState(bool isOpen)
    {
        if (!initialized)
        {
            InitializeState();
        }

        if (sashes.Count == 0)
        {
            return;
        }

        if (isLocked)
        {
            return;
        }

        SetAllUnbrokenSashesOpenState(isOpen);
    }

    public void SetLockedState(bool locked)
    {
        isLocked = locked;
    }

    public bool CanClimbOver(GameObject interactor)
    {
        return TryResolveClimbTraversal(interactor, out _, out _, out _, out _);
    }

    public bool TryStartClimbOver(GameObject interactor)
    {
        if (activeClimbRoutine != null)
            return false;

        if (!TryResolveClimbTraversal(interactor, out Vector3 startPosition, out Quaternion startRotation, out Vector3 endPosition, out Quaternion endRotation))
            return false;

        activeClimbRoutine = StartCoroutine(PerformClimbOverRoutine(interactor, startPosition, startRotation, endPosition, endRotation));
        return true;
    }

    public void TakeDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (amount < breakDamageThreshold)
            return;

        if (!initialized)
            InitializeState();

        Vector3 impactDirection = hitNormal.sqrMagnitude > 0.001f
            ? -hitNormal.normalized
            : ResolveImpactDirection(source, hitPoint);

        for (int i = 0; i < sashes.Count; i++)
            BreakSash(sashes[i], hitPoint, impactDirection, source);
    }

    private void InitializeState()
    {
        sashes.Clear();

        if (leftSashTransform != null || rightSashTransform != null)
        {
            TryAddSash(leftSashTransform, leftOpenLocalEulerOffset);

            if (rightSashTransform != null && rightSashTransform != leftSashTransform)
                TryAddSash(rightSashTransform, rightOpenLocalEulerOffset);
        }
        else
        {
            Transform single = singleSashTransform != null ? singleSashTransform : transform;
            TryAddSash(single, openLocalEulerOffset);
        }

        initialized = sashes.Count > 0;
        SyncRuntimeState();
    }

    private void TryAddSash(Transform sashTransform, Vector3 openEulerOffset)
    {
        if (sashTransform == null)
            return;

        SashRuntime sash = new SashRuntime
        {
            Transform = sashTransform,
            ClosedLocalRotation = sashTransform.localRotation,
            OpenLocalRotation = sashTransform.localRotation * Quaternion.Euler(openEulerOffset),
            IsBroken = startsBroken,
            IsOpen = startsOpen || startsBroken
        };

        sash.TargetLocalRotation = sash.IsOpen ? sash.OpenLocalRotation : sash.ClosedLocalRotation;
        sash.Transform.localRotation = sash.TargetLocalRotation;
        sashes.Add(sash);
    }

    private void BreakPreferredSash(Vector3 worldPoint, Vector3 impactDirection, GameObject source)
    {
        SashRuntime sash = GetNearestBreakableSash(worldPoint);
        BreakSash(sash, worldPoint, impactDirection, source);
    }

    private SashRuntime GetNearestBreakableSash(Vector3 worldPoint)
    {
        SashRuntime best = null;
        float bestDistanceSq = float.PositiveInfinity;

        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash == null || sash.Transform == null || sash.IsBroken)
                continue;

            float distanceSq = (sash.Transform.position - worldPoint).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                best = sash;
            }
        }

        return best;
    }

    private void BreakSash(SashRuntime sash)
    {
        BreakSash(
            sash,
            sash != null && sash.Transform != null ? sash.Transform.position : transform.position,
            transform.forward,
            null);
    }

    private void BreakSash(
        SashRuntime sash,
        Vector3 impactPoint,
        Vector3 impactDirection,
        GameObject source)
    {
        if (sash == null || sash.Transform == null || sash.IsBroken)
            return;

        DemolishShatterTargets(ResolveShatterTargetsForSash(sash), source, impactPoint, impactDirection);
        sash.IsBroken = true;
        sash.IsOpen = true;
        sash.TargetLocalRotation = sash.OpenLocalRotation;
        sash.Transform.localRotation = sash.TargetLocalRotation;
        SyncRuntimeState();
    }

    private Vector3 ResolveImpactDirection(GameObject source, Vector3 impactPoint)
    {
        if (source != null)
        {
            Vector3 sourceDirection = impactPoint - source.transform.position;
            if (sourceDirection.sqrMagnitude > 0.001f)
                return sourceDirection.normalized;
        }

        return transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
    }

    private bool HasClosedUnbrokenSash()
    {
        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash != null && !sash.IsBroken && !sash.IsOpen)
                return true;
        }

        return false;
    }

    private void SetAllUnbrokenSashesOpenState(bool isOpen)
    {
        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash == null || sash.Transform == null || sash.IsBroken)
                continue;

            sash.IsOpen = isOpen;
            sash.TargetLocalRotation = isOpen ? sash.OpenLocalRotation : sash.ClosedLocalRotation;
        }

        SyncRuntimeState();
    }

    private void SyncRuntimeState()
    {
        openSashCount = CountOpenSashes();
        brokenSashCount = CountBrokenSashes();
        isOpen = openSashCount > 0;
        isBroken = brokenSashCount > 0;
    }

    private bool TryResolveClimbTraversal(
        GameObject interactor,
        out Vector3 startPosition,
        out Quaternion startRotation,
        out Vector3 endPosition,
        out Quaternion endRotation)
    {
        startPosition = transform.position;
        endPosition = transform.position;
        startRotation = transform.rotation;
        endRotation = transform.rotation;

        if (!enableClimbOver || interactor == null || (!IsOpen && !IsBroken))
            return false;

        if (!interactor.TryGetComponent(out FirstPersonController _))
            return false;

        if (interactor.TryGetComponent(out FPSInteractionSystem interactionSystem) && interactionSystem.AreHandsOccupied)
            return false;

        if (interactor.TryGetComponent(out PlayerActionLock actionLock) && actionLock.IsFullyLocked)
            return false;

        Vector3 sideAPosition = ResolveClimbAnchorPosition(true);
        Vector3 sideBPosition = ResolveClimbAnchorPosition(false);
        float distanceToA = (interactor.transform.position - sideAPosition).sqrMagnitude;
        float distanceToB = (interactor.transform.position - sideBPosition).sqrMagnitude;

        startPosition = distanceToA <= distanceToB ? sideAPosition : sideBPosition;
        endPosition = distanceToA <= distanceToB ? sideBPosition : sideAPosition;

        if (!HasTraversableFloorOnFarSide(interactor, endPosition))
            return false;

        Vector3 lookDirection = Vector3.ProjectOnPlane(endPosition - startPosition, Vector3.up);
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Vector3.forward;

        startRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        endRotation = startRotation;
        return true;
    }

    private IEnumerator PerformClimbOverRoutine(
        GameObject interactor,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation)
    {
        PlayerActionLock actionLock = PlayerActionLock.GetOrCreate(interactor);
        CharacterController controller = interactor.GetComponent<CharacterController>();
        StarterAssetsInputs inputs = interactor.GetComponent<StarterAssetsInputs>();

        bool restoreController = controller != null && controller.enabled;
        actionLock?.AcquireFullLock();

        try
        {
            if (inputs != null)
                inputs.ClearGameplayActionInputs();

            if (restoreController)
                controller.enabled = false;

            Transform interactorTransform = interactor.transform;

            yield return MoveTransformRoutine(
                interactorTransform,
                startPosition,
                startRotation,
                Mathf.Max(0f, climbApproachDuration),
                Mathf.Max(0f, climbApproachArcHeight),
                0f);

            yield return MoveTransformRoutine(
                interactorTransform,
                endPosition,
                endRotation,
                Mathf.Max(0.01f, climbTraverseDuration),
                Mathf.Max(0f, climbTraverseArcHeight),
                climbTraverseTiltAngle);
        }
        finally
        {
            if (restoreController && controller != null)
                controller.enabled = true;

            if (inputs != null)
                inputs.ClearGameplayActionInputs();

            actionLock?.ReleaseFullLock();
            activeClimbRoutine = null;
        }
    }

    private IEnumerator MoveTransformRoutine(
        Transform target,
        Vector3 destinationPosition,
        Quaternion destinationRotation,
        float duration,
        float arcHeight,
        float tiltAngle)
    {
        if (target == null)
            yield break;

        if (duration <= 0.001f)
        {
            target.SetPositionAndRotation(destinationPosition, destinationRotation);
            yield break;
        }

        Vector3 startPosition = target.position;
        Quaternion startRotation = target.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Vector3.Lerp(startPosition, destinationPosition, easedT);

            if (arcHeight > 0f)
                position += Vector3.up * (Mathf.Sin(easedT * Mathf.PI) * arcHeight);

            Quaternion baseRotation = Quaternion.Slerp(startRotation, destinationRotation, easedT);
            Quaternion rotation = baseRotation;
            if (Mathf.Abs(tiltAngle) > 0.001f)
            {
                float currentTilt = Mathf.Sin(easedT * Mathf.PI) * tiltAngle;
                rotation = baseRotation * Quaternion.Euler(0f, 0f, currentTilt);
            }

            target.SetPositionAndRotation(position, rotation);
            yield return new WaitForEndOfFrame();
        }

        target.SetPositionAndRotation(destinationPosition, destinationRotation);
    }

    private Vector3 ResolveClimbAnchorPosition(bool sideA)
    {
        Transform anchor = sideA ? climbSideAAnchor : climbSideBAnchor;
        if (anchor != null)
            return anchor.position;

        Vector3 direction = sideA ? -transform.forward : transform.forward;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            horizontalDirection = sideA ? Vector3.left : Vector3.right;

        return transform.position + horizontalDirection.normalized * Mathf.Max(0.1f, climbAnchorForwardOffset);
    }

    private bool HasTraversableFloorOnFarSide(GameObject interactor, Vector3 farSidePosition)
    {
        Vector3 origin = GetWindowCenterWorld() + Vector3.up * Mathf.Max(0f, climbProbeOriginHeight);
        Vector3 target = farSidePosition + Vector3.down * Mathf.Max(0.1f, climbProbeDownwardOffset);
        Vector3 direction = target - origin;
        float maxDistance = direction.magnitude;
        if (maxDistance <= 0.001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            maxDistance,
            climbGroundMask,
            QueryTriggerInteraction.Ignore);

        if (drawClimbDebug)
            Debug.DrawRay(origin, direction, Color.cyan, 1.5f);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        float minNormalY = Mathf.Cos(Mathf.Clamp(climbProbeMaxSlopeAngle, 0f, 89f) * Mathf.Deg2Rad);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (interactor != null && (hitTransform == interactor.transform || hitTransform.IsChildOf(interactor.transform)))
                continue;

            if (hit.normal.y < minNormalY)
                continue;

            return true;
        }

        return false;
    }

    private Vector3 GetWindowCenterWorld()
    {
        if (sashes.Count > 0)
        {
            Vector3 accumulated = Vector3.zero;
            int count = 0;
            for (int i = 0; i < sashes.Count; i++)
            {
                SashRuntime sash = sashes[i];
                if (sash?.Transform == null)
                    continue;

                accumulated += sash.Transform.position;
                count++;
            }

            if (count > 0)
                return accumulated / count;
        }

        return transform.position;
    }

    private float GetVentilationContribution(float openValue, float brokenValue)
    {
        if (sashes.Count == 0)
            return 0f;

        float perSashOpen = sashes.Count == 1 ? openValue : openValue / sashes.Count;
        float perSashBroken = sashes.Count == 1 ? brokenValue : brokenValue / sashes.Count;
        float total = 0f;

        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash == null)
                continue;

            if (sash.IsBroken)
            {
                total += perSashBroken;
            }
            else if (sash.IsOpen)
            {
                total += perSashOpen;
            }
        }

        return total;
    }

    private int CountOpenSashes()
    {
        int count = 0;
        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash != null && (sash.IsOpen || sash.IsBroken))
                count++;
        }

        return count;
    }

    private int CountBrokenSashes()
    {
        int count = 0;
        for (int i = 0; i < sashes.Count; i++)
        {
            SashRuntime sash = sashes[i];
            if (sash != null && sash.IsBroken)
                count++;
        }

        return count;
    }

    private GameObject[] ResolveShatterTargetsForSash(SashRuntime sash)
    {
        if (sash == null || sash.Transform == null)
            return null;

        if (sash.Transform == leftSashTransform)
            return leftSashShatterTargets;

        if (sash.Transform == rightSashTransform)
            return rightSashShatterTargets;

        return singleSashShatterTargets;
    }

    private void DemolishShatterTargets(
        GameObject[] targets,
        GameObject source,
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && targets[i].TryGetComponent(out RayfireRigid rigid))
            {
                rigid.fading.onDemolition = true;
                rigid.fading.lifeType = RFFadeLifeType.ByLifeTime;
                rigid.fading.lifeTime = fragmentLifeTime;
                rigid.fading.fadeType = fragmentFadeType;
                rigid.fading.fadeTime = fragmentFadeTime;
                RayfireBreakImpact.DemolishWithImpact(
                    rigid,
                    source,
                    impactPoint,
                    impactDirection,
                    true,
                    RayfireBreakImpact.DirectionMode.ImpactDirection);
            }
        }
    }

    private static void EnsureShatterTargetComponents(GameObject[] targets)
    {
#if UNITY_EDITOR
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target == null)
                continue;

            if (target.GetComponent<RayfireRigid>() == null)
            {
                RayfireRigid rigid = target.AddComponent<RayfireRigid>();
                rigid.simulationType = SimType.Dynamic;
                rigid.demolitionType = DemolitionType.Runtime;
                rigid.meshDemolition.use = true;
            }

            if (target.GetComponent<RayfireShatter>() == null)
            {
                target.AddComponent<RayfireShatter>();
            }
        }
#endif
    }
}
