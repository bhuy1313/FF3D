using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class StartupOverviewCinematicTask : SceneStartupTask
{
    [SerializeField] private StartupOverviewCinematicController overviewController;

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

        if (overviewController == null)
        {
            yield break;
        }

        if (!overviewController.TryPlay())
        {
            yield break;
        }

        while (overviewController != null && overviewController.IsPlaying)
        {
            yield return null;
        }
    }
}
