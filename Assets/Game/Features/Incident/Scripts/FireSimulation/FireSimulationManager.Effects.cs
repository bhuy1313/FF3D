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
        UnityEngine.Transform resolvedEffectRoot = effectRoot != null ? effectRoot : EnsureRuntimeFireEffectRoot();
        effectManager.Configure(
            ordinaryEffectPrefab,
            electricalEffectPrefab,
            flammableLiquidEffectPrefab,
            gasEffectPrefab,
            resolvedEffectRoot,
            maxEffects);

        if (simulationProfile != null)
        {
            effectManager.SetMaxVisibleDistance(simulationProfile.EffectVisibleDistance);
        }

        return effectManager;
    }

    private UnityEngine.Transform EnsureRuntimeFireEffectRoot()
    {
        if (runtimeFireEffectRoot != null)
        {
            return runtimeFireEffectRoot;
        }

        UnityEngine.GameObject rootObject = new UnityEngine.GameObject("RuntimeFireEffects");
        runtimeFireEffectRoot = rootObject.transform;
        runtimeFireEffectRoot.SetParent(transform, false);
        runtimeFireEffectRoot.localPosition = UnityEngine.Vector3.zero;
        runtimeFireEffectRoot.localRotation = UnityEngine.Quaternion.identity;
        runtimeFireEffectRoot.localScale = UnityEngine.Vector3.one;
        return runtimeFireEffectRoot;
    }
}
