using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class IncidentMissionSystemTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type MissionSystemType = FindType("IncidentMissionSystem");
    private static readonly Type MissionDefinitionType = FindType("MissionDefinition");
    private static readonly Type MissionObjectiveDefinitionType = FindType("MissionObjectiveDefinition");
    private static readonly Type MissionActionDefinitionType = FindType("MissionActionDefinition");
    private static readonly Type MissionFailConditionDefinitionType = FindType("MissionFailConditionDefinition");
    private static readonly Type MissionStageDefinitionType = FindType("MissionStageDefinition");
    private static readonly Type ExtinguishFiresObjectiveDefinitionType = FindType("ExtinguishFiresObjectiveDefinition");
    private static readonly Type ReachAreaObjectiveDefinitionType = FindType("ReachAreaObjectiveDefinition");
    private static readonly Type InteractTargetObjectiveDefinitionType = FindType("InteractTargetObjectiveDefinition");
    private static readonly Type BreakTargetObjectiveDefinitionType = FindType("BreakTargetObjectiveDefinition");
    private static readonly Type DeliverTargetToZoneObjectiveDefinitionType = FindType("DeliverTargetToZoneObjectiveDefinition");
    private static readonly Type RescueTargetsObjectiveDefinitionType = FindType("RescueTargetsObjectiveDefinition");
    private static readonly Type TimeLimitFailConditionDefinitionType = FindType("TimeLimitFailConditionDefinition");
    private static readonly Type AnyVictimDeathFailConditionDefinitionType = FindType("AnyVictimDeathFailConditionDefinition");
    private static readonly Type MissionSceneObjectRegistryType = FindType("MissionSceneObjectRegistry");
    private static readonly Type MissionSignalSourceType = FindType("MissionSignalSource");
    private static readonly Type MissionInteractionSignalRelayType = FindType("MissionInteractionSignalRelay");
    private static readonly Type MissionBreakableSignalRelayType = FindType("MissionBreakableSignalRelay");
    private static readonly Type MissionRescueDeliverySignalRelayType = FindType("MissionRescueDeliverySignalRelay");
    private static readonly Type ActivateMissionObjectActionType = FindType("ActivateMissionObjectAction");
    private static readonly Type DeactivateMissionObjectActionType = FindType("DeactivateMissionObjectAction");
    private static readonly Type BreakableType = FindType("Breakable");
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type RescuableType = FindType("Rescuable");
    private static readonly Type SafeZoneType = FindType("SafeZone");
    private static readonly Type VictimConditionType = FindType("VictimCondition");
    private static readonly Type TriageStateType = FindNestedType(VictimConditionType, "TriageState");
    private static readonly Type MissionStateType = FindNestedType(MissionSystemType, "MissionState");

    [Test]
    public void Mission_Completes_WhenAllTrackedObjectivesAreResolved()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "trackedFires", CreateTypedList(FireType, fire));
            SetPrivateField(mission, "trackedRescuables", CreateTypedList(RescuableType, rescuable));

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", true);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
            Assert.That(GetFieldValue<int>(mission, "extinguishedFireCount"), Is.EqualTo(1));
            Assert.That(GetFieldValue<int>(mission, "rescuedCount"), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_Fails_WhenTimeLimitExpires()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "timeLimitSeconds", 1f);
            SetPrivateField(mission, "trackedFires", CreateTypedList(FireType, fire));
            SetPrivateField(fire, "currentHp", 1f);

            InvokeInstanceMethod(mission, "StartMission");
            SetPrivateField(mission, "elapsedTime", 1.1f);
            InvokeInstanceMethod(mission, "Update");

            object failedState = Enum.Parse(MissionStateType, "Failed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(failedState));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
    }

    [Test]
    public void Mission_UsesDefinitionObjectives_WhenMissionDefinitionIsAssigned()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        ScriptableObject missionDefinition = null;
        ScriptableObject fireObjective = null;
        ScriptableObject rescueObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            fireObjective = ScriptableObject.CreateInstance(ExtinguishFiresObjectiveDefinitionType);
            rescueObjective = ScriptableObject.CreateInstance(RescueTargetsObjectiveDefinitionType);

            SetPrivateField(missionDefinition, "missionTitle", "Definition Mission");
            SetPrivateField(missionDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, fireObjective, rescueObjective));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", true);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
            Assert.That(GetPropertyValue<string>(mission, "MissionTitle"), Is.EqualTo("Definition Mission"));

            Assert.That(GetPropertyValue<int>(mission, "ObjectiveStatusCount"), Is.EqualTo(2));
        }
        finally
        {
            if (rescueObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueObjective);
            }

            if (fireObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObjective);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_AdvancesThroughStages_BeforeCompleting()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        ScriptableObject missionDefinition = null;
        ScriptableObject extinguishStage = null;
        ScriptableObject rescueStage = null;
        ScriptableObject fireObjective = null;
        ScriptableObject rescueObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            extinguishStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            rescueStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            fireObjective = ScriptableObject.CreateInstance(ExtinguishFiresObjectiveDefinitionType);
            rescueObjective = ScriptableObject.CreateInstance(RescueTargetsObjectiveDefinitionType);

            SetPrivateField(extinguishStage, "stageTitle", "Contain Fire");
            SetPrivateField(extinguishStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, fireObjective));
            SetPrivateField(rescueStage, "stageTitle", "Extract Victims");
            SetPrivateField(rescueStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, rescueObjective));
            SetPrivateField(missionDefinition, "stages", CreateTypedList(MissionStageDefinitionType, extinguishStage, rescueStage));

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", false);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object runningState = Enum.Parse(MissionStateType, "Running");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(runningState));
            Assert.That(GetPropertyValue<int>(mission, "CurrentStageIndex"), Is.EqualTo(1));
            Assert.That(GetPropertyValue<string>(mission, "CurrentStageTitle"), Is.EqualTo("Extract Victims"));
            Assert.That(GetPropertyValue<bool>(mission, "HasActiveStage"), Is.True);

            SetPrivateField(rescuable, "isRescued", true);
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
        }
        finally
        {
            if (rescueObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueObjective);
            }

            if (fireObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObjective);
            }

            if (rescueStage != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueStage);
            }

            if (extinguishStage != null)
            {
                UnityEngine.Object.DestroyImmediate(extinguishStage);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_CapturesCompletedStageScore_WhenAdvancingToNextStage()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        ScriptableObject missionDefinition = null;
        ScriptableObject extinguishStage = null;
        ScriptableObject rescueStage = null;
        ScriptableObject fireObjective = null;
        ScriptableObject rescueObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            extinguishStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            rescueStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            fireObjective = ScriptableObject.CreateInstance(ExtinguishFiresObjectiveDefinitionType);
            rescueObjective = ScriptableObject.CreateInstance(RescueTargetsObjectiveDefinitionType);

            SetPrivateField(fireObjective, "scoreWeight", 5);
            SetPrivateField(rescueObjective, "scoreWeight", 7);
            SetPrivateField(extinguishStage, "stageTitle", "Contain Fire");
            SetPrivateField(extinguishStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, fireObjective));
            SetPrivateField(rescueStage, "stageTitle", "Extract Victims");
            SetPrivateField(rescueStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, rescueObjective));
            SetPrivateField(missionDefinition, "stages", CreateTypedList(MissionStageDefinitionType, extinguishStage, rescueStage));

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", false);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            IList records = GetFieldValue<IList>(mission, "completedStageScoreRecords");
            Assert.That(records, Has.Count.EqualTo(1));

            object firstRecord = records[0];
            Assert.That(GetPropertyValue<int>(firstRecord, "StageIndex"), Is.EqualTo(0));
            Assert.That(GetPropertyValue<int>(firstRecord, "Score"), Is.EqualTo(5));
            Assert.That(GetPropertyValue<int>(firstRecord, "MaxScore"), Is.EqualTo(5));
            Assert.That(GetPropertyValue<int>(mission, "CurrentStageIndex"), Is.EqualTo(1));
        }
        finally
        {
            if (rescueObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueObjective);
            }

            if (fireObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObjective);
            }

            if (rescueStage != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueStage);
            }

            if (extinguishStage != null)
            {
                UnityEngine.Object.DestroyImmediate(extinguishStage);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_WaitsForConfiguredStageDelay_BeforeStartingNextStage()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        ScriptableObject missionDefinition = null;
        ScriptableObject extinguishStage = null;
        ScriptableObject rescueStage = null;
        ScriptableObject fireObjective = null;
        ScriptableObject rescueObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            extinguishStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            rescueStage = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            fireObjective = ScriptableObject.CreateInstance(ExtinguishFiresObjectiveDefinitionType);
            rescueObjective = ScriptableObject.CreateInstance(RescueTargetsObjectiveDefinitionType);

            SetPrivateField(extinguishStage, "stageTitle", "Contain Fire");
            SetPrivateField(extinguishStage, "nextStageDelaySeconds", 1f);
            SetPrivateField(extinguishStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, fireObjective));
            SetPrivateField(rescueStage, "stageTitle", "Extract Victims");
            SetPrivateField(rescueStage, "objectives", CreateTypedList(MissionObjectiveDefinitionType, rescueObjective));
            SetPrivateField(missionDefinition, "stages", CreateTypedList(MissionStageDefinitionType, extinguishStage, rescueStage));

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", false);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object runningState = Enum.Parse(MissionStateType, "Running");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(runningState));
            Assert.That(GetPropertyValue<int>(mission, "CurrentStageIndex"), Is.EqualTo(0));
            Assert.That(GetPropertyValue<bool>(mission, "IsStageTransitionPending"), Is.True);

            SetPrivateField(mission, "elapsedTime", 1.1f);
            InvokeInstanceMethod(mission, "Update");

            Assert.That(GetPropertyValue<int>(mission, "CurrentStageIndex"), Is.EqualTo(1));
            Assert.That(GetPropertyValue<string>(mission, "CurrentStageTitle"), Is.EqualTo("Extract Victims"));
            Assert.That(GetPropertyValue<bool>(mission, "IsStageTransitionPending"), Is.False);
        }
        finally
        {
            if (rescueObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueObjective);
            }

            if (fireObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObjective);
            }

            if (rescueStage != null)
            {
                UnityEngine.Object.DestroyImmediate(rescueStage);
            }

            if (extinguishStage != null)
            {
                UnityEngine.Object.DestroyImmediate(extinguishStage);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_ExecutesStageStartedMissionActions_WhenStageStarts()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject markerObject = new GameObject("Marker");

        ScriptableObject missionDefinition = null;
        ScriptableObject stageDefinition = null;
        ScriptableObject activateAction = null;

        try
        {
            missionObject.SetActive(false);
            markerObject.SetActive(false);

            Component mission = missionObject.AddComponent(MissionSystemType);
            Component registry = missionObject.AddComponent(MissionSceneObjectRegistryType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            stageDefinition = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            activateAction = ScriptableObject.CreateInstance(ActivateMissionObjectActionType);

            InvokeInstanceMethod(registry, "Register", "marker", markerObject);

            SetPrivateField(activateAction, "targetKey", "marker");
            SetPrivateField(stageDefinition, "onStageStartedActions", CreateTypedList(MissionActionDefinitionType, activateAction));
            SetPrivateField(missionDefinition, "stages", CreateTypedList(MissionStageDefinitionType, stageDefinition));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            InvokeInstanceMethod(mission, "StartMission");

            Assert.That(markerObject.activeSelf, Is.True);
        }
        finally
        {
            if (activateAction != null)
            {
                UnityEngine.Object.DestroyImmediate(activateAction);
            }

            if (stageDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(stageDefinition);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(markerObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_ExecutesStageCompletedMissionActions_WhenStageCompletes()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject markerObject = new GameObject("Marker");

        ScriptableObject missionDefinition = null;
        ScriptableObject stageDefinition = null;
        ScriptableObject fireObjective = null;
        ScriptableObject deactivateAction = null;

        try
        {
            missionObject.SetActive(false);
            markerObject.SetActive(true);

            Component mission = missionObject.AddComponent(MissionSystemType);
            Component registry = missionObject.AddComponent(MissionSceneObjectRegistryType);
            Component fire = fireObject.AddComponent(FireType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            stageDefinition = ScriptableObject.CreateInstance(MissionStageDefinitionType);
            fireObjective = ScriptableObject.CreateInstance(ExtinguishFiresObjectiveDefinitionType);
            deactivateAction = ScriptableObject.CreateInstance(DeactivateMissionObjectActionType);

            InvokeInstanceMethod(registry, "Register", "marker", markerObject);

            SetPrivateField(deactivateAction, "targetKey", "marker");
            SetPrivateField(stageDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, fireObjective));
            SetPrivateField(stageDefinition, "onStageCompletedActions", CreateTypedList(MissionActionDefinitionType, deactivateAction));
            SetPrivateField(missionDefinition, "stages", CreateTypedList(MissionStageDefinitionType, stageDefinition));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(fire, "currentHp", 0f);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
            Assert.That(markerObject.activeSelf, Is.False);
        }
        finally
        {
            if (deactivateAction != null)
            {
                UnityEngine.Object.DestroyImmediate(deactivateAction);
            }

            if (fireObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObjective);
            }

            if (stageDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(stageDefinition);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(markerObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Fails_WhenDefinitionTimeLimitFailConditionExpires()
    {
        GameObject missionObject = new GameObject("Mission");

        ScriptableObject missionDefinition = null;
        ScriptableObject timeLimitFailCondition = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            timeLimitFailCondition = ScriptableObject.CreateInstance(TimeLimitFailConditionDefinitionType);

            SetPrivateField(timeLimitFailCondition, "timeLimitSeconds", 1f);
            SetPrivateField(missionDefinition, "failConditions", CreateTypedList(MissionFailConditionDefinitionType, timeLimitFailCondition));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            InvokeInstanceMethod(mission, "StartMission");
            SetPrivateField(mission, "elapsedTime", 1.1f);
            InvokeInstanceMethod(mission, "Update");

            object failedState = Enum.Parse(MissionStateType, "Failed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(failedState));
            Assert.That(GetPropertyValue<float>(mission, "TimeLimitSeconds"), Is.EqualTo(1f));
        }
        finally
        {
            if (timeLimitFailCondition != null)
            {
                UnityEngine.Object.DestroyImmediate(timeLimitFailCondition);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Fails_WhenDefinitionVictimDeathFailConditionTriggers()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject victimObject = new GameObject("Victim");

        ScriptableObject missionDefinition = null;
        ScriptableObject anyVictimDeathFailCondition = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            victimObject.AddComponent(RescuableType);
            Component victimCondition = victimObject.AddComponent(VictimConditionType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            anyVictimDeathFailCondition = ScriptableObject.CreateInstance(AnyVictimDeathFailConditionDefinitionType);

            SetPrivateField(victimCondition, "currentCondition", 0f);
            SetPrivateField(victimCondition, "triageState", Enum.Parse(TriageStateType, "Deceased"));

            SetPrivateField(missionDefinition, "failConditions", CreateTypedList(MissionFailConditionDefinitionType, anyVictimDeathFailCondition));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object failedState = Enum.Parse(MissionStateType, "Failed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(failedState));
            Assert.That(GetFieldValue<int>(mission, "deceasedVictimCount"), Is.EqualTo(1));
        }
        finally
        {
            if (anyVictimDeathFailCondition != null)
            {
                UnityEngine.Object.DestroyImmediate(anyVictimDeathFailCondition);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(victimObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_MarksProgressDirty_WhenTrackedFireBurningStateChanges()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "trackedFires", CreateTypedList(FireType, fire));
            SetPrivateField(fire, "currentHp", 1f);

            InvokeInstanceMethod(mission, "StartMission");
            Assert.That(GetFieldValue<bool>(mission, "progressDirty"), Is.False);

            InvokeInstanceMethod(fire, "ApplyWater", 1f);

            Assert.That(GetFieldValue<bool>(mission, "progressDirty"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_MarksProgressDirty_WhenTrackedVictimConditionChanges()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            victimObject.AddComponent(RescuableType);
            Component victimCondition = victimObject.AddComponent(VictimConditionType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverVictimConditions", false);
            SetPrivateField(mission, "trackedVictimConditions", CreateTypedList(VictimConditionType, victimCondition));
            SetPrivateField(victimCondition, "currentCondition", 20f);

            InvokeInstanceMethod(mission, "StartMission");
            Assert.That(GetFieldValue<bool>(mission, "progressDirty"), Is.False);

            InvokeInstanceMethod(victimCondition, "Stabilize", 0f);

            Assert.That(GetFieldValue<bool>(mission, "progressDirty"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Completes_WhenReachAreaSignalObjectiveIsRaised()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject signalObject = new GameObject("ReachAreaSignal");

        ScriptableObject missionDefinition = null;
        ScriptableObject reachAreaObjective = null;

        try
        {
            missionObject.SetActive(false);
            signalObject.SetActive(false);

            Component mission = missionObject.AddComponent(MissionSystemType);
            Component signalSource = signalObject.AddComponent(MissionSignalSourceType);
            signalObject.AddComponent<BoxCollider>().isTrigger = true;

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            reachAreaObjective = ScriptableObject.CreateInstance(ReachAreaObjectiveDefinitionType);

            SetPrivateField(reachAreaObjective, "targetSignalKey", "reach-exit");
            SetPrivateField(reachAreaObjective, "pendingSummary", "Reach the exit");
            SetPrivateField(reachAreaObjective, "completedSummary", "Exit reached");
            SetPrivateField(missionDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, reachAreaObjective));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(signalSource, "missionSystem", mission);
            SetPrivateField(signalSource, "signalKey", "reach-exit");
            SetPrivateField(signalSource, "requiredTag", string.Empty);

            InvokeInstanceMethod(mission, "StartMission");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(Enum.Parse(MissionStateType, "Running")));

            signalObject.SetActive(true);
            InvokeInstanceMethod(signalSource, "RaiseSignal");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
            Assert.That(GetPropertyValue<int>(mission, "ObjectiveStatusCount"), Is.EqualTo(1));
        }
        finally
        {
            if (reachAreaObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(reachAreaObjective);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(signalObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Completes_WhenInteractTargetSignalRelayIsTriggered()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject targetObject = new GameObject("InteractTarget");
        GameObject interactorObject = new GameObject("Interactor");

        ScriptableObject missionDefinition = null;
        ScriptableObject interactObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component relay = targetObject.AddComponent(MissionInteractionSignalRelayType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            interactObjective = ScriptableObject.CreateInstance(InteractTargetObjectiveDefinitionType);

            SetPrivateField(interactObjective, "targetSignalKey", "interact-console");
            SetPrivateField(missionDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, interactObjective));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(relay, "missionSystem", mission);
            SetPrivateField(relay, "signalKey", "interact-console");
            SetPrivateField(relay, "requiredInteractorTag", string.Empty);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(relay, "NotifyInteracted", interactorObject);
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
        }
        finally
        {
            if (interactObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(interactObjective);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(interactorObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Completes_WhenBreakTargetSignalRelayReceivesBreakEvent()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject breakableObject = new GameObject("Breakable");

        ScriptableObject missionDefinition = null;
        ScriptableObject breakObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component breakable = breakableObject.AddComponent(BreakableType);
            Component relay = breakableObject.AddComponent(MissionBreakableSignalRelayType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            breakObjective = ScriptableObject.CreateInstance(BreakTargetObjectiveDefinitionType);

            SetPrivateField(breakObjective, "targetSignalKey", "break-barricade");
            SetPrivateField(missionDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, breakObjective));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(relay, "breakable", breakable);
            SetPrivateField(relay, "missionSystem", mission);
            SetPrivateField(relay, "signalKey", "break-barricade");

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(breakable, "CompleteBreak");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
        }
        finally
        {
            if (breakObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(breakObjective);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(breakableObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    [Test]
    public void Mission_Completes_WhenDeliverTargetRelayDetectsRescueInsideSafeZone()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject safeZoneObject = new GameObject("SafeZone");
        GameObject rescuableObject = new GameObject("Rescuable");

        ScriptableObject missionDefinition = null;
        ScriptableObject deliverObjective = null;

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);

            BoxCollider safeZoneCollider = safeZoneObject.AddComponent<BoxCollider>();
            safeZoneCollider.isTrigger = true;
            safeZoneCollider.size = new Vector3(4f, 2f, 4f);
            Component safeZone = safeZoneObject.AddComponent(SafeZoneType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);
            Component relay = rescuableObject.AddComponent(MissionRescueDeliverySignalRelayType);

            missionDefinition = ScriptableObject.CreateInstance(MissionDefinitionType);
            deliverObjective = ScriptableObject.CreateInstance(DeliverTargetToZoneObjectiveDefinitionType);

            SetPrivateField(deliverObjective, "targetSignalKey", "deliver-civilian");
            SetPrivateField(missionDefinition, "objectives", CreateTypedList(MissionObjectiveDefinitionType, deliverObjective));
            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "missionDefinition", missionDefinition);

            SetPrivateField(relay, "rescuable", rescuable);
            SetPrivateField(relay, "safeZone", safeZone);
            SetPrivateField(relay, "missionSystem", mission);
            SetPrivateField(relay, "signalKey", "deliver-civilian");

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(rescuable, "CompleteRescueAt", Vector3.zero);
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
        }
        finally
        {
            if (deliverObjective != null)
            {
                UnityEngine.Object.DestroyImmediate(deliverObjective);
            }

            if (missionDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(missionDefinition);
            }

            UnityEngine.Object.DestroyImmediate(rescuableObject);
            UnityEngine.Object.DestroyImmediate(safeZoneObject);
            UnityEngine.Object.DestroyImmediate(missionObject);
        }
    }

    private static IList CreateTypedList(Type itemType, params object[] items)
    {
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(itemType);
        IList list = (IList)Activator.CreateInstance(listType);
        foreach (object item in items)
            list.Add(item);

        return list;
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return (T)property.GetValue(target);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
                return found;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
                continue;

            for (int i = 0; i < types.Length; i++)
            {
                Type candidate = types[i];
                if (candidate != null && candidate.Name == typeName)
                    return candidate;
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }

    private static Type FindNestedType(Type parentType, string nestedTypeName)
    {
        Assert.That(parentType, Is.Not.Null);
        Type nestedType = parentType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(nestedType, Is.Not.Null, $"Could not find nested type '{nestedTypeName}'.");
        return nestedType;
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, InstanceFlags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }
}
