using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IncidentProcedureDebugOverlay : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private Vector2 overlayPosition = new Vector2(24f, 320f);
    [SerializeField] private float overlayWidth = 460f;

    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;

    private readonly List<IncidentProcedureChecklistStatusSnapshot> checklistBuffer = new List<IncidentProcedureChecklistStatusSnapshot>();
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;

    private void Awake()
    {
        ResolveMissionSystem();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showOverlay = !showOverlay;
        }
    }

    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        ResolveMissionSystem();
        if (missionSystem == null)
        {
            return;
        }

        EnsureStyles();

        string overlayText = BuildOverlayText();
        if (string.IsNullOrWhiteSpace(overlayText))
        {
            return;
        }

        float width = Mathf.Max(300f, overlayWidth);
        float height = Mathf.Max(160f, labelStyle.CalcHeight(new GUIContent(overlayText), width - 16f) + 16f);
        Rect rect = new Rect(overlayPosition.x, overlayPosition.y, width, height);

        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f), overlayText, labelStyle);
    }

    private string BuildOverlayText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Procedure Checklist");

        if (!missionSystem.HasActiveProcedure)
        {
            builder.AppendLine("No active procedure resolved.");
            builder.AppendLine("Possible causes:");
            builder.AppendLine("- Scene was started directly without Call Phase payload");
            builder.AppendLine("- No matching IncidentProcedureDefinition was found");
            builder.AppendLine("- Scripts have not recompiled cleanly in Unity yet");
            builder.AppendLine();
            builder.AppendLine("Toggle: " + toggleKey);
            return builder.ToString();
        }

        builder.AppendLine(missionSystem.GetProcedureOverlaySummary());
        builder.AppendLine();

        checklistBuffer.Clear();
        missionSystem.GetProcedureChecklistStatuses(checklistBuffer);
        for (int i = 0; i < checklistBuffer.Count; i++)
        {
            IncidentProcedureChecklistStatusSnapshot item = checklistBuffer[i];
            if (!item.IsRelevant)
            {
                continue;
            }

            string prefix = item.IsContradicted
                ? "[X]"
                : item.IsCompleted
                    ? "[OK]"
                    : "[ ]";
            builder.Append(prefix);
            builder.Append(' ');
            builder.Append(item.Title);
            builder.Append("  <");
            builder.Append(item.ItemType);
            builder.Append(" | ");
            builder.Append(item.Priority);
            builder.AppendLine(">");

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                builder.AppendLine(item.Description);
            }

            if (i < checklistBuffer.Count - 1)
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine("Toggle: " + toggleKey);
        return builder.ToString();
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = GetComponent<IncidentMissionSystem>();
        }

        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    private void EnsureStyles()
    {
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.UpperLeft;
            boxStyle.padding = new RectOffset(8, 8, 8, 8);
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = true;
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;
        }
    }
}
