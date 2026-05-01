#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lightweight CSV recorder used to capture mission/fire/victim telemetry during
// playthroughs. The recorder is auto-spawned in editor and development builds and
// writes one CSV per scene load to `<projectRoot>/Logs/balance-<scene>-<timestamp>.csv`.
// It is intentionally non-invasive: it polls public APIs, hooks into existing
// events, and never mutates simulation state.
public static class BalanceTelemetry
{
    public const string DefaultEventBoot = "boot";
    public const string DefaultEventMissionStart = "mission_start";
    public const string DefaultEventMissionComplete = "mission_complete";
    public const string DefaultEventMissionFail = "mission_fail";
    public const string DefaultEventVictimState = "victim_state";
    public const string DefaultEventFireStateChanged = "fire_state_changed";
    public const string DefaultEventSnapshot = "snapshot";
    public const string DefaultEventCustom = "custom";

    private static BalanceTelemetryRecorder activeRecorder;

    public static bool IsRecording => activeRecorder != null && activeRecorder.IsWriterReady;

    internal static void RegisterRecorder(BalanceTelemetryRecorder recorder)
    {
        activeRecorder = recorder;
    }

    internal static void UnregisterRecorder(BalanceTelemetryRecorder recorder)
    {
        if (activeRecorder == recorder)
        {
            activeRecorder = null;
        }
    }

    public static void LogCustom(string subject, string detail)
    {
        if (activeRecorder == null)
        {
            return;
        }

        activeRecorder.WriteRow(DefaultEventCustom, subject, detail);
    }

    public static void LogEvent(string eventType, string subject, string detail)
    {
        if (activeRecorder == null || string.IsNullOrEmpty(eventType))
        {
            return;
        }

        activeRecorder.WriteRow(eventType, subject ?? string.Empty, detail ?? string.Empty);
    }
}

[DefaultExecutionOrder(-9000)]
public sealed class BalanceTelemetryRecorder : MonoBehaviour
{
    private const float SnapshotIntervalSeconds = 5f;
    private const float DiscoveryIntervalSeconds = 1.5f;
    private const string CsvHeader =
        "timestamp_iso,scene,mission_time,mission_state,event,subject,hazard_linked,hazard_burning,fire_total,fire_extinguished,victims_alive,victims_urgent,victims_critical,victims_deceased,score,score_max,detail";

    private static bool createdForThisProcess;

    private readonly List<FireSimulationManager> trackedFireManagers = new List<FireSimulationManager>();
    private readonly List<VictimCondition> trackedVictims = new List<VictimCondition>();
    private readonly StringBuilder rowBuilder = new StringBuilder(256);

    private StreamWriter writer;
    private string csvPath;
    private float nextSnapshotTime;
    private float nextDiscoveryTime;
    private IncidentMissionSystem missionSystem;
    private IncidentMissionSystem.MissionState lastMissionState = IncidentMissionSystem.MissionState.Idle;
    private int lastHazardLinked;
    private int lastHazardBurning;
    private float startupRealtime;

    public bool IsWriterReady => writer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorPrefs.GetBool("FF3D_EnableTelemetry", true))
        {
            return;
        }
#endif

        if (createdForThisProcess)
        {
            return;
        }

        createdForThisProcess = true;
        GameObject host = new GameObject("[BalanceTelemetryRecorder]");
        host.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        DontDestroyOnLoad(host);
        host.AddComponent<BalanceTelemetryRecorder>();
    }

    private void Awake()
    {
        BalanceTelemetry.RegisterRecorder(this);
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        OpenWriterForActiveScene();
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        UnsubscribeAll();
        CloseWriter();
        BalanceTelemetry.UnregisterRecorder(this);
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        UnsubscribeAll();
        CloseWriter();
        OpenWriterForActiveScene();
    }

    private void OpenWriterForActiveScene()
    {
        try
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string logsDir = Path.Combine(projectRoot ?? string.Empty, "Logs");
            Directory.CreateDirectory(logsDir);

            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = "Unknown";
            }

            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "balance-{0}-{1:yyyyMMdd-HHmmss}.csv",
                SanitizeFileName(sceneName),
                DateTime.Now);

            csvPath = Path.Combine(logsDir, fileName);
            writer = new StreamWriter(csvPath, append: false, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(CsvHeader);

            startupRealtime = Time.realtimeSinceStartup;
            lastMissionState = IncidentMissionSystem.MissionState.Idle;
            lastHazardLinked = 0;
            lastHazardBurning = 0;
            nextSnapshotTime = 0f;
            nextDiscoveryTime = 0f;

            DiscoverAndSubscribe();
            WriteRow(BalanceTelemetry.DefaultEventBoot, sceneName, csvPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"BalanceTelemetry: failed to open log writer ({ex.Message}). Recording disabled for this scene.");
            CloseWriter();
        }
    }

    private void CloseWriter()
    {
        if (writer != null)
        {
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch
            {
            }

            writer = null;
        }
    }

    private void Update()
    {
        if (writer == null)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now >= nextDiscoveryTime)
        {
            nextDiscoveryTime = now + DiscoveryIntervalSeconds;
            DiscoverAndSubscribe();
        }

        PollMissionState();
        PollFireDeltas();

        if (now >= nextSnapshotTime)
        {
            nextSnapshotTime = now + SnapshotIntervalSeconds;
            WriteRow(BalanceTelemetry.DefaultEventSnapshot, string.Empty, string.Empty);
        }
    }

    private void DiscoverAndSubscribe()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>(FindObjectsInactive.Include);
        }

        FireSimulationManager[] foundFires = FindObjectsByType<FireSimulationManager>(FindObjectsInactive.Include);
        for (int i = 0; i < foundFires.Length; i++)
        {
            FireSimulationManager candidate = foundFires[i];
            if (candidate == null || trackedFireManagers.Contains(candidate))
            {
                continue;
            }

            candidate.StateChanged += HandleFireStateChanged;
            trackedFireManagers.Add(candidate);
        }

        VictimCondition[] foundVictims = FindObjectsByType<VictimCondition>(FindObjectsInactive.Include);
        for (int i = 0; i < foundVictims.Length; i++)
        {
            VictimCondition victim = foundVictims[i];
            if (victim == null || trackedVictims.Contains(victim))
            {
                continue;
            }

            victim.OnTriageStateChanged += state => HandleVictimTriageChanged(victim, state);
            trackedVictims.Add(victim);
        }
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < trackedFireManagers.Count; i++)
        {
            FireSimulationManager manager = trackedFireManagers[i];
            if (manager != null)
            {
                manager.StateChanged -= HandleFireStateChanged;
            }
        }

        trackedFireManagers.Clear();
        // VictimCondition uses lambda subscriptions; we cannot easily unsubscribe per
        // instance without retaining the delegate. Clearing the list is sufficient
        // because the recorder is destroyed only on app quit, and victims being
        // destroyed clears their event invocation list automatically.
        trackedVictims.Clear();
        missionSystem = null;
    }

    private void PollMissionState()
    {
        if (missionSystem == null)
        {
            return;
        }

        IncidentMissionSystem.MissionState state = missionSystem.State;
        if (state == lastMissionState)
        {
            return;
        }

        IncidentMissionSystem.MissionState previous = lastMissionState;
        lastMissionState = state;

        switch (state)
        {
            case IncidentMissionSystem.MissionState.Running:
                if (previous == IncidentMissionSystem.MissionState.Idle ||
                    previous == IncidentMissionSystem.MissionState.Completed ||
                    previous == IncidentMissionSystem.MissionState.Failed)
                {
                    WriteRow(BalanceTelemetry.DefaultEventMissionStart, string.Empty, string.Empty);
                }

                break;
            case IncidentMissionSystem.MissionState.Completed:
                WriteRow(BalanceTelemetry.DefaultEventMissionComplete, string.Empty, string.Empty);
                break;
            case IncidentMissionSystem.MissionState.Failed:
                WriteRow(BalanceTelemetry.DefaultEventMissionFail, string.Empty, string.Empty);
                break;
        }
    }

    private void PollFireDeltas()
    {
        int hazardLinked = 0;
        int hazardBurning = 0;
        for (int i = 0; i < trackedFireManagers.Count; i++)
        {
            FireSimulationManager manager = trackedFireManagers[i];
            if (manager == null || !manager.IsInitialized)
            {
                continue;
            }

            hazardLinked += manager.GetHazardLinkedNodeCount();
            hazardBurning += manager.GetHazardLinkedBurningNodeCount();
        }

        if (hazardLinked != lastHazardLinked || hazardBurning != lastHazardBurning)
        {
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "linked:{0}->{1};burning:{2}->{3}",
                lastHazardLinked,
                hazardLinked,
                lastHazardBurning,
                hazardBurning);
            lastHazardLinked = hazardLinked;
            lastHazardBurning = hazardBurning;
            WriteRow(BalanceTelemetry.DefaultEventFireStateChanged, string.Empty, detail);
        }
    }

    private void HandleFireStateChanged()
    {
        // Defer counting to PollFireDeltas next frame to coalesce bursty events.
    }

    private void HandleVictimTriageChanged(VictimCondition victim, VictimCondition.TriageState state)
    {
        if (writer == null || victim == null)
        {
            return;
        }

        string subject = victim.name;
        string detail = string.Format(
            CultureInfo.InvariantCulture,
            "{0}@{1:0.0}%",
            state,
            victim.CurrentConditionPercent);
        WriteRow(BalanceTelemetry.DefaultEventVictimState, subject, detail);
    }

    internal void WriteRow(string eventType, string subject, string detail)
    {
        if (writer == null)
        {
            return;
        }

        try
        {
            int hazardLinked = 0;
            int hazardBurning = 0;
            for (int i = 0; i < trackedFireManagers.Count; i++)
            {
                FireSimulationManager manager = trackedFireManagers[i];
                if (manager == null || !manager.IsInitialized)
                {
                    continue;
                }

                hazardLinked += manager.GetHazardLinkedNodeCount();
                hazardBurning += manager.GetHazardLinkedBurningNodeCount();
            }

            int fireTotal = missionSystem != null ? missionSystem.TotalTrackedFires : 0;
            int fireExt = missionSystem != null ? missionSystem.ExtinguishedFireCount : 0;
            int alive = missionSystem != null ? missionSystem.AliveVictimCount : 0;
            int urgent = missionSystem != null ? missionSystem.UrgentVictimCount : 0;
            int critical = missionSystem != null ? missionSystem.CriticalVictimCount : 0;
            int deceased = missionSystem != null ? missionSystem.DeceasedVictimCount : 0;
            int score = missionSystem != null ? missionSystem.DisplayedScore : 0;
            int scoreMax = missionSystem != null ? missionSystem.DisplayedMaximumScore : 0;
            string state = missionSystem != null ? missionSystem.State.ToString() : "NoSystem";
            float missionTime = Mathf.Max(0f, Time.realtimeSinceStartup - startupRealtime);

            rowBuilder.Length = 0;
            AppendCsv(rowBuilder, DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            rowBuilder.Append(',');
            AppendCsv(rowBuilder, SceneManager.GetActiveScene().name);
            rowBuilder.Append(',');
            rowBuilder.Append(missionTime.ToString("0.000", CultureInfo.InvariantCulture));
            rowBuilder.Append(',');
            AppendCsv(rowBuilder, state);
            rowBuilder.Append(',');
            AppendCsv(rowBuilder, eventType);
            rowBuilder.Append(',');
            AppendCsv(rowBuilder, subject);
            rowBuilder.Append(',');
            rowBuilder.Append(hazardLinked);
            rowBuilder.Append(',');
            rowBuilder.Append(hazardBurning);
            rowBuilder.Append(',');
            rowBuilder.Append(fireTotal);
            rowBuilder.Append(',');
            rowBuilder.Append(fireExt);
            rowBuilder.Append(',');
            rowBuilder.Append(alive);
            rowBuilder.Append(',');
            rowBuilder.Append(urgent);
            rowBuilder.Append(',');
            rowBuilder.Append(critical);
            rowBuilder.Append(',');
            rowBuilder.Append(deceased);
            rowBuilder.Append(',');
            rowBuilder.Append(score);
            rowBuilder.Append(',');
            rowBuilder.Append(scoreMax);
            rowBuilder.Append(',');
            AppendCsv(rowBuilder, detail);

            writer.WriteLine(rowBuilder.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"BalanceTelemetry: write failed ({ex.Message}). Closing writer.");
            CloseWriter();
        }
    }

    private static void AppendCsv(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        bool needsQuoting = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuoting)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\""));
        builder.Append('"');
    }

    private static string SanitizeFileName(string raw)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return sb.ToString();
    }

#if UNITY_EDITOR
    private const string MenuPath = "Tools/FF3D/Enable Balance Telemetry";

    [UnityEditor.MenuItem(MenuPath)]
    private static void ToggleTelemetry()
    {
        bool isEnabled = UnityEditor.EditorPrefs.GetBool("FF3D_EnableTelemetry", true);
        UnityEditor.EditorPrefs.SetBool("FF3D_EnableTelemetry", !isEnabled);
    }

    [UnityEditor.MenuItem(MenuPath, true)]
    private static bool ToggleTelemetryValidate()
    {
        bool isEnabled = UnityEditor.EditorPrefs.GetBool("FF3D_EnableTelemetry", true);
        UnityEditor.Menu.SetChecked(MenuPath, isEnabled);
        return true;
    }
#endif
}
#endif
