using System;

public readonly struct KeyHintRequest
{
    public KeyHintRequest(
        string actionName,
        string labelLocalizationKey = null,
        string labelFallback = null,
        int priority = 0,
        int sortOrder = 0,
        string groupId = null,
        string deduplicationKey = null,
        string sourceId = null)
    {
        ActionName = actionName ?? string.Empty;
        LabelLocalizationKey = labelLocalizationKey ?? string.Empty;
        LabelFallback = labelFallback ?? string.Empty;
        Priority = priority;
        SortOrder = sortOrder;
        GroupId = groupId ?? string.Empty;
        DeduplicationKey = deduplicationKey ?? string.Empty;
        SourceId = sourceId ?? string.Empty;
    }

    public string ActionName { get; }
    public string LabelLocalizationKey { get; }
    public string LabelFallback { get; }
    public int Priority { get; }
    public int SortOrder { get; }
    public string GroupId { get; }
    public string DeduplicationKey { get; }
    public string SourceId { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(ActionName);

    public string GetEffectiveDeduplicationKey()
    {
        if (!string.IsNullOrWhiteSpace(DeduplicationKey))
        {
            return DeduplicationKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ActionName))
        {
            return ActionName.Trim();
        }

        return string.Empty;
    }

    public string GetEffectiveLabelFallback()
    {
        if (!string.IsNullOrWhiteSpace(LabelFallback))
        {
            return LabelFallback;
        }

        return ActionName ?? string.Empty;
    }

    public KeyHintRequest WithPriority(int priority)
    {
        return new KeyHintRequest(
            ActionName,
            LabelLocalizationKey,
            LabelFallback,
            priority,
            SortOrder,
            GroupId,
            DeduplicationKey,
            SourceId);
    }
}
