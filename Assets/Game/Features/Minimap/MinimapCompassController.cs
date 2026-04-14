using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MinimapCompassController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform hostRoot;
    [SerializeField] private bool autoResolveActiveHostRoot = true;
    [SerializeField] private string circleHostRootName = "MinimapCircle";
    [SerializeField] private string squareHostRootName = "MinimapSquare";
    [SerializeField] private Transform rotationSource;
    [SerializeField] private string rotationSourceName = "MinimapCamera";
    [SerializeField] private bool autoResolveRotationSource = true;

    [Header("Layout")]
    [SerializeField] private bool stickToHostBorder = true;
    [SerializeField] private float borderInset = 8f;
    [SerializeField] private float labelRadius = 104f;
    [SerializeField] private Vector2 labelSize = new Vector2(26f, 26f);
    [SerializeField] private float fontSize = 18f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private bool ignoreTimeScale = true;

    private float visualScale = 1f;
    private RectTransform compassRoot;
    private TextMeshProUGUI northLabel;
    private TextMeshProUGUI eastLabel;
    private TextMeshProUGUI southLabel;
    private TextMeshProUGUI westLabel;

    private void Awake()
    {
        ResolveReferences();
        EnsureCompassVisuals();
        UpdateCompass(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureCompassVisuals();
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;
        UpdateCompass(true);
    }

    private void LateUpdate()
    {
        UpdateCompass(ignoreTimeScale);
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
    }

    public void RefreshNow()
    {
        if (!isActiveAndEnabled || gameObject == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        ResolveReferences();
        EnsureCompassVisuals();
        UpdateCompass(true);
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        ApplyLabelStyle(northLabel, "N");
        ApplyLabelStyle(eastLabel, "E");
        ApplyLabelStyle(southLabel, "S");
        ApplyLabelStyle(westLabel, "W");
    }

    private void ResolveReferences()
    {
        if (autoResolveActiveHostRoot)
        {
            RectTransform resolvedHostRoot = ResolveActiveHostRoot();
            if (resolvedHostRoot != null)
            {
                hostRoot = resolvedHostRoot;
            }
        }

        if (hostRoot == null)
        {
            hostRoot = transform as RectTransform;
        }

        if (rotationSource == null && autoResolveRotationSource && gameObject.activeInHierarchy)
        {
            GameObject cameraObject = GameObject.Find(rotationSourceName);
            if (cameraObject != null)
            {
                rotationSource = cameraObject.transform;
            }
        }
    }

    private void EnsureCompassVisuals()
    {
        if (hostRoot == null)
        {
            return;
        }

        RectTransform existingCompassRoot = FindChildRect(transform as RectTransform, "CompassOverlay");
        compassRoot = existingCompassRoot != null ? existingCompassRoot : FindChildRect(hostRoot, "CompassOverlay");
        if (compassRoot == null)
        {
            GameObject rootObject = new GameObject("CompassOverlay", typeof(RectTransform));
            compassRoot = rootObject.GetComponent<RectTransform>();
        }

        if (compassRoot.parent != hostRoot)
        {
            compassRoot.SetParent(hostRoot, false);
        }

        compassRoot.SetAsLastSibling();
        compassRoot.anchorMin = Vector2.zero;
        compassRoot.anchorMax = Vector2.one;
        compassRoot.offsetMin = Vector2.zero;
        compassRoot.offsetMax = Vector2.zero;
        compassRoot.localScale = Vector3.one;

        northLabel = EnsureLabel(northLabel, "NorthLabel", "N");
        eastLabel = EnsureLabel(eastLabel, "EastLabel", "E");
        southLabel = EnsureLabel(southLabel, "SouthLabel", "S");
        westLabel = EnsureLabel(westLabel, "WestLabel", "W");
    }

    private void UpdateCompass(bool _)
    {
        ResolveReferences();
        EnsureCompassVisuals();

        if (rotationSource == null || compassRoot == null)
        {
            return;
        }

        float yaw = rotationSource.eulerAngles.y;
        UpdateLabelPosition(northLabel, Vector2.up, yaw);
        UpdateLabelPosition(eastLabel, Vector2.right, yaw);
        UpdateLabelPosition(southLabel, Vector2.down, yaw);
        UpdateLabelPosition(westLabel, Vector2.left, yaw);
    }

    private void UpdateLabelPosition(TextMeshProUGUI label, Vector2 baseDirection, float yawDegrees)
    {
        if (label == null)
        {
            return;
        }

        RectTransform labelRect = label.rectTransform;
        float radians = yawDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);

        Vector2 rotated = new Vector2(
            (baseDirection.x * cosine) - (baseDirection.y * sine),
            (baseDirection.x * sine) + (baseDirection.y * cosine));

        labelRect.anchoredPosition = ResolveAnchoredPosition(rotated);
        labelRect.localRotation = Quaternion.identity;
        labelRect.localScale = Vector3.one;
        labelRect.sizeDelta = labelSize * visualScale;
    }

    private Vector2 ResolveAnchoredPosition(Vector2 direction)
    {
        if (!stickToHostBorder || hostRoot == null)
        {
            return direction * labelRadius;
        }

        float halfWidth = hostRoot.rect.width * 0.5f;
        float halfHeight = hostRoot.rect.height * 0.5f;
        float labelHalfWidth = labelSize.x * visualScale * 0.5f;
        float labelHalfHeight = labelSize.y * visualScale * 0.5f;

        if (hostRoot.name == circleHostRootName)
        {
            float availableRadius = Mathf.Max(0f, Mathf.Min(halfWidth - labelHalfWidth, halfHeight - labelHalfHeight) - (borderInset * visualScale));
            return direction.normalized * availableRadius;
        }

        float availableX = Mathf.Max(0f, halfWidth - labelHalfWidth - (borderInset * visualScale));
        float availableY = Mathf.Max(0f, halfHeight - labelHalfHeight - (borderInset * visualScale));
        if (Mathf.Abs(direction.x) < 0.0001f)
        {
            return new Vector2(0f, Mathf.Sign(direction.y) * availableY);
        }

        if (Mathf.Abs(direction.y) < 0.0001f)
        {
            return new Vector2(Mathf.Sign(direction.x) * availableX, 0f);
        }

        float scale = Mathf.Min(
            availableX / Mathf.Abs(direction.x),
            availableY / Mathf.Abs(direction.y));

        return direction * scale;
    }

    private TextMeshProUGUI EnsureLabel(TextMeshProUGUI existing, string objectName, string textValue)
    {
        if (existing == null)
        {
            RectTransform existingTransform = FindChildRect(compassRoot, objectName);
            if (existingTransform != null)
            {
                existing = existingTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (existing == null)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(compassRoot, false);
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = labelSize * visualScale;
            existing = labelObject.GetComponent<TextMeshProUGUI>();
        }

        ApplyLabelStyle(existing, textValue);
        return existing;
    }

    private void ApplyLabelStyle(TextMeshProUGUI label, string textValue)
    {
        if (label == null)
        {
            return;
        }

        label.text = textValue;
        label.fontSize = fontSize * visualScale;
        label.color = labelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset fontAsset = null;
        if (LanguageManager.Instance != null)
        {
            fontAsset = LanguageManager.Instance.GetCurrentTMPFont(LanguageFontRole.Default);
        }

        if (fontAsset == null)
        {
            fontAsset = TMP_Settings.defaultFontAsset;
        }

        if (fontAsset != null)
        {
            label.font = fontAsset;
        }
    }

    public void SetVisualScale(float scale)
    {
        visualScale = Mathf.Max(0.5f, scale);
    }

    private static RectTransform FindChildRect(RectTransform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate as RectTransform;
            }
        }

        return null;
    }

    private RectTransform ResolveActiveHostRoot()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            return hostRoot;
        }

        RectTransform circleHostRoot = FindChildRect(rootRect, circleHostRootName);
        if (circleHostRoot != null && circleHostRoot.gameObject.activeInHierarchy)
        {
            return circleHostRoot;
        }

        RectTransform squareHostRoot = FindChildRect(rootRect, squareHostRootName);
        if (squareHostRoot != null && squareHostRoot.gameObject.activeInHierarchy)
        {
            return squareHostRoot;
        }

        if (hostRoot != null && hostRoot.gameObject.activeInHierarchy)
        {
            return hostRoot;
        }

        return circleHostRoot != null ? circleHostRoot : squareHostRoot;
    }
}
