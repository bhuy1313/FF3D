using TrueJourney.BotBehavior;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FireGroup : MonoBehaviour, IFireGroupTarget
{
    [Header("Configuration")]
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private FireSimulationManager fireSimulationManager;

    private BoxCollider boxCollider;
    private FireGroupAudioController fireGroupAudioController;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        ResolveFireSimulationManager();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterFireGroup(this);
        EnsureFireGroupAudioController();
        fireGroupAudioController?.Initialize(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterFireGroup(this);
    }

    private void OnValidate()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        ResolveFireSimulationManager();
    }

    public void SetFireSimulationManager(FireSimulationManager manager)
    {
        fireSimulationManager = manager;
    }

    [ContextMenu("Refresh Fire List")]
    public void CollectFires()
    {
        ResolveFireSimulationManager();
    }

    private void EnsureFireGroupAudioController()
    {
        if (fireGroupAudioController == null)
        {
            fireGroupAudioController = GetComponent<FireGroupAudioController>();
        }

        if (fireGroupAudioController == null)
        {
            fireGroupAudioController = gameObject.AddComponent<FireGroupAudioController>();
        }
    }

    public void ApplyWater(float amount)
    {
        ApplyWater(amount, null, FireSuppressionAgent.Water);
    }

    public void ApplyWater(float amount, GameObject sourceUser, FireSuppressionAgent suppressionAgent = FireSuppressionAgent.Water)
    {
        if (amount <= 0f)
        {
            return;
        }

        FireSimulationManager simulationManager = ResolveFireSimulationManager();
        if (simulationManager == null || !simulationManager.IsInitialized)
        {
            return;
        }

        Bounds bounds = GetWorldBounds();
        int activeFireCount = simulationManager.GetBurningTrackedNodeCount(bounds);
        if (activeFireCount <= 0)
        {
            return;
        }

        float distributedAmount = amount / activeFireCount;
        if (distributedAmount <= 0f)
        {
            return;
        }

        FireRuntimeGraph graph = simulationManager.RuntimeGraph;
        if (graph == null)
        {
            return;
        }

        for (int i = 0; i < graph.Count; i++)
        {
            FireRuntimeNode node = graph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning || !bounds.Contains(node.Position))
            {
                continue;
            }

            simulationManager.ApplySuppressionToNode(i, distributedAmount, suppressionAgent);
        }
    }

    public bool HasActiveFires
    {
        get
        {
            FireSimulationManager simulationManager = ResolveFireSimulationManager();
            return simulationManager != null &&
                simulationManager.IsInitialized &&
                simulationManager.HasActiveFire(GetWorldBounds());
        }
    }

    public Vector3 GetClosestActiveFirePosition(Vector3 fromPosition)
    {
        Vector3 fallbackPosition = GetWorldCenter();
        FireSimulationManager simulationManager = ResolveFireSimulationManager();
        if (simulationManager == null || !simulationManager.IsInitialized)
        {
            return fallbackPosition;
        }

        return simulationManager.GetClosestBurningNodePosition(GetWorldBounds(), fromPosition, fallbackPosition);
    }

    public Vector3 GetWorldCenter()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        return boxCollider != null
            ? transform.TransformPoint(boxCollider.center)
            : transform.position;
    }

    public void GetActiveFireMetrics(out int activeFireCount, out float averageIntensity01, out float totalIntensity01)
    {
        activeFireCount = 0;
        averageIntensity01 = 0f;
        totalIntensity01 = 0f;

        FireSimulationManager simulationManager = ResolveFireSimulationManager();
        if (simulationManager == null || !simulationManager.IsInitialized)
        {
            return;
        }

        Bounds bounds = GetWorldBounds();
        activeFireCount = simulationManager.GetBurningTrackedNodeCount(bounds);
        if (activeFireCount <= 0)
        {
            return;
        }

        totalIntensity01 = simulationManager.GetBurningTrackedIntensitySum(bounds);
        averageIntensity01 = Mathf.Clamp01(totalIntensity01 / activeFireCount);
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!string.IsNullOrEmpty(waterTag) && other.CompareTag(waterTag))
        {
            FireExtinguisher extinguisher = other.GetComponentInParent<FireExtinguisher>();
            if (extinguisher != null)
            {
                // FireExtinguisher now applies water through its own cone-cast pipeline.
                return;
            }

            FireHose hose = other.GetComponentInParent<FireHose>();
            if (hose != null)
            {
                // FireHose applies water through its arc logic, not particle collision callbacks.
                return;
            }

            float amount = hose != null 
                ? hose.CurrentApplyWaterRate * Time.deltaTime 
                : 2f * Time.deltaTime; // Arbitrary fallback if not from hose
                
            ApplyWater(amount);
        }
    }

    private Bounds GetWorldBounds()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        return boxCollider != null
            ? boxCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
    }

    private FireSimulationManager ResolveFireSimulationManager()
    {
        if (fireSimulationManager == null)
        {
            fireSimulationManager = FindAnyObjectByType<FireSimulationManager>(FindObjectsInactive.Include);
        }

        return fireSimulationManager;
    }
}
