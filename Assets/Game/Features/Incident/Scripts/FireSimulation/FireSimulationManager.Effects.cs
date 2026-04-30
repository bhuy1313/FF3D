public sealed partial class FireSimulationManager
{
    private void BuildNodeSnapshots()
    {
        nodeSnapshots.Clear();
        if (runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            node?.Authoring?.SetRuntimeDebugState(node, simulationProfile != null ? simulationProfile.MaxHeat : 2f);
            if (!ShouldRenderNodeEffect(node))
            {
                continue;
            }

            nodeSnapshots.Add(new FireNodeSnapshot(
                node.Index,
                node.Position,
                node.SurfaceNormal,
                GetNodeVisualIntensity01(node),
                node.HazardType,
                node.IncidentNodeKind));
        }
    }

    private void SyncEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager == null || simulationProfile == null)
        {
            return;
        }

        runtimeEffectManager.SyncNodes(nodeSnapshots);
    }

    private void DisableEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager != null)
        {
            runtimeEffectManager.DisableAll();
        }
    }

    private FireEffectManager EnsureEffectManager()
    {
        if (effectManager == null)
        {
            effectManager = GetComponent<FireEffectManager>();
            if (effectManager == null)
            {
                effectManager = gameObject.AddComponent<FireEffectManager>();
            }
        }

        int maxEffects = simulationProfile != null ? simulationProfile.MaxNodeEffects : 0;
        effectManager.Configure(
            ordinaryEffectPrefab,
            electricalEffectPrefab,
            flammableLiquidEffectPrefab,
            gasEffectPrefab,
            effectRoot,
            maxEffects);

        if (simulationProfile != null)
        {
            effectManager.SetMaxVisibleDistance(simulationProfile.EffectVisibleDistance);
        }

        return effectManager;
    }
}
