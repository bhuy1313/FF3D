using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FireHoseDeployable))]
public class FireHoseDeployableEditor : Editor
{
    SerializedProperty headProperty;
    SerializedProperty moveSpeedProperty;
    SerializedProperty groundFollowSpeedProperty;
    SerializedProperty heightOffsetProperty;
    SerializedProperty knotSpacingProperty;
    SerializedProperty normalThresholdProperty;
    SerializedProperty heightThresholdProperty;
    SerializedProperty minDistanceBeforeBreakProperty;
    SerializedProperty raycastHeightProperty;
    SerializedProperty groundMaskProperty;
    SerializedProperty useCustomProbeDistancesProperty;
    SerializedProperty lookAheadDistanceProperty;
    SerializedProperty farLookAheadDistanceProperty;
    SerializedProperty useFarLookAheadProbeProperty;
    SerializedProperty drawDebugRaysProperty;

    void OnEnable()
    {
        headProperty = serializedObject.FindProperty("head");
        moveSpeedProperty = serializedObject.FindProperty("moveSpeed");
        groundFollowSpeedProperty = serializedObject.FindProperty("groundFollowSpeed");
        heightOffsetProperty = serializedObject.FindProperty("heightOffset");
        knotSpacingProperty = serializedObject.FindProperty("knotSpacing");
        normalThresholdProperty = serializedObject.FindProperty("normalThreshold");
        heightThresholdProperty = serializedObject.FindProperty("heightThreshold");
        minDistanceBeforeBreakProperty = serializedObject.FindProperty("minDistanceBeforeBreak");
        raycastHeightProperty = serializedObject.FindProperty("raycastHeight");
        groundMaskProperty = serializedObject.FindProperty("groundMask");
        useCustomProbeDistancesProperty = serializedObject.FindProperty("useCustomProbeDistances");
        lookAheadDistanceProperty = serializedObject.FindProperty("lookAheadDistance");
        farLookAheadDistanceProperty = serializedObject.FindProperty("farLookAheadDistance");
        useFarLookAheadProbeProperty = serializedObject.FindProperty("useFarLookAheadProbe");
        drawDebugRaysProperty = serializedObject.FindProperty("drawDebugRays");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(headProperty);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveSpeedProperty);
        EditorGUILayout.PropertyField(groundFollowSpeedProperty);
        EditorGUILayout.PropertyField(heightOffsetProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Knot Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(knotSpacingProperty);
        EditorGUILayout.PropertyField(normalThresholdProperty);
        EditorGUILayout.PropertyField(heightThresholdProperty);
        EditorGUILayout.PropertyField(minDistanceBeforeBreakProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ground Probing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(raycastHeightProperty);
        EditorGUILayout.PropertyField(groundMaskProperty);
        EditorGUILayout.PropertyField(useFarLookAheadProbeProperty);
        EditorGUILayout.PropertyField(useCustomProbeDistancesProperty);

        if (useCustomProbeDistancesProperty.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(lookAheadDistanceProperty);
            EditorGUILayout.PropertyField(farLookAheadDistanceProperty);
            EditorGUI.indentLevel--;
        }
        else
        {
            float knotSpacing = Mathf.Max(0f, knotSpacingProperty.floatValue);
            float derivedLookAheadDistance = knotSpacing * 0.75f;
            float derivedFarLookAheadDistance = knotSpacing * 1.5f;

            EditorGUILayout.HelpBox(
                $"Auto probe distances from Knot Spacing:\nLook Ahead = {derivedLookAheadDistance:0.###}\nFar Look Ahead = {derivedFarLookAheadDistance:0.###}",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(drawDebugRaysProperty);

        serializedObject.ApplyModifiedProperties();
    }
}
