using UnityEngine;

[DisallowMultipleComponent]
public class FireExtinguisherStateModelSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireExtinguisher fireExtinguisher;
    [SerializeField] private GameObject idleModelRoot;
    [SerializeField] private GameObject sprayingModelRoot;

    [Header("Behavior")]
    [SerializeField] private bool hideIdleModelWhileSpraying = true;
    [SerializeField] private bool showSprayingModelOnlyWhileSpraying = true;
    [SerializeField] private bool forcePlayerHolderToUseSprayingModel;

    private bool hasAppliedState;
    private bool lastIsSpraying;
    private bool lastUsedPlayerHolderOverride;
    private bool lastUsedNoHolderOverride;

    private void Awake()
    {
        ResolveReferences();
        RefreshImmediately();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshImmediately();
    }

    private void OnValidate()
    {
        ResolveReferences();
        RefreshImmediately();
    }

    private void Update()
    {
        ResolveReferences();
        bool isSpraying = fireExtinguisher != null && fireExtinguisher.IsSpraying;
        bool usePlayerHolderOverride = ShouldForcePlayerHolderToUseSprayingModel();
        bool useNoHolderOverride = fireExtinguisher == null || fireExtinguisher.CurrentHolder == null;
        if (hasAppliedState &&
            lastIsSpraying == isSpraying &&
            lastUsedPlayerHolderOverride == usePlayerHolderOverride &&
            lastUsedNoHolderOverride == useNoHolderOverride)
        {
            return;
        }

        ApplyState(isSpraying, usePlayerHolderOverride, useNoHolderOverride);
    }

    [ContextMenu("Refresh Model State")]
    public void RefreshImmediately()
    {
        bool isSpraying = fireExtinguisher != null && fireExtinguisher.IsSpraying;
        ApplyState(
            isSpraying,
            ShouldForcePlayerHolderToUseSprayingModel(),
            fireExtinguisher == null || fireExtinguisher.CurrentHolder == null);
    }

    private void ResolveReferences()
    {
        if (fireExtinguisher == null)
        {
            fireExtinguisher = GetComponent<FireExtinguisher>();
        }
    }

    private bool ShouldForcePlayerHolderToUseSprayingModel()
    {
        if (!forcePlayerHolderToUseSprayingModel || fireExtinguisher == null)
        {
            return false;
        }

        GameObject holder = fireExtinguisher.CurrentHolder;
        return holder != null && holder.name == "PlayerCapsule";
    }

    private void ApplyState(bool isSpraying, bool usePlayerHolderOverride, bool useNoHolderOverride)
    {
        hasAppliedState = true;
        lastIsSpraying = isSpraying;
        lastUsedPlayerHolderOverride = usePlayerHolderOverride;
        lastUsedNoHolderOverride = useNoHolderOverride;

        if (usePlayerHolderOverride)
        {
            SetModelActive(idleModelRoot, false);
            SetModelActive(sprayingModelRoot, true);
            return;
        }

        if (useNoHolderOverride)
        {
            SetModelActive(idleModelRoot, true);
            SetModelActive(sprayingModelRoot, false);
            return;
        }

        if (idleModelRoot != null)
        {
            bool idleVisible = hideIdleModelWhileSpraying
                ? !isSpraying
                : true;
            SetModelActive(idleModelRoot, idleVisible);
        }

        if (sprayingModelRoot != null)
        {
            bool sprayingVisible = showSprayingModelOnlyWhileSpraying
                ? isSpraying
                : true;
            SetModelActive(sprayingModelRoot, sprayingVisible);
        }
    }

    private static void SetModelActive(GameObject target, bool active)
    {
        if (target.activeSelf == active)
        {
            return;
        }

        target.SetActive(active);
    }
}
