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

    private bool hasAppliedState;
    private bool lastIsSpraying;

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
        if (hasAppliedState && lastIsSpraying == isSpraying)
        {
            return;
        }

        ApplyState(isSpraying);
    }

    [ContextMenu("Refresh Model State")]
    public void RefreshImmediately()
    {
        bool isSpraying = fireExtinguisher != null && fireExtinguisher.IsSpraying;
        ApplyState(isSpraying);
    }

    private void ResolveReferences()
    {
        if (fireExtinguisher == null)
        {
            fireExtinguisher = GetComponent<FireExtinguisher>();
        }
    }

    private void ApplyState(bool isSpraying)
    {
        hasAppliedState = true;
        lastIsSpraying = isSpraying;

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
