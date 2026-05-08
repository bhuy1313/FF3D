using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EntranceCameraStartupTask : SceneStartupTask
{
    [SerializeField] private EntranceCameraIntro entranceCameraIntro;
    [SerializeField] private StartupOverlayRevealTask startupOverlayRevealTask;

    public override StartupTaskPhase TaskPhase => StartupTaskPhase.Final;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        if (entranceCameraIntro == null)
        {
            entranceCameraIntro = GetComponent<EntranceCameraIntro>();
        }

        if (entranceCameraIntro == null && startupFlow != null)
        {
            entranceCameraIntro = startupFlow.FindSceneObject<EntranceCameraIntro>();
        }

        if (startupOverlayRevealTask == null)
        {
            startupOverlayRevealTask = GetComponent<StartupOverlayRevealTask>();
        }

        if (startupOverlayRevealTask == null && startupFlow != null)
        {
            startupOverlayRevealTask = startupFlow.FindSceneObject<StartupOverlayRevealTask>();
        }

        if (entranceCameraIntro == null)
        {
            yield break;
        }

        if (startupOverlayRevealTask != null)
        {
            startupOverlayRevealTask.Play();
        }

        entranceCameraIntro.Play();
        while (
            (entranceCameraIntro != null && entranceCameraIntro.IsPlaying)
            || (startupOverlayRevealTask != null && startupOverlayRevealTask.IsPlaying))
        {
            yield return null;
        }
    }
}
