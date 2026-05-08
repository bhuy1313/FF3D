using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class KeyHintService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private StarterAssets.FPSInteractionSystem interactionSystem;
    [SerializeField] private FPSInventorySystem inventorySystem;
    [SerializeField] private StarterAssets.FPSCommandSystem commandSystem;

    [Header("Config")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private bool autoDiscoverSources = true;
    [SerializeField] private List<KeyHintSourceBase> sources = new List<KeyHintSourceBase>();

    private readonly List<KeyHintRequest> collectedRequests = new List<KeyHintRequest>();
    private readonly List<KeyHintRequest> finalRequests = new List<KeyHintRequest>();
    private readonly Dictionary<string, KeyHintRequest> deduplicatedRequests = new Dictionary<string, KeyHintRequest>(System.StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<KeyHintRequest> CurrentHints => finalRequests;

    private void Awake()
    {
        ResolveReferences();
        RefreshSources();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        RefreshSources();
    }

    [ContextMenu("Refresh Sources")]
    public void RefreshSources()
    {
        if (!autoDiscoverSources)
        {
            RemoveMissingSources();
            return;
        }

        sources.Clear();
        GetComponents(sources);
        RemoveMissingSources();
    }

    public bool TryBuildContext(out KeyHintContext context)
    {
        ResolveReferences();

        InputActionAsset inputActions = playerInput != null ? playerInput.actions : null;
        InputActionMap actionMap = inputActions != null
            ? inputActions.FindActionMap(actionMapName, throwIfNotFound: false)
            : null;

        if (playerInput == null || inputActions == null || actionMap == null)
        {
            context = null;
            return false;
        }

        AppLanguage language = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : AppLanguage.Vietnamese;

        context = new KeyHintContext(
            playerInput,
            inputActions,
            actionMap,
            playerInput.currentControlScheme ?? string.Empty,
            language,
            missionSystem,
            missionSystem != null ? missionSystem.State : IncidentMissionSystem.MissionState.Idle,
            missionSystem != null ? missionSystem.MissionId : string.Empty,
            interactionSystem,
            inventorySystem,
            commandSystem,
            interactionSystem != null ? interactionSystem.CurrentTarget : null,
            inventorySystem != null ? inventorySystem.HeldObject : null,
            interactionSystem != null && interactionSystem.IsGrabActive,
            interactionSystem != null && interactionSystem.CurrentCarryWeightKg > 0.01f,
            inventorySystem != null ? inventorySystem.ItemCount : 0,
            inventorySystem != null ? inventorySystem.MaxSlots : 0,
            commandSystem != null && commandSystem.IsAwaitingDestination,
            commandSystem != null ? commandSystem.HoveredCommandTarget : null,
            commandSystem != null ? commandSystem.SelectedCommandTarget : null);

        return true;
    }

    public IReadOnlyList<KeyHintRequest> RebuildHints()
    {
        finalRequests.Clear();

        if (!TryBuildContext(out KeyHintContext context))
        {
            return finalRequests;
        }

        CollectHints(context, collectedRequests);
        DeduplicateAndSort(collectedRequests, finalRequests);
        return finalRequests;
    }

    public void CollectHints(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (context == null || results == null)
        {
            return;
        }

        results.Clear();

        for (int index = 0; index < sources.Count; index++)
        {
            KeyHintSourceBase source = sources[index];
            if (source == null)
            {
                continue;
            }

            source.CollectHints(context, results);
        }
    }

    private void DeduplicateAndSort(List<KeyHintRequest> input, List<KeyHintRequest> output)
    {
        output.Clear();
        deduplicatedRequests.Clear();

        if (input == null)
        {
            return;
        }

        for (int index = 0; index < input.Count; index++)
        {
            KeyHintRequest request = input[index];
            if (!request.IsValid)
            {
                continue;
            }

            string dedupeKey = request.GetEffectiveDeduplicationKey();
            if (string.IsNullOrWhiteSpace(dedupeKey))
            {
                continue;
            }

            if (deduplicatedRequests.TryGetValue(dedupeKey, out KeyHintRequest existing))
            {
                if (!ShouldReplace(existing, request))
                {
                    continue;
                }
            }

            deduplicatedRequests[dedupeKey] = request;
        }

        foreach (KeyValuePair<string, KeyHintRequest> pair in deduplicatedRequests)
        {
            output.Add(pair.Value);
        }

        output.Sort(CompareRequests);
    }

    private static bool ShouldReplace(KeyHintRequest current, KeyHintRequest candidate)
    {
        if (candidate.Priority != current.Priority)
        {
            return candidate.Priority > current.Priority;
        }

        if (candidate.SortOrder != current.SortOrder)
        {
            return candidate.SortOrder < current.SortOrder;
        }

        return string.Compare(candidate.SourceId, current.SourceId, System.StringComparison.Ordinal) < 0;
    }

    private static int CompareRequests(KeyHintRequest left, KeyHintRequest right)
    {
        int sortOrderCompare = left.SortOrder.CompareTo(right.SortOrder);
        if (sortOrderCompare != 0)
        {
            return sortOrderCompare;
        }

        int priorityCompare = right.Priority.CompareTo(left.Priority);
        if (priorityCompare != 0)
        {
            return priorityCompare;
        }

        int groupCompare = string.Compare(left.GroupId, right.GroupId, System.StringComparison.Ordinal);
        if (groupCompare != 0)
        {
            return groupCompare;
        }

        return string.Compare(left.ActionName, right.ActionName, System.StringComparison.Ordinal);
    }

    private void ResolveReferences()
    {
        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PlayerInput>();
        }

        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }

        if (interactionSystem == null)
        {
            interactionSystem = playerInput != null
                ? playerInput.GetComponent<StarterAssets.FPSInteractionSystem>()
                : FindAnyObjectByType<StarterAssets.FPSInteractionSystem>();
        }

        if (inventorySystem == null)
        {
            inventorySystem = playerInput != null
                ? playerInput.GetComponent<FPSInventorySystem>()
                : FindAnyObjectByType<FPSInventorySystem>();
        }

        if (commandSystem == null)
        {
            commandSystem = playerInput != null
                ? playerInput.GetComponent<StarterAssets.FPSCommandSystem>()
                : FindAnyObjectByType<StarterAssets.FPSCommandSystem>();
        }
    }

    private void RemoveMissingSources()
    {
        for (int index = sources.Count - 1; index >= 0; index--)
        {
            if (sources[index] == null)
            {
                sources.RemoveAt(index);
            }
        }
    }
}
