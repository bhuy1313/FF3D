using System.Collections.Generic;
using UnityEngine;

public sealed partial class FireSimulationManager
{
    private void SyncBotFireTargets()
    {
        if (!autoRegisterBotFireTargets)
        {
            DisableBotFireTargets();
            return;
        }

        if (runtimeGraph == null)
        {
            DisableBotFireTargets();
            return;
        }

        EnsureBotFireTargetCapacity(runtimeGraph.Count);
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved)
            {
                DestroyBotFireTargetAtIndex(i);
                continue;
            }

            FireSimulationBotTarget target = botFireTargets[i];
            if (target == null)
            {
                target = CreateBotFireTarget(i);
                botFireTargets[i] = target;
            }

            if (!target.gameObject.activeSelf)
            {
                target.gameObject.SetActive(true);
            }

            target.Configure(this, i);
            target.Refresh();
        }

        for (int i = runtimeGraph.Count; i < botFireTargets.Count; i++)
        {
            FireSimulationBotTarget target = botFireTargets[i];
            if (target != null)
            {
                target.gameObject.SetActive(false);
            }
        }
    }

    private void SyncBotFireGroups()
    {
        if (!autoRegisterBotFireTargets)
        {
            DisableBotFireGroups();
            return;
        }

        IncidentOriginArea[] areas = FindObjectsByType<IncidentOriginArea>(FindObjectsInactive.Include);
        EnsureBotFireGroupCapacity(areas);

        for (int i = 0; i < botFireGroups.Count; i++)
        {
            FireSimulationAreaGroupTarget target = botFireGroups[i];
            if (target == null)
            {
                continue;
            }

            bool shouldEnable = target.gameObject.scene.IsValid();
            for (int areaIndex = 0; areaIndex < areas.Length; areaIndex++)
            {
                if (areas[areaIndex] != null && target.gameObject == areas[areaIndex].gameObject)
                {
                    shouldEnable = true;
                    target.Configure(this, areas[areaIndex]);
                    target.RefreshMembership();
                    break;
                }
            }

            target.enabled = shouldEnable;
        }
    }

    private void EnsureBotFireTargetCapacity(int count)
    {
        while (botFireTargets.Count < count)
        {
            botFireTargets.Add(null);
        }
    }

    private FireSimulationBotTarget CreateBotFireTarget(int nodeIndex)
    {
        Transform parent = EnsureRuntimeBotFireTargetRoot();
        GameObject targetObject = new GameObject($"BotFireTarget_Node{nodeIndex + 1}");
        targetObject.transform.SetParent(parent, false);
        return targetObject.AddComponent<FireSimulationBotTarget>();
    }

    private void DestroyBotFireTargetAtIndex(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= botFireTargets.Count)
        {
            return;
        }

        FireSimulationBotTarget target = botFireTargets[nodeIndex];
        if (target == null)
        {
            return;
        }

        botFireTargets[nodeIndex] = null;
        Destroy(target.gameObject);
    }

    private void EnsureBotFireGroupCapacity(IReadOnlyList<IncidentOriginArea> areas)
    {
        botFireGroups.Clear();
        if (areas == null)
        {
            return;
        }

        for (int i = 0; i < areas.Count; i++)
        {
            IncidentOriginArea area = areas[i];
            if (area == null || !area.gameObject.scene.IsValid())
            {
                continue;
            }

            FireSimulationAreaGroupTarget target = area.GetComponent<FireSimulationAreaGroupTarget>();
            if (target == null)
            {
                target = area.gameObject.AddComponent<FireSimulationAreaGroupTarget>();
            }

            botFireGroups.Add(target);
        }
    }

    private void DisableBotFireTargets()
    {
        for (int i = 0; i < botFireTargets.Count; i++)
        {
            FireSimulationBotTarget target = botFireTargets[i];
            if (target != null)
            {
                target.gameObject.SetActive(false);
            }
        }
    }

    private void DisableBotFireGroups()
    {
        for (int i = 0; i < botFireGroups.Count; i++)
        {
            FireSimulationAreaGroupTarget target = botFireGroups[i];
            if (target != null)
            {
                target.enabled = false;
            }
        }
    }

    private Transform EnsureRuntimeBotFireTargetRoot()
    {
        if (runtimeBotFireTargetRoot != null)
        {
            return runtimeBotFireTargetRoot;
        }

        GameObject rootObject = new GameObject("RuntimeBotFireTargets");
        runtimeBotFireTargetRoot = rootObject.transform;
        runtimeBotFireTargetRoot.SetParent(transform, false);
        runtimeBotFireTargetRoot.localPosition = Vector3.zero;
        runtimeBotFireTargetRoot.localRotation = Quaternion.identity;
        runtimeBotFireTargetRoot.localScale = Vector3.one;
        return runtimeBotFireTargetRoot;
    }
}
