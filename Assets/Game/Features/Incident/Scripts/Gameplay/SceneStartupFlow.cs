using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class SceneStartupFlow : MonoBehaviour
{
    public enum StartupState
    {
        Idle,
        Running,
        Completed,
        Failed,
    }

    [Header("Flow")]
    [SerializeField]
    private bool runOnStart = true;

    [SerializeField]
    private bool lockPlayerUntilReady = true;

    [SerializeField]
    private bool clearInputsWhileLocked = true;

    [Header("UI")]
    [SerializeField]
    private GameObject gameplayCanvas;

    [Header("Tasks")]
    [SerializeField]
    private List<SceneStartupTask> explicitTasks = new List<SceneStartupTask>();

    [Header("Player")]
    [Tooltip(
        "Optional override. If left empty, the flow will find the active FirstPersonController in the scene."
    )]
    [SerializeField]
    private FirstPersonController playerControllerOverride;

    [Tooltip(
        "Optional override. If left empty, the flow will use or create PlayerActionLock on the resolved player when locking is enabled."
    )]
    [SerializeField]
    private PlayerActionLock playerActionLock;

    [Tooltip(
        "Optional override. If left empty, the flow will read StarterAssetsInputs from the resolved player."
    )]
    [SerializeField]
    private StarterAssetsInputs playerInputs;

    [Header("Entrance Animation")]
    [Tooltip("Optional Animator to trigger when startup completes.")]
    [SerializeField]
    private Animator entranceAnimator;

    [Tooltip("Enable to trigger the entrance animation once when startup completes.")]
    [SerializeField]
    private bool triggerEntranceAnimationOnStartup = true;

    [Tooltip("The name of the trigger parameter to set on the animator.")]
    [SerializeField]
    private string entranceTriggerName = "BarAnimationEntranceTrigger";

    private Coroutine startupRoutine;
    private SceneStartupTask activeTask;
    private StartupState state = StartupState.Idle;
    private PlayerActionLock runtimeActionLock;
    private StarterAssetsInputs runtimeInputs;
    private bool runtimeLockAcquired;
    private int totalTaskCount;
    private int completedTaskCount;
    private int pendingNonBlockingTaskCount;

    public StartupState State => state;
    public bool IsRunning => state == StartupState.Running;
    public bool IsGameplayReady => state == StartupState.Completed;
    public SceneStartupTask ActiveTask => activeTask;
    public IReadOnlyList<SceneStartupTask> ExplicitTasks => explicitTasks;
    public int TotalTaskCount => totalTaskCount;
    public int CompletedTaskCount => completedTaskCount;
    public bool HasCompletedAllTasks => state == StartupState.Completed && completedTaskCount >= totalTaskCount;
    public bool HasStarted => state != StartupState.Idle;

    private void Start()
    {
        if (!Application.isPlaying || !runOnStart)
        {
            return;
        }

        SetGameplayCanvasVisible(false);
        StartStartup();
    }

    public void StartStartup()
    {
        if (startupRoutine != null || state == StartupState.Completed)
        {
            return;
        }

        startupRoutine = StartCoroutine(RunStartupRoutine());
    }

    private IEnumerator RunStartupRoutine()
    {
        state = StartupState.Running;
        bool completed = false;
        List<SceneStartupTask> tasks = BuildTaskList();
        totalTaskCount = tasks.Count;
        completedTaskCount = 0;
        pendingNonBlockingTaskCount = 0;

        runtimeActionLock = ResolvePlayerActionLock(lockPlayerUntilReady);
        runtimeInputs = ResolvePlayerInputs();
        runtimeLockAcquired = false;

        try
        {
            if (lockPlayerUntilReady && runtimeActionLock != null)
            {
                runtimeActionLock.AcquireFullLock();
                runtimeLockAcquired = true;
            }

            if (clearInputsWhileLocked)
            {
                ClearPlayerInputs(runtimeInputs);
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                SceneStartupTask task = tasks[i];
                if (task == null || task.TaskPhase != SceneStartupTask.StartupTaskPhase.Normal)
                {
                    continue;
                }

                activeTask = task;
                if (clearInputsWhileLocked)
                {
                    ClearPlayerInputs(runtimeInputs);
                }

                yield return RunTask(task);
                completedTaskCount++;
            }

            while (pendingNonBlockingTaskCount > 0)
            {
                yield return null;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                SceneStartupTask task = tasks[i];
                if (task == null)
                {
                    continue;
                }

                if (task.TaskPhase != SceneStartupTask.StartupTaskPhase.Final)
                {
                    continue;
                }

                activeTask = task;
                if (clearInputsWhileLocked)
                {
                    ClearPlayerInputs(runtimeInputs);
                }

                yield return RunTask(task);
                completedTaskCount++;
            }

            // Trigger entrance animation (one-shot) if configured
            if (
                triggerEntranceAnimationOnStartup
                && entranceAnimator != null
                && !string.IsNullOrEmpty(entranceTriggerName)
            )
            {
                entranceAnimator.SetTrigger(entranceTriggerName);
            }

            SetGameplayCanvasVisible(true);
            state = StartupState.Completed;
            completed = true;
        }
        finally
        {
            if (!completed && state == StartupState.Running)
            {
                state = StartupState.Failed;
            }

            activeTask = null;

            if (clearInputsWhileLocked)
            {
                ClearPlayerInputs(runtimeInputs);
            }

            if (runtimeLockAcquired && runtimeActionLock != null)
            {
                runtimeActionLock.ReleaseFullLock();
            }

            startupRoutine = null;
            runtimeLockAcquired = false;
            runtimeActionLock = null;
            runtimeInputs = null;
        }
    }

    private void OnDisable()
    {
        if (clearInputsWhileLocked)
        {
            ClearPlayerInputs(runtimeInputs);
        }

        if (runtimeLockAcquired && runtimeActionLock != null)
        {
            runtimeActionLock.ReleaseFullLock();
        }

        runtimeLockAcquired = false;
        runtimeActionLock = null;
        runtimeInputs = null;
        activeTask = null;
        startupRoutine = null;
        totalTaskCount = 0;
        completedTaskCount = 0;
        pendingNonBlockingTaskCount = 0;

        if (state == StartupState.Running)
        {
            state = StartupState.Idle;
        }
    }

    private List<SceneStartupTask> BuildTaskList()
    {
        List<SceneStartupTask> results = new List<SceneStartupTask>();
        HashSet<SceneStartupTask> seen = new HashSet<SceneStartupTask>();

        for (int i = 0; i < explicitTasks.Count; i++)
        {
            SceneStartupTask task = explicitTasks[i];
            if (task == null || !seen.Add(task))
            {
                continue;
            }

            results.Add(task);
        }

        return results;
    }

    private IEnumerator RunTask(SceneStartupTask task)
    {
        if (task == null)
        {
            yield break;
        }

        if (task.BlocksStartupSequence)
        {
            yield return task.Run(this);
            yield break;
        }

        pendingNonBlockingTaskCount++;
        StartCoroutine(RunNonBlockingTask(task));
    }

    private IEnumerator RunNonBlockingTask(SceneStartupTask task)
    {
        try
        {
            if (task != null)
            {
                yield return task.Run(this);
            }
        }
        finally
        {
            pendingNonBlockingTaskCount = Mathf.Max(0, pendingNonBlockingTaskCount - 1);
        }
    }

    public PlayerActionLock ResolvePlayerActionLock(bool createIfMissing = true)
    {
        if (playerActionLock != null)
        {
            return playerActionLock;
        }

        FirstPersonController controller = ResolvePlayerController();
        if (controller == null)
        {
            return null;
        }

        if (controller.TryGetComponent(out PlayerActionLock existingActionLock))
        {
            playerActionLock = existingActionLock;
            return playerActionLock;
        }

        if (!createIfMissing)
        {
            return null;
        }

        playerActionLock = PlayerActionLock.GetOrCreate(controller.gameObject);
        return playerActionLock;
    }

    public StarterAssetsInputs ResolvePlayerInputs()
    {
        if (playerInputs != null)
        {
            return playerInputs;
        }

        FirstPersonController controller = ResolvePlayerController();
        playerInputs = controller != null ? controller.GetComponent<StarterAssetsInputs>() : null;
        return playerInputs;
    }

    public FirstPersonController ResolvePlayerController()
    {
        if (playerControllerOverride != null)
        {
            return playerControllerOverride;
        }

        playerControllerOverride = FindAnyObjectByType<FirstPersonController>();
        return playerControllerOverride;
    }

    public T FindSceneObject<T>()
        where T : UnityEngine.Object
    {
        return FindAnyObjectByType<T>();
    }

    private void SetGameplayCanvasVisible(bool visible)
    {
        if (gameplayCanvas != null)
        {
            gameplayCanvas.SetActive(visible);
        }
    }

    public void ClearResolvedPlayerInputs()
    {
        ClearPlayerInputs(ResolvePlayerInputs());
    }

    private static void ClearPlayerInputs(StarterAssetsInputs inputs)
    {
        if (inputs == null)
        {
            return;
        }

        inputs.MoveInput(Vector2.zero);
        inputs.LookInput(Vector2.zero);
        inputs.JumpInput(false);
        inputs.SprintInput(false);
        inputs.CrouchInput(false);
        inputs.InteractInput(false);
        inputs.PickupInput(false);
        inputs.UseInput(false);
        inputs.DropInput(false);
        inputs.GrabInput(false);
        inputs.ClearGameplayActionInputs();
    }
}
