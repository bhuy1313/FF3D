using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class MinimapIconManager : MonoBehaviour
{
    [Serializable]
    private sealed class AutoIconRule
    {
        public enum MatchMode
        {
            ComponentType = 0,
            Tag = 1
        }

        public MatchMode matchMode = MatchMode.ComponentType;
        public string componentTypeName;
        public string targetTag;
        public Sprite iconSprite;
        public Color iconColor = Color.white;
        [Min(0.1f)] public float iconScale = 1f;
        public Vector3 worldOffset = new Vector3(0f, 4f, 0f);
        public bool rotateWithTargetYaw;
        public float yawOffset;
        public bool visibleOnMinimap = true;
        public string sortingLayerName = "Default";
        public int sortingOrder;
        public bool includeInactive;
    }

    [Header("Discovery")]
    [SerializeField] private bool autoDiscoverTargets = true;
    [SerializeField] [Min(0.1f)] private float rescanInterval = 1f;
    [SerializeField] private List<AutoIconRule> autoIconRules = new List<AutoIconRule>();

    [Header("Proxy Root")]
    [SerializeField] private Transform proxyRoot;
    [SerializeField] private bool createProxyRootIfMissing = true;

    [Header("Layer")]
    [SerializeField] private string minimapIconLayerName = "MinimapIcon";

    public static MinimapIconManager Instance { get; private set; }

    private readonly Dictionary<UnityEngine.Object, MinimapIconProxy> proxiesByTarget = new Dictionary<UnityEngine.Object, MinimapIconProxy>();
    private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
    private readonly HashSet<string> invalidTagWarnings = new HashSet<string>(StringComparer.Ordinal);
    private float nextRescanTime;
    private int resolvedMinimapLayer = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        resolvedMinimapLayer = ResolveLayer(minimapIconLayerName);
        EnsureProxyRoot();
        DiscoverTargets();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        resolvedMinimapLayer = ResolveLayer(minimapIconLayerName);
        EnsureProxyRoot();
        DiscoverTargets();
    }

    private void Update()
    {
        CleanupMissingTargets();

        if (!autoDiscoverTargets)
        {
            return;
        }

        if (Time.unscaledTime < nextRescanTime)
        {
            return;
        }

        nextRescanTime = Time.unscaledTime + rescanInterval;
        DiscoverTargets();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterTarget(MinimapIconTarget target)
    {
        if (target == null)
        {
            return;
        }

        RegisterOrUpdateManualTarget(target);
    }

    public void UnregisterTarget(MinimapIconTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (!proxiesByTarget.TryGetValue(target, out MinimapIconProxy proxy))
        {
            return;
        }

        proxiesByTarget.Remove(target);
        if (proxy != null)
        {
            Destroy(proxy.gameObject);
        }
    }

    private void DiscoverTargets()
    {
        MinimapIconTarget[] targets = FindObjectsByType<MinimapIconTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            RegisterOrUpdateManualTarget(targets[i]);
        }

        for (int i = 0; i < autoIconRules.Count; i++)
        {
            DiscoverTargetsForRule(autoIconRules[i]);
        }
    }

    private void CleanupMissingTargets()
    {
        if (proxiesByTarget.Count == 0)
        {
            return;
        }

        List<UnityEngine.Object> staleTargets = null;

        foreach (KeyValuePair<UnityEngine.Object, MinimapIconProxy> pair in proxiesByTarget)
        {
            if (pair.Key != null && pair.Value != null)
            {
                continue;
            }

            if (staleTargets == null)
            {
                staleTargets = new List<UnityEngine.Object>();
            }

            staleTargets.Add(pair.Key);
        }

        if (staleTargets == null)
        {
            return;
        }

        for (int i = 0; i < staleTargets.Count; i++)
        {
            UnityEngine.Object staleTarget = staleTargets[i];
            if (staleTarget != null)
            {
                if (staleTarget is MinimapIconTarget manualTarget)
                {
                    UnregisterTarget(manualTarget);
                    continue;
                }

                UnregisterObject(staleTarget);
                continue;
            }

            proxiesByTarget.Remove(staleTarget);
        }
    }

    private void EnsureProxyRoot()
    {
        if (proxyRoot != null)
        {
            return;
        }

        if (!createProxyRootIfMissing)
        {
            proxyRoot = transform;
            return;
        }

        Transform existing = transform.Find("MinimapIconProxyRoot");
        if (existing != null)
        {
            proxyRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("MinimapIconProxyRoot");
        rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        proxyRoot = rootObject.transform;
    }

    private static int ResolveLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return -1;
        }

        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : -1;
    }

    private void RegisterOrUpdateManualTarget(MinimapIconTarget target)
    {
        if (target == null)
        {
            return;
        }

        EnsureProxyRoot();
        MinimapIconProxy proxy = GetOrCreateProxy(target, target.name);
        proxy.Bind(target, resolvedMinimapLayer);
    }

    private void RegisterOrUpdateAutoTarget(Component component, AutoIconRule rule)
    {
        if (component == null || rule == null)
        {
            return;
        }

        if (component.TryGetComponent(out MinimapIconTarget manualTarget) && manualTarget != null)
        {
            RegisterOrUpdateManualTarget(manualTarget);
            return;
        }

        EnsureProxyRoot();
        MinimapIconProxy proxy = GetOrCreateProxy(component, component.name);
        proxy.Bind(
            component.transform,
            rule.iconSprite,
            rule.iconColor,
            rule.iconScale,
            rule.worldOffset,
            rule.rotateWithTargetYaw,
            rule.yawOffset,
            rule.visibleOnMinimap,
            rule.sortingLayerName,
            rule.sortingOrder,
            resolvedMinimapLayer);
    }

    private MinimapIconProxy GetOrCreateProxy(UnityEngine.Object key, string targetName)
    {
        if (proxiesByTarget.TryGetValue(key, out MinimapIconProxy existingProxy))
        {
            if (existingProxy != null)
            {
                return existingProxy;
            }

            proxiesByTarget.Remove(key);
        }

        GameObject proxyObject = new GameObject($"MinimapIcon_{targetName}", typeof(MinimapIconProxy));
        if (proxyRoot != null)
        {
            proxyObject.transform.SetParent(proxyRoot, false);
        }

        MinimapIconProxy proxy = proxyObject.GetComponent<MinimapIconProxy>();
        proxiesByTarget[key] = proxy;
        return proxy;
    }

    private void DiscoverTargetsForRule(AutoIconRule rule)
    {
        if (rule == null || rule.iconSprite == null)
        {
            return;
        }

        if (rule.matchMode == AutoIconRule.MatchMode.Tag)
        {
            DiscoverTargetsForTagRule(rule);
            return;
        }

        if (string.IsNullOrWhiteSpace(rule.componentTypeName))
        {
            return;
        }

        Type targetType = ResolveType(rule.componentTypeName);
        if (targetType == null)
        {
            return;
        }

        UnityEngine.Object[] foundObjects = FindObjectsByType(
            targetType,
            rule.includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

        for (int i = 0; i < foundObjects.Length; i++)
        {
            if (foundObjects[i] is Component component)
            {
                RegisterOrUpdateAutoTarget(component, rule);
            }
        }
    }

    private void DiscoverTargetsForTagRule(AutoIconRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.targetTag))
        {
            return;
        }

        GameObject[] taggedObjects;
        try
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(rule.targetTag);
        }
        catch (UnityException)
        {
            if (invalidTagWarnings.Add(rule.targetTag))
            {
                Debug.LogWarning($"MinimapIconManager: Tag '{rule.targetTag}' does not exist. Add the tag in Project Settings > Tags and Layers or switch this rule to ComponentType.", this);
            }
            return;
        }

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject taggedObject = taggedObjects[i];
            if (taggedObject == null)
            {
                continue;
            }

            if (!rule.includeInactive && !taggedObject.activeInHierarchy)
            {
                continue;
            }

            RegisterOrUpdateAutoTarget(taggedObject.transform, rule);
        }
    }

    private Type ResolveType(string componentTypeName)
    {
        if (typeCache.TryGetValue(componentTypeName, out Type cachedType))
        {
            return cachedType;
        }

        Type resolvedType = Type.GetType(componentTypeName, false);
        if (resolvedType == null)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                resolvedType = assemblies[i].GetType(componentTypeName, false);
                if (resolvedType != null)
                {
                    break;
                }
            }
        }

        typeCache[componentTypeName] = resolvedType;
        return resolvedType;
    }

    private void UnregisterObject(UnityEngine.Object targetObject)
    {
        if (targetObject == null)
        {
            return;
        }

        if (!proxiesByTarget.TryGetValue(targetObject, out MinimapIconProxy proxy))
        {
            return;
        }

        proxiesByTarget.Remove(targetObject);
        if (proxy != null)
        {
            Destroy(proxy.gameObject);
        }
    }
}
