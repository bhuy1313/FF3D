using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class FireSimulationBotTarget : MonoBehaviour, IFireTarget, IThermalSignatureSource
{
    [SerializeField] [Min(0.05f)] private float baseWorldRadius = 0.45f;

    private FireSimulationManager manager;
    private SphereCollider sphereCollider;
    private int nodeIndex = -1;

    public bool IsBurning
    {
        get
        {
            FireRuntimeNode node = GetRuntimeNode();
            return node != null && node.IsTrackedByIncident && node.IsBurning;
        }
    }

    public FireHazardType FireType
    {
        get
        {
            FireRuntimeNode node = GetRuntimeNode();
            return node != null ? node.HazardType : FireHazardType.OrdinaryCombustibles;
        }
    }

    public bool HasThermalSignature => IsBurning;
    public ThermalSignatureCategory ThermalSignatureCategory => ThermalSignatureCategory.Fire;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterFireTarget(this);
        BotRuntimeRegistry.RegisterThermalSignatureSource(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterFireTarget(this);
        BotRuntimeRegistry.UnregisterThermalSignatureSource(this);
    }

    public void Configure(FireSimulationManager owner, int runtimeNodeIndex)
    {
        manager = owner;
        nodeIndex = runtimeNodeIndex;
        name = $"BotFireTarget_Node{runtimeNodeIndex + 1}";
        Refresh();
    }

    public void Refresh()
    {
        FireRuntimeNode node = GetRuntimeNode();
        if (node == null)
        {
            transform.localPosition = Vector3.zero;
            return;
        }

        transform.position = node.Position;
        float radius = GetRuntimeRadius(node);
        if (sphereCollider == null)
        {
            sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
        }

        sphereCollider.radius = radius;
        sphereCollider.enabled = node.IsTrackedByIncident || node.IsBurning;
    }

    public void ApplyWater(float amount)
    {
        ApplySuppression(amount, FireSuppressionAgent.Water, null);
    }

    public void ApplySuppression(float amount, FireSuppressionAgent agent)
    {
        ApplySuppression(amount, agent, null);
    }

    public void ApplySuppression(float amount, FireSuppressionAgent agent, GameObject sourceUser)
    {
        if (manager == null || amount <= 0f)
        {
            return;
        }

        manager.ApplySuppressionSphere(GetWorldPosition(), GetWorldRadius(), amount, agent);
    }

    public FireSuppressionOutcome EvaluateSuppressionOutcome(FireSuppressionAgent agent)
    {
        if (WouldSuppressionWorsen(agent, FireType))
        {
            return FireSuppressionOutcome.UnsafeWorsens;
        }

        return GetSuppressionEffectiveness(agent, FireType, manager != null && manager.ActiveHazardSourceIsolated) >= 0.75f
            ? FireSuppressionOutcome.SafeEffective
            : FireSuppressionOutcome.SafeLimited;
    }

    public Vector3 GetWorldPosition()
    {
        FireRuntimeNode node = GetRuntimeNode();
        return node != null ? node.Position : transform.position;
    }

    public float GetWorldRadius()
    {
        FireRuntimeNode node = GetRuntimeNode();
        return node != null ? GetRuntimeRadius(node) : baseWorldRadius;
    }

    public Vector3 GetThermalSignatureWorldPosition()
    {
        return GetWorldPosition();
    }

    public float GetThermalSignatureStrength()
    {
        FireRuntimeNode node = GetRuntimeNode();
        if (node == null || !node.IsBurning)
        {
            return 0f;
        }

        return Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
    }

    private FireRuntimeNode GetRuntimeNode()
    {
        FireRuntimeGraph graph = manager != null ? manager.RuntimeGraph : null;
        return graph != null ? graph.GetNode(nodeIndex) : null;
    }

    private float GetRuntimeRadius(FireRuntimeNode node)
    {
        float intensity = Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
        return Mathf.Lerp(baseWorldRadius * 0.75f, baseWorldRadius * 1.35f, intensity);
    }

    private static bool WouldSuppressionWorsen(FireSuppressionAgent agent, FireHazardType hazardType)
    {
        return agent == FireSuppressionAgent.Water && hazardType == FireHazardType.FlammableLiquid;
    }

    private static float GetSuppressionEffectiveness(FireSuppressionAgent agent, FireHazardType hazardType, bool hazardSourceIsolated)
    {
        switch (hazardType)
        {
            case FireHazardType.Electrical:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return hazardSourceIsolated ? 0.8f : 0f;
                    case FireSuppressionAgent.CO2:
                        return hazardSourceIsolated ? 1f : 1.35f;
                    default:
                        return hazardSourceIsolated ? 1.05f : 1.25f;
                }

            case FireHazardType.FlammableLiquid:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 0f;
                    case FireSuppressionAgent.CO2:
                        return 1f;
                    default:
                        return 1.2f;
                }

            case FireHazardType.GasFed:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return hazardSourceIsolated ? 0.85f : 0.3f;
                    case FireSuppressionAgent.CO2:
                        return hazardSourceIsolated ? 1f : 0.4f;
                    default:
                        return hazardSourceIsolated ? 1.1f : 0.45f;
                }

            default:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 1f;
                    case FireSuppressionAgent.CO2:
                        return 0.55f;
                    default:
                        return 0.8f;
                }
        }
    }
}
