using System.Collections.Generic;
using UnityEngine;

public interface IKeyHintSource
{
    void CollectHints(KeyHintContext context, List<KeyHintRequest> results);
}

public abstract class KeyHintSourceBase : MonoBehaviour, IKeyHintSource
{
    [SerializeField] private bool sourceEnabled = true;
    [SerializeField] private int basePriority;

    public bool SourceEnabled => sourceEnabled && isActiveAndEnabled;
    public int BasePriority => basePriority;

    public void CollectHints(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (!SourceEnabled || context == null || results == null)
        {
            return;
        }

        CollectHintsInternal(context, results);
    }

    protected abstract void CollectHintsInternal(KeyHintContext context, List<KeyHintRequest> results);

    protected KeyHintRequest CreateHint(
        string actionName,
        string labelLocalizationKey = null,
        string labelFallback = null,
        int priorityOffset = 0,
        int sortOrder = 0,
        string groupId = null,
        string deduplicationKey = null)
    {
        return new KeyHintRequest(
            actionName,
            labelLocalizationKey,
            labelFallback,
            BasePriority + priorityOffset,
            sortOrder,
            groupId,
            deduplicationKey,
            GetType().Name);
    }
}
