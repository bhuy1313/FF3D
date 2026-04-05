using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EntranceCameraStartupTask : SceneStartupTask
{
    [SerializeField] private EntranceCameraIntro entranceCameraIntro;

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

        if (entranceCameraIntro == null)
        {
            yield break;
        }

        entranceCameraIntro.Play();
        while (entranceCameraIntro != null && entranceCameraIntro.IsPlaying)
        {
            yield return null;
        }
    }
}
