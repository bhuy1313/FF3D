using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireSimulationAreaGroupTarget : MonoBehaviour, IFireGroupTarget
{
    private readonly List<int> memberNodeIndices = new List<int>();

    private FireSimulationManager manager;
    private IncidentOriginArea area;

    public bool HasActiveFires
    {
        get
        {
            FireRuntimeGraph graph = manager != null ? manager.RuntimeGraph : null;
            if (graph == null)
            {
                return false;
            }

            for (int i = 0; i < memberNodeIndices.Count; i++)
            {
                FireRuntimeNode node = graph.GetNode(memberNodeIndices[i]);
                if (node != null && node.IsTrackedByIncident && node.IsBurning)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterFireGroup(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterFireGroup(this);
    }

    public void Configure(FireSimulationManager owner, IncidentOriginArea incidentArea)
    {
        manager = owner;
        area = incidentArea;
        RefreshMembership();
    }

    public void RefreshMembership()
    {
        memberNodeIndices.Clear();
        if (manager == null || area == null)
        {
            return;
        }

        FireRuntimeGraph graph = manager.RuntimeGraph;
        if (graph == null)
        {
            return;
        }

        for (int i = 0; i < graph.Count; i++)
        {
            FireRuntimeNode node = graph.GetNode(i);
            if (node == null || !area.ContainsWorldPosition(node.Position))
            {
                continue;
            }

            memberNodeIndices.Add(i);
        }
    }

    public void ApplyWater(float amount)
    {
        ApplyWater(amount, null, FireSuppressionAgent.Water);
    }

    public void ApplyWater(float amount, GameObject sourceUser, FireSuppressionAgent suppressionAgent)
    {
        if (manager == null || amount <= 0f)
        {
            return;
        }

        FireRuntimeGraph graph = manager.RuntimeGraph;
        if (graph == null)
        {
            return;
        }

        int activeFireCount = 0;
        for (int i = 0; i < memberNodeIndices.Count; i++)
        {
            FireRuntimeNode node = graph.GetNode(memberNodeIndices[i]);
            if (node != null && node.IsTrackedByIncident && node.IsBurning)
            {
                activeFireCount++;
            }
        }

        if (activeFireCount <= 0)
        {
            return;
        }

        float distributedAmount = amount / activeFireCount;
        for (int i = 0; i < memberNodeIndices.Count; i++)
        {
            int nodeIndex = memberNodeIndices[i];
            FireRuntimeNode node = graph.GetNode(nodeIndex);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning)
            {
                continue;
            }

            manager.ApplySuppressionToNode(nodeIndex, distributedAmount, suppressionAgent);
        }
    }

    public Vector3 GetClosestActiveFirePosition(Vector3 fromPosition)
    {
        FireRuntimeGraph graph = manager != null ? manager.RuntimeGraph : null;
        if (graph == null)
        {
            return GetWorldCenter();
        }

        float bestDistanceSq = float.PositiveInfinity;
        Vector3 bestPosition = GetWorldCenter();
        for (int i = 0; i < memberNodeIndices.Count; i++)
        {
            FireRuntimeNode node = graph.GetNode(memberNodeIndices[i]);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning)
            {
                continue;
            }

            float distanceSq = (node.Position - fromPosition).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestPosition = node.Position;
        }

        return bestPosition;
    }

    public Vector3 GetWorldCenter()
    {
        return area != null ? area.GetAreaCenter() : transform.position;
    }
}
