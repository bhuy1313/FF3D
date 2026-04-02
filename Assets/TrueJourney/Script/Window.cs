using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private string windowChildName = "Window";
    [SerializeField] private Vector3 openLocalEulerOffset = new Vector3(0f, 0f, -65f);

    [Header("Double Sash")]
    [SerializeField] private string leftWindowChildName = "WindowLeft";
    [SerializeField] private Vector3 leftOpenLocalEulerOffset = new Vector3(0f, 0f, 65f);
    [SerializeField] private string rightWindowChildName = "WindowRight";
    [SerializeField] private Vector3 rightOpenLocalEulerOffset = new Vector3(0f, 0f, -65f);

    [Header("Interaction")]
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool startsBroken;
    [SerializeField] private bool allowToggle = true;
    [SerializeField] private bool breakOnInteract;

    [Header("Ventilation")]
    [SerializeField] private float smokeVentilationReliefWhenOpen = 0.28f;
    [SerializeField] private float smokeVentilationReliefWhenBroken = 0.48f;
    [SerializeField] private float fireDraftRiskWhenOpen = 0.08f;
    [SerializeField] private float fireDraftRiskWhenBroken = 0.2f;

    [Header("Damage")]
    [SerializeField] private float breakDamageThreshold = 0.1f;

    private readonly List<SashRuntime> sashes = new List<SashRuntime>(2);
    private bool initialized;

    public bool IsOpen => CountOpenSashes() > 0;
    public bool IsBroken => CountBrokenSashes() > 0;
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
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
            InitializeState();

        if (sashes.Count == 0)
            return;

        if (breakOnInteract)
        {
            Vector3 impactPoint = interactor != null ? interactor.transform.position : transform.position;
            BreakPreferredSash(impactPoint, ResolveImpactDirection(interactor, impactPoint));
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
            BreakSash(sashes[i], sashes[i]?.Transform != null ? sashes[i].Transform.position : transform.position, transform.forward);
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
        BreakPreferredSash(hitPoint, impactDirection);
    }

    private void InitializeState()
    {
        sashes.Clear();

        Transform left = FindNamedChild(leftWindowChildName);
        Transform right = FindNamedChild(rightWindowChildName);

        if (left != null || right != null)
        {
            TryAddSash(left, leftOpenLocalEulerOffset);

            if (right != null && right != left)
                TryAddSash(right, rightOpenLocalEulerOffset);
        }
        else
        {
            Transform single = FindNamedChild(windowChildName);
            if (single == null)
                single = transform;

            TryAddSash(single, openLocalEulerOffset);
        }

        initialized = sashes.Count > 0;
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

    private Transform FindNamedChild(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        return transform.Find(childName);
    }

    private void BreakPreferredSash(Vector3 worldPoint, Vector3 impactDirection)
    {
        SashRuntime sash = GetNearestBreakableSash(worldPoint);
        BreakSash(sash, worldPoint, impactDirection);
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
        BreakSash(sash, sash != null && sash.Transform != null ? sash.Transform.position : transform.position, transform.forward);
    }

    private void BreakSash(SashRuntime sash, Vector3 impactPoint, Vector3 impactDirection)
    {
        if (sash == null || sash.Transform == null || sash.IsBroken)
            return;

        sash.IsBroken = true;
        sash.IsOpen = true;
        sash.TargetLocalRotation = sash.OpenLocalRotation;
        sash.Transform.localRotation = sash.TargetLocalRotation;
        ShatterSashGlass(sash.Transform, impactPoint, impactDirection);
    }

    private void ShatterSashGlass(Transform sashTransform, Vector3 impactPoint, Vector3 impactDirection)
    {
        if (sashTransform == null)
            return;

        MeshShatter[] shatterComponents = sashTransform.GetComponentsInChildren<MeshShatter>(true);
        for (int i = 0; i < shatterComponents.Length; i++)
        {
            MeshShatter shatterComponent = shatterComponents[i];
            if (shatterComponent != null)
                shatterComponent.Shatter(impactPoint, impactDirection, 1f);
        }
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
}
