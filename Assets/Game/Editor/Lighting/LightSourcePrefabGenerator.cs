using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LightSourcePrefabGenerator
{
    private const string RootFolder = "Assets/Game/Shared";
    private const string MaterialFolder = RootFolder + "/Materials/Lights";
    private const string PrefabFolder = RootFolder + "/Prefabs/Lights";
    private const string AutoGenerateSessionKey = "TrueJourney.LightSourcePrefabs.AutoGenerateAttempted";

    [InitializeOnLoadMethod]
    private static void ScheduleAutoGeneration()
    {
        EditorApplication.delayCall += AutoGenerateMissingPrefabs;
    }

    [MenuItem("Tools/TrueJourney/Lighting/Generate Light Source Prefabs")]
    public static void GenerateLightSourcePrefabs()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            litShader = Shader.Find("Standard");
        }

        if (litShader == null)
        {
            Debug.LogError("Could not find a Lit shader for generated light source prefabs.");
            return;
        }

        Material housingMaterial = CreateLitMaterial("LightHousing.mat", litShader, new Color(0.12f, 0.13f, 0.16f), 0f, 0.55f, 0.1f);
        Material trimMaterial = CreateLitMaterial("LightTrim.mat", litShader, new Color(0.32f, 0.33f, 0.36f), 0f, 0.75f, 0.35f);
        Material warmEmitterMaterial = CreateLitMaterial("WarmEmitter.mat", litShader, new Color(1f, 0.73f, 0.36f), 4.5f, 0.1f, 0f);
        Material coolEmitterMaterial = CreateLitMaterial("CoolEmitter.mat", litShader, new Color(0.72f, 0.9f, 1f), 4.8f, 0.05f, 0f);
        Material redEmitterMaterial = CreateLitMaterial("RedEmitter.mat", litShader, new Color(1f, 0.24f, 0.18f), 4.8f, 0.05f, 0f);
        Material neonEmitterMaterial = CreateLitMaterial("NeonEmitter.mat", litShader, new Color(0.26f, 1f, 0.94f), 5.5f, 0.05f, 0f);

        List<string> createdPrefabs = new List<string>();
        createdPrefabs.Add(CreateTorchPrefab(housingMaterial, trimMaterial, warmEmitterMaterial));
        createdPrefabs.Add(CreateLanternPrefab(housingMaterial, trimMaterial, warmEmitterMaterial));
        createdPrefabs.Add(CreateCeilingLampPrefab(housingMaterial, trimMaterial, coolEmitterMaterial));
        createdPrefabs.Add(CreateEmergencyBeaconPrefab(housingMaterial, trimMaterial, redEmitterMaterial));
        createdPrefabs.Add(CreateFloodLightPrefab(housingMaterial, trimMaterial, coolEmitterMaterial));
        createdPrefabs.Add(CreateNeonBarPrefab(trimMaterial, neonEmitterMaterial));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Generated light source prefabs:\n- " + string.Join("\n- ", createdPrefabs));
    }

    private static void AutoGenerateMissingPrefabs()
    {
        if (SessionState.GetBool(AutoGenerateSessionKey, false))
        {
            return;
        }

        if (AllExpectedPrefabsExist())
        {
            return;
        }

        SessionState.SetBool(AutoGenerateSessionKey, true);
        GenerateLightSourcePrefabs();
    }

    private static bool AllExpectedPrefabsExist()
    {
        string[] expectedPaths =
        {
            PrefabFolder + "/TorchLightSource.prefab",
            PrefabFolder + "/LanternLightSource.prefab",
            PrefabFolder + "/CeilingLampLightSource.prefab",
            PrefabFolder + "/EmergencyBeaconLightSource.prefab",
            PrefabFolder + "/FloodLightSource.prefab",
            PrefabFolder + "/NeonBarLightSource.prefab"
        };

        for (int i = 0; i < expectedPaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(expectedPaths[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static string CreateTorchPrefab(Material housingMaterial, Material trimMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("TorchLightSource");
        GameObject handle = CreatePrimitive("Handle", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.45f, 0.08f), Quaternion.identity, housingMaterial);
        CreatePrimitive("Grip", PrimitiveType.Cylinder, handle.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.22f, 0.42f, 1.22f), Quaternion.identity, trimMaterial);
        GameObject ember = CreatePrimitive("Ember", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.identity, emitterMaterial);
        CreatePrimitive("Crown", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.86f, 0f), new Vector3(0.13f, 0.12f, 0.13f), Quaternion.identity, trimMaterial);

        Light pointLight = CreateLight("FireLight", root.transform, LightType.Point, new Vector3(0f, 1f, 0f), 4.2f, 9f, new Color(1f, 0.67f, 0.33f), LightShadows.None, 0f, 0f);
        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            pointLight,
            new[] { ember.GetComponent<Renderer>() },
            AnimatedLightSource.MotionMode.Flicker,
            4.2f,
            5.2f,
            new Color(1f, 0.63f, 0.28f),
            0.35f,
            3.5f,
            null,
            Vector3.up,
            0f);

        return SavePrefab(root);
    }

    private static string CreateLanternPrefab(Material housingMaterial, Material trimMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("LanternLightSource");
        CreatePrimitive("Base", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.22f, 0.08f, 0.22f), Quaternion.identity, housingMaterial);
        CreatePrimitive("Top", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0.2f, 0.06f, 0.2f), Quaternion.identity, housingMaterial);
        CreatePrimitive("Hook", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.035f, 0.14f, 0.035f), Quaternion.identity, trimMaterial);
        CreatePrimitive("FrameA", PrimitiveType.Cube, root.transform, new Vector3(0.18f, 0.48f, 0f), new Vector3(0.04f, 0.52f, 0.04f), Quaternion.identity, trimMaterial);
        CreatePrimitive("FrameB", PrimitiveType.Cube, root.transform, new Vector3(-0.18f, 0.48f, 0f), new Vector3(0.04f, 0.52f, 0.04f), Quaternion.identity, trimMaterial);
        CreatePrimitive("FrameC", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.48f, 0.18f), new Vector3(0.04f, 0.52f, 0.04f), Quaternion.identity, trimMaterial);
        CreatePrimitive("FrameD", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.48f, -0.18f), new Vector3(0.04f, 0.52f, 0.04f), Quaternion.identity, trimMaterial);
        GameObject core = CreatePrimitive("Core", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.34f, 0.42f, 0.34f), Quaternion.identity, emitterMaterial);

        Light pointLight = CreateLight("LanternLight", root.transform, LightType.Point, new Vector3(0f, 0.52f, 0f), 5f, 10f, new Color(1f, 0.74f, 0.45f), LightShadows.Soft, 0f, 0f);
        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            pointLight,
            new[] { core.GetComponent<Renderer>() },
            AnimatedLightSource.MotionMode.Flicker,
            5f,
            4.8f,
            new Color(1f, 0.72f, 0.38f),
            0.22f,
            2.2f,
            null,
            Vector3.up,
            0f);

        return SavePrefab(root);
    }

    private static string CreateCeilingLampPrefab(Material housingMaterial, Material trimMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("CeilingLampLightSource");
        CreatePrimitive("CeilingMount", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.04f, 0.18f), Quaternion.identity, housingMaterial);
        CreatePrimitive("Stem", PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.18f, 0f), new Vector3(0.03f, 0.18f, 0.03f), Quaternion.identity, trimMaterial);
        CreatePrimitive("Shade", PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.42f, 0f), new Vector3(0.68f, 0.28f, 0.68f), Quaternion.identity, housingMaterial);
        GameObject bulb = CreatePrimitive("Bulb", PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.44f, 0f), new Vector3(0.22f, 0.22f, 0.22f), Quaternion.identity, emitterMaterial);

        Light pointLight = CreateLight("LampLight", root.transform, LightType.Point, new Vector3(0f, -0.44f, 0f), 6f, 12f, new Color(0.82f, 0.92f, 1f), LightShadows.Soft, 0f, 0f);
        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            pointLight,
            new[] { bulb.GetComponent<Renderer>() },
            AnimatedLightSource.MotionMode.Pulse,
            6f,
            4.6f,
            new Color(0.72f, 0.9f, 1f),
            0.06f,
            2.8f,
            null,
            Vector3.up,
            0f);

        return SavePrefab(root);
    }

    private static string CreateEmergencyBeaconPrefab(Material housingMaterial, Material trimMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("EmergencyBeaconLightSource");
        CreatePrimitive("Base", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.26f, 0.08f, 0.26f), Quaternion.identity, housingMaterial);
        CreatePrimitive("Mount", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.08f, 0.06f, 0.08f), Quaternion.identity, trimMaterial);
        GameObject rotatingHead = new GameObject("RotatingHead");
        rotatingHead.transform.SetParent(root.transform, false);
        rotatingHead.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        CreatePrimitive("Lens", PrimitiveType.Cylinder, rotatingHead.transform, Vector3.zero, new Vector3(0.18f, 0.14f, 0.18f), Quaternion.identity, emitterMaterial);
        CreatePrimitive("LensCap", PrimitiveType.Cylinder, rotatingHead.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.1f, 0.03f, 0.1f), Quaternion.identity, trimMaterial);

        Light pointLight = CreateLight("BeaconLight", rotatingHead.transform, LightType.Spot, new Vector3(0f, 0f, 0f), 7f, 14f, new Color(1f, 0.18f, 0.14f), LightShadows.None, 42f, 28f);
        pointLight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            pointLight,
            rotatingHead.GetComponentsInChildren<Renderer>(),
            AnimatedLightSource.MotionMode.Pulse,
            7f,
            5.4f,
            new Color(1f, 0.26f, 0.21f),
            0.12f,
            8f,
            rotatingHead.transform,
            Vector3.up,
            150f);

        return SavePrefab(root);
    }

    private static string CreateFloodLightPrefab(Material housingMaterial, Material trimMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("FloodLightSource");
        CreatePrimitive("LegLeft", PrimitiveType.Cylinder, root.transform, new Vector3(-0.18f, 0.46f, 0f), new Vector3(0.03f, 0.46f, 0.03f), Quaternion.Euler(0f, 0f, 9f), trimMaterial);
        CreatePrimitive("LegRight", PrimitiveType.Cylinder, root.transform, new Vector3(0.18f, 0.46f, 0f), new Vector3(0.03f, 0.46f, 0.03f), Quaternion.Euler(0f, 0f, -9f), trimMaterial);
        CreatePrimitive("Crossbar", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.86f, 0f), new Vector3(0.03f, 0.26f, 0.03f), Quaternion.Euler(0f, 0f, 90f), housingMaterial);
        CreatePrimitive("Head", PrimitiveType.Cube, root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.52f, 0.28f, 0.16f), Quaternion.identity, housingMaterial);
        GameObject lens = CreatePrimitive("Lens", PrimitiveType.Cube, root.transform, new Vector3(0f, 1f, 0.09f), new Vector3(0.44f, 0.2f, 0.02f), Quaternion.identity, emitterMaterial);

        Light spotLight = CreateLight("FloodLight", root.transform, LightType.Spot, new Vector3(0f, 1f, 0.12f), 8f, 18f, new Color(0.82f, 0.93f, 1f), LightShadows.Soft, 58f, 44f);
        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            spotLight,
            new[] { lens.GetComponent<Renderer>() },
            AnimatedLightSource.MotionMode.Constant,
            8f,
            5.2f,
            new Color(0.72f, 0.9f, 1f),
            0f,
            0f,
            null,
            Vector3.up,
            0f);

        return SavePrefab(root);
    }

    private static string CreateNeonBarPrefab(Material housingMaterial, Material emitterMaterial)
    {
        GameObject root = new GameObject("NeonBarLightSource");
        CreatePrimitive("BackPlate", PrimitiveType.Cube, root.transform, new Vector3(0f, 0f, -0.035f), new Vector3(1.05f, 0.16f, 0.05f), Quaternion.identity, housingMaterial);
        GameObject tube = CreatePrimitive("Tube", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.96f, 0.08f, 0.08f), Quaternion.identity, emitterMaterial);
        CreatePrimitive("CapLeft", PrimitiveType.Cube, root.transform, new Vector3(-0.52f, 0f, 0f), new Vector3(0.04f, 0.11f, 0.11f), Quaternion.identity, housingMaterial);
        CreatePrimitive("CapRight", PrimitiveType.Cube, root.transform, new Vector3(0.52f, 0f, 0f), new Vector3(0.04f, 0.11f, 0.11f), Quaternion.identity, housingMaterial);

        Light pointLight = CreateLight("NeonGlow", root.transform, LightType.Point, new Vector3(0f, 0f, 0.12f), 3.4f, 8f, new Color(0.34f, 1f, 0.96f), LightShadows.None, 0f, 0f);
        AnimatedLightSource animation = root.AddComponent<AnimatedLightSource>();
        animation.Configure(
            pointLight,
            new[] { tube.GetComponent<Renderer>() },
            AnimatedLightSource.MotionMode.Pulse,
            3.4f,
            6f,
            new Color(0.26f, 1f, 0.94f),
            0.08f,
            5.2f,
            null,
            Vector3.up,
            0f);

        return SavePrefab(root);
    }

    private static GameObject CreatePrimitive(
        string name,
        PrimitiveType primitiveType,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = localRotation;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        return primitive;
    }

    private static Light CreateLight(
        string name,
        Transform parent,
        LightType lightType,
        Vector3 localPosition,
        float intensity,
        float range,
        Color color,
        LightShadows shadows,
        float spotAngle,
        float innerSpotAngle)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;

        Light lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = lightType;
        lightComponent.intensity = intensity;
        lightComponent.range = range;
        lightComponent.color = color;
        lightComponent.shadows = shadows;
        lightComponent.renderMode = LightRenderMode.Auto;

        if (lightType == LightType.Spot)
        {
            lightComponent.spotAngle = spotAngle;
            lightComponent.innerSpotAngle = innerSpotAngle;
        }

        return lightComponent;
    }

    private static Material CreateLitMaterial(string fileName, Shader shader, Color baseColor, float emissionIntensity, float smoothness, float metallic)
    {
        string assetPath = MaterialFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }

        material.shader = shader;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            Color emissionColor = baseColor * emissionIntensity;
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static string SavePrefab(GameObject root)
    {
        string assetPath = PrefabFolder + "/" + root.name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        Object.DestroyImmediate(root);
        return assetPath;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
