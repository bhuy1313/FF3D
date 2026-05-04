using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BalanceAuditWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string reportText = string.Empty;

    [MenuItem("Tools/FF3D/Balance/Audit Gameplay Balance")]
    private static void Open()
    {
        BalanceAuditWindow window = GetWindow<BalanceAuditWindow>("Balance Audit");
        window.minSize = new Vector2(760f, 520f);
        window.RefreshReport();
    }

    [MenuItem("Tools/FF3D/Balance/Log Gameplay Balance Report")]
    private static void LogReport()
    {
        string report = BuildReport();
        Debug.Log(report);
        EditorGUIUtility.systemCopyBuffer = report;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(reportText))
        {
            RefreshReport();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            RefreshReport();
        }

        if (GUILayout.Button("Copy Markdown", EditorStyles.toolbarButton, GUILayout.Width(120f)))
        {
            EditorGUIUtility.systemCopyBuffer = reportText;
        }

        if (GUILayout.Button("Log To Console", EditorStyles.toolbarButton, GUILayout.Width(120f)))
        {
            Debug.Log(reportText);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(reportText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RefreshReport()
    {
        reportText = BuildReport();
        Repaint();
    }

    private static string BuildReport()
    {
        StringBuilder builder = new StringBuilder(8192);
        builder.AppendLine("# FF3D Gameplay Balance Audit");
        builder.AppendLine();
        builder.AppendLine("Use this report as the balancing checklist. Tune only the groups that are part of the current playtest goal.");
        builder.AppendLine();

        AppendMissionDefinitions(builder);
        AppendFireSimulationProfiles(builder);
        AppendSuppressionProfiles(builder);
        AppendRuntimeTuningChecklist(builder);
        AppendTelemetryChecklist(builder);

        return builder.ToString();
    }

    private static void AppendMissionDefinitions(StringBuilder builder)
    {
        builder.AppendLine("## Mission Timing And Scoring");
        string[] guids = AssetDatabase.FindAssets("t:MissionDefinition");
        if (guids.Length == 0)
        {
            builder.AppendLine("- No MissionDefinition assets found.");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
            if (mission == null)
            {
                continue;
            }

            MissionScoreConfig score = mission.ScoreConfig;
            builder.Append("- ");
            builder.Append(mission.name);
            builder.Append(" (`");
            builder.Append(path);
            builder.Append("`): timeLimit=");
            builder.Append(FormatSeconds(mission.TimeLimitSeconds));
            builder.Append(", score target=");
            builder.Append(FormatSeconds(score.TargetTimeSeconds));
            builder.Append(", acceptable=");
            builder.Append(FormatSeconds(score.AcceptableTimeSeconds));
            builder.Append(", completionBonus=");
            builder.Append(score.CompletionBonus);
            builder.Append(", noDeathBonus=");
            builder.Append(score.NoVictimDeathsBonus);
            builder.Append(", deathPenalty=");
            builder.Append(score.PerVictimDeathPenalty);
            builder.Append(", timeBonusMax=");
            builder.Append(score.TimeBonusMaxScore);
            builder.Append(", rank S/A/B=");
            builder.Append(score.EvaluateRank(90, 100));
            builder.Append("/");
            builder.Append(score.EvaluateRank(75, 100));
            builder.Append("/");
            builder.Append(score.EvaluateRank(50, 100));
            builder.AppendLine();
        }

        builder.AppendLine();
    }

    private static void AppendFireSimulationProfiles(StringBuilder builder)
    {
        builder.AppendLine("## Fire Simulation Profiles");
        string[] guids = AssetDatabase.FindAssets("t:FireSimulationProfile");
        if (guids.Length == 0)
        {
            builder.AppendLine("- No FireSimulationProfile assets found.");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FireSimulationProfile profile = AssetDatabase.LoadAssetAtPath<FireSimulationProfile>(path);
            if (profile == null)
            {
                continue;
            }

            builder.Append("- ");
            builder.Append(profile.name);
            builder.Append(" (`");
            builder.Append(path);
            builder.Append("`): tick=");
            builder.Append(FormatFloat(profile.SimulationTickInterval));
            builder.Append("s, maxHeat=");
            builder.Append(FormatFloat(profile.MaxHeat));
            builder.Append(", extinguishThreshold=");
            builder.Append(FormatFloat(profile.ExtinguishThreshold));
            builder.Append(", spreadPerSecond=");
            builder.Append(FormatFloat(profile.NeighborHeatTransferPerSecond));
            builder.Append(", suppressionRecovery=");
            builder.Append(FormatSeconds(profile.SuppressionRecoveryDelaySeconds));
            builder.Append(", maxNodeEffects=");
            builder.Append(profile.MaxNodeEffects);
            builder.Append(", effectDistance=");
            builder.Append(FormatFloat(profile.EffectVisibleDistance));
            builder.AppendLine();
        }

        builder.AppendLine();
    }

    private static void AppendSuppressionProfiles(StringBuilder builder)
    {
        builder.AppendLine("## Suppression Effectiveness Profiles");
        string[] guids = AssetDatabase.FindAssets("t:FireSuppressionProfile");
        if (guids.Length == 0)
        {
            builder.AppendLine("- No FireSuppressionProfile assets found.");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FireSuppressionProfile profile = AssetDatabase.LoadAssetAtPath<FireSuppressionProfile>(path);
            if (profile == null)
            {
                continue;
            }

            builder.AppendLine("- " + profile.name + " (`" + path + "`)");
            AppendSuppressionHazardLine(builder, profile, FireHazardType.OrdinaryCombustibles);
            AppendSuppressionHazardLine(builder, profile, FireHazardType.Electrical);
            AppendSuppressionHazardLine(builder, profile, FireHazardType.FlammableLiquid);
            AppendSuppressionHazardLine(builder, profile, FireHazardType.GasFed);
        }

        builder.AppendLine();
    }

    private static void AppendSuppressionHazardLine(StringBuilder builder, FireSuppressionProfile profile, FireHazardType hazardType)
    {
        builder.Append("  - ");
        builder.Append(hazardType);
        builder.Append(": notIsolated W/D/C=");
        builder.Append(FormatAgentSet(profile, hazardType, isolated: false));
        builder.Append(", isolated W/D/C=");
        builder.Append(FormatAgentSet(profile, hazardType, isolated: true));
        builder.AppendLine();
    }

    private static string FormatAgentSet(FireSuppressionProfile profile, FireHazardType hazardType, bool isolated)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}/{2}{3}",
            FormatFloat(profile.GetEffectiveness(hazardType, FireSuppressionAgent.Water, isolated)),
            FormatFloat(profile.GetEffectiveness(hazardType, FireSuppressionAgent.DryChemical, isolated)),
            FormatFloat(profile.GetEffectiveness(hazardType, FireSuppressionAgent.CO2, isolated)),
            profile.GetWorsens(hazardType, FireSuppressionAgent.Water, isolated) ? " waterWorsens" : string.Empty);
    }

    private static void AppendRuntimeTuningChecklist(StringBuilder builder)
    {
        builder.AppendLine("## Runtime/Prefab Values To Check In Scene");
        builder.AppendLine("- Player: MoveSpeed, SprintSpeed, CrouchSpeed, ClimbSpeed, SprintStaminaCostPerSecond, SprintMinStamina, WeightForMinimumSpeed, MinimumWeightSpeedMultiplier, SprintDisabledWeight.");
        builder.AppendLine("- Player vitals: maxHealth, stamina regen, maxOxygen, oxygenDamagePerSecond.");
        builder.AppendLine("- SmokeHazard: smoke accumulation/dissipation, oxygenDrainPerSecond, victimConditionDamagePerSecond, crouchedExposureMultiplier.");
        builder.AppendLine("- VictimCondition: passiveDeteriorationPerSecond, smokeDamageMultiplier, urgentThreshold, criticalThreshold, stabilization/carry rules.");
        builder.AppendLine("- FireExtinguisher: maxCharge, discharge rates, maxSprayDistance, coneHalfAngle, movementWeightKg.");
        builder.AppendLine("- FireHose: sprayRange, sprayRadius, apply water rates, pressure multipliers, straight/fog pattern multipliers, connection requirements.");
        builder.AppendLine("- Bot support: bot discharge rates, preferred spray distance, rescue priority behavior, tool acquisition availability.");
        builder.AppendLine();
    }

    private static void AppendTelemetryChecklist(StringBuilder builder)
    {
        builder.AppendLine("## Telemetry Columns For Playtest Review");
        builder.AppendLine("- Mission outcome: mission_time, mission_state, fire_total, fire_extinguished, victims_alive/urgent/critical/deceased, score, score_max.");
        builder.AppendLine("- Fire pressure: hazard_linked and hazard_burning over time.");
        builder.AppendLine("- Player pressure: player_health_pct, player_stamina_pct, player_oxygen_pct, player_smoke_pct, player_fire_glare_pct, player_visible_fire_count, player_burden_kg.");
        builder.AppendLine("- Recommended pass: run each tuned mission as novice, normal, and optimized routes before changing the next parameter group.");
        builder.AppendLine();
    }

    private static string FormatSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            return "off";
        }

        return FormatFloat(seconds) + "s";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
