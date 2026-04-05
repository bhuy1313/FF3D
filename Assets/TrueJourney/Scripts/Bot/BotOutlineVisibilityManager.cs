using TrueJourney.BotBehavior;
using UnityEngine;

public static class BotOutlineVisibilityManager
{
    private const int DefaultRenderingLayer = 7;
    private const bool DefaultOutlinesVisible = false;

    private static int renderingLayerIndex = DefaultRenderingLayer;
    private static bool outlinesVisible = DefaultOutlinesVisible;

    public static bool OutlinesVisible => outlinesVisible;
    public static int RenderingLayerIndex => renderingLayerIndex;

    public static void ConfigureRenderingLayer(int renderingLayer)
    {
        renderingLayerIndex = Mathf.Clamp(renderingLayer, 0, 31);
    }

    public static void SetOutlinesVisible(bool visible)
    {
        outlinesVisible = visible;
        ApplyToAllActiveBots();
    }

    public static void ApplyTo(BotCommandAgent bot)
    {
        if (bot == null)
        {
            return;
        }

        ApplyTo(bot.gameObject);
    }

    public static void ApplyTo(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        uint renderingLayerBit = 1u << renderingLayerIndex;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (outlinesVisible)
            {
                renderer.renderingLayerMask |= renderingLayerBit;
            }
            else
            {
                renderer.renderingLayerMask &= ~renderingLayerBit;
            }
        }
    }

    public static void ApplyToAllActiveBots()
    {
        foreach (BotCommandAgent bot in BotRuntimeRegistry.ActiveCommandAgents)
        {
            ApplyTo(bot);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlayModeSession()
    {
        renderingLayerIndex = DefaultRenderingLayer;
        outlinesVisible = DefaultOutlinesVisible;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyDefaultsOnPlayStart()
    {
        ApplyToAllActiveBots();
    }
}
