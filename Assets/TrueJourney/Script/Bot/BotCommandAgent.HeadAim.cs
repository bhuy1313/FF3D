using UnityEngine;
using UnityEngine.Animations.Rigging;

public partial class BotCommandAgent
{
    private void ResolveHeadAimReferences()
    {
        if (headAimConstraint == null)
        {
            MultiAimConstraint[] constraints = GetComponentsInChildren<MultiAimConstraint>(true);
            for (int i = 0; i < constraints.Length; i++)
            {
                MultiAimConstraint candidate = constraints[i];
                if (candidate != null && candidate.gameObject.name == "HeadAim")
                {
                    headAimConstraint = candidate;
                    break;
                }
            }
        }

        if (headAimRigBuilder == null)
        {
            headAimRigBuilder = GetComponentInChildren<RigBuilder>(true);
        }
    }

    private void ResolveSpineAimReferences()
    {
        if (spineAimConstraint == null)
        {
            MultiAimConstraint[] constraints = GetComponentsInChildren<MultiAimConstraint>(true);
            for (int i = 0; i < constraints.Length; i++)
            {
                MultiAimConstraint candidate = constraints[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.gameObject.name == "SpineIK" || candidate.gameObject.name == "SpineAim")
                {
                    spineAimConstraint = candidate;
                    break;
                }
            }
        }

        if (headAimRigBuilder == null)
        {
            headAimRigBuilder = GetComponentInChildren<RigBuilder>(true);
        }
    }

    private void EnsureHeadAimConstraintConfigured(bool rebuildRig)
    {
        if (!enableHeadAim || headAimConstraint == null)
        {
            return;
        }

        Transform activeTarget = handAimTarget;
        if (activeTarget == null)
        {
            headAimConstraintConfigured = false;
            headAimConstraint.weight = 0f;
            return;
        }

        if (HeadAimConstraintUsesTarget(activeTarget))
        {
            headAimConstraintConfigured = true;
            return;
        }

        MultiAimConstraintData data = headAimConstraint.data;
        WeightedTransformArray sourceObjects = new WeightedTransformArray(0);
        sourceObjects.Add(new WeightedTransform(activeTarget, 1f));
        data.sourceObjects = sourceObjects;
        headAimConstraint.data = data;
        headAimConstraintConfigured = true;

        if (rebuildRig && Application.isPlaying && headAimRigBuilder != null)
        {
            headAimRigBuilder.Build();
        }
    }

    private void EnsureSpineAimConstraintConfigured(bool rebuildRig)
    {
        if (!enableSpineAim || spineAimConstraint == null)
        {
            spineAimConstraintConfigured = false;
            if (spineAimConstraint != null)
            {
                spineAimConstraint.weight = 0f;
            }
            return;
        }

        Transform activeTarget = handAimTarget;
        if (activeTarget == null)
        {
            spineAimConstraintConfigured = false;
            spineAimConstraint.weight = 0f;
            return;
        }

        if (SpineAimConstraintUsesTarget(activeTarget))
        {
            spineAimConstraintConfigured = true;
            return;
        }

        MultiAimConstraintData data = spineAimConstraint.data;
        WeightedTransformArray sourceObjects = new WeightedTransformArray(0);
        sourceObjects.Add(new WeightedTransform(activeTarget, 1f));
        data.sourceObjects = sourceObjects;
        spineAimConstraint.data = data;
        spineAimConstraintConfigured = true;

        if (rebuildRig && Application.isPlaying && headAimRigBuilder != null)
        {
            headAimRigBuilder.Build();
        }
    }

    private bool HeadAimConstraintUsesTarget(Transform target)
    {
        if (headAimConstraint == null || target == null)
        {
            return false;
        }

        WeightedTransformArray sourceObjects = headAimConstraint.data.sourceObjects;
        return sourceObjects.Count == 1 && sourceObjects[0].transform == target;
    }

    private bool SpineAimConstraintUsesTarget(Transform target)
    {
        if (spineAimConstraint == null || target == null)
        {
            return false;
        }

        WeightedTransformArray sourceObjects = spineAimConstraint.data.sourceObjects;
        return sourceObjects.Count == 1 && sourceObjects[0].transform == target;
    }

    private void UpdateHeadAimTarget()
    {
        if (!enableHeadAim)
        {
            return;
        }

        EnsureHeadAimConstraintConfigured(!headAimConstraintConfigured);
        if (!headAimConstraintConfigured)
        {
            return;
        }

        UpdateHeadAimWeight();
    }

    private void UpdateSpineAimTarget()
    {
        if (!enableSpineAim)
        {
            if (spineAimConstraint != null)
            {
                spineAimConstraint.weight = 0f;
            }
            return;
        }

        EnsureSpineAimConstraintConfigured(!spineAimConstraintConfigured);
        if (!spineAimConstraintConfigured)
        {
            return;
        }

        UpdateSpineAimWeight();
    }

    private void SetHeadAimFocus(Vector3 worldPosition)
    {
        headAimFocusActive = true;
    }

    private void ClearHeadAimFocus()
    {
        headAimFocusActive = false;
    }

    private void UpdateHeadAimWeight()
    {
        if (headAimConstraint == null)
        {
            return;
        }

        float targetWeight = headAimFocusActive ? 1f : 0f;
        if (!Application.isPlaying || headAimTargetLerpSpeed <= 0f)
        {
            headAimConstraint.weight = targetWeight;
            return;
        }

        float blend = 1f - Mathf.Exp(-headAimTargetLerpSpeed * Time.deltaTime);
        headAimConstraint.weight = Mathf.Lerp(headAimConstraint.weight, targetWeight, blend);
    }

    private void UpdateSpineAimWeight()
    {
        if (spineAimConstraint == null)
        {
            return;
        }

        float configuredWeight = defaultSpineAimMaxWeight;
        if (inventorySystem != null && inventorySystem.TryGetCurrentSpineAimMaxWeight(out float poseWeight))
        {
            configuredWeight = poseWeight;
        }

        float targetWeight = headAimFocusActive ? Mathf.Clamp01(configuredWeight) : 0f;
        if (!Application.isPlaying || spineAimWeightLerpSpeed <= 0f)
        {
            spineAimConstraint.weight = targetWeight;
            return;
        }

        float blend = 1f - Mathf.Exp(-spineAimWeightLerpSpeed * Time.deltaTime);
        spineAimConstraint.weight = Mathf.Lerp(spineAimConstraint.weight, targetWeight, blend);
    }
}
