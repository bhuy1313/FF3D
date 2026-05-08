public readonly struct IncidentProcedureChecklistStatusSnapshot
{
    public IncidentProcedureChecklistStatusSnapshot(
        string itemId,
        string title,
        string description,
        IncidentProcedureChecklistItemType itemType,
        IncidentProcedurePriority priority,
        bool isCompleted,
        bool isContradicted,
        bool isRelevant)
    {
        ItemId = itemId;
        Title = title;
        Description = description;
        ItemType = itemType;
        Priority = priority;
        IsCompleted = isCompleted;
        IsContradicted = isContradicted;
        IsRelevant = isRelevant;
    }

    public string ItemId { get; }
    public string Title { get; }
    public string Description { get; }
    public IncidentProcedureChecklistItemType ItemType { get; }
    public IncidentProcedurePriority Priority { get; }
    public bool IsCompleted { get; }
    public bool IsContradicted { get; }
    public bool IsRelevant { get; }
}
