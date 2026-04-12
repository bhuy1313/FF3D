using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rescuable))]
public class RescuableCarryPoseRigDriver : MonoBehaviour
{
    [SerializeField] private Rescuable rescuable;
    [SerializeField] private Transform carryPoseRigRoot;

    [Header("Runtime")]
    [SerializeField] private bool isCarryPoseActive;

    private MultiRotationConstraint[] carryPoseConstraints;
    private float[] configuredWeights;
    private bool configuredWeightsCached;
    private Rescuable subscribedRescuable;

    private void Awake()
    {
        AutoAssignReferences();
        CacheConfiguredWeights(true);
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        if (!Application.isPlaying)
        {
            return;
        }

        SubscribeToRescuable();
        ApplyCarryPoseState(rescuable != null && rescuable.IsCarried);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UnsubscribeFromRescuable();
        ApplyCarryPoseState(false);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        if (!Application.isPlaying)
        {
            CacheConfiguredWeights(true);
        }
    }

    private void HandleCarryStateChanged(bool carried)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyCarryPoseState(carried);
    }

    private void AutoAssignReferences()
    {
        rescuable ??= GetComponent<Rescuable>();
        carryPoseRigRoot ??= FindCarryPoseRigRoot();
        CacheConstraintReferences();
    }

    private Transform FindCarryPoseRigRoot()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform candidate = childTransforms[i];
            if (candidate != null && candidate.name == "Rig 1")
            {
                return candidate;
            }
        }

        return null;
    }

    private void CacheConstraintReferences()
    {
        if (carryPoseRigRoot == null)
        {
            carryPoseConstraints = System.Array.Empty<MultiRotationConstraint>();
            return;
        }

        int childCount = carryPoseRigRoot.childCount;
        if (childCount == 0)
        {
            carryPoseConstraints = System.Array.Empty<MultiRotationConstraint>();
            return;
        }

        MultiRotationConstraint[] resolvedConstraints = new MultiRotationConstraint[childCount];
        int resolvedCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = carryPoseRigRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            MultiRotationConstraint constraint = child.GetComponent<MultiRotationConstraint>();
            if (constraint == null)
            {
                continue;
            }

            resolvedConstraints[resolvedCount++] = constraint;
        }

        if (resolvedCount != resolvedConstraints.Length)
        {
            System.Array.Resize(ref resolvedConstraints, resolvedCount);
        }

        carryPoseConstraints = resolvedConstraints;
    }

    private void CacheConfiguredWeights(bool force)
    {
        CacheConstraintReferences();
        if (!force &&
            configuredWeightsCached &&
            configuredWeights != null &&
            configuredWeights.Length == carryPoseConstraints.Length)
        {
            return;
        }

        configuredWeights = new float[carryPoseConstraints.Length];
        for (int i = 0; i < carryPoseConstraints.Length; i++)
        {
            MultiRotationConstraint constraint = carryPoseConstraints[i];
            configuredWeights[i] = constraint != null ? Mathf.Clamp01(constraint.weight) : 0f;
        }

        configuredWeightsCached = true;
    }

    private void SubscribeToRescuable()
    {
        if (subscribedRescuable == rescuable)
        {
            return;
        }

        UnsubscribeFromRescuable();
        subscribedRescuable = rescuable;
        if (subscribedRescuable != null)
        {
            subscribedRescuable.CarryStateChanged += HandleCarryStateChanged;
        }
    }

    private void UnsubscribeFromRescuable()
    {
        if (subscribedRescuable == null)
        {
            return;
        }

        subscribedRescuable.CarryStateChanged -= HandleCarryStateChanged;
        subscribedRescuable = null;
    }

    private void ApplyCarryPoseState(bool carried)
    {
        CacheConfiguredWeights(false);
        isCarryPoseActive = carried;

        for (int i = 0; i < carryPoseConstraints.Length; i++)
        {
            MultiRotationConstraint constraint = carryPoseConstraints[i];
            if (constraint == null)
            {
                continue;
            }

            constraint.weight = carried ? configuredWeights[i] : 0f;
        }
    }
}
