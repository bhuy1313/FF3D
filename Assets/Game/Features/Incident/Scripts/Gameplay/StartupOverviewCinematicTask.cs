using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class StartupOverviewCinematicTask : SceneStartupTask
{
    [SerializeField] private StartupOverviewCinematicController overviewController;
    [SerializeField] private StartupOverlayRevealTask startupOverlayRevealTask;

    public override bool BlocksStartupSequence => false;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        if (overviewController == null)
        {
            overviewController = GetComponent<StartupOverviewCinematicController>();
        }

        if (overviewController == null && startupFlow != null)
        {
            overviewController = startupFlow.FindSceneObject<StartupOverviewCinematicController>();
        }

        if (startupOverlayRevealTask == null)
        {
            startupOverlayRevealTask = GetComponent<StartupOverlayRevealTask>();
        }

        if (startupOverlayRevealTask == null && startupFlow != null)
        {
            startupOverlayRevealTask = startupFlow.FindSceneObject<StartupOverlayRevealTask>();
        }

        if (overviewController == null)
        {
            yield break;
        }

        if (startupOverlayRevealTask != null)
        {
            startupOverlayRevealTask.Play();
        }

        if (!overviewController.TryPlay())
        {
            yield break;
        }

        while (
            (overviewController != null && overviewController.IsPlaying)
            || (startupOverlayRevealTask != null && startupOverlayRevealTask.IsPlaying))
        {
            yield return null;
        }
    }
}
