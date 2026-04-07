using System;
using System.Reflection;
using NUnit.Framework;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;

public class BotEquippedItemPoseDriverTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type MultiAimConstraintType = FindType("MultiAimConstraint");
    private static readonly Type TwoBoneIKConstraintType = FindType("TwoBoneIKConstraint");

    private GameObject botObject;
    private BotCommandAgent commandAgent;
    private BotEquippedItemPoseDriver poseDriver;
    private FakeRescuableTarget rescueTarget;
    private Component carrySpine;
    private Component carrySpine1;
    private Component carrySpine2;
    private Component carryRightHandIk;
    private Component carryLeftHandIk;
    private GameObject carryRightHandIkTarget;
    private GameObject carryLeftHandIkTarget;
    private GameObject rescueRightHoldPointObject;
    private GameObject rescueLeftHoldPointObject;

    [SetUp]
    public void SetUp()
    {
        botObject = new GameObject("Bot");
        botObject.AddComponent<NavMeshAgent>();
        botObject.AddComponent<BotBehaviorContext>();
        botObject.AddComponent<BotInventorySystem>();
        commandAgent = botObject.AddComponent<BotCommandAgent>();

        carrySpine = CreateChildConstraint("CarrySpine", MultiAimConstraintType, 0.25f);
        carrySpine1 = CreateChildConstraint("CarrySpine1", MultiAimConstraintType, 0.4f);
        carrySpine2 = CreateChildConstraint("CarrySpine2", MultiAimConstraintType, 0.55f);
        carryRightHandIk = CreateChildConstraint("CarryRightHandIK", TwoBoneIKConstraintType, 0.7f);
        carryLeftHandIk = CreateChildConstraint("CarryLeftHandIK", TwoBoneIKConstraintType, 0.85f);
        carryRightHandIkTarget = CreateChildTarget("CarryRightHandIK_target", new Vector3(0.1f, 0.2f, 0.3f), Quaternion.Euler(5f, 10f, 15f));
        carryLeftHandIkTarget = CreateChildTarget("CarryLeftHandIK_target", new Vector3(-0.15f, 0.25f, 0.35f), Quaternion.Euler(-5f, -10f, -15f));

        poseDriver = botObject.AddComponent<BotEquippedItemPoseDriver>();
        SetFieldValue(poseDriver, "carryRightHandIkTarget", carryRightHandIkTarget.transform);
        SetFieldValue(poseDriver, "carryLeftHandIkTarget", carryLeftHandIkTarget.transform);
        InvokeInstanceMethod(poseDriver, "Awake");

        GameObject rescueTargetObject = new GameObject("RescueTarget");
        rescueTarget = rescueTargetObject.AddComponent<FakeRescuableTarget>();
        rescueRightHoldPointObject = new GameObject("CarryRightHandHoldPoint");
        rescueRightHoldPointObject.transform.SetParent(rescueTargetObject.transform, false);
        rescueRightHoldPointObject.transform.position = new Vector3(2f, 1f, -3f);
        rescueRightHoldPointObject.transform.rotation = Quaternion.Euler(20f, 30f, 40f);
        rescueLeftHoldPointObject = new GameObject("CarryLeftHandHoldPoint");
        rescueLeftHoldPointObject.transform.SetParent(rescueTargetObject.transform, false);
        rescueLeftHoldPointObject.transform.position = new Vector3(-2.5f, 1.25f, -2f);
        rescueLeftHoldPointObject.transform.rotation = Quaternion.Euler(-20f, -30f, -40f);
        rescueTarget.ConfigureHoldPoints(rescueRightHoldPointObject.transform, rescueLeftHoldPointObject.transform);
        SetFieldValue(commandAgent, "currentRescueTarget", rescueTarget);
    }

    [TearDown]
    public void TearDown()
    {
        if (rescueTarget != null)
        {
            UnityEngine.Object.DestroyImmediate(rescueTarget.gameObject);
        }

        if (botObject != null)
        {
            UnityEngine.Object.DestroyImmediate(botObject);
        }
    }

    [Test]
    public void LateUpdate_BlendsCarryRigWeightsOnlyWhileBotIsCarrying()
    {
        rescueTarget.Configure(isCarried: false, activeRescuer: null);
        InvokeInstanceMethod(poseDriver, "LateUpdate");

        Assert.That(GetConstraintWeight(carrySpine), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine1), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine2), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryRightHandIk), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryLeftHandIk), Is.EqualTo(0f).Within(0.0001f));

        rescueTarget.Configure(isCarried: true, activeRescuer: botObject);
        InvokeInstanceMethod(poseDriver, "LateUpdate");

        Assert.That(GetConstraintWeight(carrySpine), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine1), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine2), Is.EqualTo(0.55f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryRightHandIk), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryLeftHandIk), Is.EqualTo(0.85f).Within(0.0001f));
    }

    [Test]
    public void LateUpdate_KeepsCarryRigWeightsAfterMoveCommandWhileBotStillCarriesVictim()
    {
        rescueTarget.Configure(isCarried: true, activeRescuer: botObject);

        InvokeInstanceMethod(commandAgent, "PrepareForIssuedCommand", BotCommandType.Move);
        InvokeInstanceMethod(poseDriver, "LateUpdate");

        Assert.That(GetConstraintWeight(carrySpine), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine1), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carrySpine2), Is.EqualTo(0.55f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryRightHandIk), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(GetConstraintWeight(carryLeftHandIk), Is.EqualTo(0.85f).Within(0.0001f));
    }

    [Test]
    public void LateUpdate_MatchesCarryHandTargetsToVictimHoldPoints()
    {
        rescueTarget.Configure(isCarried: true, activeRescuer: botObject);

        InvokeInstanceMethod(poseDriver, "LateUpdate");

        Assert.That(carryRightHandIkTarget.transform.parent, Is.EqualTo(botObject.transform));
        Assert.That(Vector3.Distance(carryRightHandIkTarget.transform.position, rescueRightHoldPointObject.transform.position), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(carryRightHandIkTarget.transform.localRotation, Quaternion.Euler(5f, 10f, 15f)), Is.LessThan(0.001f));
        Assert.That(carryLeftHandIkTarget.transform.parent, Is.EqualTo(botObject.transform));
        Assert.That(Vector3.Distance(carryLeftHandIkTarget.transform.position, rescueLeftHoldPointObject.transform.position), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(carryLeftHandIkTarget.transform.localRotation, Quaternion.Euler(-5f, -10f, -15f)), Is.LessThan(0.001f));
    }

    [Test]
    public void LateUpdate_RestoresCarryHandTargetPosesWhenCarryStops()
    {
        rescueTarget.Configure(isCarried: true, activeRescuer: botObject);
        InvokeInstanceMethod(poseDriver, "LateUpdate");

        rescueTarget.Configure(isCarried: false, activeRescuer: null);
        InvokeInstanceMethod(poseDriver, "LateUpdate");

        Assert.That(carryRightHandIkTarget.transform.parent, Is.EqualTo(botObject.transform));
        Assert.That(Vector3.Distance(carryRightHandIkTarget.transform.localPosition, new Vector3(0.1f, 0.2f, 0.3f)), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(carryRightHandIkTarget.transform.localRotation, Quaternion.Euler(5f, 10f, 15f)), Is.LessThan(0.001f));
        Assert.That(carryLeftHandIkTarget.transform.parent, Is.EqualTo(botObject.transform));
        Assert.That(Vector3.Distance(carryLeftHandIkTarget.transform.localPosition, new Vector3(-0.15f, 0.25f, 0.35f)), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(carryLeftHandIkTarget.transform.localRotation, Quaternion.Euler(-5f, -10f, -15f)), Is.LessThan(0.001f));
    }

    private Component CreateChildConstraint(string objectName, Type constraintType, float weight)
    {
        Assert.That(constraintType, Is.Not.Null, $"Could not find constraint type for '{objectName}'.");
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(botObject.transform, false);
        Component constraint = child.AddComponent(constraintType);
        SetConstraintWeight(constraint, weight);
        return constraint;
    }

    private GameObject CreateChildTarget(string objectName, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(botObject.transform, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        return child;
    }

    private static float GetConstraintWeight(Component constraint)
    {
        PropertyInfo property = constraint.GetType().GetProperty("weight", InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find weight property on {constraint.GetType().Name}.");
        return (float)property.GetValue(constraint);
    }

    private static void SetConstraintWeight(Component constraint, float weight)
    {
        PropertyInfo property = constraint.GetType().GetProperty("weight", InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find weight property on {constraint.GetType().Name}.");
        property.SetValue(constraint, weight);
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}' on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
            {
                return found;
            }

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
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type candidate = types[i];
                if (candidate != null && candidate.Name == typeName)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private sealed class FakeRescuableTarget : MonoBehaviour, IRescuableTarget
    {
        private bool isCarried;
        private GameObject activeRescuer;
        private Transform carryRightHandHoldPoint;
        private Transform carryLeftHandHoldPoint;

        public bool NeedsRescue => true;
        public bool IsRescueInProgress => false;
        public GameObject ActiveRescuer => activeRescuer;
        public bool IsCarried => isCarried;
        public bool RequiresStabilization => false;
        public float RescuePriority => 0f;

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public Transform GetCarryRightHandHoldPoint()
        {
            return carryRightHandHoldPoint;
        }

        public Transform GetCarryLeftHandHoldPoint()
        {
            return carryLeftHandHoldPoint;
        }

        public bool TryStabilize(GameObject rescuer)
        {
            return false;
        }

        public bool TryBeginCarry(GameObject rescuer, Transform carryAnchor)
        {
            return false;
        }

        public void CompleteRescueAt(Vector3 dropPosition)
        {
        }

        public void Configure(bool isCarried, GameObject activeRescuer)
        {
            this.isCarried = isCarried;
            this.activeRescuer = activeRescuer;
        }

        public void ConfigureHoldPoints(Transform rightHandHoldPoint, Transform leftHandHoldPoint)
        {
            carryRightHandHoldPoint = rightHandHoldPoint;
            carryLeftHandHoldPoint = leftHandHoldPoint;
        }
    }
}
