using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class PowerLineSplineBuilder : MonoBehaviour
{
    [Serializable]
    public class PowerLineSpan
    {
        public string name = "Line";
        public Transform startAnchor;
        public Transform endAnchor;
        public float sagAmount = 1.25f;
        public float autoSmoothTension = 0.33333334f;
    }

    [Header("Lines")]
    [SerializeField] private List<PowerLineSpan> lines = new List<PowerLineSpan>();

    [Header("Update")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool rebuildOnValidate;
    [SerializeField] private bool autoUpdate = true;

    [Header("References")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Legacy Single Line")]
    [SerializeField, HideInInspector] private Transform startAnchor;
    [SerializeField, HideInInspector] private Transform endAnchor;
    [SerializeField, HideInInspector] private float sagAmount = 1.25f;
    [SerializeField, HideInInspector] private float autoSmoothTension = 0.33333334f;

    private readonly List<Vector3> lastStartPositions = new List<Vector3>();
    private readonly List<Vector3> lastEndPositions = new List<Vector3>();
    private readonly List<float> lastSagAmounts = new List<float>();
    private readonly List<float> lastAutoSmoothTensions = new List<float>();

    public IReadOnlyList<PowerLineSpan> Lines => lines;
    public SplineContainer SplineContainer
    {
        get
        {
            EnsureReferences();
            return splineContainer;
        }
    }

    private void Reset()
    {
        splineContainer = GetComponent<SplineContainer>();
        EnsureAtLeastOneLine();
    }

    private void OnEnable()
    {
        EnsureReferences();
        MigrateLegacyLineIfNeeded();

        if (rebuildOnEnable)
        {
            BuildSpline();
        }
    }

    private void OnValidate()
    {
        EnsureReferences();
        MigrateLegacyLineIfNeeded();
        ClampLineSettings();

        if (rebuildOnValidate)
        {
            BuildSpline();
        }
    }

    private void Update()
    {
        if (!autoUpdate || !HasAnyValidLine())
        {
            return;
        }

        if (HasSourceChanged())
        {
            BuildSpline();
        }
    }

    [ContextMenu("Build Spline")]
    public void BuildSpline()
    {
        EnsureReferences();
        MigrateLegacyLineIfNeeded();
        ClampLineSettings();

        if (splineContainer == null)
        {
            return;
        }

        List<Spline> splines = new List<Spline>();
        foreach (PowerLineSpan line in lines)
        {
            if (!IsValidLine(line))
            {
                continue;
            }

            splines.Add(CreateSpline(line));
        }

        if (splines.Count > 0)
        {
            splineContainer.Splines = splines;
        }

        CacheSourceValues();
    }

    [ContextMenu("Add Line")]
    public void AddLine()
    {
        lines.Add(new PowerLineSpan
        {
            name = $"Line {lines.Count + 1}",
            sagAmount = lines.Count > 0 ? Mathf.Max(0f, lines[lines.Count - 1].sagAmount) : 1.25f,
            autoSmoothTension = lines.Count > 0 ? Mathf.Clamp01(lines[lines.Count - 1].autoSmoothTension) : 0.33333334f
        });
    }

    public void ClearLines()
    {
        lines.Clear();
        CacheSourceValues();
    }

    private Spline CreateSpline(PowerLineSpan line)
    {
        Vector3 startWorld = line.startAnchor.position;
        Vector3 endWorld = line.endAnchor.position;
        Vector3 middleWorld = (startWorld + endWorld) * 0.5f;
        middleWorld.y -= line.sagAmount;

        Spline spline = new Spline();
        spline.Closed = false;
        spline.Add((float3)transform.InverseTransformPoint(startWorld), TangentMode.AutoSmooth);
        spline.Add((float3)transform.InverseTransformPoint(middleWorld), TangentMode.AutoSmooth);
        spline.Add((float3)transform.InverseTransformPoint(endWorld), TangentMode.AutoSmooth);

        for (int i = 0; i < spline.Count; i++)
        {
            spline.SetAutoSmoothTension(i, line.autoSmoothTension);
        }

        return spline;
    }

    private void EnsureReferences()
    {
        if (splineContainer == null)
        {
            splineContainer = GetComponent<SplineContainer>();
        }
    }

    private void EnsureAtLeastOneLine()
    {
        if (lines.Count == 0)
        {
            AddLine();
        }
    }

    private void MigrateLegacyLineIfNeeded()
    {
        if (lines.Count == 0 && (startAnchor != null || endAnchor != null))
        {
            lines.Add(new PowerLineSpan
            {
                name = "Line 1",
                startAnchor = startAnchor,
                endAnchor = endAnchor,
                sagAmount = sagAmount,
                autoSmoothTension = autoSmoothTension
            });
        }
    }

    private void ClampLineSettings()
    {
        foreach (PowerLineSpan line in lines)
        {
            if (line == null)
            {
                continue;
            }

            line.sagAmount = Mathf.Max(0f, line.sagAmount);
            line.autoSmoothTension = Mathf.Clamp01(line.autoSmoothTension);
        }
    }

    private bool HasAnyValidLine()
    {
        foreach (PowerLineSpan line in lines)
        {
            if (IsValidLine(line))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidLine(PowerLineSpan line)
    {
        return line != null && line.startAnchor != null && line.endAnchor != null;
    }

    private bool HasSourceChanged()
    {
        if (lines.Count != lastStartPositions.Count)
        {
            return true;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            PowerLineSpan line = lines[i];
            if (!IsValidLine(line))
            {
                continue;
            }

            if (line.startAnchor.position != lastStartPositions[i]
                || line.endAnchor.position != lastEndPositions[i]
                || !Mathf.Approximately(line.sagAmount, lastSagAmounts[i])
                || !Mathf.Approximately(line.autoSmoothTension, lastAutoSmoothTensions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void CacheSourceValues()
    {
        lastStartPositions.Clear();
        lastEndPositions.Clear();
        lastSagAmounts.Clear();
        lastAutoSmoothTensions.Clear();

        foreach (PowerLineSpan line in lines)
        {
            if (IsValidLine(line))
            {
                lastStartPositions.Add(line.startAnchor.position);
                lastEndPositions.Add(line.endAnchor.position);
            }
            else
            {
                lastStartPositions.Add(Vector3.zero);
                lastEndPositions.Add(Vector3.zero);
            }

            lastSagAmounts.Add(line != null ? line.sagAmount : 0f);
            lastAutoSmoothTensions.Add(line != null ? line.autoSmoothTension : 0f);
        }
    }
}
