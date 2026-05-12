public sealed partial class FireSimulationManager
{
    private void BuildNodeSnapshots()
    {
        nodeSnapshots.Clear();
        if (runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < visualActiveNodeIndices.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(visualActiveNodeIndices[i]);
            if (node == null || node.IsRemoved || !ShouldRenderNodeEffect(node))
            {
                continue;
            }

            node.Authoring?.SetRuntimeDebugState(node, simulationProfile != null ? simulationProfile.MaxHeat : 2f);
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
        EnsureFireNodeIconManager();
        if (runtimeEffectManager == null || simulationProfile == null)
        {
            return;
        }

        runtimeEffectManager.SyncNodes(nodeSnapshots);
    }

    private void DisableEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        FireNodeIconManager runtimeIconManager = EnsureFireNodeIconManager();
        if (runtimeEffectManager != null)
        {
            runtimeEffectManager.DisableAll();
        }

        if (runtimeIconManager != null)
        {
            runtimeIconManager.enabled = false;
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

        return effectManager;
    }

    private FireNodeIconManager EnsureFireNodeIconManager()
    {
        if (fireNodeIconManager == null)
        {
            fireNodeIconManager = GetComponent<FireNodeIconManager>();
            if (fireNodeIconManager == null)
            {
                fireNodeIconManager = gameObject.AddComponent<FireNodeIconManager>();
            }
        }

        if (!fireNodeIconManager.enabled)
        {
            fireNodeIconManager.enabled = true;
        }

        return fireNodeIconManager;
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
