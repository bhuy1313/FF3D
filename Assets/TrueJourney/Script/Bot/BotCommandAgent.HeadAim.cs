using UnityEngine;
using UnityEngine.Animations.Rigging;

public partial class BotCommandAgent
{
    private void ResolveHeadAimReferences()
    {
        if (headAimConstraint == null)
        {
            headAimConstraint = GetComponentInChildren<MultiAimConstraint>(true);
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

        if (runtimeHeadAimTarget == null)
        {
            runtimeHeadAimTarget = FindRuntimeHeadAimTarget();
            if (runtimeHeadAimTarget == null)
            {
                GameObject headAimTargetObject = new GameObject("HeadAimTarget");
                runtimeHeadAimTarget = headAimTargetObject.transform;
                runtimeHeadAimTarget.SetParent(transform, false);
            }
        }

        if (HeadAimConstraintUsesTarget(runtimeHeadAimTarget))
        {
            headAimConstraintConfigured = true;
            return;
        }

        MultiAimConstraintData data = headAimConstraint.data;
        WeightedTransformArray sourceObjects = new WeightedTransformArray(0);
        sourceObjects.Add(new WeightedTransform(runtimeHeadAimTarget, 1f));
        data.sourceObjects = sourceObjects;
        headAimConstraint.data = data;
        headAimConstraintConfigured = true;

        if (rebuildRig && Application.isPlaying && headAimRigBuilder != null)
        {
            headAimRigBuilder.Build();
        }
    }

    private Transform FindRuntimeHeadAimTarget()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            if (childTransforms[i] != null && childTransforms[i].name == "HeadAimTarget")
            {
                return childTransforms[i];
            }
        }

        return null;
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

    private void UpdateHeadAimTarget()
    {
        if (!enableHeadAim)
        {
            return;
        }

        EnsureHeadAimConstraintConfigured(!headAimConstraintConfigured);
        if (!headAimConstraintConfigured || runtimeHeadAimTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDefaultHeadAimPosition();
        if (headAimFocusActive)
        {
            desiredPosition = headAimFocusWorldPosition + Vector3.up * headAimVerticalOffset;
        }

        if (!headAimWorldPositionInitialized)
        {
            currentHeadAimWorldPosition = desiredPosition;
            headAimWorldPositionInitialized = true;
        }
        else
        {
            float blend = headAimTargetLerpSpeed > 0f
                ? 1f - Mathf.Exp(-headAimTargetLerpSpeed * Time.deltaTime)
                : 1f;
            currentHeadAimWorldPosition = Vector3.Lerp(currentHeadAimWorldPosition, desiredPosition, blend);
        }

        runtimeHeadAimTarget.position = currentHeadAimWorldPosition;
    }

    private void SetHeadAimFocus(Vector3 worldPosition)
    {
        headAimFocusWorldPosition = worldPosition;
        headAimFocusActive = true;
    }

    private void ClearHeadAimFocus()
    {
        headAimFocusActive = false;
    }

    private Vector3 GetDefaultHeadAimPosition()
    {
        Vector3 origin = viewPoint != null ? viewPoint.position : transform.position + Vector3.up * headAimVerticalOffset;
        return origin + transform.forward * Mathf.Max(0.5f, headAimDefaultDistance);
    }
}
