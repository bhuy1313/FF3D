using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FireGroup : MonoBehaviour, IFireGroupTarget
{
    private enum WaterDistributionMode
    {
        EvenSplit = 0,
        WeightedByCurrentHp = 1
    }

    [Header("Configuration")]
    [SerializeField] private string waterTag = "Water";
    [Tooltip("If true, automatically uses the BoxCollider to find all Fire scripts within its volume at Start.")]
    [SerializeField] private bool autoCollectOnStart = true;
    [SerializeField] private WaterDistributionMode waterDistributionMode = WaterDistributionMode.EvenSplit;
    
    [Header("Status")]
    [SerializeField] private List<Fire> managedFires = new List<Fire>();

    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterFireGroup(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterFireGroup(this);
    }

    private void Start()
    {
        if (autoCollectOnStart)
        {
            CollectFires();
        }
    }

    [ContextMenu("Refresh Fire List")]
    public void CollectFires()
    {
        managedFires.Clear();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();

        Vector3 center = transform.TransformPoint(boxCollider.center);
        Vector3 halfExtents = Vector3.Scale(boxCollider.size, transform.lossyScale) * 0.5f;

        // Use OverlapBox to find all colliders within the box volume, specifically looking for triggers 
        // because Fire scripts typically use trigger SphereColliders.
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        
        foreach (Collider hit in hits)
        {
            Fire fire = hit.GetComponent<Fire>();
            if (fire == null)
                fire = hit.GetComponentInParent<Fire>();

            if (fire != null && !managedFires.Contains(fire))
            {
                managedFires.Add(fire);
            }
        }
    }

    public void ApplyWater(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CleanupManagedFires();

        int activeFireCount = 0;
        float totalWeight = 0f;
        for (int i = 0; i < managedFires.Count; i++)
        {
            Fire fire = managedFires[i];
            if (fire == null || !fire.IsBurning)
            {
                continue;
            }

            activeFireCount++;
            if (waterDistributionMode == WaterDistributionMode.WeightedByCurrentHp)
            {
                totalWeight += Mathf.Max(0f, fire.CurrentHp);
            }
        }

        if (activeFireCount <= 0)
        {
            return;
        }

        FireGroupWaterDistributionMode distributionMode =
            waterDistributionMode == WaterDistributionMode.WeightedByCurrentHp
                ? FireGroupWaterDistributionMode.WeightedByCurrentHp
                : FireGroupWaterDistributionMode.EvenSplit;

        for (int i = 0; i < managedFires.Count; i++)
        {
            Fire fire = managedFires[i];
            if (fire == null || !fire.IsBurning)
            {
                continue;
            }

            float distributedAmount = FireGroupWaterDistributionUtility.GetDistributedAmount(
                amount,
                distributionMode,
                activeFireCount,
                Mathf.Max(0f, fire.CurrentHp),
                totalWeight);
            if (distributedAmount > 0f)
            {
                fire.ApplySuppression(distributedAmount, FireSuppressionAgent.Water);
            }
        }
    }

    public bool HasActiveFires
    {
        get
        {
            CleanupManagedFires();
            for (int i = 0; i < managedFires.Count; i++)
            {
                if (managedFires[i] != null && managedFires[i].IsBurning)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public Vector3 GetClosestActiveFirePosition(Vector3 fromPosition)
    {
        CleanupManagedFires();

        float bestDistanceSq = float.PositiveInfinity;
        Vector3 bestPosition = GetWorldCenter();
        bool found = false;

        for (int i = 0; i < managedFires.Count; i++)
        {
            Fire fire = managedFires[i];
            if (fire == null || !fire.IsBurning)
            {
                continue;
            }

            Vector3 candidate = fire.transform.position;
            float distanceSq = (candidate - fromPosition).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestPosition = candidate;
            found = true;
        }

        return found ? bestPosition : GetWorldCenter();
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

    private void CleanupManagedFires()
    {
        for (int i = managedFires.Count - 1; i >= 0; i--)
        {
            if (managedFires[i] == null)
            {
                managedFires.RemoveAt(i);
            }
        }

        if (managedFires.Count == 0 && autoCollectOnStart)
        {
            CollectFires();
        }
    }
}
