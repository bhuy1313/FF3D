using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class ThermalVisionController : MonoBehaviour
{
    private const int MaxVisibleSignatures = 8;

    private readonly struct ThermalSignatureSnapshot
    {
        public ThermalSignatureSnapshot(Vector2 screenPosition, float strength, float distanceMeters, Color color, string label)
        {
            ScreenPosition = screenPosition;
            Strength = strength;
            DistanceMeters = distanceMeters;
            Color = color;
            Label = label;
        }

        public Vector2 ScreenPosition { get; }
        public float Strength { get; }
        public float DistanceMeters { get; }
        public Color Color { get; }
        public string Label { get; }
    }

    [Header("Source")]
    [SerializeField] private Camera thermalCamera;

    [Header("Detection")]
    [SerializeField] private float detectionDistance = 40f;
    [SerializeField] private float minimumStrengthThreshold = 0.12f;
    [SerializeField, Range(0f, 0.45f)] private float viewportEdgePadding = 0.04f;

    [Header("Overlay")]
    [SerializeField] private Color overlayTintColor = new Color(0.08f, 0.35f, 0.16f, 0.3f);
    [SerializeField] private Color headerTextColor = new Color(0.82f, 1f, 0.82f, 1f);
    [SerializeField] private Color lowBatteryTextColor = new Color(1f, 0.76f, 0.3f, 1f);
    [SerializeField] private Color fireSignatureColor = new Color(1f, 0.52f, 0.2f, 1f);
    [SerializeField] private Color victimSignatureColor = new Color(1f, 0.86f, 0.52f, 1f);
    [SerializeField] private Color urgentVictimSignatureColor = new Color(1f, 0.7f, 0.3f, 1f);
    [SerializeField] private Color criticalVictimSignatureColor = new Color(1f, 0.32f, 0.22f, 1f);
    [SerializeField, Range(0f, 1f)] private float smokeOverlayMultiplier = 0.32f;
    [SerializeField] private bool drawSignatureBoxes = true;

    [Header("Runtime")]
    [SerializeField] private bool thermalVisionEnabled;
    [SerializeField] private int detectedSignatureCount;

    private readonly List<ThermalSignatureSnapshot> visibleSignatures = new List<ThermalSignatureSnapshot>(MaxVisibleSignatures);
    private Texture2D overlayTexture;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private IThermalVisionBatterySource batterySource;

    public bool IsThermalVisionActive => thermalVisionEnabled;
    public int DetectedSignatureCount => detectedSignatureCount;
    public float SmokeOverlayMultiplier => thermalVisionEnabled ? Mathf.Clamp01(smokeOverlayMultiplier) : 1f;

    public static ThermalVisionController GetOrCreate(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (!target.TryGetComponent(out ThermalVisionController controller))
        {
            controller = target.AddComponent<ThermalVisionController>();
        }

        controller.ResolveCamera();
        return controller;
    }

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnEnable()
    {
        ResolveCamera();
        EnsureOverlayTexture();
    }

    private void OnDisable()
    {
        thermalVisionEnabled = false;
        detectedSignatureCount = 0;
        visibleSignatures.Clear();
    }

    private void OnDestroy()
    {
        if (overlayTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(overlayTexture);
            }
            else
            {
                DestroyImmediate(overlayTexture);
            }

            overlayTexture = null;
        }
    }

    private void Update()
    {
        ResolveCamera();

        if (!thermalVisionEnabled)
        {
            detectedSignatureCount = 0;
            visibleSignatures.Clear();
            return;
        }

        RefreshVisibleSignatures();
    }

    public void SetThermalVisionEnabled(bool enabled)
    {
        thermalVisionEnabled = enabled;

        if (!thermalVisionEnabled)
        {
            detectedSignatureCount = 0;
            visibleSignatures.Clear();
        }
    }

    public bool ToggleThermalVision()
    {
        SetThermalVisionEnabled(!thermalVisionEnabled);
        return thermalVisionEnabled;
    }

    public void BindBatterySource(IThermalVisionBatterySource source)
    {
        batterySource = source;
    }

    public void UnbindBatterySource(IThermalVisionBatterySource source)
    {
        if (ReferenceEquals(batterySource, source))
        {
            batterySource = null;
        }
    }

    private void OnGUI()
    {
        if (!thermalVisionEnabled || !Application.isPlaying)
        {
            return;
        }

        EnsureOverlayTexture();
        EnsureStyles();
        DrawThermalOverlay();
    }

    private void ResolveCamera()
    {
        if (thermalCamera != null && thermalCamera.isActiveAndEnabled)
        {
            return;
        }

        thermalCamera = GetComponentInChildren<Camera>(true);

        if (thermalCamera == null || !thermalCamera.isActiveAndEnabled)
        {
            thermalCamera = Camera.main;
        }
    }

    private void RefreshVisibleSignatures()
    {
        visibleSignatures.Clear();
        detectedSignatureCount = 0;

        if (thermalCamera == null)
        {
            return;
        }

        foreach (IThermalSignatureSource source in BotRuntimeRegistry.ActiveThermalSignatureSources)
        {
            if (!TryEvaluateSignature(source, out ThermalSignatureSnapshot snapshot))
            {
                continue;
            }

            detectedSignatureCount++;
            InsertVisibleSignature(snapshot);
        }
    }

    private bool TryEvaluateSignature(IThermalSignatureSource source, out ThermalSignatureSnapshot snapshot)
    {
        snapshot = default;

        if (source == null || !source.HasThermalSignature || thermalCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = source.GetThermalSignatureWorldPosition();
        Vector3 viewportPoint = thermalCamera.WorldToViewportPoint(worldPosition);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        if (viewportPoint.x < viewportEdgePadding ||
            viewportPoint.x > 1f - viewportEdgePadding ||
            viewportPoint.y < viewportEdgePadding ||
            viewportPoint.y > 1f - viewportEdgePadding)
        {
            return false;
        }

        Vector3 origin = thermalCamera.transform.position;
        float distanceMeters = Vector3.Distance(origin, worldPosition);
        if (distanceMeters > Mathf.Max(0.1f, detectionDistance))
        {
            return false;
        }

        float sourceStrength = Mathf.Clamp01(source.GetThermalSignatureStrength());
        float distanceFactor = Mathf.Clamp01(1f - distanceMeters / Mathf.Max(0.1f, detectionDistance));
        float evaluatedStrength = Mathf.Clamp01(Mathf.Lerp(sourceStrength * 0.4f, sourceStrength, distanceFactor));
        if (evaluatedStrength < Mathf.Clamp01(minimumStrengthThreshold))
        {
            return false;
        }

        Vector2 screenPosition = new Vector2(
            viewportPoint.x * Screen.width,
            (1f - viewportPoint.y) * Screen.height);

        snapshot = new ThermalSignatureSnapshot(
            screenPosition,
            evaluatedStrength,
            distanceMeters,
            ResolveSignatureColor(source.ThermalSignatureCategory),
            ResolveSignatureLabel(source.ThermalSignatureCategory));
        return true;
    }

    private void InsertVisibleSignature(ThermalSignatureSnapshot snapshot)
    {
        int insertIndex = visibleSignatures.Count;
        for (int i = 0; i < visibleSignatures.Count; i++)
        {
            if (snapshot.Strength > visibleSignatures[i].Strength)
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex >= MaxVisibleSignatures && visibleSignatures.Count >= MaxVisibleSignatures)
        {
            return;
        }

        visibleSignatures.Insert(Mathf.Min(insertIndex, visibleSignatures.Count), snapshot);
        if (visibleSignatures.Count > MaxVisibleSignatures)
        {
            visibleSignatures.RemoveAt(visibleSignatures.Count - 1);
        }
    }

    private void DrawThermalOverlay()
    {
        Color previousColor = GUI.color;
        GUI.color = overlayTintColor;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), overlayTexture);
        GUI.color = previousColor;

        GUI.Label(
            new Rect(20f, 16f, 420f, 28f),
            BuildHeaderText(),
            headerStyle);

        for (int i = 0; i < visibleSignatures.Count; i++)
        {
            DrawSignature(visibleSignatures[i], i);
        }
    }

    private void DrawSignature(ThermalSignatureSnapshot snapshot, int index)
    {
        float size = Mathf.Lerp(22f, 54f, snapshot.Strength);
        Rect boxRect = new Rect(
            snapshot.ScreenPosition.x - size * 0.5f,
            snapshot.ScreenPosition.y - size * 0.5f,
            size,
            size);

        Color previousColor = GUI.color;
        GUI.color = new Color(snapshot.Color.r, snapshot.Color.g, snapshot.Color.b, 0.9f);
        if (drawSignatureBoxes)
        {
            GUI.Box(boxRect, GUIContent.none, boxStyle);
        }

        GUI.color = Color.Lerp(snapshot.Color, Color.white, 0.25f);
        GUI.Label(
            new Rect(
                boxRect.xMin - 14f,
                boxRect.yMax + 2f + index * 2f,
                220f,
                24f),
            $"{snapshot.Label}  {snapshot.DistanceMeters:0.0}m",
            labelStyle);
        GUI.color = previousColor;
    }

    private void EnsureOverlayTexture()
    {
        if (overlayTexture != null)
        {
            return;
        }

        overlayTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "ThermalVisionOverlayTexture",
            hideFlags = HideFlags.HideAndDontSave
        };
        overlayTexture.SetPixel(0, 0, Color.white);
        overlayTexture.Apply(false, true);
    }

    private void EnsureStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }

        headerStyle.normal.textColor = batterySource != null && batterySource.IsBatteryLow
            ? lowBatteryTextColor
            : headerTextColor;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
        }
    }

    private Color ResolveSignatureColor(ThermalSignatureCategory category)
    {
        return category switch
        {
            ThermalSignatureCategory.Fire => fireSignatureColor,
            ThermalSignatureCategory.VictimUrgent => urgentVictimSignatureColor,
            ThermalSignatureCategory.VictimCritical => criticalVictimSignatureColor,
            _ => victimSignatureColor
        };
    }

    private static string ResolveSignatureLabel(ThermalSignatureCategory category)
    {
        return category switch
        {
            ThermalSignatureCategory.Fire => "FIRE",
            ThermalSignatureCategory.VictimUrgent => "VICTIM - URGENT",
            ThermalSignatureCategory.VictimCritical => "VICTIM - CRITICAL",
            _ => "VICTIM"
        };
    }

    private string BuildHeaderText()
    {
        string batteryText = batterySource != null
            ? $"  BAT:{Mathf.RoundToInt(Mathf.Clamp01(batterySource.BatteryPercent01) * 100f):000}%"
            : string.Empty;

        string lowFlag = batterySource != null && batterySource.IsBatteryLow
            ? "  LOW"
            : string.Empty;

        return $"THERMAL CAM  HOT:{detectedSignatureCount:00}{batteryText}{lowFlag}";
    }
}
