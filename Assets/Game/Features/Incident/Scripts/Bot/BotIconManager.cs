using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BotIconManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;

    [Header("Icon")]
    [SerializeField] private bool showBotIcons = true;
    [SerializeField] private Sprite botIconSprite;
    [SerializeField] private Vector3 botIconOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] [Min(8f)] private float botIconScreenSize = 40f;
    [FormerlySerializedAs("botIconFadeStartDistance")]
    [SerializeField] [Min(0f)] private float botIconHideDistance = 10f;
    [SerializeField] [Min(0f)] private float botIconVisibleDistance = 28f;
    [SerializeField] private Color botIconColor = new Color(0.82f, 0.96f, 1f, 0.95f);

    [Header("Debug")]
    [SerializeField] private bool debugBotIcons;
    [SerializeField] private int debugVisibleBotCandidateCount;
    [SerializeField] private int debugActiveBotIconCount;
    [SerializeField] private string debugResolvedCameraName;
    [SerializeField] private string debugResolvedDistanceSourceName;

    private readonly Stack<BotIconView> pooledIcons = new Stack<BotIconView>();
    private readonly Dictionary<BotCommandAgent, BotIconView> activeIconsByBot =
        new Dictionary<BotCommandAgent, BotIconView>();
    private readonly List<BotCommandAgent> releaseScratch = new List<BotCommandAgent>();
    private readonly HashSet<BotCommandAgent> wantedBots = new HashSet<BotCommandAgent>();

    private Canvas runtimeIconCanvas;
    private Transform iconDistanceReference;
    private int nextIconBindingId = 1;

#pragma warning disable 649
    [FormerlySerializedAs("reverseDistanceFade")]
    [SerializeField] private bool legacyReverseDistanceFade;
#pragma warning restore 649

    private void LateUpdate()
    {
        if (!showBotIcons)
        {
            DisableAllIcons();
            return;
        }

        Camera camera = ResolveCamera();
        debugResolvedCameraName = camera != null ? camera.name : "<none>";
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        bool hasCamera = camera != null;

        Transform distanceReference = ResolveDistanceReference();
        Vector3 distanceReferencePosition = distanceReference != null ? distanceReference.position : cameraPosition;
        bool hasDistanceReference = distanceReference != null || hasCamera;
        debugResolvedDistanceSourceName = distanceReference != null ? distanceReference.name : debugResolvedCameraName;

        float hideDistance = Mathf.Max(0f, botIconHideDistance);
        float visibleDistance = Mathf.Max(hideDistance, botIconVisibleDistance);
        float visibleDistanceSqr = visibleDistance * visibleDistance;
        wantedBots.Clear();
        int visibleCandidates = 0;

        foreach (BotCommandAgent bot in BotRuntimeRegistry.ActiveCommandAgents)
        {
            if (bot == null || !bot.isActiveAndEnabled || !bot.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 botPosition = bot.transform.position;
            Vector2 horizontalOffset = hasDistanceReference
                ? new Vector2(
                    botPosition.x - distanceReferencePosition.x,
                    botPosition.z - distanceReferencePosition.z)
                : Vector2.zero;
            float distanceSqr = horizontalOffset.sqrMagnitude;
            if (hasDistanceReference && distanceSqr > visibleDistanceSqr)
            {
                continue;
            }

            float distance = hasDistanceReference ? Mathf.Sqrt(distanceSqr) : 0f;
            bool shouldShowIcon = !hasDistanceReference || (distance >= hideDistance && distance <= visibleDistance);

            if (!shouldShowIcon)
            {
                continue;
            }

            visibleCandidates++;
            wantedBots.Add(bot);
            if (!activeIconsByBot.TryGetValue(bot, out BotIconView iconView) || iconView == null)
            {
                iconView = AcquireIconFromPool();
                if (iconView == null)
                {
                    continue;
                }

                iconView.Bind(nextIconBindingId++);
                activeIconsByBot[bot] = iconView;
            }

            iconView.Apply(
                camera,
                botPosition + botIconOffset,
                1f,
                botIconScreenSize,
                botIconSprite,
                botIconColor);
        }

        debugVisibleBotCandidateCount = visibleCandidates;
        releaseScratch.Clear();
        foreach (KeyValuePair<BotCommandAgent, BotIconView> pair in activeIconsByBot)
        {
            if (pair.Key == null || !wantedBots.Contains(pair.Key))
            {
                releaseScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < releaseScratch.Count; i++)
        {
            BotCommandAgent bot = releaseScratch[i];
            if (activeIconsByBot.TryGetValue(bot, out BotIconView iconView))
            {
                ReleaseIconToPool(iconView);
                activeIconsByBot.Remove(bot);
            }
        }

        debugActiveBotIconCount = activeIconsByBot.Count;
        if (debugBotIcons)
        {
            Debug.Log(
                $"[{nameof(BotIconManager)}] candidates={debugVisibleBotCandidateCount}, activeIcons={debugActiveBotIconCount}, camera={debugResolvedCameraName}, distanceSource={debugResolvedDistanceSourceName}",
                this);
        }
    }

    private void OnDisable()
    {
        DisableAllIcons();
    }

    private void OnDestroy()
    {
        if (runtimeIconCanvas != null)
        {
            Destroy(runtimeIconCanvas.gameObject);
        }
    }

    private void OnValidate()
    {
        botIconHideDistance = Mathf.Max(0f, botIconHideDistance);
        botIconVisibleDistance = Mathf.Max(botIconHideDistance, botIconVisibleDistance);
        botIconScreenSize = Mathf.Max(8f, botIconScreenSize);
    }

    private Camera ResolveCamera()
    {
        if (referenceCamera != null)
        {
            return referenceCamera;
        }

        Camera main = Camera.main;
        if (main != null)
        {
            referenceCamera = main;
        }

        return referenceCamera;
    }

    private Transform ResolveDistanceReference()
    {
        if (iconDistanceReference != null)
        {
            return iconDistanceReference;
        }

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "FirstPersonController" || typeName == "PlayerVitals")
            {
                iconDistanceReference = behaviour.transform;
                return iconDistanceReference;
            }
        }

        Camera camera = ResolveCamera();
        iconDistanceReference = camera != null ? camera.transform : null;
        return iconDistanceReference;
    }

    private BotIconView AcquireIconFromPool()
    {
        while (pooledIcons.Count > 0)
        {
            BotIconView pooled = pooledIcons.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        GameObject iconObject = new GameObject("BotIcon");
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(EnsureIconCanvas().transform, false);

        RectTransform rectTransform = iconObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.15f);
        rectTransform.anchoredPosition3D = Vector3.zero;

        iconObject.AddComponent<CanvasRenderer>();
        return iconObject.AddComponent<BotIconView>();
    }

    private void ReleaseIconToPool(BotIconView iconView)
    {
        if (iconView == null)
        {
            return;
        }

        iconView.Unbind();
        pooledIcons.Push(iconView);
    }

    private void DisableAllIcons()
    {
        foreach (KeyValuePair<BotCommandAgent, BotIconView> pair in activeIconsByBot)
        {
            ReleaseIconToPool(pair.Value);
        }

        activeIconsByBot.Clear();
    }

    private Canvas EnsureIconCanvas()
    {
        if (runtimeIconCanvas != null)
        {
            return runtimeIconCanvas;
        }

        GameObject canvasObject = new GameObject("RuntimeBotIcons");
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        runtimeIconCanvas = canvasObject.AddComponent<Canvas>();
        runtimeIconCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeIconCanvas.sortingOrder = 510;

        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return runtimeIconCanvas;
    }
}
