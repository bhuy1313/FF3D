using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MinimapIconManager))]
public class MinimapIconManagerEditor : Editor
{
    private const float VerticalSpacing = 4f;
    private const float SectionSpacing = 6f;
    private const float ElementPadding = 6f;

    private SerializedProperty autoDiscoverTargetsProperty;
    private SerializedProperty rescanIntervalProperty;
    private SerializedProperty autoIconRulesProperty;
    private SerializedProperty proxyRootProperty;
    private SerializedProperty createProxyRootIfMissingProperty;
    private SerializedProperty minimapIconLayerNameProperty;

    private ReorderableList autoIconRulesList;

    private void OnEnable()
    {
        autoDiscoverTargetsProperty = serializedObject.FindProperty("autoDiscoverTargets");
        rescanIntervalProperty = serializedObject.FindProperty("rescanInterval");
        autoIconRulesProperty = serializedObject.FindProperty("autoIconRules");
        proxyRootProperty = serializedObject.FindProperty("proxyRoot");
        createProxyRootIfMissingProperty = serializedObject.FindProperty("createProxyRootIfMissing");
        minimapIconLayerNameProperty = serializedObject.FindProperty("minimapIconLayerName");

        autoIconRulesList = new ReorderableList(serializedObject, autoIconRulesProperty, true, true, true, true);
        autoIconRulesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Auto Icon Rules");
        autoIconRulesList.elementHeightCallback = GetRuleElementHeight;
        autoIconRulesList.drawElementCallback = DrawRuleElement;
        autoIconRulesList.drawElementBackgroundCallback = DrawRuleBackground;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Discovery", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(autoDiscoverTargetsProperty);
        EditorGUILayout.PropertyField(rescanIntervalProperty);

        EditorGUILayout.Space();
        autoIconRulesList.DoLayoutList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Proxy Root", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(proxyRootProperty);
        EditorGUILayout.PropertyField(createProxyRootIfMissingProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Render Layer", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minimapIconLayerNameProperty);

        serializedObject.ApplyModifiedProperties();
    }

    private float GetRuleElementHeight(int index)
    {
        SerializedProperty element = autoIconRulesProperty.GetArrayElementAtIndex(index);
        if (element == null)
        {
            return EditorGUIUtility.singleLineHeight + (ElementPadding * 2f);
        }

        float line = EditorGUIUtility.singleLineHeight;
        float height = (ElementPadding * 2f) + line;

        if (!element.isExpanded)
        {
            return height;
        }

        height += SectionHeight(2); // Match
        height += SectionHeight(2); // Icon
        height += SectionHeight(2); // Transform
        height += SectionHeight(2); // Render
        height += SectionSpacing * 3f;

        return height;
    }

    private void DrawRuleBackground(Rect rect, int index, bool isActive, bool isFocused)
    {
        Rect backgroundRect = new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f);
        GUI.Box(backgroundRect, GUIContent.none, EditorStyles.helpBox);
    }

    private void DrawRuleElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = autoIconRulesProperty.GetArrayElementAtIndex(index);
        if (element == null)
        {
            return;
        }

        SerializedProperty matchModeProperty = element.FindPropertyRelative("matchMode");
        SerializedProperty componentTypeNameProperty = element.FindPropertyRelative("componentTypeName");
        SerializedProperty targetTagProperty = element.FindPropertyRelative("targetTag");
        SerializedProperty iconSpriteProperty = element.FindPropertyRelative("iconSprite");
        SerializedProperty iconColorProperty = element.FindPropertyRelative("iconColor");
        SerializedProperty iconScaleProperty = element.FindPropertyRelative("iconScale");
        SerializedProperty worldOffsetProperty = element.FindPropertyRelative("worldOffset");
        SerializedProperty rotateWithTargetYawProperty = element.FindPropertyRelative("rotateWithTargetYaw");
        SerializedProperty yawOffsetProperty = element.FindPropertyRelative("yawOffset");
        SerializedProperty visibleOnMinimapProperty = element.FindPropertyRelative("visibleOnMinimap");
        SerializedProperty sortingLayerNameProperty = element.FindPropertyRelative("sortingLayerName");
        SerializedProperty sortingOrderProperty = element.FindPropertyRelative("sortingOrder");
        SerializedProperty includeInactiveProperty = element.FindPropertyRelative("includeInactive");

        Rect contentRect = new Rect(
            rect.x + ElementPadding,
            rect.y + ElementPadding,
            rect.width - (ElementPadding * 2f),
            rect.height - (ElementPadding * 2f));

        Rect lineRect = new Rect(
            contentRect.x,
            contentRect.y,
            contentRect.width,
            EditorGUIUtility.singleLineHeight);

        string summary = BuildRuleSummary(element, index);
        element.isExpanded = EditorGUI.Foldout(lineRect, element.isExpanded, summary, true);

        if (!element.isExpanded)
        {
            return;
        }

        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;

        DrawSectionLabel(ref lineRect, "Match");
        EditorGUI.PropertyField(lineRect, matchModeProperty);
        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;

        if (matchModeProperty.enumValueIndex == 1)
        {
            targetTagProperty.stringValue = EditorGUI.TagField(lineRect, "Target Tag", targetTagProperty.stringValue);
        }
        else
        {
            EditorGUI.PropertyField(lineRect, componentTypeNameProperty, new GUIContent("Component Type"));
        }

        lineRect.y += EditorGUIUtility.singleLineHeight + SectionSpacing;

        DrawSectionLabel(ref lineRect, "Icon");
        EditorGUI.PropertyField(lineRect, iconSpriteProperty);
        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        DrawTwoColumnFields(
            lineRect,
            iconColorProperty,
            new GUIContent("Color"),
            iconScaleProperty,
            new GUIContent("Scale"));

        lineRect.y += EditorGUIUtility.singleLineHeight + SectionSpacing;

        DrawSectionLabel(ref lineRect, "Transform");
        EditorGUI.PropertyField(lineRect, worldOffsetProperty);
        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        DrawTwoColumnFields(
            lineRect,
            rotateWithTargetYawProperty,
            new GUIContent("Rotate With Yaw"),
            yawOffsetProperty,
            new GUIContent("Yaw Offset"),
            !rotateWithTargetYawProperty.boolValue);

        lineRect.y += EditorGUIUtility.singleLineHeight + SectionSpacing;

        DrawSectionLabel(ref lineRect, "Render");
        DrawTwoColumnFields(
            lineRect,
            visibleOnMinimapProperty,
            new GUIContent("Visible"),
            includeInactiveProperty,
            new GUIContent("Include Inactive"));

        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        DrawTwoColumnFields(
            lineRect,
            sortingLayerNameProperty,
            new GUIContent("Sorting Layer"),
            sortingOrderProperty,
            new GUIContent("Order"));
    }

    private static float SectionHeight(int rowCount)
    {
        return EditorGUIUtility.singleLineHeight + (rowCount * EditorGUIUtility.singleLineHeight) + (rowCount * VerticalSpacing);
    }

    private static void DrawSectionLabel(ref Rect lineRect, string label)
    {
        EditorGUI.LabelField(lineRect, label, EditorStyles.boldLabel);
        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
    }

    private static void DrawTwoColumnFields(
        Rect rect,
        SerializedProperty leftProperty,
        GUIContent leftLabel,
        SerializedProperty rightProperty,
        GUIContent rightLabel,
        bool disableRight = false)
    {
        float halfWidth = (rect.width - 6f) * 0.5f;
        Rect leftRect = new Rect(rect.x, rect.y, halfWidth, EditorGUIUtility.singleLineHeight);
        Rect rightRect = new Rect(leftRect.xMax + 6f, rect.y, halfWidth, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(leftRect, leftProperty, leftLabel);

        EditorGUI.BeginDisabledGroup(disableRight);
        EditorGUI.PropertyField(rightRect, rightProperty, rightLabel);
        EditorGUI.EndDisabledGroup();
    }

    private static string BuildRuleSummary(SerializedProperty element, int index)
    {
        SerializedProperty matchModeProperty = element.FindPropertyRelative("matchMode");
        SerializedProperty componentTypeNameProperty = element.FindPropertyRelative("componentTypeName");
        SerializedProperty targetTagProperty = element.FindPropertyRelative("targetTag");

        string summaryTarget = matchModeProperty.enumValueIndex == 1
            ? targetTagProperty.stringValue
            : componentTypeNameProperty.stringValue;

        if (string.IsNullOrWhiteSpace(summaryTarget))
        {
            summaryTarget = "Unconfigured";
        }

        string modeLabel = matchModeProperty.enumDisplayNames[matchModeProperty.enumValueIndex];
        return $"Rule {index + 1} - {modeLabel}: {summaryTarget}";
    }
}
