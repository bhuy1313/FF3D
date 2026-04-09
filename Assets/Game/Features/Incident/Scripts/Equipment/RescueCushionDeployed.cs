using UnityEngine;

[DisallowMultipleComponent]
public class RescueCushionDeployed : MonoBehaviour, IFallImpactResponder, IInteractable
{
    private enum CushionState
    {
        Inflating = 0,
        Ready = 1,
        Recovering = 2
    }

    [Header("Geometry")]
    [SerializeField] private float cushionWidth = 3.4f;
    [SerializeField] private float cushionHeight = 0.45f;
    [SerializeField] private float landingTriggerHeight = 1.25f;
    [SerializeField] private Collider solidCollider;
    [SerializeField] private Collider landingTrigger;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] stateRenderers = System.Array.Empty<Renderer>();

    [Header("Behavior")]
    [SerializeField] private bool startReady;
    [SerializeField] private float inflateDuration = 1.75f;
    [SerializeField] private float recoveryDuration = 0.9f;
    [SerializeField] private float minimumHandledFallDistance = 3.0f;
    [SerializeField] private float minimumHandledLandingSpeed = 7.5f;
    [SerializeField, Range(0f, 1f)] private float damageMultiplierWhenHandled = 0f;
    [SerializeField] private float reboundVerticalVelocity;

    [Header("Indicator")]
    [SerializeField] private string indicatorColorProperty = "_BaseColor";
    [SerializeField] private Color inflatingColor = new Color(1f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color readyColor = new Color(0.25f, 0.88f, 0.45f, 1f);
    [SerializeField] private Color recoveringColor = new Color(0.95f, 0.38f, 0.25f, 1f);

    [Header("Runtime")]
    [SerializeField] private bool isReady;
    [SerializeField] private bool isInflating;
    [SerializeField] private bool isRecovering;

    private CushionState currentState;
    private float stateTimer;
    private Vector3 visualBaseScale = Vector3.one;

    public bool IsReady => currentState == CushionState.Ready;

    private void Awake()
    {
        EnsureFallbackSetup();
        RefreshGeometry();
        EnterState(startReady ? CushionState.Ready : CushionState.Inflating, immediate: true);
    }

    private void OnEnable()
    {
        EnsureFallbackSetup();
        RefreshGeometry();
        EnterState(startReady ? CushionState.Ready : CushionState.Inflating, immediate: true);
    }

    private void Update()
    {
        TickState(Time.deltaTime);
    }

    private void OnValidate()
    {
        cushionWidth = Mathf.Max(1f, cushionWidth);
        cushionHeight = Mathf.Max(0.1f, cushionHeight);
        landingTriggerHeight = Mathf.Max(0.25f, landingTriggerHeight);
        inflateDuration = Mathf.Max(0f, inflateDuration);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
        minimumHandledFallDistance = Mathf.Max(0f, minimumHandledFallDistance);
        minimumHandledLandingSpeed = Mathf.Max(0f, minimumHandledLandingSpeed);
        damageMultiplierWhenHandled = Mathf.Clamp01(damageMultiplierWhenHandled);
        reboundVerticalVelocity = Mathf.Max(0f, reboundVerticalVelocity);
        RefreshGeometry();
        ApplyVisualStateImmediate();
    }

    public void Interact(GameObject interactor)
    {
    }

    public bool TryHandleFallImpact(FallImpactData impactData, ref FallImpactResponse response)
    {
        if (currentState != CushionState.Ready || impactData.Actor == null)
        {
            return false;
        }

        if (impactData.FallDistance < minimumHandledFallDistance ||
            impactData.LandingSpeed < minimumHandledLandingSpeed)
        {
            return false;
        }

        if (!IsPointWithinCaptureVolume(impactData.ImpactPosition))
        {
            return false;
        }

        response.DamageMultiplier = damageMultiplierWhenHandled;
        response.PreventDamage = damageMultiplierWhenHandled <= 0f;
        if (reboundVerticalVelocity > 0f)
        {
            response.OverrideVerticalVelocity = true;
            response.VerticalVelocity = reboundVerticalVelocity;
        }

        EnterState(CushionState.Recovering, immediate: false);
        return true;
    }

    private void TickState(float deltaTime)
    {
        if (currentState == CushionState.Ready)
        {
            return;
        }

        if (stateTimer > 0f)
        {
            stateTimer = Mathf.Max(0f, stateTimer - Mathf.Max(0f, deltaTime));
        }

        if (currentState == CushionState.Inflating && stateTimer <= 0f)
        {
            EnterState(CushionState.Ready, immediate: false);
            return;
        }

        if (currentState == CushionState.Recovering && stateTimer <= 0f)
        {
            EnterState(CushionState.Ready, immediate: false);
            return;
        }

        ApplyVisualStateImmediate();
    }

    private bool IsPointWithinCaptureVolume(Vector3 worldPosition)
    {
        if (landingTrigger == null)
        {
            return false;
        }

        Vector3 closestPoint = landingTrigger.ClosestPoint(worldPosition);
        return (closestPoint - worldPosition).sqrMagnitude <= 0.04f || landingTrigger.bounds.Contains(worldPosition);
    }

    private void EnterState(CushionState newState, bool immediate)
    {
        currentState = newState;
        switch (currentState)
        {
            case CushionState.Inflating:
                stateTimer = inflateDuration;
                break;
            case CushionState.Recovering:
                stateTimer = recoveryDuration;
                break;
            default:
                stateTimer = 0f;
                break;
        }

        isInflating = currentState == CushionState.Inflating;
        isReady = currentState == CushionState.Ready;
        isRecovering = currentState == CushionState.Recovering;

        if (landingTrigger != null)
        {
            landingTrigger.enabled = currentState == CushionState.Ready;
        }

        ApplyVisualStateImmediate();
        if (!immediate && stateTimer <= 0f && currentState != CushionState.Ready)
        {
            EnterState(CushionState.Ready, immediate: true);
        }
    }

    private void ApplyVisualStateImmediate()
    {
        if (visualRoot == null)
        {
            return;
        }

        float verticalScaleMultiplier = 1f;
        switch (currentState)
        {
            case CushionState.Inflating:
                if (inflateDuration > 0.001f)
                {
                    float inflateT = 1f - Mathf.Clamp01(stateTimer / inflateDuration);
                    verticalScaleMultiplier = Mathf.Lerp(0.2f, 1f, inflateT);
                }
                else
                {
                    verticalScaleMultiplier = 1f;
                }
                break;
            case CushionState.Recovering:
                verticalScaleMultiplier = 0.82f;
                break;
        }

        Vector3 scaled = visualBaseScale;
        scaled.y *= verticalScaleMultiplier;
        visualRoot.localScale = scaled;

        Color targetColor = currentState switch
        {
            CushionState.Inflating => inflatingColor,
            CushionState.Recovering => recoveringColor,
            _ => readyColor
        };

        for (int i = 0; i < stateRenderers.Length; i++)
        {
            Renderer targetRenderer = stateRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Material material = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(indicatorColorProperty))
            {
                material.SetColor(indicatorColorProperty, targetColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", targetColor);
            }
        }
    }

    private void RefreshGeometry()
    {
        if (visualRoot != null)
        {
            visualBaseScale = new Vector3(cushionWidth, cushionHeight, cushionWidth);
            visualRoot.localPosition = new Vector3(0f, cushionHeight * 0.5f, 0f);
        }

        if (solidCollider is BoxCollider solidBox)
        {
            solidBox.size = new Vector3(cushionWidth, cushionHeight, cushionWidth);
            solidBox.center = new Vector3(0f, cushionHeight * 0.5f, 0f);
        }

        if (landingTrigger is BoxCollider triggerBox)
        {
            triggerBox.isTrigger = true;
            triggerBox.size = new Vector3(cushionWidth * 0.9f, landingTriggerHeight, cushionWidth * 0.9f);
            triggerBox.center = new Vector3(0f, cushionHeight + landingTriggerHeight * 0.5f, 0f);
        }
    }

    private void EnsureFallbackSetup()
    {
        if (solidCollider == null)
        {
            solidCollider = GetComponent<Collider>();
        }

        if (solidCollider == null)
        {
            BoxCollider solidBox = gameObject.AddComponent<BoxCollider>();
            solidBox.size = new Vector3(cushionWidth, cushionHeight, cushionWidth);
            solidBox.center = new Vector3(0f, cushionHeight * 0.5f, 0f);
            solidCollider = solidBox;
        }

        if (visualRoot == null)
        {
            Transform existingVisual = transform.Find("CushionVisual");
            if (existingVisual != null)
            {
                visualRoot = existingVisual;
            }
        }

        if (visualRoot == null)
        {
            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualObject.name = "CushionVisual";
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = new Vector3(0f, cushionHeight * 0.5f, 0f);
            visualRoot = visualObject.transform;

            Collider visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null)
            {
                DestroyRuntimeSafe(visualCollider);
            }
        }

        if (landingTrigger == null)
        {
            Transform existingTrigger = transform.Find("LandingTrigger");
            if (existingTrigger != null)
            {
                landingTrigger = existingTrigger.GetComponent<Collider>();
            }
        }

        if (landingTrigger == null)
        {
            GameObject triggerObject = new GameObject("LandingTrigger");
            triggerObject.transform.SetParent(transform, false);
            BoxCollider triggerCollider = triggerObject.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            landingTrigger = triggerCollider;
        }

        if (stateRenderers == null || stateRenderers.Length == 0)
        {
            Renderer fallbackRenderer = visualRoot != null ? visualRoot.GetComponent<Renderer>() : null;
            stateRenderers = fallbackRenderer != null
                ? new[] { fallbackRenderer }
                : System.Array.Empty<Renderer>();
        }

        visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
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
}
