using UnityEngine;

public sealed partial class FireSimulationManager
{
    private FireSuppressionProfile fallbackSuppressionProfile;

    public void ApplyDraftHeatInBounds(Bounds bounds, float amount)
    {
        if (!initialized || runtimeGraph == null || amount <= 0f)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning || !bounds.Contains(node.Position))
            {
                continue;
            }

            float intensityScale = Mathf.Max(0.35f, Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold)));
            node.Heat += amount * intensityScale;
        }

        NotifyStateChanged();
    }

    public bool ApplySuppressionToNode(int nodeIndex, float amount, FireSuppressionAgent agent)
    {
        if (!initialized || runtimeGraph == null || amount <= 0f)
        {
            return false;
        }

        FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
        if (node == null || !node.IsTrackedByIncident)
        {
            return false;
        }

        float effectiveness = ResolveSuppressionEffectiveness(node, agent);
        bool worsens = ResolveSuppressionWorsens(node, agent);
        if (Mathf.Approximately(effectiveness, 0f) && !worsens)
        {
            return false;
        }

        if (worsens)
        {
            node.Heat += amount * 0.6f;
        }
        else
        {
            if (agent == FireSuppressionAgent.Water)
            {
                node.Wetness += amount;
            }

            node.Heat = Mathf.Max(0f, node.Heat - amount * effectiveness);
            MarkNodeRecentlySuppressed(node);
        }

        NotifyStateChanged();
        return true;
    }

    public int ApplySuppressionSphere(
        Vector3 worldCenter,
        float radius,
        float amount,
        FireSuppressionAgent agent)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f || amount <= 0f)
        {
            return 0;
        }

        int affectedNodeCount = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldCenter).sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                continue;
            }

            float effectiveness = ResolveSuppressionEffectiveness(node, agent);
            bool worsens = ResolveSuppressionWorsens(node, agent);
            if (Mathf.Approximately(effectiveness, 0f) && !worsens)
            {
                continue;
            }

            float normalizedFalloff = 1f - Mathf.Clamp01(Mathf.Sqrt(distanceSqr) / radius);
            if (worsens)
            {
                node.Heat += amount * 0.6f * normalizedFalloff;
            }
            else
            {
                float heatRemoval = amount * effectiveness * normalizedFalloff;
                if (agent == FireSuppressionAgent.Water)
                {
                    node.Wetness += amount * normalizedFalloff;
                }

                node.Heat = Mathf.Max(0f, node.Heat - heatRemoval);
                if (heatRemoval > 0f || (agent == FireSuppressionAgent.Water && normalizedFalloff > 0f))
                {
                    MarkNodeRecentlySuppressed(node);
                }
            }

            affectedNodeCount++;
        }

        if (affectedNodeCount > 0)
        {
            NotifyStateChanged();
        }

        return affectedNodeCount;
    }

    public int ApplySuppressionSphere(
        Vector3 worldCenter,
        float radius,
        float wetnessAmount,
        float heatRemovalAmount)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f)
        {
            return 0;
        }

        int affectedNodeCount = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldCenter).sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                continue;
            }

            float normalizedFalloff = 1f - Mathf.Clamp01(Mathf.Sqrt(distanceSqr) / radius);
            node.Wetness += wetnessAmount * normalizedFalloff;
            node.Heat = Mathf.Max(0f, node.Heat - heatRemovalAmount * normalizedFalloff);
            if (wetnessAmount > 0f || heatRemovalAmount > 0f)
            {
                MarkNodeRecentlySuppressed(node);
            }
            affectedNodeCount++;
        }

        if (affectedNodeCount > 0)
        {
            NotifyStateChanged();
        }

        return affectedNodeCount;
    }

    private FireSuppressionProfile ResolveSuppressionProfile()
    {
        if (suppressionProfile != null)
        {
            return suppressionProfile;
        }

        if (fallbackSuppressionProfile == null)
        {
            fallbackSuppressionProfile = FireSuppressionProfile.CreateDefault();
            fallbackSuppressionProfile.hideFlags = HideFlags.HideAndDontSave;
        }

        return fallbackSuppressionProfile;
    }

    private bool ResolveSuppressionWorsens(FireRuntimeNode node, FireSuppressionAgent agent)
    {
        FireHazardType hazardType = (node != null && node.IncidentNodeKind == FireIncidentNodeKind.Late)
            ? FireHazardType.OrdinaryCombustibles
            : activeIncidentHazardType;
        return ResolveSuppressionProfile().GetWorsens(hazardType, agent, activeHazardSourceIsolated);
    }

    private float ResolveSuppressionEffectiveness(FireRuntimeNode node, FireSuppressionAgent agent)
    {
        FireHazardType hazardType = (node != null && node.IncidentNodeKind == FireIncidentNodeKind.Late)
            ? FireHazardType.OrdinaryCombustibles
            : activeIncidentHazardType;
        return ResolveSuppressionProfile().GetEffectiveness(hazardType, agent, activeHazardSourceIsolated);
    }

    private void MarkNodeRecentlySuppressed(FireRuntimeNode node)
    {
        if (node == null || simulationProfile == null)
        {
            return;
        }

        node.SuppressionRecoveryTimer = Mathf.Max(
            node.SuppressionRecoveryTimer,
            simulationProfile.SuppressionRecoveryDelaySeconds);
    }
}
