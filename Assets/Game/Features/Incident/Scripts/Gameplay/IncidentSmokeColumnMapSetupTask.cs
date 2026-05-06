using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentSmokeColumnMapSetupTask : IncidentMapSetupTask
{
    [SerializeField] private LargeSmokeColumnVfx smokeColumnPrefab;
    [SerializeField] private LargeSmokeColumnVfx ventSmokeColumnPrefab;
    [SerializeField] private Transform runtimeParent;
    [SerializeField] private bool spawnOnlyWhenAnchorResolved = true;
    [SerializeField] private bool spawnVentSmokeColumns = true;
    [SerializeField] private Vector3 ventLocalDirection = Vector3.forward;
    [SerializeField, Range(0f, 1f)] private float ventUpwardBias = 0.62f;
    [SerializeField, Min(0f)] private float ventSurfaceOffset = 0.12f;
    [SerializeField, Min(0)] private int maxVentSmokeColumns = 6;
    [SerializeField] private bool logSpawn;

    private LargeSmokeColumnVfx runtimeSmokeColumn;
    private readonly System.Collections.Generic.List<LargeSmokeColumnVfx> runtimeVentSmokeColumns =
        new System.Collections.Generic.List<LargeSmokeColumnVfx>();

    protected override IEnumerator Execute(IncidentMapSetupContext context)
    {
        if (context == null || context.Payload == null)
        {
            yield break;
        }

        if (smokeColumnPrefab == null)
        {
            yield break;
        }

        IncidentPayloadAnchor anchor = context.ResolvedAnchor;
        if (anchor == null)
        {
            if (spawnOnlyWhenAnchorResolved)
            {
                yield break;
            }

            anchor = IncidentAnchorHazardMapSetupTask.ResolveBestAnchor(
                context.Payload,
                FindObjectsByType<IncidentPayloadAnchor>(FindObjectsInactive.Include));
        }

        if (anchor == null)
        {
            yield break;
        }

        if (runtimeSmokeColumn != null)
        {
            Destroy(runtimeSmokeColumn.gameObject);
            runtimeSmokeColumn = null;
        }

        Transform parent = runtimeParent != null
            ? runtimeParent
            : context.SetupRoot != null
                ? context.SetupRoot.transform
                : transform;

        runtimeSmokeColumn = Instantiate(smokeColumnPrefab, parent);
        runtimeSmokeColumn.gameObject.name = smokeColumnPrefab.gameObject.name;
        runtimeSmokeColumn.PlaceNearAnchor(anchor);
        runtimeSmokeColumn.BindSmokeHazard(anchor.RuntimeSmokeHazard);
        SpawnVentSmokeColumns(anchor, parent);

        if (logSpawn)
        {
            Debug.Log(
                $"{nameof(IncidentSmokeColumnMapSetupTask)} spawned smoke column near anchor '{anchor.name}'.",
                runtimeSmokeColumn);
        }
    }

    private void SpawnVentSmokeColumns(IncidentPayloadAnchor anchor, Transform parent)
    {
        ClearRuntimeVentSmokeColumns();

        if (!spawnVentSmokeColumns || ventSmokeColumnPrefab == null || anchor == null || anchor.RuntimeSmokeHazard == null)
        {
            return;
        }

        anchor.RuntimeSmokeHazard.RefreshLinkedVentPoints();
        var ventPoints = anchor.RuntimeSmokeHazard.LinkedVentPoints;
        if (ventPoints == null || ventPoints.Count == 0)
        {
            return;
        }

        int spawnedCount = 0;
        int spawnLimit = maxVentSmokeColumns <= 0 ? int.MaxValue : maxVentSmokeColumns;
        for (int i = 0; i < ventPoints.Count && spawnedCount < spawnLimit; i++)
        {
            MonoBehaviour ventPoint = ventPoints[i];
            if (ventPoint == null || !(ventPoint is ISmokeVentPoint smokeVentPoint))
            {
                continue;
            }

            LargeSmokeColumnVfx ventSmokeColumn = Instantiate(ventSmokeColumnPrefab, parent);
            ventSmokeColumn.gameObject.name = ventSmokeColumnPrefab.gameObject.name + "_" + spawnedCount;
            ventSmokeColumn.PlaceAtVentPoint(ventPoint, ventLocalDirection, ventUpwardBias, ventSurfaceOffset);
            ventSmokeColumn.BindSmokeHazard(anchor.RuntimeSmokeHazard);
            ventSmokeColumn.BindSmokeVentPoint(smokeVentPoint);
            runtimeVentSmokeColumns.Add(ventSmokeColumn);
            spawnedCount++;
        }
    }

    private void ClearRuntimeVentSmokeColumns()
    {
        for (int i = runtimeVentSmokeColumns.Count - 1; i >= 0; i--)
        {
            LargeSmokeColumnVfx smokeColumn = runtimeVentSmokeColumns[i];
            if (smokeColumn != null)
            {
                Destroy(smokeColumn.gameObject);
            }
        }

        runtimeVentSmokeColumns.Clear();
    }
}
