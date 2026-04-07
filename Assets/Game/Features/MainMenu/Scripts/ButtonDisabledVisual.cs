using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ButtonDisabledVisual : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField, Range(0.1f, 1f)] private float disabledAlphaMultiplier = 0.45f;
    [SerializeField] private bool autoCollectGraphics = true;
    [SerializeField] private Graphic[] explicitGraphics = new Graphic[0];

    private readonly List<Graphic> trackedGraphics = new List<Graphic>();
    private readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();
    private bool initialized;
    private bool lastInteractable = true;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        ApplyCurrentState(force: true);
    }

    private void OnDisable()
    {
        RestoreBaseColors();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            Initialize();
        }

        ApplyCurrentState(force: false);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            initialized = false;
            Initialize();
            ApplyCurrentState(force: true);
        }
    }

    public void RefreshBindings()
    {
        initialized = false;
        Initialize();
        ApplyCurrentState(force: true);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        CollectGraphics();
        CaptureBaseColors();
        lastInteractable = IsInteractable();
        initialized = true;
    }

    private void CollectGraphics()
    {
        trackedGraphics.Clear();
        AddGraphics(explicitGraphics);

        if (autoCollectGraphics)
        {
            AddGraphics(GetComponentsInChildren<Graphic>(true));
        }
    }

    private void AddGraphics(IList<Graphic> graphics)
    {
        if (graphics == null)
        {
            return;
        }

        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || trackedGraphics.Contains(graphic))
            {
                continue;
            }

            trackedGraphics.Add(graphic);
        }
    }

    private void CaptureBaseColors()
    {
        baseColors.Clear();
        for (int i = 0; i < trackedGraphics.Count; i++)
        {
            Graphic graphic = trackedGraphics[i];
            if (graphic == null)
            {
                continue;
            }

            baseColors[graphic] = graphic.color;
        }
    }

    private void ApplyCurrentState(bool force)
    {
        bool interactable = IsInteractable();
        if (!force && interactable == lastInteractable)
        {
            return;
        }

        if (interactable)
        {
            CaptureBaseColors();
        }

        for (int i = 0; i < trackedGraphics.Count; i++)
        {
            Graphic graphic = trackedGraphics[i];
            if (graphic == null || !baseColors.TryGetValue(graphic, out Color baseColor))
            {
                continue;
            }

            graphic.color = interactable ? baseColor : MultiplyAlpha(baseColor, disabledAlphaMultiplier);
        }

        lastInteractable = interactable;
    }

    private void RestoreBaseColors()
    {
        foreach (KeyValuePair<Graphic, Color> entry in baseColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = entry.Value;
            }
        }
    }

    private bool IsInteractable()
    {
        return button == null || button.IsInteractable();
    }

    private static Color MultiplyAlpha(Color color, float multiplier)
    {
        color.a *= Mathf.Clamp01(multiplier);
        return color;
    }
}
