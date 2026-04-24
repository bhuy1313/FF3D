using System.Collections.Generic;
using UnityEngine;

public sealed class IncidentMapSetupContext
{
    private readonly List<string> warningSink;

    public IncidentMapSetupContext(
        IncidentWorldSetupPayload payload,
        SceneStartupFlow startupFlow,
        IncidentMapSetupRoot setupRoot,
        IncidentFirePrefabLibrary firePrefabLibrary,
        IncidentFireSpawnProfile fireSpawnProfile,
        FireSimulationManager fireSimulationManager,
        List<string> warningSink)
    {
        Payload = payload;
        StartupFlow = startupFlow;
        SetupRoot = setupRoot;
        FirePrefabLibrary = firePrefabLibrary;
        FireSpawnProfile = fireSpawnProfile;
        FireSimulationManager = fireSimulationManager;
        this.warningSink = warningSink ?? new List<string>();
    }

    public IncidentWorldSetupPayload Payload { get; }
    public SceneStartupFlow StartupFlow { get; }
    public IncidentMapSetupRoot SetupRoot { get; }
    public IncidentFirePrefabLibrary FirePrefabLibrary { get; }
    public IncidentFireSpawnProfile FireSpawnProfile { get; }
    public FireSimulationManager FireSimulationManager { get; }
    public IncidentPayloadAnchor ResolvedAnchor { get; set; }
    public IncidentOriginArea ResolvedOriginArea { get; set; }

    public void AddWarning(string message, Object context = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        warningSink.Add(message);
        Debug.LogWarning(message, context != null ? context : SetupRoot);
    }
}
